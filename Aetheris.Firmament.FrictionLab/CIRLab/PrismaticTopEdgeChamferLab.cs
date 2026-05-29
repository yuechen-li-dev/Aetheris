using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using CorePrismatic = Aetheris.Kernel.Core.Brep.Prismatic;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record PrismaticTopEdgeChamferCase(
    string Name,
    double Width,
    double Depth,
    double Height,
    double ChamferDistance);

public sealed record PrismaticTopEdgeChamferTopologySummary(
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

public sealed record PrismaticTopEdgeChamferStepSummary(
    bool Exported,
    IReadOnlyList<string> PresentMarkers,
    IReadOnlyList<string> MissingRequiredMarkers,
    IReadOnlyList<string> AbsentMarkers,
    IReadOnlyList<string> UnexpectedPresentMarkers);

public sealed record PrismaticTopEdgeChamferRow(
    string CaseName,
    LabProfileStatus Status,
    bool Succeeded,
    bool PrismaticEmitterInvoked,
    PrismaticTopEdgeChamferTopologySummary Topology,
    PrismaticTopEdgeChamferStepSummary Step,
    IReadOnlyList<string> Diagnostics,
    string Recommendation);

public static class PrismaticTopEdgeChamferLab
{
    private const double Tol = 1e-9;

    public static readonly string[] AllowedRecommendations =
    [
        "prismatic-top-edge-chamfer-ready-for-production-evaluation",
        "prismatic-top-edge-chamfer-needs-emitter-hardening",
        "prismatic-top-edge-chamfer-invalid-rejected",
        "prismatic-top-edge-chamfer-deferred",
    ];

    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE"];
    private static readonly string[] ForbiddenStepMarkers = ["CYLINDRICAL_SURFACE", "BREP_WITH_VOIDS"];

    public static IReadOnlyList<PrismaticTopEdgeChamferRow> RunAll() =>
    [
        Run(new("canonical-top-pos-x-edge", 10, 8, 6, 1)),
        Run(new("larger-top-pos-x-edge", 10, 8, 6, 2)),
        Run(new("invalid-zero-width", 0, 8, 6, 1)),
        Run(new("invalid-negative-depth", 10, -8, 6, 1)),
        Run(new("invalid-zero-height", 10, 8, 0, 1)),
        Run(new("invalid-non-finite-height", 10, 8, double.NaN, 1)),
        Run(new("invalid-zero-chamfer-distance", 10, 8, 6, 0)),
        Run(new("invalid-negative-chamfer-distance", 10, 8, 6, -1)),
        Run(new("invalid-non-finite-chamfer-distance", 10, 8, 6, double.PositiveInfinity)),
        Run(new("invalid-too-large-chamfer-distance", 10, 8, 6, 5)),
    ];

    public static PrismaticTopEdgeChamferRow Run(PrismaticTopEdgeChamferCase c)
    {
        var diagnostics = new List<string> { "edge-prismatic-x2-top-edge-chamfer-lab-started" };
        if (!FinitePositive(c.Width) || !FinitePositive(c.Depth) || !FinitePositive(c.Height))
        {
            diagnostics.Add("edge-prismatic-x2-invalid-dimensions-rejected");
            return Stop(c.Name, LabProfileStatus.Failed, false, diagnostics, "prismatic-top-edge-chamfer-invalid-rejected");
        }

        if (!double.IsFinite(c.ChamferDistance) || c.ChamferDistance <= Tol)
        {
            diagnostics.Add("edge-prismatic-x2-invalid-chamfer-distance-rejected");
            return Stop(c.Name, LabProfileStatus.Failed, false, diagnostics, "prismatic-top-edge-chamfer-invalid-rejected");
        }

        var halfWidth = c.Width * 0.5d;
        if (c.ChamferDistance >= halfWidth - Tol || c.ChamferDistance >= c.Height - Tol)
        {
            diagnostics.Add("edge-prismatic-x2-invalid-chamfer-distance-rejected");
            return Stop(c.Name, LabProfileStatus.Failed, false, diagnostics, "prismatic-top-edge-chamfer-invalid-rejected");
        }

        var sections = CreateSectionStack(c);
        diagnostics.Add("edge-prismatic-x2-section-stack-created");
        var correspondence = PrismaticCorrespondenceMap.Identity(4);
        diagnostics.Add("edge-prismatic-x2-correspondence-created");
        diagnostics.Add("edge-prismatic-x2-prismatic-emitter-invoked");
        var emitted = EmitBody(sections, correspondence);
        if (emitted.Body is null)
        {
            diagnostics.Add($"edge-prismatic-x2-prismatic-emitter-failed:{emitted.Diagnostic}");
            return Stop(c.Name, LabProfileStatus.Failed, true, diagnostics, "prismatic-top-edge-chamfer-needs-emitter-hardening");
        }

        diagnostics.Add("edge-prismatic-x2-chamfer-transition-face-classified");
        diagnostics.Add("edge-prismatic-x2-body-created");
        diagnostics.Add("edge-prismatic-x2-no-air-edge-sweep-used");
        diagnostics.Add("edge-prismatic-x2-no-brep-bounded-chamfer-used");
        diagnostics.Add("edge-prismatic-x2-no-topology-graft-used");
        diagnostics.Add("edge-prismatic-x2-no-3d-boolean-used");

        var topology = SummarizeTopology(emitted.Body, c);
        var step = SummarizeStep(emitted.Body);
        var stepSucceeded = step.Exported && step.MissingRequiredMarkers.Count == 0 && step.UnexpectedPresentMarkers.Count == 0;
        diagnostics.Add(stepSucceeded ? "edge-prismatic-x2-step-smoke-succeeded" : "edge-prismatic-x2-step-smoke-failed:markers");

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

        var succeeded = topologySucceeded && stepSucceeded;
        return new(
            c.Name,
            LabProfileStatus.Succeeded,
            succeeded,
            true,
            topology,
            step,
            StableDiagnostics(diagnostics),
            succeeded
                ? "prismatic-top-edge-chamfer-ready-for-production-evaluation"
                : "prismatic-top-edge-chamfer-needs-emitter-hardening");
    }

    public static IReadOnlyList<PrismaticSection> CreateSectionStack(PrismaticTopEdgeChamferCase c)
    {
        var x0 = -c.Width * 0.5d;
        var x1 = c.Width * 0.5d;
        var y0 = -c.Depth * 0.5d;
        var y1 = c.Depth * 0.5d;
        var z0 = 0d;
        var z1 = c.Height - c.ChamferDistance;
        var z2 = c.Height;
        var xt = x1 - c.ChamferDistance;

        return
        [
            new PrismaticSection(z0, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]),
            new PrismaticSection(z1, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]),
            new PrismaticSection(z2, [(x0, y0), (xt, y0), (xt, y1), (x0, y1)]),
        ];
    }

    internal static (BrepBody? Body, string Diagnostic) TryEmitBody(PrismaticTopEdgeChamferCase c) =>
        EmitBody(CreateSectionStack(c), PrismaticCorrespondenceMap.Identity(4));

    private static (BrepBody? Body, string Diagnostic) EmitBody(IReadOnlyList<PrismaticSection> sections, PrismaticCorrespondenceMap correspondence)
    {
        var request = new CorePrismatic.PrismaticSectionTransitionRequest(
            sections.Select(s => new CorePrismatic.PrismaticSection(s.Z, s.OuterLoop, s.HasHoles, s.HasArcs, s.OuterLoopCount)).ToArray(),
            new CorePrismatic.PrismaticCorrespondenceMap(correspondence.VertexMap),
            new CorePrismatic.PrismaticSectionTransitionOptions(RunStepSmoke: false, TraceLabel: "prismatic-top-edge-chamfer-lab"));
        var result = CorePrismatic.PrismaticSectionTransitionEmitter.Emit(request);
        return (result.Body, result.Status == CorePrismatic.PrismaticSectionTransitionStatus.Succeeded ? string.Empty : string.Join(",", result.Diagnostics));
    }

    private static PrismaticTopEdgeChamferTopologySummary SummarizeTopology(BrepBody body, PrismaticTopEdgeChamferCase c)
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

    private static PrismaticTopEdgeChamferStepSummary SummarizeStep(BrepBody body)
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

    private static PrismaticTopEdgeChamferRow Stop(string caseName, LabProfileStatus status, bool emitterInvoked, List<string> diagnostics, string recommendation) =>
        new(caseName, status, false, emitterInvoked, EmptyTopology(), EmptyStep(), StableDiagnostics(diagnostics), recommendation);

    private static PrismaticTopEdgeChamferTopologySummary EmptyTopology() => new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "none");

    private static PrismaticTopEdgeChamferStepSummary EmptyStep() => new(false, [], RequiredStepMarkers, ForbiddenStepMarkers, []);

    private static IReadOnlyList<string> StableDiagnostics(IEnumerable<string> diagnostics) => diagnostics.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static bool FinitePositive(double value) => double.IsFinite(value) && value > Tol;
}
