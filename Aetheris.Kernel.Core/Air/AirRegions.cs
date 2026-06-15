namespace Aetheris.Kernel.Core.Air.Regions;

internal enum AirRegionKind { RootRegion, FaceAttachedRegion, Unsupported }
internal enum AirRegionEffectKind { PureConstruction, Additive, Subtractive, Replacement, SelectionOnly, AnnotationOnly, Unsupported }
internal enum AirRegionYieldKind { None, YieldBody, YieldAdditiveBody, YieldSubtractiveVolume, YieldReplacementPatch, YieldProfileBoundary, YieldSectionStack, YieldFaceLoopRewrite, YieldAttachmentInterface, YieldSelection, YieldUnsupported }
internal enum AirRegionBoundaryContractKind { DoesNotEscape, YieldsBody, YieldsCutVolume, YieldsPatch, YieldsLoopRewrite, YieldsAttachmentInterface, YieldsSelection, RejectedOrDeferred }
internal enum AirLocalFrameKind { WorldRoot, FaceAttached, Unsupported }
internal enum AirLocalFrameHandedness { RightHanded, LeftHanded, Unknown }
internal enum AirRegionIntegrationStatus { NotRequired, Deferred, Rejected, Admitted, Unsupported }
internal enum AirRegionCirMirrorStatus { MirrorAdmittedConservative, MirrorUnavailable, MirrorRejectedLossyForRequest }
internal enum AirRegionBRepBoundaryStatus { PlannedContractOnly, Deferred, Rejected, Unsupported }


internal sealed record AirVectorSummary(double X, double Y, double Z);
internal sealed record AirPoint2Summary(double X, double Y);

internal sealed record AirRegionYieldSummary(string YieldId, AirRegionYieldKind YieldKind, string FeatureKind, AirRegionEffectKind EffectKind, string LocalFrameId, string ParentRegionId, AirRegionAttachmentSummary Attachment, AirRegionProfileSummary Profile, AirRegionDirectionSummary Direction, AirRegionAffectedScopeSummary AffectedScope, AirRegionBoundaryIntentSummary BoundaryIntent, AirRegionIntegrationStatus IntegrationStatus, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> KnownLosses, IReadOnlyList<string> Guarantees);
internal sealed record AirRegionAttachmentSummary(string AttachmentKind, string ParentRegionId, string ParentBody, string FaceSelector, string FaceRole, string LocalFrameId, IReadOnlyList<string> AttachmentDiagnostics);
internal sealed record AirRegionProfileSummary(string ProfileKind, AirPoint2Summary Center, double Radius, string? LoopKind, string? ProfileFrame);
internal sealed record AirRegionDirectionSummary(string DirectionKind, string Axis, string Sense, bool IsThrough, string Depth, IReadOnlyList<string> Diagnostics);
internal sealed record AirRegionAffectedScopeSummary(string ScopeKind, bool ParentBodyOnly, string AffectedFaceSelector, bool MayAffectSiblings, bool EscapesOnlyThroughYield, IReadOnlyList<string> Diagnostics);
internal sealed record AirRegionBoundaryIntentSummary(string BoundaryKind, string EntryBoundary, string ExitBoundary, string RimIntent, string PatchIntent, IReadOnlyList<string> Diagnostics);
internal sealed record AirRegionCirMirrorSummary(string SourceRegionId, AirRegionKind SourceRegionKind, string YieldId, string YieldFeatureKind, string Status, string Backend, AirRegionEffectKind Effect, string ParentField, string SubtractField, IReadOnlyList<string> Capabilities, IReadOnlyList<string> KnownLosses, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> Guarantees);
internal sealed record AirRegionBRepBoundarySummary(string SourceRegionId, AirRegionKind SourceRegionKind, string YieldId, string FeatureKind, AirRegionBRepBoundaryStatus Status, AirRegionAffectedParentSummary AffectedParent, AirRegionEntryBoundarySummary EntryBoundary, AirRegionExitBoundarySummary ExitBoundary, AirRegionCutWallIntentSummary CutWallIntent, string PatchIntent, IReadOnlyList<string> PlannedRoles, IReadOnlyList<string> DeferredElements, AirRegionIntegrationStatus IntegrationStatus, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> KnownLosses, IReadOnlyList<string> Guarantees);
internal sealed record AirRegionAffectedParentSummary(string ParentRegionId, string ParentBody, string AffectedFaceSelector, string AffectedFaceRole, string AffectedScope, string Locality);
internal sealed record AirRegionEntryBoundarySummary(string BoundaryKind, string ProfileKind, string ProfileSource, string LocalFrameId, string LoopIntent, string Role);
internal sealed record AirRegionExitBoundarySummary(string BoundaryKind, string ExitKind, string Role, string Status, IReadOnlyList<string> Diagnostics);
internal sealed record AirRegionCutWallIntentSummary(string WallKind, string SourceProfile, string Direction, string Role, string Status);

internal sealed record AirRegionCirMirrorRequest(AirRegionSummary Region, AirRegionYieldSummary Yield, IReadOnlyList<string> RequestedCapabilities);
internal sealed record AirRegionCirMirrorResult(bool Succeeded, AirRegionCirMirrorSummary Summary);

internal static class AirSideHoleRegionCirMirrorAdapter
{
    private static readonly string[] AllowedCapabilities = ["occupancy", "containment", "bounds"];
    private static readonly string[] KnownLosses = ["no-topology-authority", "no-face-identity", "no-entry-loop-identity", "no-exit-loop-identity", "no-boundary-patch-identity", "no-brep-plan-role-parity", "no-step-export-authority", "no-production-integration"];
    private static readonly string[] Guarantees = ["analysis mirror only", "integration deferred", "no Boolean", "no BRep emission", "no STEP smoke", "summary-only; CIR evaluator composition deferred"];

    public static AirRegionCirMirrorResult Admit(AirRegionSummary region, IReadOnlyList<string>? requestedCapabilities = null)
    {
        if (region.Yield is null) throw new ArgumentException("Region must carry a yield summary.", nameof(region));
        var requested = requestedCapabilities ?? AllowedCapabilities;
        var diagnostics = new List<string>
        {
            "air-region-x3-cir-mirror-request-created",
            "air-region-x3-side-hole-cir-mirror-created",
            "air-region-x3-parent-box-field-recorded",
            "air-region-x3-subtract-cylinder-field-recorded",
            "air-region-x3-region-effect-mirrored",
            "air-region-x3-cir-analysis-side-channel-only",
            "air-region-x3-no-topology-authority",
            "air-region-x3-no-face-identity",
            "air-region-x3-no-entry-loop-identity",
            "air-region-x3-no-exit-loop-identity",
            "air-region-x3-no-boundary-patch-identity",
            "air-region-x3-no-brep-plan-role-parity",
            "air-region-x3-no-step-export-authority",
            "air-region-x3-parent-integration-deferred",
            "air-region-x3-no-boolean",
            "air-region-x3-no-brep-emission",
            "air-region-x3-no-step-smoke",
            "air-region-x3-cir-composition-deferred",
            "air-region-x3-cir-mirror-summary-only"
        };
        if (requested.Contains("face-identity", StringComparer.Ordinal)) diagnostics.Add("air-region-x3-face-identity-request-rejected-lossy");
        if (requested.Contains("topology-parity", StringComparer.Ordinal)) diagnostics.Add("air-region-x3-topology-parity-request-rejected-lossy");
        if (requested.Contains("entry-loop-identity", StringComparer.Ordinal)) diagnostics.Add("air-region-x3-entry-loop-identity-request-rejected-lossy");
        if (requested.Contains("boundary-patch-identity", StringComparer.Ordinal)) diagnostics.Add("air-region-x3-boundary-patch-identity-request-rejected-lossy");
        var forbidden = requested.Any(x => x is "face-identity" or "topology-parity" or "entry-loop-identity" or "boundary-patch-identity");
        var status = forbidden ? "mirror-rejected-lossy-for-request" : "mirror-admitted-conservative";
        var caps = forbidden ? Array.Empty<string>() : AllowedCapabilities;
        return new(!forbidden, new(region.RegionId, region.RegionKind, region.Yield.YieldId, region.Yield.FeatureKind, status, "cir-region-parent-minus-cylinder", region.EffectKind, "Box", "Cylinder", Stable(caps).ToArray(), KnownLosses, Stable(diagnostics).ToArray(), Guarantees));
    }

    private static IEnumerable<string> Stable(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal);
}

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
    IReadOnlyList<string> Guarantees,
    AirRegionYieldSummary? Yield = null,
    AirRegionCirMirrorSummary? CirMirror = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    AirRegionBRepBoundarySummary? BrepBoundary = null);

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
        AirRegionSummary sideBase = new(
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
            ["air-region-x1-face-attached-region-created", "air-region-x1-region-effect-declared", "air-region-x1-region-yield-declared", "air-region-x1-boundary-contract-declared", "air-region-x1-parent-integration-deferred", "air-region-x2-yield-contract-created", "air-region-x2-side-hole-yield-created", "air-region-x2-face-attachment-recorded", "air-region-x2-local-profile-recorded", "air-region-x2-circle-profile-recorded", "air-region-x2-through-direction-recorded", "air-region-x2-affected-scope-recorded", "air-region-x2-boundary-intent-recorded", "air-region-x2-entry-loop-intent-recorded", "air-region-x2-exit-boundary-deferred", "air-region-x2-region-locality-enforced", "air-region-x2-explicit-yield-only", "air-region-x2-parent-integration-deferred", "air-region-x2-no-implicit-parent-mutation", "air-region-x2-no-boolean", "air-region-x2-no-brep-emission", "air-region-x2-no-step-smoke", "air-region-x2-no-cir-mirror", "air-region-x2-trace-only"],
            ["side-hole geometry integration not implemented", "concrete through depth deferred", "opposite-side exit boundary deferred"],
            ["escapes only through explicit yield", "no implicit parent mutation", "no Boolean", "no BRep emission", "no production route replacement", "trace-only"],
            SideHoleYield(root.RegionId));
        var mirror = AirSideHoleRegionCirMirrorAdapter.Admit(sideBase);
        var boundary = SideHoleBRepBoundary(sideBase);
        var side = sideBase with
        {
            StageReached = "region-brep-boundary",
            Provenance = "AIR-REGION-X4 metadata-driven fixture",
            Diagnostics = Stable([.. sideBase.Diagnostics.Where(d => d != "air-region-x2-no-cir-mirror"), .. mirror.Summary.Diagnostics, .. boundary.Diagnostics]).ToArray(),
            KnownLosses = Stable([.. sideBase.KnownLosses, .. mirror.Summary.KnownLosses, .. boundary.KnownLosses]).ToArray(),
            Guarantees = Stable([.. sideBase.Guarantees, .. mirror.Summary.Guarantees, .. boundary.Guarantees]).ToArray(),
            CirMirror = mirror.Summary,
            BrepBoundary = boundary
        };
        return Summary([root, side], ["air-region-x1-region-trace-created", "air-region-x1-no-implicit-parent-mutation", "air-region-x1-no-boolean", "air-region-x1-no-brep-emission", "air-region-x1-no-production-route-replacement", "air-region-x1-trace-only", "air-region-x2-yield-contract-created", "air-region-x2-side-hole-yield-created", "air-region-x2-region-locality-enforced", "air-region-x2-explicit-yield-only", "air-region-x2-parent-integration-deferred", "air-region-x2-no-boolean", "air-region-x2-no-brep-emission", "air-region-x2-no-step-smoke", "air-region-x2-trace-only", .. side.CirMirror!.Diagnostics, .. side.BrepBoundary!.Diagnostics], ["escapes only through explicit yield", "no Boolean", "no geometry", "no BRep emission", "no STEP smoke", "no production route replacement", .. side.CirMirror.Guarantees, .. side.BrepBoundary.Guarantees]);
    }

    public static AirRegionTraceSummary ForImplicitParentMutationRejected()
    {
        var root = RootRegion("region-created");
        var bad = new AirRegionSummary("region:implicit-parent-mutation", AirRegionKind.FaceAttachedRegion, root.RegionId, AirRegionEffectKind.Subtractive, AirRegionYieldKind.None, AirRegionBoundaryContractKind.RejectedOrDeferred, new AirLocalFrameSummary("frame:implicit-parent-mutation", AirLocalFrameKind.FaceAttached, new(5, 0, 0), new(0, 1, 0), new(0, 0, 1), new(1, 0, 0), AirLocalFrameHandedness.RightHanded, "box.face(\"+X\")", "+X", "+Z", true, ["air-region-x1-face-attached-frame-created"]), AirRegionIntegrationStatus.Rejected, "rejected:implicit-parent-mutation", "region-rejected", "AIR-REGION-X1 metadata-driven fixture", ["air-region-x1-implicit-parent-mutation-rejected", "air-region-x2-implicit-parent-mutation-rejected", "air-region-x2-missing-explicit-yield-rejected", "air-region-x2-boundary-contract-required"], ["implicit parent mutation rejected", "missing explicit yield", "boundary contract required"], ["no geometry", "no BRep emission", "no Boolean"]);
        return Summary([root, bad], ["air-region-x1-region-trace-created", "air-region-x1-implicit-parent-mutation-rejected", "air-region-x1-no-brep-emission", "air-region-x1-no-boolean", "air-region-x2-implicit-parent-mutation-rejected", "air-region-x2-missing-explicit-yield-rejected", "air-region-x2-boundary-contract-required"], ["no Boolean", "no geometry", "no BRep emission"]);
    }

    private static AirRegionYieldSummary SideHoleYield(string parentRegionId) => new(
        "yield:side-hole:+x:subtractive-volume", AirRegionYieldKind.YieldSubtractiveVolume, "SideHole", AirRegionEffectKind.Subtractive, "frame:side-hole:+x", parentRegionId,
        new("Face", parentRegionId, "parser-backed/root box fixture", "+X", "SideFace", "frame:side-hole:+x", ["air-region-x2-face-attachment-recorded"]),
        new("Circle", new(0, 0), 1, "CircularEntryLoop", "frame:side-hole:+x/profile"),
        new("FaceNormal", "LocalZ", "Inward", true, "Through", ["air-region-x2-through-direction-recorded"]),
        new("ParentBodyLocalFeature", true, "+X", false, true, ["air-region-x2-affected-scope-recorded", "air-region-x2-explicit-yield-only"]),
        new("ThroughCut", "CircularEntryLoop", "Deferred", "CircularRimIntent", "Deferred", ["air-region-x2-boundary-intent-recorded", "air-region-x2-entry-loop-intent-recorded", "air-region-x2-exit-boundary-deferred"]),
        AirRegionIntegrationStatus.Deferred,
        ["air-region-x2-yield-contract-created", "air-region-x2-side-hole-yield-created", "air-region-x2-region-locality-enforced"],
        ["concrete through depth deferred", "opposite-side exit boundary deferred", "side-hole geometry integration not implemented"],
        ["escapes only through explicit yield", "parent body local feature scope", "no implicit parent mutation", "no Boolean", "no BRep emission"]);

    private static AirRegionBRepBoundarySummary SideHoleBRepBoundary(AirRegionSummary region)
    {
        if (region.Yield is null) throw new ArgumentException("Side-hole region must carry a yield summary.", nameof(region));
        var y = region.Yield;
        return new(
            region.RegionId,
            region.RegionKind,
            y.YieldId,
            y.FeatureKind,
            AirRegionBRepBoundaryStatus.PlannedContractOnly,
            new(y.ParentRegionId, "root box", y.Attachment.FaceSelector, y.Attachment.FaceRole, y.AffectedScope.ScopeKind, "parent body local; no sibling effects"),
            new("CircularEntry", y.Profile.ProfileKind, "side-hole yield profile", y.LocalFrameId, "CircularEntryLoop", "EntryLoopIntent"),
            new("ThroughExit", "OppositeSideExit", "ExitLoopIntent", "Deferred", ["air-region-x4-exit-boundary-deferred"]),
            new("CylindricalCutWallIntent", y.Profile.ProfileKind, "through/inward", "CutWallIntent", "Deferred"),
            "Deferred",
            ["AffectedParentFace", "CutBoundaryPatch", "CutEntryLoop", "CutExitLoop", "CutWallFace", "DeferredIntegration", "RegionIntegrationPatch", "SideHoleFeature"],
            ["entry-loop-identity", "exit-loop-identity", "cut-wall-face-identity", "boundary-patch-identity", "parent-topology-mutation", "brep-plan-element-materialization", "boolean-invocation", "brep-emission", "step-smoke"],
            AirRegionIntegrationStatus.Deferred,
            Stable(["air-region-x4-brep-boundary-request-created", "air-region-x4-side-hole-brep-boundary-created", "air-region-x4-affected-parent-recorded", "air-region-x4-affected-face-recorded", "air-region-x4-entry-boundary-intent-recorded", "air-region-x4-exit-boundary-deferred", "air-region-x4-cut-wall-intent-recorded", "air-region-x4-planned-boundary-roles-recorded", "air-region-x4-region-topology-contract-created", "air-region-x4-no-brep-plan-elements-materialized", "air-region-x4-no-parent-topology-mutation", "air-region-x4-parent-integration-deferred", "air-region-x4-no-boolean", "air-region-x4-no-brep-emission", "air-region-x4-no-step-smoke", "air-region-x4-no-cir-authority-for-topology", "air-region-x4-entry-loop-identity-not-materialized", "air-region-x4-exit-loop-identity-not-materialized", "air-region-x4-cut-wall-face-identity-not-materialized", "air-region-x4-boundary-patch-identity-not-materialized"]).ToArray(),
            ["no-emitted-entry-loop-identity", "no-emitted-exit-loop-identity", "no-emitted-cut-wall-face-identity", "no-emitted-boundary-patch-identity", "no-parent-topology-mutation", "no-brep-plan-element-materialization", "integration-deferred", "boolean-not-invoked"],
            ["no parent topology mutation", "no BRepPlan elements materialized", "no Boolean", "no BRep emission", "no STEP smoke", "CIR topology authority remains denied"]);
    }

    private static AirRegionSummary RootRegion(string stageReached) => new("region:root", AirRegionKind.RootRegion, null, AirRegionEffectKind.PureConstruction, AirRegionYieldKind.YieldBody, AirRegionBoundaryContractKind.YieldsBody, new AirLocalFrameSummary("frame:world-root", AirLocalFrameKind.WorldRoot, new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1), AirLocalFrameHandedness.RightHanded, null, null, null, true, ["air-region-x1-world-root-frame-created"]), AirRegionIntegrationStatus.NotRequired, "root-body", stageReached, "AIR-REGION-X1", ["air-region-x1-root-region-created"], [], ["root region is a trace-only summary"]);
    private static AirRegionTraceSummary Summary(IReadOnlyList<AirRegionSummary> regions, IReadOnlyList<string> diagnostics, IReadOnlyList<string> guarantees) => new(regions, "region:root", regions.Count, regions.Any(r => r.ParentRegionId is not null), diagnostics.Order(StringComparer.Ordinal).ToArray(), guarantees.Order(StringComparer.Ordinal).ToArray());
    private static IEnumerable<string> Stable(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal);
}
