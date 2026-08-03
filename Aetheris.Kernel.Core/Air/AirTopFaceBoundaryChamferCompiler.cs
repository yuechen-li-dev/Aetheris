using Aetheris.Kernel.Core.Air.BRepPlan;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Prismatic;

namespace Aetheris.Kernel.Core.Air;

internal enum AirFeatureAdmissionStatus { Admitted, Rejected, Deferred }
internal sealed record AirSourceSpan(int Start, int Length, string SourceName);
internal sealed record AirFaceBoundarySelection(string FaceAxis, string BoundaryKind = "Outer", bool CompleteLoop = true);
internal sealed record AirEqualDistanceChamferRule(double Distance, string Unit = "mm");

internal sealed record AirChamferFeature(
    string FeatureId,
    string FeatureName,
    string BodyId,
    AirFaceBoundarySelection Selection,
    AirEqualDistanceChamferRule Rule,
    AirSourceSpan SourceSpan,
    string ConstructionHistoryKind,
    AirFeatureAdmissionStatus Admission,
    string AdmissionReason);

internal sealed record AirPlanarProfileNode(string NodeId, int Order, double Z, IReadOnlyList<(double X, double Y)> OuterLoop);
internal sealed record AirSectionTransitionNode(string NodeId, IReadOnlyList<string> OrderedProfileNodeIds, string Correspondence, string SplitPolicy);
internal sealed record AirChamferConstruction(
    string ConstructionId,
    string SourceFeatureId,
    IReadOnlyList<AirPlanarProfileNode> Profiles,
    AirSectionTransitionNode Transition,
    PrismaticSectionTransitionRequest Request);

internal sealed record AirTopFaceBoundaryChamferCompileRequest(
    string BodyId,
    string FeatureId,
    string FeatureName,
    double Width,
    double Depth,
    double Height,
    string FaceAxis,
    string Target,
    string Kind,
    double Distance,
    AirSourceSpan SourceSpan,
    bool HistoryKnown = true);

internal sealed record AirTopFaceBoundaryChamferCompileResult(
    bool Succeeded,
    AirChamferFeature Feature,
    AirChamferConstruction? Construction,
    AirBRepPlan? BRepPlan,
    BrepBody? Body,
    PrismaticTransitionTopologySummary? Topology,
    IReadOnlyList<string> Diagnostics)
{
    public const string ProductionRoute = "AirPrismaticTopFaceBoundaryChamfer";
}

/// <summary>Production compiler for the single admitted history-known rectangular-prism top-boundary chamfer.</summary>
internal static class AirTopFaceBoundaryChamferCompiler
{
    private const double Tol = 1e-9;

    public static AirTopFaceBoundaryChamferCompileResult Compile(AirTopFaceBoundaryChamferCompileRequest input)
    {
        var (status, reason) = Admit(input);
        var feature = new AirChamferFeature(
            input.FeatureId,
            input.FeatureName,
            input.BodyId,
            new AirFaceBoundarySelection(input.FaceAxis),
            new AirEqualDistanceChamferRule(input.Distance),
            input.SourceSpan,
            input.HistoryKnown ? "generated/history-known-axis-aligned-rectangular-prism" : "imported/no-history",
            status,
            reason);
        if (status != AirFeatureAdmissionStatus.Admitted)
            return new(false, feature, null, null, null, null, [reason]);

        var geometricRequest = new PrismaticTopFaceLoopChamferRequest(input.Width, input.Depth, input.Height, input.Distance);
        var sections = PrismaticTopFaceLoopChamferPrototype.CreateSectionStack(geometricRequest);
        var profiles = sections.Select((section, index) => new AirPlanarProfileNode(
            $"{input.FeatureId}:profile:{index}", index, section.Z, section.OuterLoop.ToArray())).ToArray();
        var request = new PrismaticSectionTransitionRequest(
            sections,
            PrismaticCorrespondenceMap.Identity(4),
            new PrismaticSectionTransitionOptions(false, AirTopFaceBoundaryChamferCompileResult.ProductionRoute));
        var transition = new AirSectionTransitionNode(
            $"{input.FeatureId}:section-transition",
            profiles.Select(p => p.NodeId).ToArray(),
            "identity-by-profile-index",
            PrismaticSectionTransitionTopologyPlanner.PreserveSectionSplits);
        var construction = new AirChamferConstruction($"construction:{input.FeatureId}", input.FeatureId, profiles, transition, request);

        var featureContext = new AirBRepPlanFeatureContext(
            AirNodeKind.TopFaceLoopChamfer,
            AirRouteKind.TopFaceLoopChamferPrismatic,
            AirSelectionClass.FaceBoundaryLoop,
            AirRuleKind.UniformChamfer,
            feature.ConstructionHistoryKind,
            "BoundedProductionAdmission",
            ["complete +Z outer boundary loop", "uniform equal-distance rule"]);
        var planned = AirTopFaceLoopChamferBRepPlanner.Plan(geometricRequest, featureContext, input.FeatureId);
        if (!planned.Succeeded || planned.Plan?.RealizationPlan is null)
            return new(false, feature, construction, planned.Plan, null, null, planned.Validation.Diagnostics.Select(d => d.Code).ToArray());

        var emitted = PrismaticSectionTransitionEmitter.Emit(
            planned.Plan.RealizationPlan,
            new PrismaticSectionTransitionOptions(false, AirTopFaceBoundaryChamferCompileResult.ProductionRoute));
        if (!emitted.Succeeded || emitted.Body is null)
            return new(false, feature, construction, planned.Plan, emitted.Body, emitted.Topology, emitted.Diagnostics);

        var topologyMatchesPlan = emitted.Topology.VertexCount == planned.Plan.RealizationPlan.Vertices.Count
            && emitted.Topology.EdgeCount == planned.Plan.RealizationPlan.Edges.Count
            && emitted.Topology.FaceCount == planned.Plan.RealizationPlan.Faces.Count
            && emitted.Topology.LoopCount == planned.Plan.RealizationPlan.ExpectedLoopCount
            && emitted.Topology.CoedgeCount == planned.Plan.RealizationPlan.ExpectedCoedgeCount;
        if (!topologyMatchesPlan)
            return new(false, feature, construction, planned.Plan, emitted.Body, emitted.Topology, emitted.Diagnostics.Concat(["air-chamfer-materialization-diverged-from-authoritative-brep-plan"]).ToArray());

        var top = planned.Plan.RealizationPlan.Vertices.Where(v => v.SectionIndex == 2).Select(v => v.Point).ToArray();
        var insetIsExact = top.Min(p => p.X) == (-input.Width / 2d) + input.Distance
            && top.Max(p => p.X) == (input.Width / 2d) - input.Distance
            && top.Min(p => p.Y) == (-input.Depth / 2d) + input.Distance
            && top.Max(p => p.Y) == (input.Depth / 2d) - input.Distance;
        if (!insetIsExact)
            return new(false, feature, construction, planned.Plan, emitted.Body, emitted.Topology, ["air-chamfer-top-inset-evidence-mismatch"]);

        return new(true, feature, construction, planned.Plan, emitted.Body, emitted.Topology,
            emitted.Diagnostics.Concat(["air-chamfer-feature-admitted", "air-chamfer-authoritative-brep-plan-consumed", "air-chamfer-top-inset-verified"]).Distinct().Order().ToArray());
    }

    private static (AirFeatureAdmissionStatus Status, string Reason) Admit(AirTopFaceBoundaryChamferCompileRequest input)
    {
        if (!input.HistoryKnown) return (AirFeatureAdmissionStatus.Deferred, "air-chamfer-imported-no-history-body-deferred");
        if (!string.Equals(input.FaceAxis, "+Z", StringComparison.Ordinal)) return (AirFeatureAdmissionStatus.Rejected, "air-chamfer-unsupported-face-rejected:expected-+Z");
        if (!string.Equals(input.Target, "Boundary", StringComparison.Ordinal)) return (AirFeatureAdmissionStatus.Rejected, "air-chamfer-unsupported-selection-rejected:expected-Boundary");
        if (!string.Equals(input.Kind, "Chamfer", StringComparison.Ordinal)) return (AirFeatureAdmissionStatus.Rejected, "air-chamfer-unsupported-edge-finish-kind-rejected:expected-Chamfer");
        if (!double.IsFinite(input.Width) || !double.IsFinite(input.Depth) || !double.IsFinite(input.Height) || input.Width <= Tol || input.Depth <= Tol || input.Height <= Tol)
            return (AirFeatureAdmissionStatus.Rejected, "air-chamfer-invalid-host-dimensions-rejected");
        if (!double.IsFinite(input.Distance) || input.Distance <= Tol) return (AirFeatureAdmissionStatus.Rejected, "air-chamfer-distance-must-be-positive");
        if (input.Distance >= input.Width / 2d - Tol || input.Distance >= input.Depth / 2d - Tol || input.Distance >= input.Height - Tol)
            return (AirFeatureAdmissionStatus.Rejected, "air-chamfer-distance-too-large-rejected");
        return (AirFeatureAdmissionStatus.Admitted, "air-chamfer-bounded-top-face-boundary-admitted");
    }
}
