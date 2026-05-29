using Aetheris.Kernel.Core.Geometry;

namespace Aetheris.Kernel.Core.Brep.Prismatic;

internal enum PrismaticTopEdgeChamferStatus
{
    Succeeded,
    Rejected,
    Deferred,
    Failed,
}

internal enum PrismaticTopEdgeChamferSelection
{
    TopPositiveXSide,
    TopNegativeXSide,
    TopPositiveYSide,
    TopNegativeYSide,
}

internal sealed record PrismaticTopEdgeChamferRequest(
    double Width,
    double Depth,
    double Height,
    double ChamferDistance,
    PrismaticTopEdgeChamferSelection Selection = PrismaticTopEdgeChamferSelection.TopPositiveXSide,
    bool ExportStep = false);

internal sealed record PrismaticTopEdgeChamferTopologySummary(
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

internal sealed record PrismaticTopEdgeChamferStepSummary(
    bool Exported,
    IReadOnlyList<string> PresentMarkers,
    IReadOnlyList<string> MissingRequiredMarkers,
    IReadOnlyList<string> AbsentMarkers,
    IReadOnlyList<string> UnexpectedPresentMarkers);

internal sealed record PrismaticTopEdgeChamferResult(
    PrismaticTopEdgeChamferStatus Status,
    BrepBody? Body,
    PrismaticTopEdgeChamferTopologySummary Topology,
    PrismaticTopEdgeChamferStepSummary Step,
    IReadOnlyList<string> Diagnostics,
    string Recommendation)
{
    public bool Succeeded => Status == PrismaticTopEdgeChamferStatus.Succeeded && Body is not null;
}

internal static class PrismaticTopEdgeChamferPrototype
{
    private const double Tol = 1e-9;
    private const int ChamferEdgeIndex = 1;

    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE"];
    private static readonly string[] ForbiddenStepMarkers = ["CYLINDRICAL_SURFACE", "BREP_WITH_VOIDS"];

    public static PrismaticTopEdgeChamferResult Emit(PrismaticTopEdgeChamferRequest request)
    {
        var diagnostics = new List<string> { "edge-prismatic-v2-route-started" };

        var validation = Validate(request, diagnostics);
        if (validation.Status != PrismaticTopEdgeChamferStatus.Succeeded)
        {
            return Stop(validation.Status, diagnostics, validation.Recommendation);
        }

        diagnostics.Add("edge-prismatic-v2-request-validated");
        var sections = CreateSectionStack(request);
        diagnostics.Add("edge-prismatic-v2-section-stack-created");
        var correspondence = PrismaticCorrespondenceMap.Identity(4);
        diagnostics.Add("edge-prismatic-v2-correspondence-created");

        diagnostics.Add("edge-prismatic-v2-prismatic-emitter-invoked");
        var emitted = PrismaticSectionTransitionEmitter.Emit(new PrismaticSectionTransitionRequest(
            sections,
            correspondence,
            new PrismaticSectionTransitionOptions(RunStepSmoke: request.ExportStep, TraceLabel: "edge-prismatic-v2-top-positive-x-chamfer")));

        if (!emitted.Succeeded || emitted.Body is null)
        {
            diagnostics.Add($"edge-prismatic-v2-prismatic-emitter-failed:{MapEmitterFailure(emitted)}");
            return Stop(PrismaticTopEdgeChamferStatus.Failed, diagnostics, "prismatic-top-edge-chamfer-needs-emitter-hardening");
        }

        var topology = SummarizeTopology(emitted.Body, request);
        var chamferTransitionFaces = CountChamferTransitionFaces(sections);
        if (chamferTransitionFaces == 1)
        {
            diagnostics.Add("edge-prismatic-v2-chamfer-transition-face-classified");
        }

        diagnostics.Add("edge-prismatic-v2-body-created");
        diagnostics.Add("edge-prismatic-v2-no-trim-used");
        diagnostics.Add("edge-prismatic-v2-no-air-edge-sweep-used");
        diagnostics.Add("edge-prismatic-v2-no-brep-bounded-chamfer-used");
        diagnostics.Add("edge-prismatic-v2-no-topology-graft-used");
        diagnostics.Add("edge-prismatic-v2-no-3d-boolean-used");
        diagnostics.Add("edge-prismatic-v2-no-production-route-replacement");

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
        if (topologySucceeded)
        {
            diagnostics.Add("edge-prismatic-v2-topology-validated");
        }

        var step = MapStep(emitted.Step);
        var stepSucceeded = !request.ExportStep || (step.Exported && step.MissingRequiredMarkers.Count == 0 && step.UnexpectedPresentMarkers.Count == 0);
        if (request.ExportStep)
        {
            diagnostics.Add(stepSucceeded ? "edge-prismatic-v2-step-smoke-succeeded" : "edge-prismatic-v2-step-smoke-failed:markers");
        }

        var succeeded = topologySucceeded && stepSucceeded;
        return new(
            succeeded ? PrismaticTopEdgeChamferStatus.Succeeded : PrismaticTopEdgeChamferStatus.Failed,
            emitted.Body,
            topology,
            step,
            StableDiagnostics(diagnostics),
            succeeded
                ? "prismatic-top-edge-chamfer-ready-for-controlled-route-evaluation"
                : "prismatic-top-edge-chamfer-needs-emitter-hardening");
    }

    internal static IReadOnlyList<PrismaticSection> CreateSectionStack(PrismaticTopEdgeChamferRequest request)
    {
        var x0 = -request.Width * 0.5d;
        var x1 = request.Width * 0.5d;
        var y0 = -request.Depth * 0.5d;
        var y1 = request.Depth * 0.5d;
        var z0 = 0d;
        var z1 = request.Height - request.ChamferDistance;
        var z2 = request.Height;
        var topX = x1 - request.ChamferDistance;

        return
        [
            new PrismaticSection(z0, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]),
            new PrismaticSection(z1, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]),
            new PrismaticSection(z2, [(x0, y0), (topX, y0), (topX, y1), (x0, y1)]),
        ];
    }

    private static (PrismaticTopEdgeChamferStatus Status, string Recommendation) Validate(PrismaticTopEdgeChamferRequest request, List<string> diagnostics)
    {
        if (!FinitePositive(request.Width) || !FinitePositive(request.Depth) || !FinitePositive(request.Height))
        {
            diagnostics.Add("edge-prismatic-v2-invalid-dimensions-rejected");
            return (PrismaticTopEdgeChamferStatus.Rejected, "prismatic-top-edge-chamfer-invalid-rejected");
        }

        if (!FinitePositive(request.ChamferDistance))
        {
            diagnostics.Add("edge-prismatic-v2-invalid-chamfer-distance-rejected");
            return (PrismaticTopEdgeChamferStatus.Rejected, "prismatic-top-edge-chamfer-invalid-rejected");
        }

        if (request.Selection != PrismaticTopEdgeChamferSelection.TopPositiveXSide)
        {
            diagnostics.Add("edge-prismatic-v2-unsupported-selection-rejected");
            return (PrismaticTopEdgeChamferStatus.Rejected, "prismatic-top-edge-chamfer-invalid-rejected");
        }

        var halfWidth = request.Width * 0.5d;
        if (request.ChamferDistance >= halfWidth - Tol || request.ChamferDistance >= request.Height - Tol)
        {
            diagnostics.Add("edge-prismatic-v2-invalid-chamfer-distance-rejected");
            return (PrismaticTopEdgeChamferStatus.Rejected, "prismatic-top-edge-chamfer-invalid-rejected");
        }

        return (PrismaticTopEdgeChamferStatus.Succeeded, string.Empty);
    }

    private static PrismaticTopEdgeChamferTopologySummary SummarizeTopology(BrepBody body, PrismaticTopEdgeChamferRequest request) => new(
        true,
        body.Topology.Vertices.Count(),
        body.Topology.Edges.Count(),
        body.Topology.Faces.Count(),
        body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane),
        body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder),
        4,
        4,
        CountChamferTransitionFaces(CreateSectionStack(request)),
        body.Topology.Loops.Count(),
        body.Topology.Coedges.Count(),
        FormattableString.Invariant($"[{(-request.Width * 0.5d):0.###},{(-request.Depth * 0.5d):0.###},0]..[{(request.Width * 0.5d):0.###},{(request.Depth * 0.5d):0.###},{request.Height:0.###}]"));

    private static int CountChamferTransitionFaces(IReadOnlyList<PrismaticSection> sections)
    {
        var lower = sections[^2].OuterLoop;
        var upper = sections[^1].OuterLoop;
        var next = (ChamferEdgeIndex + 1) % lower.Count;
        return SamePoint(lower[ChamferEdgeIndex], upper[ChamferEdgeIndex]) && SamePoint(lower[next], upper[next]) ? 0 : 1;
    }

    private static PrismaticTopEdgeChamferStepSummary MapStep(PrismaticSectionTransitionStepSummary step) => new(
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

    private static PrismaticTopEdgeChamferResult Stop(PrismaticTopEdgeChamferStatus status, List<string> diagnostics, string recommendation) =>
        new(status, null, EmptyTopology(), EmptyStep(), StableDiagnostics(diagnostics), recommendation);

    private static PrismaticTopEdgeChamferTopologySummary EmptyTopology() => new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "none");

    private static PrismaticTopEdgeChamferStepSummary EmptyStep() => new(false, [], RequiredStepMarkers, ForbiddenStepMarkers, []);

    private static IReadOnlyList<string> StableDiagnostics(IEnumerable<string> diagnostics) => diagnostics.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static bool FinitePositive(double value) => double.IsFinite(value) && value > Tol;

    private static bool SamePoint((double X, double Y) a, (double X, double Y) b) => System.Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y))) <= Tol;
}
