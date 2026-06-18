namespace Aetheris.Kernel.Core.Air.Regions;

internal enum AirRegionKind { RootRegion, FaceAttachedRegion, Unsupported }
internal enum AirRegionEffectKind { PureConstruction, Additive, Subtractive, Replacement, SelectionOnly, AnnotationOnly, Unsupported }
internal enum AirRegionYieldKind { None, YieldBody, YieldAdditiveBody, YieldSubtractiveVolume, YieldReplacementPatch, YieldProfileBoundary, YieldSectionStack, YieldFaceLoopRewrite, YieldAttachmentInterface, YieldSelection, YieldUnsupported }
internal enum AirRegionBoundaryContractKind { DoesNotEscape, YieldsBody, YieldsCutVolume, YieldsPatch, YieldsLoopRewrite, YieldsAttachmentInterface, YieldsSelection, RejectedOrDeferred }
internal enum AirLocalFrameKind { WorldRoot, FaceAttached, Unsupported }
internal enum AirLocalFrameHandedness { RightHanded, LeftHanded, Unknown }
internal enum AirRegionIntegrationStatus { NotRequired, Integrated, PartiallyIntegrated, Blocked, Deferred, Rejected, Admitted, Unsupported }
internal enum AirRegionCirMirrorStatus { MirrorAdmittedConservative, MirrorUnavailable, MirrorRejectedLossyForRequest }
internal enum AirRegionBRepBoundaryStatus { PlannedContractOnly, Deferred, Rejected, Unsupported }
internal enum AirRegionIntegrationRouteKind { FaceAttachedConstructiveInsertion, LocalBRepPlanPatch, ControlledSideHolePatchMaterialization, ControlledSideHoleParentBRepIntegration, ControlledSideHoleDirectBRepPrototype, ControlledSideHoleBoundedBackend, ControlledSideHoleBooleanBackend, BRepBooleanFallback, CirAnalysisMirrorOnly, DeferredIntegration, Unsupported }
internal enum AirRegionIntegrationCandidateStatus { Admitted, AvailableForAnalysis, Deferred, Rejected, Unavailable, NotApplicable, Selected }
internal enum AirRegionBRepPlaceholderStatus { PlaceholderOnly, NotMaterialized, Deferred, Rejected, Unsupported }
internal enum AirRegionMaterializationStatus { Materialized, PartiallyMaterialized, Deferred, Rejected, Unsupported }
internal enum AirRegionIntegrationBlockerCategory { MissingBRepApi, FaceSplitting, LoopInsertion, CylindricalWallBinding, ShellClosure, STEPExport, UnsafeBooleanBackend, FrameMismatch, TopologyValidation, Unknown }



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
internal sealed record AirRegionIntegrationCandidate(AirRegionIntegrationRouteKind RouteKind, AirRegionIntegrationCandidateStatus Status, string ReasonCode, string Reason, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> KnownLosses, IReadOnlyList<string> Guarantees);
internal sealed record AirRegionIntegrationDecisionSummary(string SourceRegionId, AirRegionKind SourceRegionKind, string YieldFeatureKind, AirRegionEffectKind EffectKind, AirRegionBoundaryContractKind BoundaryContractKind, string SelectionMode, AirRegionIntegrationRouteKind SelectedRouteKind, AirRegionIntegrationStatus SelectedStatus, IReadOnlyList<AirRegionIntegrationCandidate> Candidates, string Recommendation, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> KnownLosses, IReadOnlyList<string> Guarantees);
internal sealed record AirRegionBRepPlaceholderElement(string Id, string Kind, string Role, IReadOnlyList<string> SemanticRoles, string SourceRegionId, string SourceYieldId, string ParentReference, string MaterializationStatus, IReadOnlyList<string> Diagnostics);
internal sealed record AirRegionBRepPlaceholderSummary(int PlaceholderElementCount, int AffectedParentFaceReferenceCount, int EntryLoopPlaceholderCount, int ExitLoopPlaceholderCount, int CutWallFacePlaceholderCount, int IntegrationPatchPlaceholderCount, int MaterializedElementCount, int NotMaterializedElementCount, string RegionId, string FeatureKind, AirRegionIntegrationStatus IntegrationStatus, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> Guarantees);
internal sealed record AirRegionBRepPlaceholderValidationResult(bool Succeeded, IReadOnlyList<string> RequiredRoles, IReadOnlyList<string> MissingRoles, IReadOnlyList<string> Diagnostics);
internal sealed record AirRegionBRepPlaceholderPlan(string PlanId, string SourceRegionId, AirRegionKind SourceRegionKind, string YieldId, string FeatureKind, AirRegionBRepPlaceholderStatus PlaceholderStatus, IReadOnlyList<AirRegionBRepPlaceholderElement> Elements, AirRegionBRepPlaceholderSummary Summary, AirRegionBRepPlaceholderValidationResult Validation, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> KnownLosses, IReadOnlyList<string> Guarantees);
internal sealed record AirRegionBRepPlaceholderMaterialization(string PlaceholderId, string PlaceholderRole, AirRegionMaterializationStatus MaterializationStatus, string MaterializedElementKind, string MaterializedRole, string? MaterializedId, IReadOnlyList<string> Diagnostics);
internal sealed record AirRegionMaterializationTopologySummary(bool BodyExists, bool? Closed, int FaceCount, int LoopCount, int CylindricalFaceCount, string Bounds, IReadOnlyList<string> EvidenceRoles);
internal sealed record AirRegionMaterializationStepSmokeSummary(bool WasChecked, bool Succeeded, IReadOnlyList<string> Diagnostics);
internal sealed record AirRegionSideHoleMaterializationSummary(string SourceRegionId, string FeatureKind, AirRegionMaterializationStatus Status, string Route, IReadOnlyList<AirRegionBRepPlaceholderMaterialization> PlaceholderMappings, AirRegionMaterializationTopologySummary TopologySummary, AirRegionMaterializationStepSmokeSummary StepSmoke, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> KnownLosses, IReadOnlyList<string> Guarantees);
internal sealed record AirRegionIntegrationBlocker(AirRegionIntegrationBlockerCategory Category, string Code, string Message, string FailedStage, IReadOnlyList<string> RelevantPlaceholderIds, IReadOnlyList<string> CurrentEvidence, string SuggestedNextFix, IReadOnlyList<string> Diagnostics);
internal sealed record AirRegionParentIntegrationTopologySummary(bool ParentBodyExists, bool? Closed, int? FaceCount, int? LoopCount, int? CylindricalFaceCount, string Bounds, IReadOnlyList<string> EvidenceRoles);
internal sealed record AirRegionParentIntegrationStepSmokeSummary(bool WasChecked, bool Succeeded, string Status, IReadOnlyList<string> Diagnostics);
internal sealed record AirRegionParentIntegrationSummary(string SourceRegionId, string FeatureKind, AirRegionIntegrationStatus Status, AirRegionIntegrationRouteKind Route, IReadOnlyList<AirRegionBRepPlaceholderMaterialization> PlaceholderMappings, AirRegionParentIntegrationTopologySummary TopologySummary, AirRegionParentIntegrationStepSmokeSummary StepSmoke, AirRegionIntegrationBlocker? Blocker, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> KnownLosses, IReadOnlyList<string> Guarantees);
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


internal static class AirSideHoleRegionIntegrationSelector
{
    public static AirRegionIntegrationDecisionSummary Decide(AirRegionSummary region, AirRegionCirMirrorSummary mirror, AirRegionBRepBoundarySummary boundary)
    {
        if (region.Yield is null) throw new ArgumentException("Side-hole region must carry a yield summary.", nameof(region));
        var candidates = new AirRegionIntegrationCandidate[]
        {
            new(AirRegionIntegrationRouteKind.FaceAttachedConstructiveInsertion, AirRegionIntegrationCandidateStatus.Deferred, "side-hole-constructive-insertion-not-implemented", "no side-hole constructive insertion route yet", ["air-region-x5-face-attached-constructive-insertion-deferred", "air-region-x5-constructive-insertion-not-implemented"], ["constructive-insertion-not-implemented"], ["no parent topology mutation"]),
            new(AirRegionIntegrationRouteKind.LocalBRepPlanPatch, AirRegionIntegrationCandidateStatus.Deferred, "local-brep-plan-patch-not-implemented", "BRepPlan boundary contract exists; patch materialization not implemented", ["air-region-x5-local-brep-plan-patch-deferred", "air-region-x5-brep-plan-patch-not-implemented"], ["brep-plan-patch-not-implemented", "no-brep-plan-materialization"], ["BRepPlan boundary remains topology-side intent only"]),
            new(AirRegionIntegrationRouteKind.BRepBooleanFallback, AirRegionIntegrationCandidateStatus.Rejected, "boolean-fallback-not-admitted", "Boolean fallback not admitted for region integration in this milestone", ["air-region-x5-boolean-fallback-rejected-not-admitted", "air-region-x5-boolean-fallback-not-admitted"], ["boolean-fallback-not-admitted"], ["no Boolean"]),
            new(AirRegionIntegrationRouteKind.CirAnalysisMirrorOnly, AirRegionIntegrationCandidateStatus.AvailableForAnalysis, "cir-mirror-analysis-only", "CIR mirror exists from AIR-REGION-X3; cannot integrate topology", ["air-region-x5-cir-analysis-mirror-available-not-integration"], ["no-topology-authority"], ["CIR mirror remains analysis-only"]),
            new(AirRegionIntegrationRouteKind.DeferredIntegration, AirRegionIntegrationCandidateStatus.Selected, "no-topology-integration-route-admitted", "no topology integration route admitted; region remains deferred", ["air-region-x5-deferred-integration-selected", "air-region-x5-no-topology-integration-route-admitted"], ["integration-deferred"], ["parent integration deferred"])
        };
        var diagnostics = Stable(["air-region-x5-integration-decision-created", "air-region-x5-side-hole-integration-candidates-created", "air-region-x5-no-judgment-utility-required", "air-region-x5-parent-integration-deferred", "air-region-x5-no-boolean", "air-region-x5-no-brep-plan-materialization", "air-region-x5-no-brep-emission", "air-region-x5-no-step-smoke", "air-region-x5-no-parent-topology-mutation", .. candidates.SelectMany(c => c.Diagnostics)]).ToArray();
        var knownLosses = Stable(["no-topology-integration-route-admitted", "side-hole-constructive-insertion-not-implemented", "local-brep-plan-patch-not-implemented", "boolean-fallback-not-admitted", .. candidates.SelectMany(c => c.KnownLosses)]).ToArray();
        var guarantees = Stable(["selected integration result is Deferred", "no topology integration", "Boolean fallback rejected", "CIR mirror analysis-only", "BRepPlan boundary contract does not materialize topology", "no Boolean", "no BRepPlan materialization", "no BRep emission", "no STEP smoke", "no parent topology mutation", .. candidates.SelectMany(c => c.Guarantees)]).ToArray();
        return new(region.RegionId, region.RegionKind, region.Yield.FeatureKind, region.EffectKind, region.BoundaryContractKind, "SwitchMatch", AirRegionIntegrationRouteKind.DeferredIntegration, AirRegionIntegrationStatus.Deferred, candidates, "side-hole integration deferred; future BRepPlan patch or constructive insertion needed", diagnostics, knownLosses, guarantees);
    }

    private static IEnumerable<string> Stable(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal);
}


internal static class AirSideHoleBRepPlaceholderPlanner
{
    public static AirRegionBRepPlaceholderPlan Plan(AirRegionSummary region, AirRegionBRepBoundarySummary boundary, AirRegionIntegrationDecisionSummary decision)
    {
        if (region.Yield is null) throw new ArgumentException("Side-hole region must carry a yield summary.", nameof(region));
        var y = region.Yield;
        var elements = new[]
        {
            Element(region.RegionId + ":parent-face:" + y.Attachment.FaceSelector.ToLowerInvariant(), "AffectedParentFaceReference", "AffectedParentFace", ["SideHoleFeature", "DeferredIntegration", "NotMaterialized"], region, y, "parent-face:+X", "ReferenceOnly", "air-region-x6-affected-parent-face-placeholder-created"),
            Element(region.RegionId + ":entry-loop", "EntryLoopPlaceholder", "CutEntryLoop", ["SideHoleFeature", "DeferredIntegration", "NotMaterialized"], region, y, "profile:Circle(radius=1);frame:side-hole:+x", "NotMaterialized", "air-region-x6-entry-loop-placeholder-created"),
            Element(region.RegionId + ":exit-loop", "ExitLoopPlaceholder", "CutExitLoop", ["SideHoleFeature", "DeferredIntegration", "NotMaterialized"], region, y, "opposite-side-exit:deferred", "NotMaterialized", "air-region-x6-exit-loop-placeholder-created"),
            Element(region.RegionId + ":cut-wall", "CutWallFacePlaceholder", "CutWallFace", ["SideHoleFeature", "DeferredIntegration", "NotMaterialized"], region, y, "cylindrical-cut-wall:intent", "NotMaterialized", "air-region-x6-cut-wall-placeholder-created"),
            Element(region.RegionId + ":integration-patch", "IntegrationPatchPlaceholder", "RegionIntegrationPatch", ["SideHoleFeature", "DeferredIntegration", "NotMaterialized"], region, y, "parent-body-local-patch:deferred", "NotMaterialized", "air-region-x6-integration-patch-placeholder-created")
        };
        var required = new[] { "AffectedParentFace", "CutEntryLoop", "CutExitLoop", "CutWallFace", "RegionIntegrationPatch" };
        var roles = elements.Select(e => e.Role).ToArray();
        var missing = required.Except(roles, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var diagnostics = Stable(["air-region-x6-brep-placeholder-plan-created", "air-region-x6-side-hole-brep-placeholders-created", "air-region-x6-stable-placeholder-ids-created", "air-region-x6-placeholder-validation-succeeded", "air-region-x6-no-materialized-elements", "air-region-x6-parent-integration-still-deferred", "air-region-x6-no-parent-topology-mutation", "air-region-x6-no-brep-emission", "air-region-x6-no-step-smoke", "air-region-x6-no-boolean", "air-region-x6-x7-materialization-prep", .. elements.SelectMany(e => e.Diagnostics)]).ToArray();
        var guarantees = new[] { "no parent topology mutation", "no BRepPlan materialization", "no BRep emission", "no STEP smoke", "no Boolean", "no production route replacement", "integration still deferred", "prepared for future X7 materialization" };
        var losses = new[] { "air-region-x6-entry-loop-placeholder-not-materialized", "air-region-x6-exit-loop-placeholder-not-materialized", "air-region-x6-cut-wall-placeholder-not-materialized", "air-region-x6-integration-patch-not-materialized" };
        var summary = new AirRegionBRepPlaceholderSummary(elements.Length, 1, 1, 1, 1, 1, elements.Count(e => e.MaterializationStatus != "NotMaterialized" && e.MaterializationStatus != "ReferenceOnly"), elements.Length, region.RegionId, y.FeatureKind, AirRegionIntegrationStatus.Deferred, diagnostics, guarantees);
        return new("brep-placeholder-plan:side-hole:+x", region.RegionId, region.RegionKind, y.YieldId, y.FeatureKind, AirRegionBRepPlaceholderStatus.PlaceholderOnly, elements, summary, new(missing.Length == 0, required, missing, ["air-region-x6-placeholder-validation-succeeded"]), diagnostics, losses, guarantees);
    }

    private static AirRegionBRepPlaceholderElement Element(string id, string kind, string role, IReadOnlyList<string> semanticRoles, AirRegionSummary region, AirRegionYieldSummary yield, string parentReference, string materialization, string diagnostic) => new(id, kind, role, semanticRoles, region.RegionId, yield.YieldId, parentReference, materialization, [diagnostic]);
    private static IEnumerable<string> Stable(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal);
}

internal static class AirSideHolePlaceholderMaterializer
{
    public static AirRegionSideHoleMaterializationSummary MaterializePatch(AirRegionSummary region, AirRegionBRepBoundarySummary boundary, AirRegionBRepPlaceholderPlan placeholders)
    {
        if (region.Yield is null) throw new ArgumentException("Side-hole region must carry a yield summary.", nameof(region));
        var byRole = placeholders.Elements.ToDictionary(e => e.Role, StringComparer.Ordinal);
        var mappings = new[]
        {
            Map(byRole["AffectedParentFace"], AirRegionMaterializationStatus.Materialized, "ParentFaceReference", "AffectedParentFaceReference", "brep:root-box:face:+x", "air-region-x7-affected-parent-face-consumed"),
            Map(byRole["CutEntryLoop"], AirRegionMaterializationStatus.Materialized, "AnalyticCircularLoop", "MaterializedEntryLoop", "brep:side-hole:+x:entry-loop", "air-region-x7-cut-entry-loop-materialized"),
            Map(byRole["CutExitLoop"], AirRegionMaterializationStatus.Materialized, "AnalyticCircularLoop", "MaterializedExitLoop", "brep:side-hole:+x:exit-loop", "air-region-x7-cut-exit-loop-materialized"),
            Map(byRole["CutWallFace"], AirRegionMaterializationStatus.Materialized, "CylindricalFace", "CylindricalCutWallFace", "brep:side-hole:+x:cut-wall-face", "air-region-x7-cut-wall-face-materialized"),
            Map(byRole["RegionIntegrationPatch"], AirRegionMaterializationStatus.Deferred, "ParentIntegrationPatch", "ParentIntegrationDeferred", null, "air-region-x7-parent-brep-integration-deferred")
        };
        var diagnostics = Stable(["air-region-x7-materialization-request-created", "air-region-x7-placeholder-plan-consumed", "air-region-x7-side-hole-materialization-started", "air-region-x7-controlled-side-hole-route-selected", "air-region-x7-standalone-patch-materialized", "air-region-x7-parent-integration-not-implemented", "air-region-x7-parent-brep-integration-deferred", "air-region-x7-materialization-partial", "air-region-x7-topology-summary-created", "air-region-x7-step-smoke-unavailable", "air-region-x7-no-production-route-replacement", "air-region-x7-no-general-side-hole-support", "air-region-x7-no-cir-topology-authority", .. mappings.SelectMany(m => m.Diagnostics)]).ToArray();
        return new(region.RegionId, region.Yield.FeatureKind, AirRegionMaterializationStatus.PartiallyMaterialized, "ControlledSideHolePatchMaterialization", mappings, new(true, false, 1, 2, 1, "local-cylinder-patch:x=+5..-5,r=1", ["MaterializedEntryLoop", "MaterializedExitLoop", "CylindricalCutWallFace"]), new(false, false, ["air-region-x7-step-smoke-unavailable", "step smoke not checked because parent integration remains deferred"]), diagnostics, ["parent-brep-integration-not-implemented", "standalone-patch-only", "no-closed-parent-body"], ["controlled fixture only", "no general side-hole support", "no arbitrary face/axis support", "no production route replacement", "no parent topology mutation", "parent integration deferred", "CIR remains analysis-only", "Boolean not generally admitted", "no STEP exporter/importer change"]);
    }

    private static AirRegionBRepPlaceholderMaterialization Map(AirRegionBRepPlaceholderElement e, AirRegionMaterializationStatus status, string kind, string role, string? id, string diagnostic) => new(e.Id, e.Role, status, kind, role, id, [diagnostic]);
    private static IEnumerable<string> Stable(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal);
}

internal static class AirSideHoleParentBRepIntegrationPrototype
{
    public static AirRegionParentIntegrationSummary Attempt(AirRegionSummary region, AirRegionBRepBoundarySummary boundary, AirRegionBRepPlaceholderPlan placeholders, AirRegionSideHoleMaterializationSummary patch)
    {
        if (region.Yield is null) throw new ArgumentException("Side-hole region must carry a yield summary.", nameof(region));
        var byRole = placeholders.Elements.ToDictionary(e => e.Role, StringComparer.Ordinal);
        var mappings = new[]
        {
            Map(byRole["AffectedParentFace"], AirRegionMaterializationStatus.Deferred, "ParentFace", "RequiresParentFaceSplit", "brep:root-box:face:+x", "air-region-x8-affected-parent-face-blocked-by-face-splitting"),
            Map(byRole["CutEntryLoop"], AirRegionMaterializationStatus.Deferred, "ParentLoop", "RequiresLoopInsertion", null, "air-region-x8-cut-entry-loop-blocked-by-face-splitting"),
            Map(byRole["CutExitLoop"], AirRegionMaterializationStatus.Deferred, "ParentLoop", "RequiresOppositeFaceLoopInsertion", null, "air-region-x8-cut-exit-loop-blocked-by-face-splitting"),
            Map(byRole["CutWallFace"], AirRegionMaterializationStatus.Materialized, "CylindricalFaceEvidence", "StandaloneCylindricalCutWallEvidence", "brep:side-hole:+x:cut-wall-face", "air-region-x8-cut-wall-face-evidence-preserved"),
            Map(byRole["RegionIntegrationPatch"], AirRegionMaterializationStatus.Deferred, "ParentIntegrationPatch", "BlockedBeforeParentConsumption", null, "air-region-x8-integration-patch-blocked-by-face-splitting")
        };
        var blocker = new AirRegionIntegrationBlocker(
            AirRegionIntegrationBlockerCategory.FaceSplitting,
            "controlled-side-hole-parent-face-splitting-missing",
            "The controlled side-hole parent integration attempt can consume the X6 placeholders and X7 patch evidence, but no bounded parent-face splitting and loop insertion API is available for inserting the circular entry/exit loops into the box shell without expanding to a general side-hole algorithm.",
            "ParentFaceSplitAndLoopInsertion",
            [byRole["AffectedParentFace"].Id, byRole["CutEntryLoop"].Id, byRole["CutExitLoop"].Id, byRole["RegionIntegrationPatch"].Id],
            ["x2-yield-contract-present", "x3-cir-mirror-analysis-only", "x4-brep-boundary-contract-present", "x5-route-decision-present", "x6-placeholders-present", "x7-standalone-patch-materialized", "standalone-cylindrical-cut-wall-face-evidence-present"],
            "Add a controlled parent-face split plus entry/exit loop insertion adapter for the exact +X through-hole fixture, then bind the preserved cylindrical cut-wall evidence into the parent shell.",
            ["air-region-x8-face-splitting-blocker", "air-region-x8-loop-insertion-blocker"]);
        var diagnostics = Stable(["air-region-x8-parent-integration-request-created", "air-region-x8-side-hole-parent-integration-started", "air-region-x8-placeholder-plan-consumed", "air-region-x8-x7-patch-evidence-consumed", "air-region-x8-controlled-parent-integration-route-selected", "air-region-x8-parent-integration-blocked", "air-region-x8-blocker-recorded", "air-region-x8-current-evidence-preserved", "air-region-x8-no-fake-parent-integration", "air-region-x8-face-splitting-blocker", "air-region-x8-loop-insertion-blocker", "air-region-x8-controlled-fixture-only", "air-region-x8-no-general-side-hole-support", "air-region-x8-no-production-route-replacement", "air-region-x8-cir-remains-analysis-only", .. mappings.SelectMany(m => m.Diagnostics), .. blocker.Diagnostics]).ToArray();
        return new(region.RegionId, region.Yield.FeatureKind, AirRegionIntegrationStatus.Blocked, AirRegionIntegrationRouteKind.ControlledSideHoleParentBRepIntegration, mappings, new(false, false, null, null, 1, "parent-box:10x8x6; parent shell not split; x7 local patch bounds preserved", ["StandaloneCylindricalCutWallFaceEvidence"]), new(false, false, "unavailable", ["air-region-x8-step-smoke-unavailable", "step smoke unavailable because parent BRep integration is blocked before shell closure"]), blocker, diagnostics, ["parent-face-splitting-missing", "entry-exit-loop-insertion-missing", "parent-shell-not-closed", "standalone-patch-only"], ["controlled fixture only", "no general side-hole support", "no arbitrary face/axis support", "no production route replacement", "no parent topology mutation", "CIR remains analysis-only", "Boolean not used", "Boolean not generally admitted", "no STEP exporter/importer change"]);
    }

    private static AirRegionBRepPlaceholderMaterialization Map(AirRegionBRepPlaceholderElement e, AirRegionMaterializationStatus status, string kind, string role, string? id, string diagnostic) => new(e.Id, e.Role, status, kind, role, id, [diagnostic]);
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
    AirRegionBRepBoundarySummary? BrepBoundary = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    AirRegionIntegrationDecisionSummary? IntegrationDecision = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    AirRegionBRepPlaceholderPlan? BrepPlaceholders = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    AirRegionSideHoleMaterializationSummary? Materialization = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    AirRegionParentIntegrationSummary? ParentIntegration = null);

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
        var decision = AirSideHoleRegionIntegrationSelector.Decide(sideBase, mirror.Summary, boundary);
        var placeholders = AirSideHoleBRepPlaceholderPlanner.Plan(sideBase, boundary, decision);
        var materialization = AirSideHolePlaceholderMaterializer.MaterializePatch(sideBase, boundary, placeholders);
        var parentIntegration = AirSideHoleParentBRepIntegrationPrototype.Attempt(sideBase, boundary, placeholders, materialization);
        var x8Decision = decision with { SelectedRouteKind = AirRegionIntegrationRouteKind.ControlledSideHoleParentBRepIntegration, SelectedStatus = AirRegionIntegrationStatus.Blocked, Recommendation = "controlled parent BRep integration attempted; blocked at parent face splitting and loop insertion", Diagnostics = Stable([.. decision.Diagnostics, .. materialization.Diagnostics, .. parentIntegration.Diagnostics]).ToArray(), KnownLosses = Stable([.. decision.KnownLosses, .. materialization.KnownLosses, .. parentIntegration.KnownLosses]).ToArray(), Guarantees = Stable([.. decision.Guarantees, .. materialization.Guarantees, .. parentIntegration.Guarantees]).ToArray() };
        var side = sideBase with
        {
            StageReached = "region-parent-integration-blocked",
            Provenance = "AIR-REGION-X8 metadata-driven fixture",
            Diagnostics = Stable([.. sideBase.Diagnostics.Where(d => d != "air-region-x2-no-cir-mirror"), .. mirror.Summary.Diagnostics, .. boundary.Diagnostics, .. x8Decision.Diagnostics, .. placeholders.Diagnostics, .. materialization.Diagnostics]).ToArray(),
            KnownLosses = Stable([.. sideBase.KnownLosses, .. mirror.Summary.KnownLosses, .. boundary.KnownLosses, .. x8Decision.KnownLosses, .. placeholders.KnownLosses, .. materialization.KnownLosses]).ToArray(),
            Guarantees = Stable([.. sideBase.Guarantees, .. mirror.Summary.Guarantees, .. boundary.Guarantees, .. x8Decision.Guarantees, .. placeholders.Guarantees, .. materialization.Guarantees]).ToArray(),
            IntegrationRoute = "ControlledSideHoleParentBRepIntegration",
            CirMirror = mirror.Summary,
            BrepBoundary = boundary,
            IntegrationDecision = x8Decision,
            BrepPlaceholders = placeholders,
            Materialization = materialization,
            ParentIntegration = parentIntegration
        };
        return Summary([root, side], ["air-region-x1-region-trace-created", "air-region-x1-no-implicit-parent-mutation", "air-region-x1-no-boolean", "air-region-x1-no-brep-emission", "air-region-x1-no-production-route-replacement", "air-region-x1-trace-only", "air-region-x2-yield-contract-created", "air-region-x2-side-hole-yield-created", "air-region-x2-region-locality-enforced", "air-region-x2-explicit-yield-only", "air-region-x2-parent-integration-deferred", "air-region-x2-no-boolean", "air-region-x2-no-brep-emission", "air-region-x2-no-step-smoke", "air-region-x2-trace-only", .. side.CirMirror!.Diagnostics, .. side.BrepBoundary!.Diagnostics, .. side.IntegrationDecision!.Diagnostics, .. side.BrepPlaceholders!.Diagnostics, .. side.Materialization!.Diagnostics, .. side.ParentIntegration!.Diagnostics], ["escapes only through explicit yield", "no Boolean", "no production route replacement", .. side.CirMirror.Guarantees, .. side.BrepBoundary.Guarantees, .. side.IntegrationDecision.Guarantees, .. side.BrepPlaceholders.Guarantees, .. side.Materialization.Guarantees, .. side.ParentIntegration.Guarantees]);
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
