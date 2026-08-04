using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Prismatic;

namespace Aetheris.Kernel.Core.Air.BRepPlan;

internal enum AirBRepPlanKind { PrismaticSectionTransition, RevolvedProfile, LocalizedPlanarReplacement, LocalizedTangentBlend, LocalizedEdgeJunction, Unsupported }
internal enum AirBRepPlanElementKind { Vertex, Curve, Edge, Coedge, Loop, Surface, Face, Shell, Body }
internal enum AirBRepPlanRole { Unknown, SectionVertex, SectionEdge, VerticalTransitionEdge, SectionLoop, ProfileVertex, ProfileSegment, CircularRim, SeamEdge, CapFace, SideFace, CylindricalFace, ConicalTransitionFace, TransitionFace, PrismaticTransitionFace, ChamferFace, FilletFace, ReplacementFace, ReplacementFaceA, ReplacementFaceB, ReplacementBoundaryA, ReplacementBoundaryB, SharedJunction, DirectJunctionBoundary, CornerPatch, RetainedSupportFaceA, RetainedSupportFaceB, EndpointTransitionStart, EndpointTransitionEnd, RemoteEndpointA, RemoteEndpointB, UnaffectedFace, BodyShell, Body }

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
    IReadOnlyList<AirDiagnostic>? Diagnostics = null,
    IReadOnlyList<AirBRepPlanRole>? SemanticRoles = null);

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
    IReadOnlyList<string> Guarantees,
    AirBRepPlanFeatureContext? FeatureContext = null);

internal sealed record AirBRepPlanFeatureContext(
    AirNodeKind SourceNodeKind,
    AirRouteKind RouteKind,
    AirSelectionClass SelectionClass,
    AirRuleKind RuleKind,
    string ConstructionHistoryKind,
    string RouteSelectionMode,
    IReadOnlyList<string> Notes);

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
    IReadOnlyList<string> Guarantees,
    AirBRepPlanFeatureContext? FeatureContext = null,
    PrismaticSectionTransitionTopologyPlan? RealizationPlan = null,
    RevolvedProfileTopologyPlan? RevolvedRealizationPlan = null,
    AirLocalizedPlanarReplacementTopologyPlan? LocalizedPlanarReplacementRealizationPlan = null,
    AirLocalizedTangentBlendTopologyPlan? LocalizedTangentBlendRealizationPlan = null,
    LocalizedEdgeReplacementTopologyPlan? LocalizedEdgeReplacementRealizationPlan = null,
    LocalizedEdgeJunctionTopologyPlan? LocalizedEdgeJunctionRealizationPlan = null)
{
    public bool IsAuthoritative => RealizationPlan is not null || RevolvedRealizationPlan is not null || LocalizedPlanarReplacementRealizationPlan is not null || LocalizedTangentBlendRealizationPlan is not null || LocalizedEdgeReplacementRealizationPlan is not null || LocalizedEdgeJunctionRealizationPlan is not null;
}

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

        var realizationPlan = PrismaticSectionTransitionTopologyPlanner.Create(request);
        var elements = BuildElements(realizationPlan, sourceAirNodeId, provenance, diagnostics);
        var summary = Summary(realizationPlan, sourceAirNodeId, diagnostics, RouteGuarantees);
        diagnostics.Add(D("air-x3-prismatic-plan-created"));
        diagnostics.Add(D("air-x3-prismatic-plan-validated"));
        diagnostics.AddRange(RouteGuarantees.Select(g => D("air-x3-" + g.Replace(" ", "-").Replace("STEP", "step").Replace("BRep", "brep").Replace("AirEdgeSweep", "air-edge-sweep").Replace("Boolean", "boolean"))));
        diagnostics = Stable(diagnostics).ToList();
        summary = summary with { Diagnostics = diagnostics };
        var plan = new AirBRepPlan("brep-plan:prismatic-section-transition:" + sourceAirNodeId, AirBRepPlanKind.PrismaticSectionTransition, sourceAirNodeId, provenance, elements, summary, diagnostics, RouteGuarantees, RealizationPlan: realizationPlan);
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

    private static IReadOnlyList<AirBRepPlanElement> BuildElements(PrismaticSectionTransitionTopologyPlan plan, string sourceAirNodeId, AirProvenance provenance, List<AirDiagnostic> diagnostics)
    {
        var e = new List<AirBRepPlanElement>();
        foreach (var vertex in plan.Vertices) e.Add(E(vertex.Id, AirBRepPlanElementKind.Vertex, AirBRepPlanRole.SectionVertex, vertex.SectionIndex, vertex.ProfileVertexIndex));
        diagnostics.Add(D("air-x3-section-vertices-planned"));
        foreach (var edge in plan.Edges)
        {
            var role = edge.Kind == PrismaticPlannedEdgeKind.Section ? AirBRepPlanRole.SectionEdge : AirBRepPlanRole.VerticalTransitionEdge;
            e.Add(E(edge.Id.Replace("e:", "c:", StringComparison.Ordinal), AirBRepPlanElementKind.Curve, role, edge.SectionIndex, null, edge.IntervalIndex, edge.ProfileEdgeIndex));
            e.Add(E(edge.Id, AirBRepPlanElementKind.Edge, role, edge.SectionIndex, null, edge.IntervalIndex, edge.ProfileEdgeIndex));
        }
        diagnostics.Add(D("air-x3-section-edges-planned"));
        diagnostics.Add(D("air-x3-transition-edges-planned"));
        var coedgeIndex = 0;
        foreach (var face in plan.Faces)
        {
            var role = face.Kind switch
            {
                PrismaticPlannedFaceKind.BottomCap or PrismaticPlannedFaceKind.TopCap => AirBRepPlanRole.CapFace,
                PrismaticPlannedFaceKind.StableSide => AirBRepPlanRole.SideFace,
                _ => AirBRepPlanRole.TransitionFace,
            };
            var faceRole = face.Kind switch { PrismaticPlannedFaceKind.BottomCap => "bottom-cap", PrismaticPlannedFaceKind.TopCap => "top-cap", PrismaticPlannedFaceKind.StableSide => "side", _ => "transition" };
            var suffix = face.Id[2..];
            e.Add(E($"surf:{suffix}", AirBRepPlanElementKind.Surface, role, interval: face.IntervalIndex, edge: face.ProfileEdgeIndex));
            e.Add(E($"loop:face:{suffix}", AirBRepPlanElementKind.Loop, role, interval: face.IntervalIndex, edge: face.ProfileEdgeIndex));
            e.Add(E(face.Id, AirBRepPlanElementKind.Face, role, interval: face.IntervalIndex, edge: face.ProfileEdgeIndex, faceRole: faceRole));
            foreach (var _ in face.Boundary) e.Add(E($"coedge:{coedgeIndex++:00}", AirBRepPlanElementKind.Coedge, role, interval: face.IntervalIndex, edge: face.ProfileEdgeIndex));
        }
        diagnostics.Add(D("air-x3-cap-faces-planned"));
        diagnostics.Add(D("air-x3-transition-faces-planned"));
        e.Add(E("shell:body:0", AirBRepPlanElementKind.Shell, AirBRepPlanRole.BodyShell)); diagnostics.Add(D("air-x3-shell-planned"));
        e.Add(E("body:0", AirBRepPlanElementKind.Body, AirBRepPlanRole.Body)); diagnostics.Add(D("air-x3-body-planned"));
        diagnostics.Add(D("air-x3-stable-planned-ids-created"));
        return e;
        AirBRepPlanElement E(string id, AirBRepPlanElementKind kind, AirBRepPlanRole role, int? section = null, int? vertex = null, int? interval = null, int? edge = null, string? faceRole = null) => new(new AirBRepPlanId(id), kind, role, sourceAirNodeId, provenance, section, vertex, interval, edge, faceRole, [], role == AirBRepPlanRole.TransitionFace ? [AirBRepPlanRole.PrismaticTransitionFace] : [role]);
    }

    private static AirBRepPlanSummary Summary(PrismaticSectionTransitionTopologyPlan plan, string sourceAirNodeId, IReadOnlyList<AirDiagnostic> diagnostics, IReadOnlyList<string> guarantees)
    {
        var bounds = FormattableString.Invariant($"[{plan.Vertices.Min(v => v.Point.X):0.###},{plan.Vertices.Min(v => v.Point.Y):0.###},{plan.Vertices.Min(v => v.Point.Z):0.###}]..[{plan.Vertices.Max(v => v.Point.X):0.###},{plan.Vertices.Max(v => v.Point.Y):0.###},{plan.Vertices.Max(v => v.Point.Z):0.###}]");
        var caps = plan.Faces.Count(f => f.Kind is PrismaticPlannedFaceKind.BottomCap or PrismaticPlannedFaceKind.TopCap);
        var sides = plan.Faces.Count(f => f.Kind == PrismaticPlannedFaceKind.StableSide);
        var transitions = plan.Faces.Count(f => f.Kind == PrismaticPlannedFaceKind.Transition);
        return new(AirBRepPlanKind.PrismaticSectionTransition, sourceAirNodeId, plan.Vertices.Count, plan.Edges.Count, plan.Edges.Count, plan.ExpectedCoedgeCount, plan.ExpectedLoopCount, plan.Faces.Count, plan.Faces.Count, 1, 1, caps, sides, transitions, 0, bounds, plan.SplitPolicy, diagnostics, guarantees);
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

internal static class AirTopFaceLoopChamferBRepPlanner
{
    public static AirBRepPlanResult Plan(
        PrismaticTopFaceLoopChamferRequest request,
        AirBRepPlanFeatureContext? featureContext = null,
        string sourceAirNodeId = "air-x4-top-face-loop-chamfer-canonical")
    {
        featureContext ??= CanonicalFeatureContext();
        var diagnostics = new List<AirDiagnostic> { D("air-x4-top-face-loop-chamfer-brep-plan-created") };
        var rejected = ValidateFeatureContext(featureContext);
        if (rejected is not null)
            return Reject(sourceAirNodeId, diagnostics, rejected);

        var selection = request.Selection ?? new FaceLoopChamferSelection();
        rejected = ValidateRequest(request, selection);
        if (rejected is not null)
            return Reject(sourceAirNodeId, diagnostics, rejected);

        var provenance = new AirProvenance(
            "AIR-X4",
            "Constructive AIR BRepPlan feature role overlay",
            "canonical-top-face-loop-chamfer",
            sourceAirNodeId,
            nameof(AirRouteKind.TopFaceLoopChamferPrismatic),
            AirSelectionClass.FaceBoundaryLoop,
            AirRuleKind.UniformChamfer,
            "generated/history-known",
            true,
            ["Class B face-boundary loop uniform chamfer lowered through the authoritative prismatic section-transition plan.", "not-four-independent-single-edge-chamfers"]);

        var prismatic = AirPrismaticSectionTransitionBRepPlanner.Plan(
            new PrismaticSectionTransitionRequest(PrismaticTopFaceLoopChamferPrototype.CreateSectionStack(request), PrismaticCorrespondenceMap.Identity(4), new PrismaticSectionTransitionOptions(RunStepSmoke: request.ExportStep, TraceLabel: "air-x4-top-face-loop-chamfer-brep-plan")),
            sourceAirNodeId,
            provenance);
        if (!prismatic.Succeeded || prismatic.Plan is null)
            return prismatic;

        diagnostics.AddRange(prismatic.Diagnostics());
        diagnostics.AddRange(new[]
        {
            D("air-x4-feature-role-overlay-applied"),
            D("air-x4-class-b-face-boundary-loop-provenance"),
            D("air-x4-uniform-chamfer-rule-provenance"),
            D("air-x4-upper-transition-faces-marked-chamfer"),
            D("air-x4-not-four-independent-single-edge-chamfers"),
            D("air-x4-prismatic-plan-reused"),
            D("air-x4-plan-matches-existing-loop-chamfer-emitter"),
            D("air-x4-split-preserving-plan"),
            D("air-x4-no-production-route-replacement"),
            D("air-x4-no-emitter-rewrite"),
            D("air-x4-no-step-exporter-change"),
            D("air-x4-no-brep-topology-change"),
            D("air-x4-no-coplanar-merge"),
            D("air-x4-no-air-edge-sweep"),
            D("air-x4-no-brep-bounded-chamfer"),
            D("air-x4-no-boolean"),
            D("air-x4-no-topology-graft"),
        });

        var elements = prismatic.Plan.Elements.Select(e =>
            e.Kind == AirBRepPlanElementKind.Face && e.IntervalIndex == 1 && e.Role == AirBRepPlanRole.TransitionFace
                ? e with { SemanticRoles = StableRoles((e.SemanticRoles ?? []).Concat([AirBRepPlanRole.PrismaticTransitionFace, AirBRepPlanRole.ChamferFace])), FaceRole = "top-face-loop-chamfer" }
                : e).ToArray();
        var chamferFaceCount = elements.Count(e => e.Kind == AirBRepPlanElementKind.Face && (e.SemanticRoles ?? []).Contains(AirBRepPlanRole.ChamferFace));
        if (chamferFaceCount != 4)
            return Reject(sourceAirNodeId, diagnostics, D("air-x4-plan-overlay-mismatch-rejected", AirDiagnosticSeverity.Error));

        var guarantees = StableStrings(prismatic.Plan.Guarantees.Concat(["no BrepBoundedChamfer", "not four independent single-edge chamfers"])).ToArray();
        diagnostics = StableDiagnostics(diagnostics).ToList();
        var summary = prismatic.Plan.Summary with { ChamferFaceCount = chamferFaceCount, Diagnostics = diagnostics, Guarantees = guarantees, FeatureContext = featureContext };
        var plan = prismatic.Plan with { Elements = elements, Summary = summary, Diagnostics = diagnostics, Guarantees = guarantees, FeatureContext = featureContext };
        return new AirBRepPlanResult(plan, new AirBRepPlanValidationResult(true, diagnostics, [], [], summary));
    }

    public static AirBRepPlanFeatureContext CanonicalFeatureContext() => new(
        AirNodeKind.TopFaceLoopChamfer,
        AirRouteKind.TopFaceLoopChamferPrismatic,
        AirSelectionClass.FaceBoundaryLoop,
        AirRuleKind.UniformChamfer,
        "generated/history-known",
        "SwitchMatch",
        ["not-four-independent-single-edge-chamfers"]);

    private static AirDiagnostic? ValidateFeatureContext(AirBRepPlanFeatureContext context)
    {
        if (context.SourceNodeKind != AirNodeKind.TopFaceLoopChamfer) return D("air-x4-missing-loop-chamfer-provenance-rejected", AirDiagnosticSeverity.Error);
        if (context.SelectionClass != AirSelectionClass.FaceBoundaryLoop) return D("air-x4-non-face-boundary-loop-rejected", AirDiagnosticSeverity.Error);
        if (context.RuleKind != AirRuleKind.UniformChamfer) return D("air-x4-non-uniform-chamfer-rule-rejected", AirDiagnosticSeverity.Error);
        if (context.RouteKind != AirRouteKind.TopFaceLoopChamferPrismatic) return D("air-x4-non-prismatic-lowering-deferred", AirDiagnosticSeverity.Warning);
        return null;
    }

    private static AirDiagnostic? ValidateRequest(PrismaticTopFaceLoopChamferRequest request, FaceLoopChamferSelection selection)
    {
        if (selection.SelectionKind != FaceLoopChamferSelectionKind.FaceBoundaryLoop) return D("air-x4-non-face-boundary-loop-rejected", AirDiagnosticSeverity.Error);
        if (request.Rule != FaceLoopChamferRuleKind.UniformSymmetric) return D("air-x4-non-uniform-chamfer-rule-rejected", AirDiagnosticSeverity.Error);
        if (selection.OwningFace != FaceLoopChamferOwningFaceKind.TopCap || selection.LoopKind != FaceLoopChamferLoopKind.Outer) return D("air-x4-non-top-face-loop-deferred", AirDiagnosticSeverity.Warning);
        return null;
    }

    private static AirBRepPlanResult Reject(string sourceAirNodeId, List<AirDiagnostic> diagnostics, AirDiagnostic error)
    {
        diagnostics.Add(error);
        diagnostics = StableDiagnostics(diagnostics).ToList();
        var summary = new AirBRepPlanSummary(AirBRepPlanKind.Unsupported, sourceAirNodeId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "none", AirPrismaticSectionTransitionBRepPlanner.PreserveSectionSplits, diagnostics, []);
        return new AirBRepPlanResult(null, new AirBRepPlanValidationResult(false, diagnostics, error.Severity == AirDiagnosticSeverity.Error ? [error] : [], error.Severity == AirDiagnosticSeverity.Warning ? [error] : [], summary));
    }

    private static AirDiagnostic D(string code, AirDiagnosticSeverity severity = AirDiagnosticSeverity.Info) => new(code, severity, code);
    private static IReadOnlyList<AirDiagnostic> StableDiagnostics(IEnumerable<AirDiagnostic> d) => d.GroupBy(x => x.Code).Select(g => g.First()).OrderBy(x => x.Code, StringComparer.Ordinal).ToArray();
    private static IReadOnlyList<AirBRepPlanRole> StableRoles(IEnumerable<AirBRepPlanRole> roles) => roles.Distinct().OrderBy(r => r.ToString(), StringComparer.Ordinal).ToArray();
    private static IEnumerable<string> StableStrings(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal);
    private static IEnumerable<AirDiagnostic> Diagnostics(this AirBRepPlanResult result) => result.Plan?.Diagnostics ?? result.Validation.Diagnostics;
}
