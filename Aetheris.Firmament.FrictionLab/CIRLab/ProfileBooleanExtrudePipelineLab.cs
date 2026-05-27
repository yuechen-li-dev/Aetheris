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
        var diagnostics = new List<string> { "v2-x5-profile-boolean-extrude-pipeline-started", "v2-x5-profile-expression-normalization-attempted" };
        var normalized = ProfileBooleanNormalizationLab.Normalize(row.Expression);
        diagnostics.AddRange(normalized.Diagnostics);

        if (normalized.Status != LabProfileStatus.Succeeded || normalized.Profile is null)
        {
            diagnostics.Add(normalized.Status == LabProfileStatus.Deferred
                ? $"v2-x5-profile-expression-deferred:{string.Join(",", normalized.Diagnostics)}"
                : $"v2-x5-profile-expression-rejected:{string.Join(",", normalized.Diagnostics)}");
            var rec = normalized.Status == LabProfileStatus.Deferred
                ? ProfileBooleanRecommendation.profile_boolean_extrude_deferred_topology
                : ProfileBooleanRecommendation.profile_boolean_extrude_normalization_rejected;
            return Build(row.CaseName, normalized.Status, 0, 0, 0, 0, false, diagnostics, rec);
        }

        diagnostics.Add("v2-x5-profile-expression-normalized");
        if (!TryAdapt(normalized.Profile, row.Height, out var req, out var reason))
        {
            diagnostics.Add($"v2-x5-profile-hole-extrude-failed:{reason}");
            return Build(row.CaseName, LabProfileStatus.Failed, 1, Math.Max(0, normalized.Profile.Loops.Count-1), 0, 0, false, diagnostics, ProfileBooleanRecommendation.profile_boolean_extrude_emitter_blocked);
        }

        diagnostics.Add("v2-x5-resolved-profile-adapted-to-emitter");
        diagnostics.Add("v2-x5-profile-hole-extrude-attempted");
        var emit = ProfileHoleExtrudeEmitter.TryEmit(req!);
        diagnostics.AddRange(emit.Diagnostics);
        if (emit.Status != ProfileHoleExtrudeStatus.Succeeded || emit.Body is null)
        {
            diagnostics.Add("v2-x5-profile-hole-extrude-failed:emitter-rejected-or-failed");
            return Build(row.CaseName, LabProfileStatus.Failed, 1, normalized.Profile.Loops.Count-1, 0, 0, false, diagnostics, ProfileBooleanRecommendation.profile_boolean_extrude_emitter_blocked);
        }

        diagnostics.Add("v2-x5-profile-hole-extrude-succeeded");
        diagnostics.Add("v2-x5-no-3d-boolean-used");
        diagnostics.Add("v2-v3-no-3d-boolean-used");
        var topo = CountFaces(emit.Body);
        var stepOk = StepOk(emit.Body, diagnostics);
        return Build(row.CaseName, LabProfileStatus.Succeeded, 1, normalized.Profile.Loops.Count-1, topo.Planar, topo.Cyl, stepOk, diagnostics, ProfileBooleanRecommendation.profile_boolean_extrude_ready_for_production_evaluation);
    }

    private static bool TryAdapt(LabResolvedProfile2D p, double height, out ProfileHoleExtrudeRequest? req, out string reason)
    {
        req = null; reason = string.Empty;
        if (height <= 1e-9) { reason = "invalid-height"; return false; }
        if (p.Loops.Count < 1) { reason = "no-outer-loop"; return false; }
        var outer = p.Loops[0];
        if (outer.Curves.Count != 4 || outer.Curves.Any(c => c is not LabAirLineSegment2D)) { reason = "outer-not-rectangle"; return false; }
        var pts = outer.Curves.Cast<LabAirLineSegment2D>().SelectMany(x => new[] { x.Start, x.End }).ToArray();
        var minX = pts.Min(x => x.X); var maxX = pts.Max(x => x.X); var minY = pts.Min(x => x.Y); var maxY = pts.Max(x => x.Y);
        var holes = new List<ProfileHoleLoop2D>();
        foreach (var loop in p.Loops.Skip(1))
        {
            if (loop.Curves.Count != 1 || loop.Curves[0] is not LabAirFullCircle2D c) { reason = "hole-not-full-circle"; return false; }
            holes.Add(new(c.Center.X, c.Center.Y, c.Radius));
        }
        req = new(maxX-minX, maxY-minY, height, holes);
        return true;
    }

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
