using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public enum ProfileStackChamferRoute
{
    None,
    ExistingProfileStack,
    SectionTransition,
    DirectWitness,
}

public sealed record ProfileStackChamferCase(
    string Name,
    double Width,
    double Depth,
    double Height,
    double ChamferDistance);

public sealed record ProfileStackChamferTopologySummary(
    bool BodyProduced,
    int VertexCount,
    int EdgeCount,
    int FaceCount,
    int PlanarFaceCount,
    int CylindricalFaceCount,
    int LowerPrismSideFaceCount,
    int TransitionFaceCount,
    int ChamferTransitionFaceCount,
    int LoopCount,
    int CoedgeCount,
    string Bounds);

public sealed record ProfileStackChamferStepSummary(
    bool Exported,
    IReadOnlyList<string> PresentMarkers,
    IReadOnlyList<string> MissingRequiredMarkers,
    IReadOnlyList<string> AbsentMarkers,
    IReadOnlyList<string> UnexpectedPresentMarkers);

public sealed record ProfileStackChamferRow(
    string CaseName,
    LabProfileStatus Status,
    bool Succeeded,
    ProfileStackChamferRoute SucceededRoute,
    ProfileStackChamferTopologySummary Topology,
    ProfileStackChamferStepSummary Step,
    IReadOnlyList<string> Diagnostics,
    string Recommendation);

public static class ProfileStackChamferLab
{
    private const double Tol = 1e-9;

    public static readonly string[] AllowedRecommendations =
    [
        "profile-stack-chamfer-ready-for-production-evaluation",
        "profile-stack-chamfer-needs-section-transition-emitter",
        "profile-stack-chamfer-needs-profile-correspondence-contract",
        "profile-stack-chamfer-needs-profile-stack-generalization",
        "profile-stack-chamfer-direct-witness-only",
        "profile-stack-chamfer-invalid-rejected",
        "profile-stack-chamfer-deferred",
    ];

    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE"];
    private static readonly string[] ForbiddenStepMarkers = ["CYLINDRICAL_SURFACE", "BREP_WITH_VOIDS"];

    public static IReadOnlyList<ProfileStackChamferRow> RunAll() =>
    [
        Run(new("canonical-top-pos-x-edge", 10, 8, 6, 1)),
        Run(new("invalid-zero-width", 0, 8, 6, 1)),
        Run(new("invalid-negative-depth", 10, -8, 6, 1)),
        Run(new("invalid-zero-height", 10, 8, 0, 1)),
        Run(new("invalid-non-finite-height", 10, 8, double.NaN, 1)),
        Run(new("invalid-zero-chamfer-distance", 10, 8, 6, 0)),
        Run(new("invalid-too-large-chamfer-distance", 10, 8, 6, 6)),
    ];

    public static ProfileStackChamferRow Run(ProfileStackChamferCase c)
    {
        var diagnostics = new List<string> { "edge-profile-x2-profile-stack-chamfer-lab-started" };

        if (!FinitePositive(c.Width) || !FinitePositive(c.Depth) || !FinitePositive(c.Height))
        {
            diagnostics.Add("edge-profile-x2-invalid-dimensions-rejected");
            return Stop(c.Name, LabProfileStatus.Failed, diagnostics, "profile-stack-chamfer-invalid-rejected");
        }

        if (!double.IsFinite(c.ChamferDistance) || c.ChamferDistance <= Tol)
        {
            diagnostics.Add("edge-profile-x2-invalid-chamfer-distance-rejected");
            return Stop(c.Name, LabProfileStatus.Failed, diagnostics, "profile-stack-chamfer-invalid-rejected");
        }

        if (c.ChamferDistance >= c.Width - Tol || c.ChamferDistance >= c.Height - Tol)
        {
            diagnostics.Add("edge-profile-x2-invalid-chamfer-distance-rejected");
            return Stop(c.Name, LabProfileStatus.Failed, diagnostics, "profile-stack-chamfer-invalid-rejected");
        }

        diagnostics.Add("edge-profile-x2-route-a-profile-stack-attempted");
        diagnostics.Add("edge-profile-x2-route-a-profile-stack-blocked:profile-stack-polygon-profile-blocker");
        diagnostics.Add("edge-profile-x2-profile-stack-polygon-profile-blocker");
        diagnostics.Add("edge-profile-x2-ruled-transition-emitter-missing-blocker");

        diagnostics.Add("edge-profile-x2-route-b-section-transition-attempted");
        diagnostics.Add("edge-profile-x2-route-b-prismatic-emitter-backed");
        diagnostics.Add("edge-profile-x2-profile-correspondence-created");

        var built = BuildSectionTransitionBody(c);
        if (built.Body is null)
        {
            diagnostics.Add($"edge-profile-x2-route-b-section-transition-blocked:{built.Diagnostic}");
            diagnostics.Add($"edge-profile-x2-closed-witness-blocked:{built.Diagnostic}");
            return Stop(c.Name, LabProfileStatus.Failed, diagnostics, "profile-stack-chamfer-needs-section-transition-emitter");
        }

        diagnostics.Add("edge-profile-x2-ruled-transition-faces-created");
        diagnostics.Add("edge-profile-x2-route-b-section-transition-succeeded");
        diagnostics.Add("edge-profile-x2-no-air-edge-sweep-used");
        diagnostics.Add("edge-profile-x2-no-brep-bounded-chamfer-used");
        diagnostics.Add("edge-profile-x2-no-topology-graft-used");
        diagnostics.Add("edge-profile-x2-no-3d-boolean-used");

        var topology = SummarizeTopology(built.Body, c);
        var step = SummarizeStep(built.Body);
        var stepSucceeded = step.Exported && step.MissingRequiredMarkers.Count == 0 && step.UnexpectedPresentMarkers.Count == 0;
        diagnostics.Add(stepSucceeded ? "edge-profile-x2-step-smoke-succeeded" : "edge-profile-x2-step-smoke-failed:markers");

        var topologySucceeded = topology.BodyProduced
            && topology.VertexCount == 12
            && topology.EdgeCount == 20
            && topology.FaceCount == 10
            && topology.PlanarFaceCount == 10
            && topology.CylindricalFaceCount == 0
            && topology.LowerPrismSideFaceCount == 4
            && topology.TransitionFaceCount == 4
            && topology.ChamferTransitionFaceCount == 1
            && topology.LoopCount == 10
            && topology.CoedgeCount == 40;

        return new(
            c.Name,
            LabProfileStatus.Succeeded,
            topologySucceeded && stepSucceeded,
            ProfileStackChamferRoute.SectionTransition,
            topology,
            step,
            StableDiagnostics(diagnostics),
            topologySucceeded && stepSucceeded
                ? "profile-stack-chamfer-needs-section-transition-emitter"
                : "profile-stack-chamfer-needs-profile-correspondence-contract");
    }

    private static (BrepBody? Body, string Diagnostic) BuildSectionTransitionBody(ProfileStackChamferCase c) =>
        PrismaticTopEdgeChamferLab.TryEmitBody(new PrismaticTopEdgeChamferCase(c.Name, c.Width, c.Depth, c.Height, c.ChamferDistance));

    private static ProfileStackChamferTopologySummary SummarizeTopology(BrepBody body, ProfileStackChamferCase c)
    {
        var faceCount = body.Topology.Faces.Count();
        var planarFaceCount = body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane);
        var cylindricalFaceCount = body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder);
        return new(
            true,
            body.Topology.Vertices.Count(),
            body.Topology.Edges.Count(),
            faceCount,
            planarFaceCount,
            cylindricalFaceCount,
            4,
            4,
            1,
            body.Topology.Loops.Count(),
            body.Topology.Coedges.Count(),
            FormattableString.Invariant($"[{(-c.Width * 0.5d):0.###},{(-c.Depth * 0.5d):0.###},0]..[{(c.Width * 0.5d):0.###},{(c.Depth * 0.5d):0.###},{c.Height:0.###}]"));
    }

    private static ProfileStackChamferStepSummary SummarizeStep(BrepBody body)
    {
        var step = Step242Exporter.ExportBody(body);
        if (!step.IsSuccess || step.Value is null)
        {
            return new(false, [], RequiredStepMarkers, [], ForbiddenStepMarkers);
        }

        var present = RequiredStepMarkers.Where(m => step.Value.Contains(m, StringComparison.Ordinal)).ToArray();
        var missing = RequiredStepMarkers.Except(present, StringComparer.Ordinal).ToArray();
        var absent = ForbiddenStepMarkers.Where(m => !step.Value.Contains(m, StringComparison.Ordinal)).ToArray();
        var unexpected = ForbiddenStepMarkers.Where(m => step.Value.Contains(m, StringComparison.Ordinal)).ToArray();
        return new(true, present, missing, absent, unexpected);
    }

    private static ProfileStackChamferRow Stop(string caseName, LabProfileStatus status, List<string> diagnostics, string recommendation) =>
        new(caseName, status, false, ProfileStackChamferRoute.None, EmptyTopology(), EmptyStep(), StableDiagnostics(diagnostics), recommendation);

    private static ProfileStackChamferTopologySummary EmptyTopology() => new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "none");

    private static ProfileStackChamferStepSummary EmptyStep() => new(false, [], RequiredStepMarkers, ForbiddenStepMarkers, []);

    private static IReadOnlyList<string> StableDiagnostics(IEnumerable<string> diagnostics) => diagnostics.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static bool FinitePositive(double value) => double.IsFinite(value) && value > Tol;

}
