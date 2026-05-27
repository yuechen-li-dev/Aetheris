using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record ProfileBooleanExtrudePipelineRow(string CaseName, ProfileBooleanExpr2D Expression, double Height);
public sealed record ProfileBooleanExtrudePipelineResult(
    string CaseName,
    LabProfileStatus Status,
    int OuterLoopCount,
    int HoleCount,
    int PlanarFaceCount,
    int CylindricalFaceCount,
    bool StepSmokePassed,
    IReadOnlyList<string> Diagnostics,
    ProfileBooleanRecommendation Recommendation);

public static class ProfileBooleanExtrudePipelineLab
{
    private static readonly string[] Required = ["ISO-10303-21","MANIFOLD_SOLID_BREP","ADVANCED_FACE","PLANE","CYLINDRICAL_SURFACE"];

    public static IReadOnlyList<ProfileBooleanExtrudePipelineResult> RunAll() =>
    [
        Evaluate(new("success-centered", new ProfileBooleanDifference(new ProfileBooleanRectangle(0,0,20,20), [new ProfileBooleanCircle(0,0,3)]), 10)),
        Evaluate(new("success-offcenter", new ProfileBooleanDifference(new ProfileBooleanRectangle(0,0,30,20), [new ProfileBooleanCircle(5,2,2)]), 8)),
        Evaluate(new("success-two-holes", new ProfileBooleanDifference(new ProfileBooleanRectangle(0,0,30,20), [new ProfileBooleanCircle(-5,0,2), new ProfileBooleanCircle(5,0,2)]), 8)),
        Evaluate(new("invalid-circle-outside", new ProfileBooleanDifference(new ProfileBooleanRectangle(0,0,20,20), [new ProfileBooleanCircle(20,20,2)]), 10)),
        Evaluate(new("invalid-height", new ProfileBooleanDifference(new ProfileBooleanRectangle(0,0,20,20), [new ProfileBooleanCircle(0,0,2)]), 0)),
        Evaluate(new("deferred-capsule", new ProfileBooleanDifference(new ProfileBooleanRectangle(0,0,20,20), [new ProfileBooleanCapsule(0,0,10,2)]), 10)),
        Evaluate(new("deferred-disjoint-union", new ProfileBooleanUnion([new ProfileBooleanRectangle(-20,0,10,10), new ProfileBooleanRectangle(20,0,10,10)]), 10))
    ];

    public static ProfileBooleanExtrudePipelineResult Evaluate(ProfileBooleanExtrudePipelineRow row)
    {
        var diagnostics = new List<string>();
        var req = new ProfileExpressionHoleExtrudeRequest(ToFirmamentExpression(row.Expression), row.Height);
        var result = ProfileExpressionHoleExtrudeEmitter.TryEmit(req);
        diagnostics.AddRange(result.Diagnostics);

        if (result.Status != ProfileExpressionHoleExtrudeStatus.Succeeded || result.Body is null)
        {
            var status = result.Status == ProfileExpressionHoleExtrudeStatus.Deferred ? LabProfileStatus.Deferred : LabProfileStatus.Failed;
            var rec = result.Status == ProfileExpressionHoleExtrudeStatus.Deferred
                ? ProfileBooleanRecommendation.profile_boolean_extrude_deferred_topology
                : ProfileBooleanRecommendation.profile_boolean_extrude_normalization_rejected;
            return Build(row.CaseName, status, 0, 0, 0, 0, false, diagnostics, rec);
        }

        var holeCount = row.Expression is ProfileBooleanDifference { Rights: var rights }
            ? rights.Count(x => x is ProfileBooleanCircle)
            : 0;
        var topo = CountFaces(result.Body);
        diagnostics.Add("v2-x5-no-3d-boolean-used");
        var stepOk = StepOk(result.Body, diagnostics);
        return Build(row.CaseName, LabProfileStatus.Succeeded, 1, holeCount, topo.Planar, topo.Cyl, stepOk, diagnostics, ProfileBooleanRecommendation.profile_boolean_extrude_ready_for_production_evaluation);
    }

    private static ProfileExpression2D ToFirmamentExpression(ProfileBooleanExpr2D expr) => expr switch
    {
        ProfileBooleanRectangle r => new ProfileRectangleExpr2D(r.CenterX, r.CenterY, r.Width, r.Height),
        ProfileBooleanCircle c => new ProfileCircleExpr2D(c.CenterX, c.CenterY, c.Radius),
        ProfileBooleanCapsule c => new ProfileCapsuleExpr2D(c.CenterX, c.CenterY, c.Length, c.Radius),
        ProfileBooleanUnsupportedPrimitive u => new ProfileUnsupportedPrimitiveExpr2D(u.Name),
        ProfileBooleanDifference d => new ProfileDifferenceExpr2D(ToFirmamentExpression(d.Left), d.Rights.Select(ToFirmamentExpression).ToArray()),
        ProfileBooleanUnion u => new ProfileUnionExpr2D(u.Operands.Select(ToFirmamentExpression).ToArray()),
        ProfileBooleanIntersection i => new ProfileIntersectionExpr2D(ToFirmamentExpression(i.Left), ToFirmamentExpression(i.Right)),
        _ => new ProfileUnsupportedPrimitiveExpr2D(expr.GetType().Name)
    };

    private static (int Planar, int Cyl) CountFaces(BrepBody b)
    {
        var planar = b.Geometry.Surfaces.Count(x => x.Value.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Plane);
        var cyl = b.Geometry.Surfaces.Count(x => x.Value.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cylinder);
        return (planar, cyl);
    }

    private static bool StepOk(BrepBody b, List<string> d)
    {
        var step = Step242Exporter.ExportBody(b);
        if (!step.IsSuccess || step.Value is null) { d.Add("v2-x5-step-smoke-failed:export-failed"); return false; }
        var text = step.Value;
        if (Required.Any(m => !text.Contains(m, StringComparison.Ordinal)) || text.Contains("BREP_WITH_VOIDS", StringComparison.Ordinal))
        { d.Add("v2-x5-step-smoke-failed:markers"); return false; }
        d.Add("v2-x5-step-smoke-succeeded");
        return true;
    }

    private static ProfileBooleanExtrudePipelineResult Build(string caseName, LabProfileStatus status, int outer, int holes, int planar, int cyl, bool step, List<string> d, ProfileBooleanRecommendation r)
        => new(caseName, status, outer, holes, planar, cyl, step, d.Distinct().OrderBy(x => x).ToArray(), r);
}
