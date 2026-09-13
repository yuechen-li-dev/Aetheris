using System.Globalization;
using System.Text;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Regions.Analytic;
using Aetheris.FEA.Analysis;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FEA.Mechanics;

/// <summary>Deterministic diagnostic views for the experimental parameter grid and mapped solution.</summary>
public static class ThinWallDebugArtifacts
{
    public static string ParameterDomainSvg(LinearElasticAnalysisIr analysis)
    {
        var settings=analysis.ExperimentalShell??throw new ArgumentException("ExperimentalShell settings are required.",nameof(analysis));
        if(analysis.MidsurfacePatchGraph is not null)return NativeParameterDomainSvg(analysis,settings);
        var bounds=analysis.Body.ContinuumRegion.Bounds;var classifier=(IBoundsClassificationCapability)analysis.Body.ContinuumRegion;
        const double ox=50,oy=35,w=520,h=340;var dx=(bounds.Max.X-bounds.Min.X)/settings.MasterCellsR;var dy=(bounds.Max.Y-bounds.Min.Y)/settings.MasterCellsS;
        double X(double value)=>ox+(value-bounds.Min.X)/(bounds.Max.X-bounds.Min.X)*w;
        double Y(double value)=>oy+h-(value-bounds.Min.Y)/(bounds.Max.Y-bounds.Min.Y)*h;
        var svg=Start("ExperimentalShell parameter domain",640,420);
        for(var j=0;j<settings.MasterCellsS;j++)for(var i=0;i<settings.MasterCellsR;i++)
        {
            var cell=new BoundingBox3D(new(bounds.Min.X+i*dx,bounds.Min.Y+j*dy,bounds.Min.Z),new(bounds.Min.X+(i+1)*dx,bounds.Min.Y+(j+1)*dy,bounds.Max.Z));
            var kind=classifier.ClassifyBounds(cell);var fill=kind switch{ContinuumBoundsClassification.Inside=>"#b7e4c7",ContinuumBoundsClassification.Outside=>"#e5e7eb",_=>"#ffd166"};
            svg.Append(Invariant($"<rect x='{X(cell.Min.X)}' y='{Y(cell.Max.Y)}' width='{w/settings.MasterCellsR}' height='{h/settings.MasterCellsS}' fill='{fill}' stroke='#334155' stroke-width='2'/><text x='{X(cell.Min.X)+8}' y='{Y(cell.Max.Y)+20}' font-size='12'>{i},{j} {kind}</text>"));
        }
        if(analysis.Body.ContinuumRegion is BlockWithCylindricalHoleRegion hole)
            svg.Append(Invariant($"<circle cx='{X(hole.HoleCenter.X)}' cy='{Y(hole.HoleCenter.Y)}' r='{hole.HoleRadius/(bounds.Max.X-bounds.Min.X)*w}' fill='white' stroke='#dc2626' stroke-width='3'/>") );
        svg.Append(Invariant($"<text x='{ox}' y='400' font-size='13'>grid {settings.MasterCellsR}x{settings.MasterCellsS}; p={settings.PolynomialOrder}; alpha={settings.FictitiousStiffnessFactor:R}; depth={settings.MaxSubdivisionDepth}</text></g></svg>"));
        return svg.ToString();
    }

    public static string MappedAndDeformedSvg(LinearElasticAnalysisIr analysis,LinearElasticAnalysisResult result)
    {
        var settings=analysis.ExperimentalShell??throw new ArgumentException("ExperimentalShell settings are required.",nameof(analysis));var bounds=analysis.Body.ContinuumRegion.Bounds;
        var synthetic=analysis.MidsurfacePatchGraph is null?new ThinWallGeometryMap(settings,bounds):null;
        var patches=analysis.MidsurfacePatchGraph?.Patches??[];const double ox=55,oy=330,w=760,h=255;var maxU=double.Max(result.MaximumDisplacementMeters,1e-30);
        var physical=patches.Count>0?patches.SelectMany(p=>Corners(p)).ToArray():new[]{synthetic!.Evaluate(bounds.Min.X,bounds.Min.Y,0).Position,synthetic.Evaluate(bounds.Max.X,bounds.Max.Y,0).Position};
        var minX=physical.Min(x=>x.X+.25*x.Y);var maxX=physical.Max(x=>x.X+.25*x.Y);var minZ=physical.Min(x=>x.Z);var maxZ=physical.Max(x=>x.Z);var span=double.Max(maxX-minX,maxZ-minZ);var scale=.12*span/maxU;
        (double X,double Y) Project(Point3D p)=>(ox+(p.X+.25*p.Y-minX)/span*w,oy-(p.Z-minZ)/span*h);
        var displacements=result.Displacements.GroupBy(item=>Round(item.Position)).ToDictionary(group=>group.Key,group=>group.First().DisplacementMeters);var svg=Start("Mapped cells and amplified deformation",880,390);
        if(patches.Count==0)DrawPatch(bounds,(r,s,t)=>synthetic!.Evaluate(r,s,t));else foreach(var patch in patches)DrawPatch(patch.ParameterDomain.Bounds,(r,s,t)=>ThinWallAnalysisAuthority.Evaluate(patch,r,s,t));
        svg.Append(Invariant($"<text x='55' y='370' font-size='13'>blue: mapped midsurface; red: deformation amplified {scale:R}x (diagnostic only)</text></g></svg>"));return svg.ToString();

        void DrawPatch(BoundingBox3D patchBounds,Func<double,double,double,ThinWallMapEvaluation> map)
        {
            for(var j=0;j<=settings.MasterCellsS;j++)for(var i=0;i<settings.MasterCellsR;i++)Line(i,j,i+1,j);
            for(var i=0;i<=settings.MasterCellsR;i++)for(var j=0;j<settings.MasterCellsS;j++)Line(i,j,i,j+1);
            void Line(int i0,int j0,int i1,int j1)
            {
            var r0=patchBounds.Min.X+(patchBounds.Max.X-patchBounds.Min.X)*i0/settings.MasterCellsR;var s0=patchBounds.Min.Y+(patchBounds.Max.Y-patchBounds.Min.Y)*j0/settings.MasterCellsS;
            var r1=patchBounds.Min.X+(patchBounds.Max.X-patchBounds.Min.X)*i1/settings.MasterCellsR;var s1=patchBounds.Min.Y+(patchBounds.Max.Y-patchBounds.Min.Y)*j1/settings.MasterCellsS;
            var p0=map(r0,s0,0).Position;var p1=map(r1,s1,0).Position;var a=Project(p0);var b=Project(p1);
            svg.Append(Invariant($"<line x1='{a.X}' y1='{a.Y}' x2='{b.X}' y2='{b.Y}' stroke='#2563eb' stroke-width='2'/>") );
            var d0=Displacement(r0,s0,p0,map);var d1=Displacement(r1,s1,p1,map);var da=Project(p0+d0*scale);var db=Project(p1+d1*scale);
            svg.Append(Invariant($"<line x1='{da.X}' y1='{da.Y}' x2='{db.X}' y2='{db.Y}' stroke='#dc2626' stroke-width='2'/>") );
            }
        }
        Vector3D Displacement(double r,double s,Point3D p,Func<double,double,double,ThinWallMapEvaluation> map)
        {
            var top=map(r,s,1).Position;
            if(displacements.TryGetValue(Round(top),out var value))return value;
            return result.Displacements.OrderBy(item=>(item.Position-p).LengthSquared).FirstOrDefault()?.DisplacementMeters??Vector3D.Zero;
        }
        static IEnumerable<Point3D> Corners(MidsurfacePatch patch){var b=patch.ParameterDomain.Bounds;yield return ThinWallAnalysisAuthority.Evaluate(patch,b.Min.X,b.Min.Y,0).Position;yield return ThinWallAnalysisAuthority.Evaluate(patch,b.Max.X,b.Min.Y,0).Position;yield return ThinWallAnalysisAuthority.Evaluate(patch,b.Max.X,b.Max.Y,0).Position;yield return ThinWallAnalysisAuthority.Evaluate(patch,b.Min.X,b.Max.Y,0).Position;}
    }

    private static string NativeParameterDomainSvg(LinearElasticAnalysisIr analysis,ExperimentalShellSettings settings)
    {
        var graph=analysis.MidsurfacePatchGraph!;const double left=35,top=50,panelWidth=230,panelHeight=245,gap=25;var width=(int)(70+graph.Patches.Count*(panelWidth+gap));var svg=Start("Native SheetMetal midsurface patch graph",width,350);
        for(var index=0;index<graph.Patches.Count;index++)
        {
            var patch=graph.Patches[index];var b=patch.ParameterDomain.Bounds;var ox=left+index*(panelWidth+gap);var dx=(b.Max.X-b.Min.X)/settings.MasterCellsR;var ds=(b.Max.Y-b.Min.Y)/settings.MasterCellsS;
            double X(double r)=>ox+(r-b.Min.X)/(b.Max.X-b.Min.X)*panelWidth;double Y(double s)=>top+panelHeight-(s-b.Min.Y)/(b.Max.Y-b.Min.Y)*panelHeight;
            for(var j=0;j<settings.MasterCellsS;j++)for(var i=0;i<settings.MasterCellsR;i++)
            {
                var cell=new BoundingBox3D(new(b.Min.X+i*dx,b.Min.Y+j*ds,b.Min.Z),new(b.Min.X+(i+1)*dx,b.Min.Y+(j+1)*ds,b.Max.Z));var kind=((IBoundsClassificationCapability)patch.ParameterDomain).ClassifyBounds(cell);var fill=kind switch{ContinuumBoundsClassification.Inside=>"#b7e4c7",ContinuumBoundsClassification.Outside=>"#e5e7eb",_=>"#ffd166"};
                svg.Append(Invariant($"<rect x='{X(cell.Min.X)}' y='{Y(cell.Max.Y)}' width='{panelWidth/settings.MasterCellsR}' height='{panelHeight/settings.MasterCellsS}' fill='{fill}' stroke='#334155' stroke-width='1.5'/>") );
            }
            svg.Append(Invariant($"<text x='{ox}' y='315' font-size='12'>{patch.Identity} | {patch.Kind} | openings {patch.OpeningCount}</text>"));
        }
        svg.Append(Invariant($"<text x='{left}' y='338' font-size='12'>interfaces {graph.Interfaces.Count}; exact C0 shared trace DOFs; grid {settings.MasterCellsR}x{settings.MasterCellsS}; p={settings.PolynomialOrder}</text></g></svg>"));return svg.ToString();
    }

    private static string Round(Point3D p)=>Invariant($"{p.X:F12}|{p.Y:F12}|{p.Z:F12}");
    private static StringBuilder Start(string title,int width,int height)=>new StringBuilder(Invariant($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'><rect width='100%' height='100%' fill='white'/><text x='24' y='24' font-family='sans-serif' font-size='17' font-weight='bold'>{title}</text><g font-family='sans-serif'>"));
    private static string Invariant(FormattableString value)=>value.ToString(CultureInfo.InvariantCulture);
}
