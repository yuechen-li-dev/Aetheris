namespace Aetheris.SheetMetal;

public sealed record SheetMetalDfmPolicy(
    double MinimumInsideRadiusRatio = 1d,
    double MinimumHoleToBendDistanceFactor = 2d,
    double MinimumEdgeDistanceFactor = 1.5d);

public enum SheetMetalDfmStatus { Pass, Warning, Fail, NotEvaluated }

public sealed record SheetMetalDfmFinding(
    string RuleId,
    SheetMetalDfmStatus Status,
    string Message,
    string? SubjectId,
    double? Measured,
    double? Required);

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
        }
        if(flat is not null)
        {
            findings.Add(new("sheetmetal-dfm-flat-overlap",flat.Status==FlatPatternStatus.Overlapping?SheetMetalDfmStatus.Fail:SheetMetalDfmStatus.Pass,flat.Status==FlatPatternStatus.Overlapping?"Flat material regions overlap.":"No planar-region overlap was detected.",flat.StableId,null,null));
            foreach(var cut in flat.CutLoops)
            {
                var c=new SheetPoint2(cut.Boundary.Average(p=>p.X),cut.Boundary.Average(p=>p.Y));var nearest=flat.BendLines.Count==0?double.PositiveInfinity:flat.BendLines.Min(b=>PointLineDistance(c,b.Start,b.End));var required=policy.MinimumHoleToBendDistanceFactor*part.Thickness;
                findings.Add(new("sheetmetal-dfm-hole-to-bend-distance",double.IsPositiveInfinity(nearest)?SheetMetalDfmStatus.NotEvaluated:nearest>=required?SheetMetalDfmStatus.Pass:SheetMetalDfmStatus.Warning,double.IsPositiveInfinity(nearest)?"No bend line is available for this cut.":$"Cut-center to nearest bend line is {nearest:G4}; experimental policy requires {required:G4}.",cut.FeatureId,double.IsPositiveInfinity(nearest)?null:nearest,required));
            }
        }
        return new(findings);
    }
    private static double PointLineDistance(SheetPoint2 p,SheetPoint2 a,SheetPoint2 b){var dx=b.X-a.X;var dy=b.Y-a.Y;var l2=dx*dx+dy*dy;if(l2<=1e-20)return Math.Sqrt((p.X-a.X)*(p.X-a.X)+(p.Y-a.Y)*(p.Y-a.Y));var t=Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/l2,0,1);var x=a.X+t*dx;var y=a.Y+t*dy;return Math.Sqrt((p.X-x)*(p.X-x)+(p.Y-y)*(p.Y-y));}
}
