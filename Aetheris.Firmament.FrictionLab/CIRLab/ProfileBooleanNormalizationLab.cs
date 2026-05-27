namespace Aetheris.Firmament.FrictionLab.CIRLab;

public enum ProfileBooleanRecommendation
{
    profile_boolean_normalized,
    profile_boolean_invalid_rejected,
    profile_boolean_deferred_topology,
    profile_boolean_needs_bounded_clipping_lab,
    profile_boolean_extrude_ready_for_production_evaluation,
    profile_boolean_extrude_normalization_rejected,
    profile_boolean_extrude_deferred_topology,
    profile_boolean_extrude_emitter_blocked,
    profile_boolean_extrude_needs_production_profile_adapter
}

public abstract record ProfileBooleanExpr2D;
public sealed record ProfileBooleanRectangle(double CenterX, double CenterY, double Width, double Height) : ProfileBooleanExpr2D;
public sealed record ProfileBooleanCircle(double CenterX, double CenterY, double Radius) : ProfileBooleanExpr2D;
public sealed record ProfileBooleanCapsule(double CenterX, double CenterY, double Length, double Radius) : ProfileBooleanExpr2D;
public sealed record ProfileBooleanUnsupportedPrimitive(string Name) : ProfileBooleanExpr2D;
public sealed record ProfileBooleanDifference(ProfileBooleanExpr2D Left, IReadOnlyList<ProfileBooleanExpr2D> Rights) : ProfileBooleanExpr2D;
public sealed record ProfileBooleanUnion(IReadOnlyList<ProfileBooleanExpr2D> Operands) : ProfileBooleanExpr2D;
public sealed record ProfileBooleanIntersection(ProfileBooleanExpr2D Left, ProfileBooleanExpr2D Right) : ProfileBooleanExpr2D;

public sealed record ProfileBooleanNormalizeResult(
    LabProfileStatus Status,
    LabResolvedProfile2D? Profile,
    IReadOnlyList<string> Diagnostics,
    ProfileBooleanRecommendation Recommendation);

public sealed record ProfileBooleanNormalizationArtifact(
    string CaseName,
    string ExpressionSummary,
    LabProfileStatus Status,
    LabResolvedProfile2D? NormalizedProfile,
    int OuterLoopCount,
    int HoleCount,
    int CurveCount,
    string BoundingBox,
    IReadOnlyList<string> Diagnostics,
    ProfileBooleanRecommendation Recommendation);

public static class ProfileBooleanNormalizationLab
{
    private const double Tol = 1e-6;

    public static IReadOnlyList<ProfileBooleanNormalizationArtifact> RunAll() =>
    [
        Row("success-rect-minus-circle", new ProfileBooleanDifference(Rect(0,0,20,20), [Circle(0,0,3)])),
        Row("success-rect-minus-offcenter-circle", new ProfileBooleanDifference(Rect(0,0,30,20), [Circle(5,2,2)])),
        Row("success-rect-minus-two-circles", new ProfileBooleanDifference(Rect(0,0,30,20), [Circle(-5,0,2), Circle(5,0,2)])),
        Row("success-union-identical-rectangles", new ProfileBooleanUnion([Rect(0,0,20,20), Rect(0,0,20,20)])),
        Row("success-union-contained-rectangles", new ProfileBooleanUnion([Rect(0,0,30,20), Rect(0,0,10,5)])),
        Row("success-intersect-contained-rectangles", new ProfileBooleanIntersection(Rect(0,0,30,20), Rect(0,0,10,5))),
        Row("invalid-circle-outside", new ProfileBooleanDifference(Rect(0,0,20,20), [Circle(20,20,2)])),
        Row("invalid-circle-touches-boundary", new ProfileBooleanDifference(Rect(0,0,20,20), [Circle(7,0,3)])),
        Row("invalid-circles-overlap", new ProfileBooleanDifference(Rect(0,0,30,20), [Circle(0,0,3), Circle(4,0,3)])),
        Row("invalid-circle-radius", new ProfileBooleanDifference(Rect(0,0,20,20), [Circle(0,0,0)])),
        Row("invalid-rectangle-dimensions", new ProfileBooleanDifference(Rect(0,0,0,20), [Circle(0,0,2)])),
        Row("invalid-unsupported-primitive", new ProfileBooleanDifference(Rect(0,0,20,20), [new ProfileBooleanUnsupportedPrimitive("triangle")])),
        Row("deferred-multiple-islands", new ProfileBooleanDifference(Rect(0,0,20,20), [new ProfileBooleanRectangle(0,0,2,40)])),
        Row("deferred-disjoint-union", new ProfileBooleanUnion([Rect(-20,0,10,10), Rect(20,0,10,10)])),
        Row("deferred-partial-overlap-union", new ProfileBooleanUnion([Rect(0,0,20,20), Rect(5,0,20,20)])),
        Row("deferred-partial-overlap-intersection", new ProfileBooleanIntersection(Rect(0,0,20,20), Rect(9,0,20,20))),
        Row("deferred-nested-topology-expression", new ProfileBooleanDifference(new ProfileBooleanDifference(Rect(0,0,20,20), [Circle(0,0,4)]), [Circle(0,0,2)])),
        Row("deferred-capsule", new ProfileBooleanDifference(Rect(0,0,20,20), [new ProfileBooleanCapsule(0,0,10,2)]))
    ];

    public static ProfileBooleanNormalizeResult Normalize(ProfileBooleanExpr2D expr)
    {
        var diagnostics = new List<string> { "profile-boolean-normalization-started", "profile-boolean-no-3d-boolean-used" };
        switch (expr)
        {
            case ProfileBooleanDifference(var left, var rights): return NormalizeDifference(left, rights, diagnostics);
            case ProfileBooleanUnion(var operands): return NormalizeUnion(operands, diagnostics);
            case ProfileBooleanIntersection(var left, var right): return NormalizeIntersection(left, right, diagnostics);
            default:
                diagnostics.Add("profile-boolean-unsupported-operation");
                return Fail(diagnostics, ProfileBooleanRecommendation.profile_boolean_invalid_rejected);
        }
    }

    private static ProfileBooleanNormalizeResult NormalizeDifference(ProfileBooleanExpr2D left, IReadOnlyList<ProfileBooleanExpr2D> rights, List<string> diagnostics)
    {
        if (left is ProfileBooleanDifference) { diagnostics.Add("profile-boolean-nested-topology-deferred"); return Defer(diagnostics, ProfileBooleanRecommendation.profile_boolean_deferred_topology); }
        if (left is not ProfileBooleanRectangle r || !ValidRectangle(r)) { diagnostics.Add("profile-boolean-invalid-primitive"); return Fail(diagnostics, ProfileBooleanRecommendation.profile_boolean_invalid_rejected); }
        if (rights.Count == 0) { diagnostics.Add("profile-boolean-invalid-expression"); return Fail(diagnostics, ProfileBooleanRecommendation.profile_boolean_invalid_rejected); }
        if (rights.Any(x => x is ProfileBooleanDifference)) { diagnostics.Add("profile-boolean-nested-topology-deferred"); return Defer(diagnostics, ProfileBooleanRecommendation.profile_boolean_deferred_topology); }
        if (rights.Any(x => x is ProfileBooleanCapsule)) { diagnostics.Add("profile-boolean-capsule-deferred"); return Defer(diagnostics, ProfileBooleanRecommendation.profile_boolean_deferred_topology); }
        if (rights.Any(x => x is ProfileBooleanRectangle)) { diagnostics.Add("profile-boolean-multiple-islands-deferred"); return Defer(diagnostics, ProfileBooleanRecommendation.profile_boolean_deferred_topology); }
        var circles = new List<ProfileBooleanCircle>();
        foreach (var x in rights)
        {
            if (x is ProfileBooleanUnsupportedPrimitive) { diagnostics.Add("profile-boolean-invalid-primitive"); return Fail(diagnostics, ProfileBooleanRecommendation.profile_boolean_invalid_rejected); }
            if (x is not ProfileBooleanCircle c) { diagnostics.Add("profile-boolean-unsupported-operation"); return Fail(diagnostics, ProfileBooleanRecommendation.profile_boolean_invalid_rejected); }
            if (c.Radius <= Tol) { diagnostics.Add("profile-boolean-invalid-primitive"); return Fail(diagnostics, ProfileBooleanRecommendation.profile_boolean_invalid_rejected); }
            if (!CircleInsideRectangle(c, r)) { diagnostics.Add("profile-boolean-circle-outside-rectangle"); return Fail(diagnostics, ProfileBooleanRecommendation.profile_boolean_invalid_rejected); }
            if (CircleTouchesBoundary(c, r)) { diagnostics.Add("profile-boolean-circle-touches-boundary"); return Fail(diagnostics, ProfileBooleanRecommendation.profile_boolean_invalid_rejected); }
            circles.Add(c);
        }
        for (var i = 0; i < circles.Count; i++) for (var j = i + 1; j < circles.Count; j++) if (Distance(circles[i], circles[j]) <= circles[i].Radius + circles[j].Radius + Tol)
            { diagnostics.Add("profile-boolean-circles-overlap"); return Fail(diagnostics, ProfileBooleanRecommendation.profile_boolean_invalid_rejected); }
        diagnostics.Add(circles.Count == 1 ? "profile-boolean-difference-rectangle-circle-recognized" : "profile-boolean-difference-rectangle-multicircle-recognized");
        var profile = RectangleWithCircleHoles(r, circles);
        diagnostics.Add("profile-boolean-normalized-to-resolved-profile");
        return Success(profile, diagnostics);
    }

    private static ProfileBooleanNormalizeResult NormalizeUnion(IReadOnlyList<ProfileBooleanExpr2D> operands, List<string> diagnostics)
    {
        if (operands.Count != 2 || operands.Any(x => x is not ProfileBooleanRectangle)) { diagnostics.Add("profile-boolean-union-normalization-deferred"); return Defer(diagnostics, ProfileBooleanRecommendation.profile_boolean_needs_bounded_clipping_lab); }
        var a = (ProfileBooleanRectangle)operands[0]; var b = (ProfileBooleanRectangle)operands[1];
        if (!ValidRectangle(a) || !ValidRectangle(b)) { diagnostics.Add("profile-boolean-invalid-primitive"); return Fail(diagnostics, ProfileBooleanRecommendation.profile_boolean_invalid_rejected); }
        if (SameRect(a,b)) { var p = RectangleWithCircleHoles(a, []); diagnostics.Add("profile-boolean-normalized-to-resolved-profile"); return Success(p, diagnostics); }
        if (Contains(a,b)) { var p=RectangleWithCircleHoles(a,[]); diagnostics.Add("profile-boolean-normalized-to-resolved-profile"); return Success(p, diagnostics); }
        if (Contains(b,a)) { var p=RectangleWithCircleHoles(b,[]); diagnostics.Add("profile-boolean-normalized-to-resolved-profile"); return Success(p, diagnostics); }
        diagnostics.Add("profile-boolean-union-normalization-deferred");
        return Defer(diagnostics, ProfileBooleanRecommendation.profile_boolean_needs_bounded_clipping_lab);
    }

    private static ProfileBooleanNormalizeResult NormalizeIntersection(ProfileBooleanExpr2D left, ProfileBooleanExpr2D right, List<string> diagnostics)
    {
        if (left is not ProfileBooleanRectangle a || right is not ProfileBooleanRectangle b || !ValidRectangle(a) || !ValidRectangle(b)) { diagnostics.Add("profile-boolean-invalid-primitive"); return Fail(diagnostics, ProfileBooleanRecommendation.profile_boolean_invalid_rejected); }
        if (Contains(a,b)) { var p=RectangleWithCircleHoles(b,[]); diagnostics.Add("profile-boolean-normalized-to-resolved-profile"); return Success(p, diagnostics); }
        if (Contains(b,a)) { var p=RectangleWithCircleHoles(a,[]); diagnostics.Add("profile-boolean-normalized-to-resolved-profile"); return Success(p, diagnostics); }
        diagnostics.Add("profile-boolean-intersection-normalization-deferred");
        return Defer(diagnostics, ProfileBooleanRecommendation.profile_boolean_needs_bounded_clipping_lab);
    }

    private static ProfileBooleanNormalizationArtifact Row(string caseName, ProfileBooleanExpr2D expr)
    {
        var result = Normalize(expr);
        var profile = result.Profile;
        var holes = profile?.Loops.Skip(1).Count() ?? 0;
        var outer = profile is null ? 0 : 1;
        var curves = profile?.Loops.Sum(l => l.Curves.Count) ?? 0;
        var bbox = profile is null ? "empty" : ResolvedProfile2DLab.Evaluate(caseName, profile).BoundingBox;
        return new(caseName, expr.ToString() ?? expr.GetType().Name, result.Status, profile, outer, holes, curves, bbox, result.Diagnostics, result.Recommendation);
    }

    private static ProfileBooleanNormalizeResult Success(LabResolvedProfile2D p, List<string> d) => new(LabProfileStatus.Succeeded, p, d.Distinct().OrderBy(x => x).ToArray(), ProfileBooleanRecommendation.profile_boolean_normalized);
    private static ProfileBooleanNormalizeResult Fail(List<string> d, ProfileBooleanRecommendation r) => new(LabProfileStatus.Failed, null, d.Distinct().OrderBy(x => x).ToArray(), r);
    private static ProfileBooleanNormalizeResult Defer(List<string> d, ProfileBooleanRecommendation r) => new(LabProfileStatus.Deferred, null, d.Distinct().OrderBy(x => x).ToArray(), r);

    private static LabResolvedProfile2D RectangleWithCircleHoles(ProfileBooleanRectangle rect, IReadOnlyList<ProfileBooleanCircle> holes)
    {
        var x0 = rect.CenterX - rect.Width / 2d; var x1 = rect.CenterX + rect.Width / 2d; var y0 = rect.CenterY - rect.Height / 2d; var y1 = rect.CenterY + rect.Height / 2d;
        var loops = new List<LabAirLoop2D> { new([new LabAirLineSegment2D((x0,y0),(x1,y0)), new LabAirLineSegment2D((x1,y0),(x1,y1)), new LabAirLineSegment2D((x1,y1),(x0,y1)), new LabAirLineSegment2D((x0,y1),(x0,y0))], "outer") };
        loops.AddRange(holes.Select(h => new LabAirLoop2D([new LabAirFullCircle2D((h.CenterX, h.CenterY), h.Radius, false)], "hole")));
        return new(loops);
    }

    private static bool ValidRectangle(ProfileBooleanRectangle r) => r.Width > Tol && r.Height > Tol;
    private static bool CircleInsideRectangle(ProfileBooleanCircle c, ProfileBooleanRectangle r)
    {
        var x0 = r.CenterX - r.Width / 2d; var x1 = r.CenterX + r.Width / 2d; var y0 = r.CenterY - r.Height / 2d; var y1 = r.CenterY + r.Height / 2d;
        return c.CenterX - c.Radius >= x0 - Tol && c.CenterX + c.Radius <= x1 + Tol && c.CenterY - c.Radius >= y0 - Tol && c.CenterY + c.Radius <= y1 + Tol;
    }
    private static bool CircleTouchesBoundary(ProfileBooleanCircle c, ProfileBooleanRectangle r)
    {
        var x0 = r.CenterX - r.Width / 2d; var x1 = r.CenterX + r.Width / 2d; var y0 = r.CenterY - r.Height / 2d; var y1 = r.CenterY + r.Height / 2d;
        return Math.Abs((c.CenterX - c.Radius) - x0) <= Tol || Math.Abs((c.CenterX + c.Radius) - x1) <= Tol || Math.Abs((c.CenterY - c.Radius) - y0) <= Tol || Math.Abs((c.CenterY + c.Radius) - y1) <= Tol;
    }
    private static double Distance(ProfileBooleanCircle a, ProfileBooleanCircle b) => Math.Sqrt((a.CenterX - b.CenterX)*(a.CenterX - b.CenterX)+(a.CenterY - b.CenterY)*(a.CenterY - b.CenterY));
    private static ProfileBooleanRectangle Rect(double x,double y,double w,double h)=>new(x,y,w,h);
    private static ProfileBooleanCircle Circle(double x,double y,double r)=>new(x,y,r);
    private static bool SameRect(ProfileBooleanRectangle a, ProfileBooleanRectangle b) => Math.Abs(a.CenterX-b.CenterX)<=Tol && Math.Abs(a.CenterY-b.CenterY)<=Tol && Math.Abs(a.Width-b.Width)<=Tol && Math.Abs(a.Height-b.Height)<=Tol;
    private static bool Contains(ProfileBooleanRectangle outer, ProfileBooleanRectangle inner)
    {
        var ox0 = outer.CenterX - outer.Width/2d; var ox1 = outer.CenterX + outer.Width/2d; var oy0 = outer.CenterY - outer.Height/2d; var oy1 = outer.CenterY + outer.Height/2d;
        var ix0 = inner.CenterX - inner.Width/2d; var ix1 = inner.CenterX + inner.Width/2d; var iy0 = inner.CenterY - inner.Height/2d; var iy1 = inner.CenterY + inner.Height/2d;
        return ix0 >= ox0 - Tol && ix1 <= ox1 + Tol && iy0 >= oy0 - Tol && iy1 <= oy1 + Tol;
    }
}
