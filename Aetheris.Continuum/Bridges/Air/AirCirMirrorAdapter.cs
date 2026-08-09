using Aetheris.Kernel.Core.Brep.Prismatic;
using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Air.BRepPlan;
using Aetheris.Continuum.Mirrors;

namespace Aetheris.Continuum.Bridges.Air;

[Flags]
internal enum AirCirMirrorCapability
{
    None = 0,
    Occupancy = 1 << 0,
    Map = 1 << 1,
    Containment = 1 << 2,
    Bounds = 1 << 3,
    VolumeEstimate = 1 << 4,
    ThicknessSummary = 1 << 5,
    FaceIdentity = 1 << 16,
    LoopIdentity = 1 << 17,
    TopologyParity = 1 << 18,
    FeatureLabels = 1 << 19,
    ChamferFaceIdentity = 1 << 20,
    BRepPlanRoleParity = 1 << 21,
}

[Flags]
internal enum AirCirMirrorKnownLoss
{
    None = 0,
    FaceIdentity = 1 << 0,
    LoopIdentity = 1 << 1,
    EdgeIdentity = 1 << 2,
    TopologyParity = 1 << 3,
    FeatureLabels = 1 << 4,
    ChamferFaceIdentity = 1 << 5,
    BRepPlanRoleParity = 1 << 6,
}

internal enum AirCirMirrorSourceKind { GeneratedNativeAir, ImportedOrRecovered, BRepOnly, Unsupported }

internal sealed record AirCirMirrorRequest(
    AirNodeKind SourceNodeKind,
    AirRouteKind RouteKind,
    AirCirMirrorSourceKind SourceKind,
    string SourceNodeId,
    AirCirMirrorCapability RequestedCapabilities = AirCirMirrorCapability.Occupancy | AirCirMirrorCapability.Map | AirCirMirrorCapability.Containment | AirCirMirrorCapability.Bounds,
    AirSelectionClass SelectionClass = AirSelectionClass.None,
    AirRuleKind RuleKind = AirRuleKind.None,
    string ConstructionHistoryKind = "generated/constructive");

internal sealed record AirCirMirrorAdapterSummary(
    AirNodeKind SourceNodeKind,
    AirRouteKind RouteKind,
    CirMirrorStatus Status,
    string StatusText,
    string MirrorBackend,
    AirCirMirrorCapability Capabilities,
    AirCirMirrorKnownLoss KnownLosses,
    AirCirMirrorSourceKind SourceKind,
    string SourceNodeId,
    AirSelectionClass SelectionClass,
    AirRuleKind RuleKind,
    string MirrorBuilderRoute,
    IReadOnlyList<string> Provenance,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Guarantees);

internal sealed record AirCirMirrorAdapterResult(
    bool Succeeded,
    AirCirMirrorAdapterSummary Summary,
    CirPrismaticMirrorResult? PrismaticMirrorResult = null,
    CirPrismaticMirrorSummary? TopViewSummary = null);

internal static class AirCirMirrorAdapter
{
    private const string Backend = "cir-convex-polyhedron";
    private const string BuilderRoute = "CirPrismaticMirrorBuilder.BuildFromSections";

    private static readonly AirCirMirrorCapability TopologyClaims = AirCirMirrorCapability.FaceIdentity | AirCirMirrorCapability.LoopIdentity | AirCirMirrorCapability.TopologyParity | AirCirMirrorCapability.FeatureLabels | AirCirMirrorCapability.ChamferFaceIdentity | AirCirMirrorCapability.BRepPlanRoleParity;
    private static readonly AirCirMirrorKnownLoss BaseLosses = AirCirMirrorKnownLoss.FaceIdentity | AirCirMirrorKnownLoss.LoopIdentity | AirCirMirrorKnownLoss.EdgeIdentity | AirCirMirrorKnownLoss.TopologyParity | AirCirMirrorKnownLoss.FeatureLabels;
    private static readonly string[] BaseGuarantees = ["evaluation side-channel only", "no topology authority", "no production analyzer change", "no production route replacement", "no BRepPlan behavior change"];

    public static AirCirMirrorAdapterResult AdmitPrismaticSectionTransition(PrismaticSectionTransitionRequest request, AirCirMirrorRequest? mirrorRequest = null)
    {
        mirrorRequest ??= new(AirNodeKind.PrismaticSectionTransition, AirRouteKind.PrismaticSectionTransitionEmitter, AirCirMirrorSourceKind.GeneratedNativeAir, "air-x5-prismatic-canonical");
        var diagnostics = new List<string> { "air-x5-cir-mirror-adapter-created", "air-x5-prismatic-cir-mirror-request-created" };
        var rejected = RejectInvalidSourceOrClaim(mirrorRequest, diagnostics, BaseLosses);
        if (rejected is not null) return rejected;

        var result = CirPrismaticMirrorBuilder.BuildFromSections(mirrorRequest.SourceNodeId, request.Sections, request.Correspondence);
        diagnostics.AddRange(result.Diagnostics);
        if (result.Succeeded) diagnostics.Add("air-x5-convex-polyhedron-mirror-admitted");
        else diagnostics.Add("air-x5-non-convex-prismatic-mirror-rejected");
        AddBoundaryDiagnostics(diagnostics);
        return FromMirrorResult(mirrorRequest, result, BaseLosses, diagnostics);
    }

    public static AirCirMirrorAdapterResult AdmitCanonicalPrismaticSectionTransition(AirCirMirrorRequest? mirrorRequest = null) =>
        AdmitPrismaticSectionTransition(new PrismaticSectionTransitionRequest(AirPrismaticSectionTransitionWrapper.CanonicalSections(), PrismaticCorrespondenceMap.Identity(4), new PrismaticSectionTransitionOptions(TraceLabel: "air-x5-prismatic-cir-mirror")), mirrorRequest);

    public static AirCirMirrorAdapterResult AdmitTopFaceLoopChamfer(PrismaticTopFaceLoopChamferRequest request, AirCirMirrorRequest? mirrorRequest = null, AirBRepPlanFeatureContext? featureContext = null)
    {
        mirrorRequest ??= new(AirNodeKind.TopFaceLoopChamfer, AirRouteKind.TopFaceLoopChamferPrismatic, AirCirMirrorSourceKind.GeneratedNativeAir, "air-x5-top-face-loop-chamfer-canonical", SelectionClass: AirSelectionClass.FaceBoundaryLoop, RuleKind: AirRuleKind.UniformChamfer, ConstructionHistoryKind: "generated/history-known");
        featureContext ??= AirTopFaceLoopChamferBRepPlanner.CanonicalFeatureContext();
        var losses = BaseLosses | AirCirMirrorKnownLoss.ChamferFaceIdentity | AirCirMirrorKnownLoss.BRepPlanRoleParity;
        var diagnostics = new List<string> { "air-x5-cir-mirror-adapter-created", "air-x5-top-face-loop-chamfer-cir-mirror-request-created" };
        var rejected = RejectInvalidSourceOrClaim(mirrorRequest, diagnostics, losses);
        if (rejected is not null) return rejected;
        if (featureContext.SourceNodeKind != AirNodeKind.TopFaceLoopChamfer || featureContext.SelectionClass != AirSelectionClass.FaceBoundaryLoop || featureContext.RuleKind != AirRuleKind.UniformChamfer || featureContext.RouteKind != AirRouteKind.TopFaceLoopChamferPrismatic)
        {
            diagnostics.Add("air-x5-missing-air-provenance-rejected");
            AddBoundaryDiagnostics(diagnostics);
            return Unavailable(mirrorRequest, CirMirrorStatus.MirrorRejectedStaleOrMismatched, losses, diagnostics);
        }

        diagnostics.Add("air-x5-class-b-provenance-preserved");
        diagnostics.Add("air-x5-uniform-chamfer-provenance-preserved");
        diagnostics.Add("air-x5-cir-does-not-claim-chamfer-face-identity");
        diagnostics.Add("air-x5-cir-does-not-claim-brep-plan-role-parity");
        var result = CirPrismaticMirrorBuilder.BuildFromSections(mirrorRequest.SourceNodeId, PrismaticTopFaceLoopChamferPrototype.CreateSectionStack(request), PrismaticCorrespondenceMap.Identity(4));
        diagnostics.AddRange(result.Diagnostics);
        if (result.Succeeded) diagnostics.Add("air-x5-top-face-loop-chamfer-convex-mirror-admitted");
        else diagnostics.Add("air-x5-non-convex-prismatic-mirror-rejected");
        AddBoundaryDiagnostics(diagnostics);
        return FromMirrorResult(mirrorRequest, result, losses, diagnostics);
    }

    public static AirCirMirrorAdapterResult AdmitCanonicalTopFaceLoopChamfer(AirCirMirrorRequest? mirrorRequest = null) =>
        AdmitTopFaceLoopChamfer(new PrismaticTopFaceLoopChamferRequest(10, 8, 6, 1), mirrorRequest);

    private static AirCirMirrorAdapterResult? RejectInvalidSourceOrClaim(AirCirMirrorRequest request, List<string> diagnostics, AirCirMirrorKnownLoss losses)
    {
        if (request.SourceKind == AirCirMirrorSourceKind.ImportedOrRecovered) { diagnostics.Add("air-x5-imported-source-mirror-inference-rejected"); return Unavailable(request, CirMirrorStatus.MirrorUnavailable, losses, diagnostics); }
        if (request.SourceKind == AirCirMirrorSourceKind.BRepOnly) { diagnostics.Add("air-x5-brep-only-source-mirror-unavailable"); return Unavailable(request, CirMirrorStatus.MirrorUnavailable, losses, diagnostics); }
        if (request.SourceNodeKind == AirNodeKind.Unsupported || request.RouteKind == AirRouteKind.Unsupported || request.SourceKind == AirCirMirrorSourceKind.Unsupported) { diagnostics.Add("air-x5-unsupported-air-node-mirror-unavailable"); return Unavailable(request, CirMirrorStatus.MirrorUnavailable, losses, diagnostics); }
        if ((request.RequestedCapabilities & AirCirMirrorCapability.FaceIdentity) != 0) diagnostics.Add("air-x5-face-identity-request-rejected-lossy");
        if ((request.RequestedCapabilities & AirCirMirrorCapability.TopologyParity) != 0) diagnostics.Add("air-x5-topology-parity-request-rejected-lossy");
        if ((request.RequestedCapabilities & AirCirMirrorCapability.ChamferFaceIdentity) != 0) diagnostics.Add("air-x5-chamfer-face-identity-request-rejected-lossy");
        if ((request.RequestedCapabilities & TopologyClaims) != 0) return Unavailable(request, CirMirrorStatus.MirrorRejectedLossyForRequest, losses, diagnostics);
        return null;
    }

    private static AirCirMirrorAdapterResult FromMirrorResult(AirCirMirrorRequest request, CirPrismaticMirrorResult result, AirCirMirrorKnownLoss losses, List<string> diagnostics)
    {
        var capabilities = result.Succeeded ? MapCapabilities(result.Admission.AllowedCapabilities) | AirCirMirrorCapability.Bounds | AirCirMirrorCapability.ThicknessSummary : AirCirMirrorCapability.None;
        var topView = result.Mirror?.CreateTopViewSummary(4, 4);
        var summary = CreateSummary(request, result.Status, result.Succeeded ? Backend : "none", capabilities, losses, diagnostics, result.Recommendation);
        return new(result.Succeeded, summary, result, topView);
    }

    private static AirCirMirrorAdapterResult Unavailable(AirCirMirrorRequest request, CirMirrorStatus status, AirCirMirrorKnownLoss losses, List<string> diagnostics)
    {
        AddBoundaryDiagnostics(diagnostics);
        return new(false, CreateSummary(request, status, "none", AirCirMirrorCapability.None, losses, diagnostics, status.ToStableString()));
    }

    private static AirCirMirrorCapability MapCapabilities(CirMirrorCapability caps)
    {
        var mapped = AirCirMirrorCapability.None;
        if ((caps & CirMirrorCapability.PointContainment) != 0) mapped |= AirCirMirrorCapability.Containment | AirCirMirrorCapability.Occupancy;
        if ((caps & CirMirrorCapability.MapOccupancy) != 0) mapped |= AirCirMirrorCapability.Map | AirCirMirrorCapability.Occupancy;
        if ((caps & CirMirrorCapability.ApproximateVolume) != 0) mapped |= AirCirMirrorCapability.VolumeEstimate;
        if ((caps & CirMirrorCapability.SectionSampling) != 0) mapped |= AirCirMirrorCapability.ThicknessSummary;
        return mapped;
    }

    private static AirCirMirrorAdapterSummary CreateSummary(AirCirMirrorRequest request, CirMirrorStatus status, string backend, AirCirMirrorCapability capabilities, AirCirMirrorKnownLoss losses, IEnumerable<string> diagnostics, string recommendation)
    {
        var stableDiagnostics = Stable(diagnostics).ToArray();
        var provenance = Stable(["generated/native AIR", $"source-node-id:{request.SourceNodeId}", $"route:{request.RouteKind}", $"selection-class:{request.SelectionClass}", $"rule-kind:{request.RuleKind}", $"mirror-builder-route:{BuilderRoute}", $"recommendation:{recommendation}"]).ToArray();
        return new(request.SourceNodeKind, request.RouteKind, status, status.ToStableString(), backend, capabilities, losses, request.SourceKind, request.SourceNodeId, request.SelectionClass, request.RuleKind, BuilderRoute, provenance, stableDiagnostics, BaseGuarantees);
    }

    private static void AddBoundaryDiagnostics(List<string> diagnostics)
    {
        diagnostics.Add("air-x5-cir-evaluation-side-channel-only");
        diagnostics.Add("air-x5-no-topology-authority");
        diagnostics.Add("air-x5-no-face-identity");
        diagnostics.Add("air-x5-no-loop-identity");
        diagnostics.Add("air-x5-no-topology-parity");
        diagnostics.Add("air-x5-no-production-analyzer-change");
        diagnostics.Add("air-x5-no-production-route-replacement");
    }

    private static IEnumerable<string> Stable(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal);
}
