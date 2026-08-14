using Aetheris.Kernel.Core.Math;

namespace Aetheris.SheetMetal;

public sealed record SheetMetalFlatPatternValidationReport(
    bool Finite,
    bool LoopsClosed,
    bool FeaturesContained,
    IReadOnlyList<(string A,string B)> Overlaps,
    FlatPatternStatus Status,
    IReadOnlyList<SheetMetalDiagnostic> Diagnostics);

public static class SheetMetalFlatPatternValidation
{
    public static SheetMetalFlatPatternValidationReport Validate(SheetMetalFlatPatternIr flat)
    {
        ArgumentNullException.ThrowIfNull(flat);var diagnostics=new List<SheetMetalDiagnostic>();
        var finite=flat.Regions2D.SelectMany(r=>r.Boundary).Concat(flat.CutLoops.SelectMany(c=>c.Boundary)).All(p=>double.IsFinite(p.X)&&double.IsFinite(p.Y));
        if(!finite)diagnostics.Add(new(SheetMetalDiagnosticCodes.FeatureMappingFailure,SheetMetalDiagnosticSeverity.Error,"Flat pattern contains non-finite coordinates."));
        var closed=flat.Regions2D.All(r=>r.Boundary.Count>=3)&&flat.CutLoops.All(c=>c.Boundary.Count>=3);
        if(!closed)diagnostics.Add(new(SheetMetalDiagnosticCodes.FeatureMappingFailure,SheetMetalDiagnosticSeverity.Error,"A flat region or cut loop has fewer than three boundary points."));
        var overlaps=FindOverlaps(flat.Regions2D.Where(r=>r.Kind==SheetRegionKind.Planar).ToArray());
        if(overlaps.Count>0)diagnostics.Add(new(SheetMetalDiagnosticCodes.FlatOverlap,SheetMetalDiagnosticSeverity.Error,$"Flat planar regions overlap: {string.Join(", ",overlaps.Select(x=>$"{x.A}/{x.B}"))}."));
        var contained=flat.CutLoops.All(c=>flat.Regions2D.Any(r=>r.SourceRegionId==c.SourceRegionId&&c.Boundary.All(p=>PointInConvex(p,r.Boundary))));
        if(!contained)diagnostics.Add(new(SheetMetalDiagnosticCodes.FeatureMappingFailure,SheetMetalDiagnosticSeverity.Warning,"One or more cut loops are not contained by their owning flat region."));
        var status=!finite||!closed?FlatPatternStatus.Unsupported:overlaps.Count>0?FlatPatternStatus.Overlapping:flat.Status;
        return new(finite,closed,contained,overlaps,status,diagnostics);
    }

    public static IReadOnlyList<(string A,string B)> FindOverlaps(IReadOnlyList<FlatRegion2D> regions)
    {
        var result=new List<(string,string)>();for(var i=0;i<regions.Count;i++)for(var j=i+1;j<regions.Count;j++)if(Overlap(regions[i].Boundary,regions[j].Boundary))result.Add((regions[i].SourceRegionId,regions[j].SourceRegionId));return result;
    }
    private static bool Overlap(IReadOnlyList<SheetPoint2>a,IReadOnlyList<SheetPoint2>b){if(a.Count<3||b.Count<3)return false;foreach(var axis in Axes(a).Concat(Axes(b))){var aa=a.Select(p=>Dot(p,axis)).ToArray();var bb=b.Select(p=>Dot(p,axis)).ToArray();if(Math.Min(aa.Max(),bb.Max())-Math.Max(aa.Min(),bb.Min())<=1e-7)return false;}return true;}
    private static IEnumerable<SheetPoint2> Axes(IReadOnlyList<SheetPoint2>p){for(var i=0;i<p.Count;i++){var e=Sub(p[(i+1)%p.Count],p[i]);var axis=new SheetPoint2(-e.Y,e.X);var len=Math.Sqrt(Dot(axis,axis));yield return len<=1e-12?new(1,0):new(axis.X/len,axis.Y/len);}}
    private static bool PointInConvex(SheetPoint2 p,IReadOnlyList<SheetPoint2>poly){if(poly.Count<3)return false;double? sign=null;for(var i=0;i<poly.Count;i++){var a=poly[i];var b=poly[(i+1)%poly.Count];var c=(b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X);if(Math.Abs(c)<=1e-7)continue;var s=Math.Sign(c);if(sign is null)sign=s;else if(sign!=s)return false;}return true;}
    private static SheetPoint2 Sub(SheetPoint2 a,SheetPoint2 b)=>new(a.X-b.X,a.Y-b.Y);private static double Dot(SheetPoint2 a,SheetPoint2 b)=>a.X*b.X+a.Y*b.Y;
}

public sealed record SheetMetalRoundTripReport(bool IsWithinTolerance,double MaximumPointResidual,double MaximumBendAngleResidual,IReadOnlyList<SheetMetalDiagnostic> Diagnostics);

public static class SheetMetalRoundTrip
{
    /// <summary>Reconstructs authored planar reference points from flat mappings and verifies stored bend angles.</summary>
    public static SheetMetalRoundTripReport ValidateReferenceSurface(SheetMetalPartIr part,SheetMetalFlatPatternIr flat,double tolerance=1e-8)
    {
        var maxPoint=0d;var diagnostics=new List<SheetMetalDiagnostic>();
        foreach(var mapping in flat.SourceToFlatMappings)
        {
            var region=part.Regions.First(r=>r.StableId==mapping.SourceRegionId);if(region.Plane is null)continue;
            foreach(var source in region.Boundary3D){var d=source-mapping.PlaneOrigin;var flatPoint=Add(mapping.FlatOrigin,Add(Scale(mapping.FlatU,d.Dot(mapping.SourceU)),Scale(mapping.FlatV,d.Dot(mapping.SourceV))));if(!TryInverse(mapping,flatPoint,out var u,out var v)){maxPoint=double.PositiveInfinity;continue;}var reconstructed=mapping.PlaneOrigin+mapping.SourceU*u+mapping.SourceV*v;var projected=source-region.Plane.Normal*((source-region.Plane.Origin).Dot(region.Plane.Normal));maxPoint=Math.Max(maxPoint,(reconstructed-projected).Length);}
        }
        var maxAngle=0d;foreach(var bend in part.Bends){var a=part.Regions.First(r=>r.StableId==bend.AdjacentRegionA).Plane;var b=part.Regions.First(r=>r.StableId==bend.AdjacentRegionB).Plane;if(a is null||b is null)continue;var angle=Math.Acos(Math.Clamp(Math.Abs(a.Normal.Dot(b.Normal)),-1,1));maxAngle=Math.Max(maxAngle,Math.Abs(angle-bend.BendAngleRadians));}
        var pass=maxPoint<=tolerance&&maxAngle<=tolerance;if(!pass)diagnostics.Add(new(SheetMetalDiagnosticCodes.UnsupportedBendTopology,SheetMetalDiagnosticSeverity.Error,$"Reference re-fold residuals exceed tolerance {tolerance:G6}: point={maxPoint:G6}, angle={maxAngle:G6}."));return new(pass,maxPoint,maxAngle,diagnostics);
    }
    private static bool TryInverse(SourceToFlatMapping m,SheetPoint2 p,out double u,out double v){var q=Sub(p,m.FlatOrigin);var det=m.FlatU.X*m.FlatV.Y-m.FlatU.Y*m.FlatV.X;if(Math.Abs(det)<=1e-14){u=v=0;return false;}u=(q.X*m.FlatV.Y-q.Y*m.FlatV.X)/det;v=(m.FlatU.X*q.Y-m.FlatU.Y*q.X)/det;return true;}
    private static SheetPoint2 Add(SheetPoint2 a,SheetPoint2 b)=>new(a.X+b.X,a.Y+b.Y);private static SheetPoint2 Sub(SheetPoint2 a,SheetPoint2 b)=>new(a.X-b.X,a.Y-b.Y);private static SheetPoint2 Scale(SheetPoint2 a,double s)=>new(a.X*s,a.Y*s);
}
