using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.SheetMetal;

public sealed record SheetMetalFlatPatternValidationReport(
    bool Finite,
    bool LoopsClosed,
    bool FeaturesContained,
    bool ExactContoursValid,
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
        var slivers=flat.Regions2D.Where(r=>Math.Abs(SignedArea(r.Boundary))<=1e-10).Select(r=>r.SourceRegionId).ToArray();
        if(slivers.Length>0)diagnostics.Add(new(SheetMetalDiagnosticCodes.ZeroWidthSliver,SheetMetalDiagnosticSeverity.Error,$"Flat regions have zero/negligible area: {string.Join(", ",slivers)}."));
        var duplicateCuts=flat.CutLoops.GroupBy(c=>BoundaryKey(c.Boundary),StringComparer.Ordinal).Where(g=>g.Count()>1).SelectMany(g=>g.Select(c=>c.FeatureId)).ToArray();
        if(duplicateCuts.Length>0)diagnostics.Add(new(SheetMetalDiagnosticCodes.DuplicateCut,SheetMetalDiagnosticSeverity.Error,$"Coincident cut loops were detected: {string.Join(", ",duplicateCuts)}."));
        var overlaps=FindOverlaps(flat.Regions2D.Where(r=>r.Kind==SheetRegionKind.Planar).ToArray());
        if(overlaps.Count>0)diagnostics.Add(new(SheetMetalDiagnosticCodes.FlatOverlap,SheetMetalDiagnosticSeverity.Error,$"Flat planar regions overlap: {string.Join(", ",overlaps.Select(x=>$"{x.A}/{x.B}"))}."));
        var contained=flat.CutLoops.All(c=>flat.Regions2D.Any(r=>r.SourceRegionId==c.SourceRegionId&&c.Boundary.All(p=>PointInConvex(p,r.Boundary))));
        if(!contained)diagnostics.Add(new(SheetMetalDiagnosticCodes.FeatureMappingFailure,SheetMetalDiagnosticSeverity.Warning,"One or more cut loops are not contained by their owning flat region."));
        var bendLinesInside=flat.BendLines.All(b=>flat.Regions2D.Any(r=>PointInPolygon(b.Start,r.Boundary,true))&&flat.Regions2D.Any(r=>PointInPolygon(b.End,r.Boundary,true)));
        if(!bendLinesInside)diagnostics.Add(new(SheetMetalDiagnosticCodes.BendLineOutsideMaterial,SheetMetalDiagnosticSeverity.Error,"One or more bend-line endpoints lie outside recovered flat material."));
        var exactContoursValid=true;
        foreach(var contour in new[]{flat.ExactBlankContour}.Concat(flat.Regions2D.Select(r=>r.ExactContour)).Concat(flat.CutLoops.Select(c=>c.ExactContour)).Concat((flat.ReliefLoops??[]).Select(r=>(PlanarContour2?)r.ExactContour)).OfType<PlanarContour2>())
        {
            var validation=PlanarContourKernel.Validate(contour);exactContoursValid&=validation.IsValid;
            diagnostics.AddRange(validation.Diagnostics.Where(x=>x.Severity==PlanarContourDiagnosticSeverity.Error).Select(x=>new SheetMetalDiagnostic(SheetMetalDiagnosticCodes.ImpossibleTopology,SheetMetalDiagnosticSeverity.Error,$"Exact contour '{contour.StableId}': {x.Code}: {x.Message}")));
        }
        var status=!finite||!closed||slivers.Length>0||duplicateCuts.Length>0||!bendLinesInside||!exactContoursValid?FlatPatternStatus.Unsupported:overlaps.Count>0?FlatPatternStatus.Overlapping:flat.Status;
        return new(finite,closed,contained,exactContoursValid,overlaps,status,diagnostics);
    }

    public static IReadOnlyList<(string A,string B)> FindOverlaps(IReadOnlyList<FlatRegion2D> regions)
    {
        var result=new List<(string,string)>();for(var i=0;i<regions.Count;i++)for(var j=i+1;j<regions.Count;j++)if(Overlap(regions[i].Boundary,regions[j].Boundary))result.Add((regions[i].SourceRegionId,regions[j].SourceRegionId));return result;
    }
    private static bool Overlap(IReadOnlyList<SheetPoint2>a,IReadOnlyList<SheetPoint2>b){if(a.Count<3||b.Count<3)return false;for(var i=0;i<a.Count;i++)for(var j=0;j<b.Count;j++)if(Cross(a[i],a[(i+1)%a.Count],b[j])*Cross(a[i],a[(i+1)%a.Count],b[(j+1)%b.Count])< -1e-14&&Cross(b[j],b[(j+1)%b.Count],a[i])*Cross(b[j],b[(j+1)%b.Count],a[(i+1)%a.Count])< -1e-14)return true;return Probes(a).Any(p=>PointInPolygon(p,b,false))||Probes(b).Any(p=>PointInPolygon(p,a,false));}
    private static IEnumerable<SheetPoint2> Probes(IReadOnlyList<SheetPoint2> p){var area=0d;for(var i=0;i<p.Count;i++){var q=p[(i+1)%p.Count];area+=p[i].X*q.Y-q.X*p[i].Y;}var sign=area>=0?1d:-1d;for(var i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];var dx=b.X-a.X;var dy=b.Y-a.Y;var len=Math.Sqrt(dx*dx+dy*dy);if(len>1e-12)yield return new((a.X+b.X)/2-sign*dy/len*1e-6,(a.Y+b.Y)/2+sign*dx/len*1e-6);}}
    private static bool PointInConvex(SheetPoint2 p,IReadOnlyList<SheetPoint2>poly)=>PointInPolygon(p,poly,true);
    private static bool PointInPolygon(SheetPoint2 p,IReadOnlyList<SheetPoint2>poly,bool boundaryInside){if(poly.Count<3)return false;var inside=false;for(var i=0;i<poly.Count;i++){var a=poly[i];var b=poly[(i+1)%poly.Count];if(Math.Abs(Cross(a,b,p))<1e-8&&p.X>=Math.Min(a.X,b.X)-1e-8&&p.X<=Math.Max(a.X,b.X)+1e-8&&p.Y>=Math.Min(a.Y,b.Y)-1e-8&&p.Y<=Math.Max(a.Y,b.Y)+1e-8)return boundaryInside;if((a.Y>p.Y)!=(b.Y>p.Y)&&p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X)inside=!inside;}return inside;}
    private static double Cross(SheetPoint2 a,SheetPoint2 b,SheetPoint2 c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
    private static double SignedArea(IReadOnlyList<SheetPoint2> p){var sum=0d;for(var i=0;i<p.Count;i++){var q=p[(i+1)%p.Count];sum+=p[i].X*q.Y-q.X*p[i].Y;}return sum/2d;}
    private static string BoundaryKey(IReadOnlyList<SheetPoint2> p)=>string.Join('|',p.Select(x=>$"{Math.Round(x.X,7):R},{Math.Round(x.Y,7):R}").Order(StringComparer.Ordinal));
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
