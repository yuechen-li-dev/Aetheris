using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Continuum.Lattice;
using Aetheris.FEA.Analysis;
using Aetheris.Kernel.Core.Math;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Boundaries;

namespace Aetheris.FEA.Abaqus;

public sealed record AbaqusExportArtifact(string Text, string Sha256, int NodeCount, int ElementCount, IReadOnlyList<string> Diagnostics);

/// <summary>
/// Verification-only conventional lowering. Only provably full occupied lattice cells become C3D8 elements;
/// native partial Cut cells are never mislabeled as full bricks.
/// </summary>
public static class AbaqusInpExporter
{
    private static readonly (int X,int Y,int Z)[] Offsets=[(0,0,0),(1,0,0),(1,1,0),(0,1,0),(0,0,1),(1,0,1),(1,1,1),(0,1,1)];
    public static AbaqusExportArtifact Export(LinearElasticAnalysisIr analysis,Transform3D? orientation=null)
    {
        IContinuumRegion region=analysis.Body.ContinuumRegion;if(orientation is { } transform)region=new TransformedContinuumRegion(region,transform);
        var lattice=orientation is null?analysis.Lattice:new LatticeSpec(region.Bounds,analysis.Lattice.CountX,analysis.Lattice.CountY,analysis.Lattice.CountZ);
        var grid=ContinuumGridClassifier.Classify(region,lattice,2);
        var cells=grid.Cells.Where(cell=>cell.Classification==CellClassification.Inside).OrderBy(cell=>cell.Index.K).ThenBy(cell=>cell.Index.J).ThenBy(cell=>cell.Index.I).ToArray();
        var diagnostics=new List<string>();
        if(cells.Length==0)diagnostics.Add("abaqus-lowering-empty-full-cell-domain");
        if(grid.CutCellCount>0)diagnostics.Add($"verification-approximation:omitted-{grid.CutCellCount}-partial-cut-cells");
        var keys=cells.SelectMany(c=>Offsets.Select(o=>(c.Index.I+o.X,c.Index.J+o.Y,c.Index.K+o.Z))).Distinct().OrderBy(p=>p.Item3).ThenBy(p=>p.Item2).ThenBy(p=>p.Item1).ToArray();
        var ids=keys.Select((key,index)=>(key,id:index+1)).ToDictionary(x=>x.key,x=>x.id);
        var sb=new StringBuilder();
        sb.AppendLine("** Aetheris AETHERIS-FEA-M5 verification deck");
        sb.AppendLine("** Conventional C3D8 full-cell approximation; native Cut cells are omitted, never exported as full bricks.");
        sb.AppendLine("*HEADING"); sb.AppendLine(analysis.Id); sb.AppendLine("*NODE");
        foreach(var key in keys){var p=Position(lattice,key);sb.AppendLine(FormattableString.Invariant($"{ids[key]}, {p.X:R}, {p.Y:R}, {p.Z:R}"));}
        sb.AppendLine("*ELEMENT, TYPE=C3D8, ELSET=SOLID");
        for(var e=0;e<cells.Length;e++){var n=Offsets.Select(o=>ids[(cells[e].Index.I+o.X,cells[e].Index.J+o.Y,cells[e].Index.K+o.Z)]);sb.AppendLine($"{e+1}, {string.Join(", ",n)}");}
        WriteIds(sb,"*ELSET, ELSET=SOLID",Enumerable.Range(1,cells.Length));
        foreach(var constraint in analysis.Constraints)
        {
            var name=SetName(constraint.Id);var selected=SelectNodes(keys,lattice,region,constraint.Region).Select(k=>ids[k]).ToArray();
            WriteIds(sb,$"*NSET, NSET={name}",selected);
        }
        foreach(var load in analysis.Loads)
        {
            var name=SetName(load.Id);var selected=SelectNodes(keys,lattice,region,load.Region).Select(k=>ids[k]).ToArray();
            WriteIds(sb,$"*NSET, NSET={name}",selected);
        }
        var material=analysis.Materials.Single();
        sb.AppendLine($"*MATERIAL, NAME={SetName(material.Id)}"); sb.AppendLine("*ELASTIC"); sb.AppendLine(FormattableString.Invariant($"{material.YoungsModulusPascal:R}, {material.PoissonRatio:R}"));
        if(material.DensityKilogramsPerCubicMeter is double density){sb.AppendLine("*DENSITY");sb.AppendLine(density.ToString("R",CultureInfo.InvariantCulture));}
        sb.AppendLine($"*SOLID SECTION, ELSET=SOLID, MATERIAL={SetName(material.Id)}"); sb.AppendLine(",");
        sb.AppendLine("*BOUNDARY");
        foreach(var constraint in analysis.Constraints)foreach(var component in constraint.Components.Order())
        {var dof=(int)component+1;var value=component switch{DisplacementComponent.X=>constraint.ValueMeters.X,DisplacementComponent.Y=>constraint.ValueMeters.Y,_=>constraint.ValueMeters.Z};sb.AppendLine(FormattableString.Invariant($"{SetName(constraint.Id)}, {dof}, {dof}, {value:R}"));}
        sb.AppendLine("*STEP, NAME=LINEAR_STATIC, NLGEOM=NO");sb.AppendLine("*STATIC");sb.AppendLine("1., 1., 1e-05, 1.");
        foreach(var load in analysis.Loads)
        {
            var set=SetName(load.Id);var count=SelectNodes(keys,lattice,region,load.Region).Count;if(count==0)continue;
            var vector=orientation is { } t?t.Apply(load.VectorSi):load.VectorSi;var area=ExactArea(region,load.Region);
            var total=load.Kind==BoundaryLoadKind.ResultantForce?vector:vector*area;
            if(load.Kind==BoundaryLoadKind.Pressure){var normal=ExactNormal(region,load.Region);total=normal*(-load.PressurePascal*area);}
            sb.AppendLine("*CLOAD");if(total.X!=0)sb.AppendLine(FormattableString.Invariant($"{set}, 1, {total.X/count:R}"));if(total.Y!=0)sb.AppendLine(FormattableString.Invariant($"{set}, 2, {total.Y/count:R}"));if(total.Z!=0)sb.AppendLine(FormattableString.Invariant($"{set}, 3, {total.Z/count:R}"));
        }
        sb.AppendLine("*OUTPUT, FIELD");sb.AppendLine("*NODE OUTPUT");sb.AppendLine("U, RF");sb.AppendLine("*ELEMENT OUTPUT, DIRECTIONS=YES");sb.AppendLine("S, E");sb.AppendLine("*OUTPUT, HISTORY");sb.AppendLine("*ENERGY OUTPUT");sb.AppendLine("ALLSE");sb.AppendLine("*END STEP");
        var text=sb.ToString().Replace("\r\n","\n",StringComparison.Ordinal);var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        return new(text,hash,keys.Length,cells.Length,diagnostics);
    }

    private static void WriteIds(StringBuilder sb,string header,IEnumerable<int> values){sb.AppendLine(header);var list=values.ToArray();for(var i=0;i<list.Length;i+=16)sb.AppendLine(string.Join(", ",list.Skip(i).Take(16)));}
    private static string SetName(string value)=>Regex.Replace(value.ToUpperInvariant(),"[^A-Z0-9_]","_");
    private static Point3D Position(LatticeSpec l,(int,int,int) k)=>new(l.Bounds.Min.X+k.Item1*l.CellSize.X,l.Bounds.Min.Y+k.Item2*l.CellSize.Y,l.Bounds.Min.Z+k.Item3*l.CellSize.Z);
    private static IReadOnlyList<(int,int,int)> SelectNodes(IReadOnlyList<(int,int,int)> keys,LatticeSpec lattice,IContinuumRegion region,SemanticRegionBinding binding)
    {
        if(region is not IPlanarBoundaryDomainCapability capability||!capability.TryResolvePlanarBoundary(binding.Path,binding.ExactBrepFaceId,out var face))return keys.Where(k=>Matches(Position(lattice,k),binding.Path,lattice.Bounds)).ToArray();
        var rows=keys.Select(key=>(key,d:double.Abs((Position(lattice,key)-face.Origin).Dot(face.OutwardNormal)))).ToArray();if(rows.Length==0)return[];var projected=.51*(double.Abs(face.OutwardNormal.X)*lattice.CellSize.X+double.Abs(face.OutwardNormal.Y)*lattice.CellSize.Y+double.Abs(face.OutwardNormal.Z)*lattice.CellSize.Z);var selected=rows.Where(row=>row.d<=projected+1e-12).Select(row=>row.key).ToArray();return selected.Length>0?selected:rows.Where(row=>row.d<=rows.Min(x=>x.d)+1e-12).Select(row=>row.key).ToArray();
    }
    private static double ExactArea(IContinuumRegion region,SemanticRegionBinding binding)=>region is IPlanarBoundaryDomainCapability c&&c.TryResolvePlanarBoundary(binding.Path,binding.ExactBrepFaceId,out var face)?face.ExactArea:0;
    private static Vector3D ExactNormal(IContinuumRegion region,SemanticRegionBinding binding)=>region is IPlanarBoundaryDomainCapability c&&c.TryResolvePlanarBoundary(binding.Path,binding.ExactBrepFaceId,out var face)?face.OutwardNormal:Outward(binding.Path);
    private static bool Matches(Point3D p,string path,BoundingBox3D b){var (d,pos)=Axis(path);var v=d==0?p.X:d==1?p.Y:p.Z;var t=d==0?(pos?b.Max.X:b.Min.X):d==1?(pos?b.Max.Y:b.Min.Y):(pos?b.Max.Z:b.Min.Z);return double.Abs(v-t)<1e-12;}
    private static (int,bool) Axis(string path){if(path.Contains("+X")||path.Contains("x-max",StringComparison.OrdinalIgnoreCase))return(0,true);if(path.Contains("-X")||path.Contains("x-min",StringComparison.OrdinalIgnoreCase))return(0,false);if(path.Contains("+Y")||path.Contains("y-max",StringComparison.OrdinalIgnoreCase))return(1,true);if(path.Contains("-Y")||path.Contains("y-min",StringComparison.OrdinalIgnoreCase))return(1,false);if(path.Contains("+Z")||path.Contains("z-max",StringComparison.OrdinalIgnoreCase))return(2,true);return(2,false);}
    private static Vector3D Outward(string path){var(a,p)=Axis(path);return a==0?new(p?1:-1,0,0):a==1?new(0,p?1:-1,0):new(0,0,p?1:-1);}
    private static double ApproximateFaceArea(LatticeSpec l,string path){var(a,_)=Axis(path);var s=l.Bounds.Max-l.Bounds.Min;return a==0?s.Y*s.Z:a==1?s.X*s.Z:s.X*s.Y;}
}

public sealed record AbaqusDeckValidation(bool IsValid,int NodeCount,int ElementCount,IReadOnlyList<string> Diagnostics);

public static class AbaqusInpValidator
{
    public static AbaqusDeckValidation Validate(string text)
    {
        var diagnostics=new List<string>();var nodes=new Dictionary<int,Point3D>();var elements=new Dictionary<int,int[]>();var mode="";
        foreach(var raw in text.Replace("\r","").Split('\n'))
        {
            var line=raw.Trim();if(line.Length==0||line.StartsWith("**"))continue;
            if(line.StartsWith('*')){mode=line.StartsWith("*NODE",StringComparison.OrdinalIgnoreCase)?"node":line.StartsWith("*ELEMENT",StringComparison.OrdinalIgnoreCase)?"element":"";continue;}
            var parts=line.Split(',').Select(p=>p.Trim()).ToArray();
            if(mode=="node"&&parts.Length>=4&&int.TryParse(parts[0],out var nid)&&double.TryParse(parts[1],NumberStyles.Float,CultureInfo.InvariantCulture,out var x)&&double.TryParse(parts[2],NumberStyles.Float,CultureInfo.InvariantCulture,out var y)&&double.TryParse(parts[3],NumberStyles.Float,CultureInfo.InvariantCulture,out var z)){if(!nodes.TryAdd(nid,new(x,y,z)))diagnostics.Add($"duplicate-node:{nid}");}
            if(mode=="element"&&parts.Length>=9&&int.TryParse(parts[0],out var eid)){var connectivity=parts.Skip(1).Select(int.Parse).ToArray();if(!elements.TryAdd(eid,connectivity))diagnostics.Add($"duplicate-element:{eid}");}
        }
        foreach(var element in elements){if(element.Value.Any(id=>!nodes.ContainsKey(id)))diagnostics.Add($"missing-connectivity-node:{element.Key}");else if(SignedBrickVolume(element.Value.Select(id=>nodes[id]).ToArray())<=0)diagnostics.Add($"nonpositive-element-volume:{element.Key}");}
        foreach(var required in new[]{"*MATERIAL","*ELASTIC","*SOLID SECTION","*BOUNDARY","*STEP","*STATIC","*END STEP"})if(!text.Contains(required,StringComparison.OrdinalIgnoreCase))diagnostics.Add("missing-keyword:"+required);
        return new(diagnostics.Count==0,nodes.Count,elements.Count,diagnostics);
    }
    private static double SignedBrickVolume(Point3D[] p)=>((p[1]-p[0]).Cross(p[3]-p[0])).Dot(p[4]-p[0]);
}
