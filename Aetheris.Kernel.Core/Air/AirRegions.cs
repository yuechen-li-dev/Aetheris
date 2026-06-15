namespace Aetheris.Kernel.Core.Air.Regions;

internal enum AirRegionKind { RootRegion, FaceAttachedRegion, Unsupported }
internal enum AirRegionEffectKind { PureConstruction, Additive, Subtractive, Replacement, SelectionOnly, AnnotationOnly, Unsupported }
internal enum AirRegionYieldKind { None, YieldBody, YieldAdditiveBody, YieldSubtractiveVolume, YieldReplacementPatch, YieldProfileBoundary, YieldSectionStack, YieldFaceLoopRewrite, YieldAttachmentInterface, YieldSelection, YieldUnsupported }
internal enum AirRegionBoundaryContractKind { DoesNotEscape, YieldsBody, YieldsCutVolume, YieldsPatch, YieldsLoopRewrite, YieldsAttachmentInterface, YieldsSelection, RejectedOrDeferred }
internal enum AirLocalFrameKind { WorldRoot, FaceAttached, Unsupported }
internal enum AirLocalFrameHandedness { RightHanded, LeftHanded, Unknown }
internal enum AirRegionIntegrationStatus { NotRequired, Deferred, Rejected, Admitted, Unsupported }

internal sealed record AirVectorSummary(double X, double Y, double Z);

internal sealed record AirLocalFrameSummary(
    string FrameId,
    AirLocalFrameKind FrameKind,
    AirVectorSummary Origin,
    AirVectorSummary XAxis,
    AirVectorSummary YAxis,
    AirVectorSummary ZAxis,
    AirLocalFrameHandedness Handedness,
    string? AttachmentSource,
    string? SourceFace,
    string? ReferenceDirection,
    bool IsValid,
    IReadOnlyList<string> Diagnostics);

internal sealed record AirRegionSummary(
    string RegionId,
    AirRegionKind RegionKind,
    string? ParentRegionId,
    AirRegionEffectKind EffectKind,
    AirRegionYieldKind YieldKind,
    AirRegionBoundaryContractKind BoundaryContractKind,
    AirLocalFrameSummary LocalFrame,
    AirRegionIntegrationStatus IntegrationStatus,
    string IntegrationRoute,
    string StageReached,
    string Provenance,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> KnownLosses,
    IReadOnlyList<string> Guarantees);

internal sealed record AirRegionTraceSummary(
    IReadOnlyList<AirRegionSummary> Regions,
    string RootRegionId,
    int RegionCount,
    bool HasNestedRegions,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Guarantees);

internal static class AirRegionTraceFactory
{
    public static AirRegionTraceSummary ForRootBody(string stageReached = "emitted-brep")
    {
        var root = RootRegion(stageReached);
        return Summary([root], ["air-region-x1-region-trace-created", "air-region-x1-root-region-created", "air-region-x1-world-root-frame-created"], ["AIR Region trace-only summary", "root region does not change BRepPlan semantics"]);
    }

    public static AirRegionTraceSummary ForFaceAttachedSideHoleDeferred()
    {
        var root = RootRegion("region-created");
        var side = new AirRegionSummary(
            "region:side-hole:+x",
            AirRegionKind.FaceAttachedRegion,
            root.RegionId,
            AirRegionEffectKind.Subtractive,
            AirRegionYieldKind.YieldSubtractiveVolume,
            AirRegionBoundaryContractKind.YieldsCutVolume,
            new AirLocalFrameSummary("frame:side-hole:+x", AirLocalFrameKind.FaceAttached, new(5, 0, 0), new(0, 1, 0), new(0, 0, 1), new(1, 0, 0), AirLocalFrameHandedness.RightHanded, "box.face(\"+X\")", "+X", "+Z", true, ["air-region-x1-face-attached-frame-created"]),
            AirRegionIntegrationStatus.Deferred,
            "deferred:no-geometry-integration",
            "region-integration-deferred",
            "AIR-REGION-X1 metadata-driven fixture",
            ["air-region-x1-face-attached-region-created", "air-region-x1-region-effect-declared", "air-region-x1-region-yield-declared", "air-region-x1-boundary-contract-declared", "air-region-x1-parent-integration-deferred"],
            ["side-hole geometry integration not implemented"],
            ["no implicit parent mutation", "no Boolean", "no BRep emission", "no production route replacement", "trace-only"]);
        return Summary([root, side], ["air-region-x1-region-trace-created", "air-region-x1-no-implicit-parent-mutation", "air-region-x1-no-boolean", "air-region-x1-no-brep-emission", "air-region-x1-no-production-route-replacement", "air-region-x1-trace-only"], ["no Boolean", "no geometry", "no BRep emission", "no STEP smoke", "no production route replacement"]);
    }

    public static AirRegionTraceSummary ForImplicitParentMutationRejected()
    {
        var root = RootRegion("region-created");
        var bad = new AirRegionSummary("region:implicit-parent-mutation", AirRegionKind.FaceAttachedRegion, root.RegionId, AirRegionEffectKind.Subtractive, AirRegionYieldKind.None, AirRegionBoundaryContractKind.RejectedOrDeferred, new AirLocalFrameSummary("frame:implicit-parent-mutation", AirLocalFrameKind.FaceAttached, new(5, 0, 0), new(0, 1, 0), new(0, 0, 1), new(1, 0, 0), AirLocalFrameHandedness.RightHanded, "box.face(\"+X\")", "+X", "+Z", true, ["air-region-x1-face-attached-frame-created"]), AirRegionIntegrationStatus.Rejected, "rejected:implicit-parent-mutation", "region-rejected", "AIR-REGION-X1 metadata-driven fixture", ["air-region-x1-implicit-parent-mutation-rejected"], ["implicit parent mutation rejected"], ["no geometry", "no BRep emission", "no Boolean"]);
        return Summary([root, bad], ["air-region-x1-region-trace-created", "air-region-x1-implicit-parent-mutation-rejected", "air-region-x1-no-brep-emission", "air-region-x1-no-boolean"], ["no Boolean", "no geometry", "no BRep emission"]);
    }

    private static AirRegionSummary RootRegion(string stageReached) => new("region:root", AirRegionKind.RootRegion, null, AirRegionEffectKind.PureConstruction, AirRegionYieldKind.YieldBody, AirRegionBoundaryContractKind.YieldsBody, new AirLocalFrameSummary("frame:world-root", AirLocalFrameKind.WorldRoot, new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1), AirLocalFrameHandedness.RightHanded, null, null, null, true, ["air-region-x1-world-root-frame-created"]), AirRegionIntegrationStatus.NotRequired, "root-body", stageReached, "AIR-REGION-X1", ["air-region-x1-root-region-created"], [], ["root region is a trace-only summary"]);
    private static AirRegionTraceSummary Summary(IReadOnlyList<AirRegionSummary> regions, IReadOnlyList<string> diagnostics, IReadOnlyList<string> guarantees) => new(regions, "region:root", regions.Count, regions.Any(r => r.ParentRegionId is not null), diagnostics.Order(StringComparer.Ordinal).ToArray(), guarantees.Order(StringComparer.Ordinal).ToArray());
}
