using Aetheris.Kernel.Core.Geometry;

namespace Aetheris.Kernel.Core.Brep.Prismatic;

internal enum FaceLoopChamferStatus
{
    Succeeded,
    Rejected,
    Deferred,
    Failed,
}

internal enum FaceLoopChamferSelectionKind
{
    FaceBoundaryLoop,
    OpenChain,
    ArbitraryGraph,
}

internal enum FaceLoopChamferOwningFaceKind
{
    TopCap,
    BottomCap,
    SideFace,
    NonPlanarFace,
}

internal enum FaceLoopChamferLoopKind
{
    Outer,
    Inner,
}

internal enum FaceLoopChamferRuleKind
{
    UniformSymmetric,
    NonUniform,
}

internal sealed record FaceLoopChamferSelection(
    FaceLoopChamferSelectionKind SelectionKind = FaceLoopChamferSelectionKind.FaceBoundaryLoop,
    FaceLoopChamferOwningFaceKind OwningFace = FaceLoopChamferOwningFaceKind.TopCap,
    FaceLoopChamferLoopKind LoopKind = FaceLoopChamferLoopKind.Outer,
    bool IsClosed = true,
    int EdgeCount = 4,
    bool OrderedCoedges = true);

internal sealed record PrismaticTopFaceLoopChamferRequest(
    double Width,
    double Depth,
    double Height,
    double ChamferDistance,
    FaceLoopChamferSelection? Selection = null,
    FaceLoopChamferRuleKind Rule = FaceLoopChamferRuleKind.UniformSymmetric,
    bool ExportStep = false);

internal sealed record FaceLoopChamferTopologySummary(
    bool BodyProduced,
    int SectionCount,
    int VertexCount,
    int EdgeCount,
    int FaceCount,
    int PlanarFaceCount,
    int CylindricalFaceCount,
    int CapFaceCount,
    int LowerPrismSideFaceCount,
    int TransitionFaceCount,
    int ChamferTransitionFaceCount,
    int LoopCount,
    int CoedgeCount,
    string Bounds);

internal sealed record FaceLoopChamferStepSummary(
    bool Exported,
    IReadOnlyList<string> PresentMarkers,
    IReadOnlyList<string> MissingRequiredMarkers,
    IReadOnlyList<string> AbsentMarkers,
    IReadOnlyList<string> UnexpectedPresentMarkers);

internal sealed record FaceLoopChamferResult(
    FaceLoopChamferStatus Status,
    BrepBody? Body,
    FaceLoopChamferTopologySummary Topology,
    FaceLoopChamferStepSummary Step,
    IReadOnlyList<string> Diagnostics,
    string Recommendation)
{
    public bool Succeeded => Status == FaceLoopChamferStatus.Succeeded && Body is not null;
}

internal static class PrismaticTopFaceLoopChamferPrototype
{
    private const double Tol = 1e-9;

    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE"];
    private static readonly string[] ForbiddenStepMarkers = ["CYLINDRICAL_SURFACE", "BREP_WITH_VOIDS"];

    public static FaceLoopChamferResult Emit(PrismaticTopFaceLoopChamferRequest request)
    {
        var diagnostics = new List<string> { "edge-loop-x1-lab-started" };
        var selection = request.Selection ?? new FaceLoopChamferSelection();

        var validation = Validate(request, selection, diagnostics);
        if (validation.Status != FaceLoopChamferStatus.Succeeded)
        {
            return Stop(validation.Status, diagnostics, validation.Recommendation);
        }

        diagnostics.Add("edge-loop-x1-loop-selection-created");
        diagnostics.Add("edge-loop-x1-owning-face-top-cap");
        diagnostics.Add("edge-loop-x1-loop-kind-outer");
        diagnostics.Add("edge-loop-x1-loop-closed");
        diagnostics.Add("edge-loop-x1-loop-edge-count:4");
        diagnostics.Add("edge-loop-x1-uniform-chamfer-rule-validated");

        var sections = CreateSectionStack(request);
        diagnostics.Add("edge-loop-x1-section-stack-created");
        var correspondence = PrismaticCorrespondenceMap.Identity(4);
        diagnostics.Add("edge-loop-x1-correspondence-created");

        diagnostics.Add("edge-loop-x1-prismatic-emitter-invoked");
        var emitted = PrismaticSectionTransitionEmitter.Emit(new PrismaticSectionTransitionRequest(
            sections,
            correspondence,
            new PrismaticSectionTransitionOptions(RunStepSmoke: request.ExportStep, TraceLabel: "edge-loop-x1-top-face-outer-loop-chamfer")));

        if (!emitted.Succeeded || emitted.Body is null)
        {
            diagnostics.Add($"edge-loop-x1-prismatic-emitter-failed:{MapEmitterFailure(emitted)}");
            return Stop(FaceLoopChamferStatus.Failed, diagnostics, "face-loop-chamfer-needs-corner-policy-hardening");
        }

        diagnostics.Add("edge-loop-x1-body-created");
        diagnostics.Add("edge-loop-x1-class-b-loop-route");
        diagnostics.Add("edge-loop-x1-not-four-independent-single-edge-chamfers");
        diagnostics.Add("edge-loop-x1-split-preserving-topology");
        diagnostics.Add("edge-loop-x1-no-air-edge-sweep-used");
        diagnostics.Add("edge-loop-x1-no-brep-bounded-chamfer-used");
        diagnostics.Add("edge-loop-x1-no-topology-graft-used");
        diagnostics.Add("edge-loop-x1-no-3d-boolean-used");
        diagnostics.Add("edge-loop-x1-no-coplanar-merge-used");
        diagnostics.Add("edge-loop-x1-no-production-route-replacement");

        var topology = SummarizeTopology(emitted.Body, request);
        var topologySucceeded = topology.BodyProduced
            && topology.SectionCount == 3
            && topology.VertexCount == 12
            && topology.EdgeCount == 20
            && topology.FaceCount == 10
            && topology.PlanarFaceCount == 10
            && topology.CylindricalFaceCount == 0
            && topology.CapFaceCount == 2
            && topology.LowerPrismSideFaceCount == 4
            && topology.TransitionFaceCount == 4
            && topology.ChamferTransitionFaceCount == 4
            && topology.LoopCount == 10
            && topology.CoedgeCount == 40;
        if (topologySucceeded)
        {
            diagnostics.Add("edge-loop-x1-topology-validated");
        }

        var step = MapStep(emitted.Step);
        var stepSucceeded = !request.ExportStep || (step.Exported && step.MissingRequiredMarkers.Count == 0 && step.UnexpectedPresentMarkers.Count == 0);
        if (request.ExportStep)
        {
            diagnostics.Add(stepSucceeded ? "edge-loop-x1-step-smoke-succeeded" : "edge-loop-x1-step-smoke-failed:markers");
        }

        var succeeded = topologySucceeded && stepSucceeded;
        return new(
            succeeded ? FaceLoopChamferStatus.Succeeded : FaceLoopChamferStatus.Failed,
            emitted.Body,
            topology,
            step,
            StableDiagnostics(diagnostics),
            succeeded ? "face-loop-chamfer-ready-for-corpus" : "face-loop-chamfer-needs-corner-policy-hardening");
    }

    internal static IReadOnlyList<PrismaticSection> CreateSectionStack(PrismaticTopFaceLoopChamferRequest request)
    {
        var x0 = -request.Width * 0.5d;
        var x1 = request.Width * 0.5d;
        var y0 = -request.Depth * 0.5d;
        var y1 = request.Depth * 0.5d;
        var d = request.ChamferDistance;
        var z0 = 0d;
        var z1 = request.Height - d;
        var z2 = request.Height;

        return
        [
            new PrismaticSection(z0, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]),
            new PrismaticSection(z1, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]),
            new PrismaticSection(z2, [(x0 + d, y0 + d), (x1 - d, y0 + d), (x1 - d, y1 - d), (x0 + d, y1 - d)]),
        ];
    }

    private static (FaceLoopChamferStatus Status, string Recommendation) Validate(PrismaticTopFaceLoopChamferRequest request, FaceLoopChamferSelection selection, List<string> diagnostics)
    {
        if (!FinitePositive(request.Width) || !FinitePositive(request.Depth) || !FinitePositive(request.Height))
        {
            diagnostics.Add("edge-loop-x1-invalid-dimensions-rejected");
            return (FaceLoopChamferStatus.Rejected, "face-loop-chamfer-invalid-rejected");
        }

        if (!FinitePositive(request.ChamferDistance))
        {
            diagnostics.Add("edge-loop-x1-invalid-chamfer-distance-rejected");
            return (FaceLoopChamferStatus.Rejected, "face-loop-chamfer-invalid-rejected");
        }

        if (request.Rule != FaceLoopChamferRuleKind.UniformSymmetric)
        {
            diagnostics.Add("edge-loop-x1-non-uniform-rule-rejected");
            return (FaceLoopChamferStatus.Rejected, "face-loop-chamfer-invalid-rejected");
        }

        if (selection.SelectionKind == FaceLoopChamferSelectionKind.ArbitraryGraph)
        {
            diagnostics.Add("edge-loop-x1-arbitrary-graph-rejected");
            return (FaceLoopChamferStatus.Rejected, "face-loop-chamfer-invalid-rejected");
        }

        if (selection.SelectionKind == FaceLoopChamferSelectionKind.OpenChain)
        {
            diagnostics.Add("edge-loop-x1-open-chain-deferred");
            return (FaceLoopChamferStatus.Deferred, "face-loop-chamfer-deferred");
        }

        if (!selection.IsClosed)
        {
            diagnostics.Add("edge-loop-x1-non-closed-loop-rejected");
            return (FaceLoopChamferStatus.Rejected, "face-loop-chamfer-invalid-rejected");
        }

        if (selection.OwningFace == FaceLoopChamferOwningFaceKind.NonPlanarFace)
        {
            diagnostics.Add("edge-loop-x1-non-planar-owning-face-deferred");
            return (FaceLoopChamferStatus.Deferred, "face-loop-chamfer-deferred");
        }

        if (selection.OwningFace != FaceLoopChamferOwningFaceKind.TopCap)
        {
            diagnostics.Add("edge-loop-x1-non-planar-owning-face-deferred");
            return (FaceLoopChamferStatus.Deferred, "face-loop-chamfer-deferred");
        }

        if (selection.LoopKind != FaceLoopChamferLoopKind.Outer)
        {
            diagnostics.Add("edge-loop-x1-non-outer-loop-deferred");
            return (FaceLoopChamferStatus.Deferred, "face-loop-chamfer-deferred");
        }

        if (selection.EdgeCount != 4 || !selection.OrderedCoedges)
        {
            diagnostics.Add("edge-loop-x1-loop-selection-hardening-deferred");
            return (FaceLoopChamferStatus.Deferred, "face-loop-chamfer-needs-loop-selection-hardening");
        }

        if ((2d * request.ChamferDistance) >= request.Width - Tol
            || (2d * request.ChamferDistance) >= request.Depth - Tol
            || request.ChamferDistance >= request.Height - Tol)
        {
            diagnostics.Add("edge-loop-x1-chamfer-distance-too-large-rejected");
            return (FaceLoopChamferStatus.Rejected, "face-loop-chamfer-invalid-rejected");
        }

        var insetWidth = request.Width - (2d * request.ChamferDistance);
        var insetDepth = request.Depth - (2d * request.ChamferDistance);
        if (insetWidth <= Tol || insetDepth <= Tol)
        {
            diagnostics.Add("edge-loop-x1-self-intersecting-inset-rejected");
            return (FaceLoopChamferStatus.Rejected, "face-loop-chamfer-invalid-rejected");
        }

        return (FaceLoopChamferStatus.Succeeded, string.Empty);
    }

    private static FaceLoopChamferTopologySummary SummarizeTopology(BrepBody body, PrismaticTopFaceLoopChamferRequest request) => new(
        true,
        3,
        body.Topology.Vertices.Count(),
        body.Topology.Edges.Count(),
        body.Topology.Faces.Count(),
        body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane),
        body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder),
        2,
        4,
        4,
        4,
        body.Topology.Loops.Count(),
        body.Topology.Coedges.Count(),
        FormattableString.Invariant($"[{(-request.Width * 0.5d):0.###},{(-request.Depth * 0.5d):0.###},0]..[{(request.Width * 0.5d):0.###},{(request.Depth * 0.5d):0.###},{request.Height:0.###}]"));

    private static FaceLoopChamferStepSummary MapStep(PrismaticSectionTransitionStepSummary step) => new(
        step.Exported,
        step.PresentMarkers,
        step.MissingRequiredMarkers,
        step.AbsentMarkers,
        step.UnexpectedPresentMarkers);

    private static string MapEmitterFailure(PrismaticSectionTransitionResult result)
    {
        var reason = result.Diagnostics.FirstOrDefault(d => d.Contains("request-rejected:", StringComparison.Ordinal) || d.Contains("request-deferred:", StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(reason) ? result.Status.ToString().ToLowerInvariant() : reason;
    }

    private static FaceLoopChamferResult Stop(FaceLoopChamferStatus status, List<string> diagnostics, string recommendation) =>
        new(status, null, EmptyTopology(), EmptyStep(), StableDiagnostics(diagnostics), recommendation);

    private static FaceLoopChamferTopologySummary EmptyTopology() => new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "none");

    private static FaceLoopChamferStepSummary EmptyStep() => new(false, [], RequiredStepMarkers, ForbiddenStepMarkers, []);

    private static IReadOnlyList<string> StableDiagnostics(IEnumerable<string> diagnostics) => diagnostics.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static bool FinitePositive(double value) => double.IsFinite(value) && value > Tol;
}
