using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.SheetMetal;

public sealed record SheetMetalDfmPolicy(
    double MinimumInsideRadiusRatio = 1d,
    double MinimumHoleToBendDistanceFactor = 2d,
    double MinimumEdgeDistanceFactor = 1.5d,
    double MinimumFlangeBeyondTangentFactor = 1d);

public enum SheetMetalDfmStatus { Pass, Warning, Fail, NotEvaluated }

public sealed record SheetMetalDfmFinding(
    string RuleId,
    SheetMetalDfmStatus Status,
    string Message,
    string? SubjectId,
    double? Measured,
    double? Required,
    string? SuggestedFix = null);

public sealed record SheetMetalDfmReport(IReadOnlyList<SheetMetalDfmFinding> Findings)
{
    public SheetMetalDfmStatus Overall => Findings.Any(f=>f.Status==SheetMetalDfmStatus.Fail)?SheetMetalDfmStatus.Fail:
        Findings.Any(f=>f.Status==SheetMetalDfmStatus.Warning)?SheetMetalDfmStatus.Warning:SheetMetalDfmStatus.Pass;
}

public static class SheetMetalDfm
{
    public static SheetMetalDfmReport Evaluate(SheetMetalPartIr part,SheetMetalFlatPatternIr? flat=null,SheetMetalDfmPolicy? policy=null)
    {
        ArgumentNullException.ThrowIfNull(part);policy??=new();var findings=new List<SheetMetalDfmFinding>();
        findings.Add(new("sheetmetal-dfm-thickness-positive",part.Thickness>0?SheetMetalDfmStatus.Pass:SheetMetalDfmStatus.Fail,part.Thickness>0?"Thickness is positive.":"Thickness must be positive.",part.StableId,part.Thickness,0d));
        foreach(var bend in part.Bends)
        {
            var ratio=bend.InsideRadius/part.Thickness;var pass=ratio>=policy.MinimumInsideRadiusRatio;
            findings.Add(new("sheetmetal-dfm-inside-radius-ratio",pass?SheetMetalDfmStatus.Pass:SheetMetalDfmStatus.Warning,$"Inside-radius/thickness ratio {ratio:G4} {(pass?"meets":"is below")} the parameterized experimental policy {policy.MinimumInsideRadiusRatio:G4}.",bend.StableId,ratio,policy.MinimumInsideRadiusRatio));
            var flange=part.Regions.FirstOrDefault(r=>r.StableId==bend.AdjacentRegionB);var axisLength=part.Regions.Where(r=>r.Cylinder is not null).OrderBy(r=>Math.Abs(r.Cylinder!.InsideRadius-bend.InsideRadius)+(1-Math.Abs(r.Cylinder.AxisDirection.Dot(bend.AxisDirection)))*1000).FirstOrDefault()?.Cylinder?.AxisLength??0;var flangeLength=flange is null||axisLength<=0?double.NaN:flange.ApproximateArea/axisLength;var requiredFlange=bend.InsideRadius+part.Thickness*policy.MinimumFlangeBeyondTangentFactor;var flangePass=double.IsNaN(flangeLength)||flangeLength>=requiredFlange;
            findings.Add(new("sheetmetal-dfm-minimum-flange-length",double.IsNaN(flangeLength)?SheetMetalDfmStatus.NotEvaluated:flangePass?SheetMetalDfmStatus.Pass:SheetMetalDfmStatus.Warning,double.IsNaN(flangeLength)?$"'{bend.AdjacentRegionB}' flange length could not be derived.":$"'{bend.AdjacentRegionB}' tangent-to-edge length is {flangeLength:G4} mm; policy requires {requiredFlange:G4} mm.",bend.AdjacentRegionB,double.IsNaN(flangeLength)?null:flangeLength,requiredFlange,!flangePass?$"Increase {bend.AdjacentRegionB} Height/Length by at least {requiredFlange-flangeLength:G4} mm.":null));
        }
        foreach(var corner in part.Corners??[])
        {
            var relief=(part.Reliefs??[]).FirstOrDefault(r=>r.CornerId==corner.StableId);var requiresRelief=corner.Policy is SheetCornerPolicy.RectangularRelief or SheetCornerPolicy.RoundRelief;var minimumDepth=(part.Bends.Where(b=>b.AdjacentRegionB==corner.RegionA||b.AdjacentRegionB==corner.RegionB).Select(b=>b.InsideRadius).DefaultIfEmpty(0).Max()+part.Thickness);var valid=!requiresRelief||relief is not null&&relief.Width>=part.Thickness&&relief.Depth>=minimumDepth;
            findings.Add(new("sheetmetal-dfm-corner-resolution",valid?SheetMetalDfmStatus.Pass:SheetMetalDfmStatus.Warning,valid?$"'{corner.StableId}' uses bounded {corner.Policy} corner resolution.":$"'{corner.StableId}' has insufficient or missing relief geometry.",corner.StableId,relief?.Depth,requiresRelief?minimumDepth:part.Thickness,valid?null:$"Increase {corner.ReliefId??"the corner relief"} width to at least {part.Thickness:G4} mm and depth to at least {minimumDepth:G4} mm."));
        }
        if(flat is not null)
        {
            var exactValid=flat.ExactBlankContour is not null&&SheetMetalFlatPatternValidation.Validate(flat).ExactContoursValid;findings.Add(new("sheetmetal-dfm-exact-blank-contour",exactValid?SheetMetalDfmStatus.Pass:SheetMetalDfmStatus.Fail,exactValid?"One validated exact analytic blank contour is authoritative.":"No validated exact analytic blank contour is available.",flat.StableId,null,null,exactValid?null:"Resolve the reported corner/contour topology before fabrication export."));
            foreach(var relief in part.Reliefs??[]){var flatRelief=(flat.ReliefLoops??[]).FirstOrDefault(x=>x.ReliefId==relief.StableId);var valid=flatRelief is not null&&PlanarContourKernel.Validate(flatRelief.ExactContour).IsValid;findings.Add(new("sheetmetal-dfm-exact-relief-topology",valid?SheetMetalDfmStatus.Pass:SheetMetalDfmStatus.Fail,valid?$"'{relief.StableId}' has exact {relief.Kind} line/arc removal geometry.":$"'{relief.StableId}' is missing valid exact flat relief geometry.",relief.StableId,relief.Depth,relief.Width,valid?null:$"Rebuild {relief.StableId} from its typed corner policy."));}
            findings.Add(new("sheetmetal-dfm-flat-overlap",flat.Status==FlatPatternStatus.Overlapping?SheetMetalDfmStatus.Fail:SheetMetalDfmStatus.Pass,flat.Status==FlatPatternStatus.Overlapping?"Flat material regions overlap.":"No planar-region overlap was detected.",flat.StableId,null,null));
            foreach(var cut in flat.CutLoops)
            {
                var semanticSubject=$"{part.StableId.Replace("sheetmetal-",string.Empty,StringComparison.Ordinal)}.{cut.SourceRegionId}.{cut.FeatureId}";
                var c=new SheetPoint2(cut.Boundary.Average(p=>p.X),cut.Boundary.Average(p=>p.Y));var nearest=flat.BendLines.Count==0?double.PositiveInfinity:cut.Boundary.Min(p=>flat.BendLines.Min(b=>PointLineDistance(p,b.Start,b.End)));var required=policy.MinimumHoleToBendDistanceFactor*part.Thickness;
                findings.Add(new("sheetmetal-dfm-cut-to-bend-distance",double.IsPositiveInfinity(nearest)?SheetMetalDfmStatus.NotEvaluated:nearest>=required?SheetMetalDfmStatus.Pass:SheetMetalDfmStatus.Warning,double.IsPositiveInfinity(nearest)?"No bend line is available for this cut.":$"'{semanticSubject}' boundary to nearest bend line is {nearest:G4} mm; parameterized policy requires {required:G4} mm.",semanticSubject,double.IsPositiveInfinity(nearest)?null:nearest,required,!double.IsPositiveInfinity(nearest)&&nearest<required?$"Move {semanticSubject} at least {required-nearest:G4} mm farther from the nearest bend, subject to engineer review.":null));
                var owner=flat.Regions2D.FirstOrDefault(r=>r.SourceRegionId==cut.SourceRegionId);var edge=owner is null?double.PositiveInfinity:cut.Boundary.Min(p=>Enumerable.Range(0,owner.Boundary.Count).Min(i=>PointLineDistance(p,owner.Boundary[i],owner.Boundary[(i+1)%owner.Boundary.Count])));
                var edgeRequired=policy.MinimumEdgeDistanceFactor*part.Thickness;
                findings.Add(new("sheetmetal-dfm-cut-to-edge-distance",edge>=edgeRequired?SheetMetalDfmStatus.Pass:SheetMetalDfmStatus.Warning,$"'{semanticSubject}' boundary to nearest recovered source edge is {edge:G4} mm; bounded policy requires {edgeRequired:G4} mm.",semanticSubject,edge,edgeRequired,edge<edgeRequired?$"Move {semanticSubject} at least {edgeRequired-edge:G4} mm inward, then re-run exact boundary validation.":null));
            }
        }
        return new(findings);
    }
    private static double PointLineDistance(SheetPoint2 p,SheetPoint2 a,SheetPoint2 b){var dx=b.X-a.X;var dy=b.Y-a.Y;var l2=dx*dx+dy*dy;if(l2<=1e-20)return Math.Sqrt((p.X-a.X)*(p.X-a.X)+(p.Y-a.Y)*(p.Y-a.Y));var t=Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/l2,0,1);var x=a.X+t*dx;var y=a.Y+t*dy;return Math.Sqrt((p.X-x)*(p.X-x)+(p.Y-y)*(p.Y-y));}
}
