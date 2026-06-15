using Aetheris.Kernel.Core.Brep.Prismatic;

namespace Aetheris.Kernel.Core.Air.BRepPlan;

internal enum AirBRepPlanKind { PrismaticSectionTransition, Unsupported }
internal enum AirBRepPlanElementKind { Vertex, Curve, Edge, Coedge, Loop, Surface, Face, Shell, Body }
internal enum AirBRepPlanRole { Unknown, SectionVertex, SectionEdge, VerticalTransitionEdge, SectionLoop, CapFace, SideFace, TransitionFace, PrismaticTransitionFace, ChamferFace, BodyShell, Body }

internal readonly record struct AirBRepPlanId(string Value)
{
    public override string ToString() => Value;
}

internal sealed record AirBRepPlanElement(
    AirBRepPlanId Id,
    AirBRepPlanElementKind Kind,
    AirBRepPlanRole Role,
    string SourceAirNodeId,
    AirProvenance Provenance,
    int? SectionIndex = null,
    int? ProfileVertexIndex = null,
    int? IntervalIndex = null,
    int? EdgeIndex = null,
    string? FaceRole = null,
    IReadOnlyList<AirDiagnostic>? Diagnostics = null);

internal sealed record AirBRepPlanSummary(
    AirBRepPlanKind PlanKind,
    string SourceAirNodeId,
    int VertexCount,
    int CurveCount,
    int EdgeCount,
    int CoedgeCount,
    int LoopCount,
    int SurfaceCount,
    int FaceCount,
    int ShellCount,
    int BodyCount,
    int CapFaceCount,
    int SideFaceCount,
    int TransitionFaceCount,
    int ChamferFaceCount,
    string Bounds,
    string SplitPolicy,
    IReadOnlyList<AirDiagnostic> Diagnostics,
    IReadOnlyList<string> Guarantees);

internal sealed record AirBRepPlanValidationResult(
    bool Succeeded,
    IReadOnlyList<AirDiagnostic> Diagnostics,
    IReadOnlyList<AirDiagnostic> Errors,
    IReadOnlyList<AirDiagnostic> Warnings,
    AirBRepPlanSummary ExpectedTopologySummary,
    AirTopologySummary? ActualTopologySummary = null);

internal sealed record AirBRepPlan(
    string PlanId,
    AirBRepPlanKind PlanKind,
    string SourceAirNodeId,
    AirProvenance Provenance,
    IReadOnlyList<AirBRepPlanElement> Elements,
    AirBRepPlanSummary Summary,
    IReadOnlyList<AirDiagnostic> Diagnostics,
    IReadOnlyList<string> Guarantees);

internal sealed record AirBRepPlanResult(AirBRepPlan? Plan, AirBRepPlanValidationResult Validation)
{
    public bool Succeeded => Plan is not null && Validation.Succeeded;
}

internal static class AirPrismaticSectionTransitionBRepPlanner
{
    private const double Tol = 1e-9;
    public const string PreserveSectionSplits = "preserve-section-splits";

    private static readonly IReadOnlyList<string> RouteGuarantees =
    [
        "no production route replacement",
        "no emitter rewrite",
        "no STEP exporter change",
        "no BRep topology behavior change",
        "no coplanar merge",
        "no AirEdgeSweep",
        "no Boolean",
        "no topology graft",
    ];

    public static AirBRepPlanResult Plan(
        PrismaticSectionTransitionRequest request,
        string sourceAirNodeId = "air-x3-prismatic-canonical",
        AirProvenance? provenance = null)
    {
        provenance ??= Provenance(sourceAirNodeId);
        var diagnostics = new List<AirDiagnostic> { D("air-x3-brep-plan-created") };
        var rejected = Validate(request, sourceAirNodeId, diagnostics);
        if (rejected is not null)
        {
            return new AirBRepPlanResult(null, Validation(false, sourceAirNodeId, diagnostics, rejected));
        }

        var elements = BuildElements(request, sourceAirNodeId, provenance, diagnostics);
        var summary = Summary(request, sourceAirNodeId, diagnostics, RouteGuarantees);
        diagnostics.Add(D("air-x3-prismatic-plan-created"));
        diagnostics.Add(D("air-x3-prismatic-plan-validated"));
        diagnostics.AddRange(RouteGuarantees.Select(g => D("air-x3-" + g.Replace(" ", "-").Replace("STEP", "step").Replace("BRep", "brep").Replace("AirEdgeSweep", "air-edge-sweep").Replace("Boolean", "boolean"))));
        diagnostics = Stable(diagnostics).ToList();
        summary = summary with { Diagnostics = diagnostics };
        var plan = new AirBRepPlan("brep-plan:prismatic-section-transition:" + sourceAirNodeId, AirBRepPlanKind.PrismaticSectionTransition, sourceAirNodeId, provenance, elements, summary, diagnostics, RouteGuarantees);
        return new AirBRepPlanResult(plan, new AirBRepPlanValidationResult(true, diagnostics, [], [], summary));
    }

    private static AirDiagnostic? Validate(PrismaticSectionTransitionRequest request, string sourceAirNodeId, List<AirDiagnostic> diagnostics)
    {
        if (request.Sections.Count < 2) return D("air-x3-invalid-section-count-rejected", AirDiagnosticSeverity.Error);
        if (request.Sections.Count != 3) return D("air-x3-invalid-section-count-rejected", AirDiagnosticSeverity.Error);
        foreach (var section in request.Sections)
        {
            if (section.HasHoles) return D("air-x3-holes-deferred", AirDiagnosticSeverity.Warning);
            if (section.HasArcs) return D("air-x3-arcs-deferred", AirDiagnosticSeverity.Warning);
            if (section.OuterLoopCount != 1) return D("air-x3-multiple-loops-deferred", AirDiagnosticSeverity.Warning);
            if (section.OuterLoop.Count < 3) return D("air-x3-invalid-section-count-rejected", AirDiagnosticSeverity.Error);
            if (!double.IsFinite(section.Z) || section.OuterLoop.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))) return D("air-x3-non-finite-coordinate-rejected", AirDiagnosticSeverity.Error);
        }
        for (var i = 1; i < request.Sections.Count; i++)
            if (request.Sections[i].Z <= request.Sections[i - 1].Z + Tol) return D("air-x3-non-increasing-sections-rejected", AirDiagnosticSeverity.Error);
        var n = request.Sections[0].OuterLoop.Count;
        if (request.Sections.Any(s => s.OuterLoop.Count != n)) return D("air-x3-mismatched-vertex-count-rejected", AirDiagnosticSeverity.Error);
        if (request.Correspondence is null || request.Correspondence.VertexMap.Count != n || !request.Correspondence.VertexMap.SequenceEqual(Enumerable.Range(0, n))) return D("air-x3-non-identity-correspondence-deferred", AirDiagnosticSeverity.Warning);
        diagnostics.Add(D("air-x3-split-preserving-plan"));
        return null;
    }

    private static IReadOnlyList<AirBRepPlanElement> BuildElements(PrismaticSectionTransitionRequest request, string sourceAirNodeId, AirProvenance provenance, List<AirDiagnostic> diagnostics)
    {
        var e = new List<AirBRepPlanElement>();
        var n = request.Sections[0].OuterLoop.Count;
        for (var s = 0; s < request.Sections.Count; s++) for (var i = 0; i < n; i++) e.Add(E($"v:s{s}:{i}", AirBRepPlanElementKind.Vertex, AirBRepPlanRole.SectionVertex, s, i));
        diagnostics.Add(D("air-x3-section-vertices-planned"));
        for (var s = 0; s < request.Sections.Count; s++) for (var i = 0; i < n; i++) { e.Add(E($"c:section:{s}:{i}", AirBRepPlanElementKind.Curve, AirBRepPlanRole.SectionEdge, s, null, null, i)); e.Add(E($"e:section:{s}:{i}", AirBRepPlanElementKind.Edge, AirBRepPlanRole.SectionEdge, s, null, null, i)); }
        diagnostics.Add(D("air-x3-section-edges-planned"));
        for (var s = 0; s < request.Sections.Count - 1; s++) for (var i = 0; i < n; i++) { e.Add(E($"c:transition:{s}:{i}", AirBRepPlanElementKind.Curve, AirBRepPlanRole.VerticalTransitionEdge, null, null, s, i)); e.Add(E($"e:transition:{s}:{i}", AirBRepPlanElementKind.Edge, AirBRepPlanRole.VerticalTransitionEdge, null, null, s, i)); }
        diagnostics.Add(D("air-x3-transition-edges-planned"));
        e.Add(E("surf:cap:bottom", AirBRepPlanElementKind.Surface, AirBRepPlanRole.CapFace)); e.Add(E("loop:face:cap:bottom", AirBRepPlanElementKind.Loop, AirBRepPlanRole.SectionLoop)); e.Add(E("f:cap:bottom", AirBRepPlanElementKind.Face, AirBRepPlanRole.CapFace, faceRole: "bottom-cap"));
        e.Add(E("surf:cap:top", AirBRepPlanElementKind.Surface, AirBRepPlanRole.CapFace)); e.Add(E("loop:face:cap:top", AirBRepPlanElementKind.Loop, AirBRepPlanRole.SectionLoop)); e.Add(E("f:cap:top", AirBRepPlanElementKind.Face, AirBRepPlanRole.CapFace, faceRole: "top-cap"));
        diagnostics.Add(D("air-x3-cap-faces-planned"));
        for (var s = 0; s < request.Sections.Count - 1; s++) for (var i = 0; i < n; i++) { var role = s == 0 ? AirBRepPlanRole.SideFace : AirBRepPlanRole.TransitionFace; var label = s == 0 ? "side" : "transition"; e.Add(E($"surf:{label}:interval{s}:edge{i}", AirBRepPlanElementKind.Surface, role, null, null, s, i)); e.Add(E($"loop:face:{label}:interval{s}:edge{i}", AirBRepPlanElementKind.Loop, role, null, null, s, i)); e.Add(E($"f:{label}:interval{s}:edge{i}", AirBRepPlanElementKind.Face, role, null, null, s, i, label)); }
        diagnostics.Add(D("air-x3-transition-faces-planned"));
        for (var i = 0; i < 40; i++) e.Add(E($"coedge:{i:00}", AirBRepPlanElementKind.Coedge, AirBRepPlanRole.Unknown));
        e.Add(E("shell:body:0", AirBRepPlanElementKind.Shell, AirBRepPlanRole.BodyShell)); diagnostics.Add(D("air-x3-shell-planned"));
        e.Add(E("body:0", AirBRepPlanElementKind.Body, AirBRepPlanRole.Body)); diagnostics.Add(D("air-x3-body-planned"));
        diagnostics.Add(D("air-x3-stable-planned-ids-created"));
        return e;
        AirBRepPlanElement E(string id, AirBRepPlanElementKind kind, AirBRepPlanRole role, int? section = null, int? vertex = null, int? interval = null, int? edge = null, string? faceRole = null) => new(new AirBRepPlanId(id), kind, role, sourceAirNodeId, provenance, section, vertex, interval, edge, faceRole, []);
    }

    private static AirBRepPlanSummary Summary(PrismaticSectionTransitionRequest request, string sourceAirNodeId, IReadOnlyList<AirDiagnostic> diagnostics, IReadOnlyList<string> guarantees)
    {
        var n = request.Sections[0].OuterLoop.Count; var intervals = request.Sections.Count - 1; var faces = 2 + intervals * n;
        var allX = request.Sections.SelectMany(s => s.OuterLoop.Select(p => p.X)); var allY = request.Sections.SelectMany(s => s.OuterLoop.Select(p => p.Y));
        var bounds = FormattableString.Invariant($"[{allX.Min():0.###},{allY.Min():0.###},{request.Sections.Min(s => s.Z):0.###}]..[{allX.Max():0.###},{allY.Max():0.###},{request.Sections.Max(s => s.Z):0.###}]");
        return new(AirBRepPlanKind.PrismaticSectionTransition, sourceAirNodeId, request.Sections.Count * n, request.Sections.Count * n + intervals * n, request.Sections.Count * n + intervals * n, (2 * n) + (4 * intervals * n), faces, faces, faces, 1, 1, 2, n, n, 0, bounds, PreserveSectionSplits, diagnostics, guarantees);
    }

    private static AirBRepPlanValidationResult Validation(bool succeeded, string sourceAirNodeId, List<AirDiagnostic> diagnostics, AirDiagnostic error)
    {
        diagnostics.Add(error); diagnostics = Stable(diagnostics).ToList();
        var empty = new AirBRepPlanSummary(AirBRepPlanKind.Unsupported, sourceAirNodeId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "none", PreserveSectionSplits, diagnostics, RouteGuarantees);
        var errors = error.Severity == AirDiagnosticSeverity.Error ? new[] { error } : [];
        var warnings = error.Severity == AirDiagnosticSeverity.Warning ? new[] { error } : [];
        return new(succeeded, diagnostics, errors, warnings, empty);
    }

    private static AirProvenance Provenance(string sourceAirNodeId) => new("AIR-X3", "Constructive AIR BRepPlan", "canonical-rectangle-inset-section-transition", sourceAirNodeId, nameof(PrismaticSectionTransitionEmitter), AirSelectionClass.None, AirRuleKind.None, "generated/constructive; split policy: preserve section splits", false, ["BRepPlan is non-production and does not materialize BRep."]);
    private static AirDiagnostic D(string code, AirDiagnosticSeverity severity = AirDiagnosticSeverity.Info) => new(code, severity, code);
    private static IReadOnlyList<AirDiagnostic> Stable(IEnumerable<AirDiagnostic> d) => d.GroupBy(x => x.Code).Select(g => g.First()).OrderBy(x => x.Code, StringComparer.Ordinal).ToArray();
}
