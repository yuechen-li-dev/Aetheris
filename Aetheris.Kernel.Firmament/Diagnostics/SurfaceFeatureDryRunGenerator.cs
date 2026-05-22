using Aetheris.Kernel.Firmament.Execution;
namespace Aetheris.Kernel.Firmament.Diagnostics;

internal enum SurfaceFeatureDryRunStatus
{
    Planned,
    Deferred,
    Forge,
    Unsupported,
    Invalid
}

internal enum SurfaceFeaturePatchRole
{
    HostRetainedPlanarPatch,
    GrooveWallPatch,
    GrooveBottomOrProfilePatch,
    DeferredPatch
}

internal enum SurfaceFeatureTrimRole
{
    OuterGrooveBoundary,
    InnerGrooveBoundary,
    ProfileBoundary,
    DeferredTrim
}

internal sealed record SurfaceFeaturePatchDryRun(
    SurfaceFeaturePatchRole PatchRole,
    SurfacePatchFamily SurfaceFamily,
    string ExpectedGeometry,
    SurfaceFeatureCapabilityTier Capability,
    IReadOnlyList<string> Diagnostics);

internal sealed record SurfaceFeatureTrimDryRun(
    SurfaceFeatureTrimRole TrimRole,
    TrimCurveFamily CurveFamily,
    TrimCurveCapability Capability,
    string ExactnessPolicy,
    IReadOnlyList<string> Diagnostics);

internal sealed record SurfaceFeatureDryRunResult(
    bool Success,
    string FeatureId,
    SurfaceFeatureKind FeatureKind,
    SurfaceFeatureDryRunStatus Status,
    SurfaceFeatureCapabilityTier CapabilityTier,
    bool MaterializationClaimed,
    IReadOnlyList<SurfaceFeaturePatchDryRun> PatchExpectations,
    IReadOnlyList<SurfaceFeatureTrimDryRun> TrimExpectations,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> Diagnostics);

internal static class SurfaceFeatureDryRunGenerator
{
    internal static SurfaceFeatureDryRunResult Generate(SurfaceFeatureDescriptor descriptor)
    {
        var plan = SurfaceFeaturePlanner.Plan(descriptor);
        var diagnostics = new List<string>
        {
            "dry-run-only: reports patch/trim expectations and does not emit topology or mutate BRep.",
            "generic-torus-boolean-not-used: dry-run stays in surface feature planning domain."
        };
        diagnostics.AddRange(plan.Diagnostics);

        if (plan.Status != SurfaceFeaturePlanningStatus.Planned)
        {
            diagnostics.Add("planner-terminal: dry-run stopped because planning did not reach Planned status.");
            return new(
                Success: false,
                FeatureId: plan.FeatureId,
                FeatureKind: plan.FeatureKind,
                Status: MapStatus(plan.Status),
                CapabilityTier: plan.CapabilityTier,
                MaterializationClaimed: false,
                PatchExpectations: [],
                TrimExpectations: [],
                BlockingReasons: plan.BlockingReasons,
                Diagnostics: diagnostics);
        }

        if (!IsPlanarRoundGroove(descriptor, plan))
        {
            diagnostics.Add("dry-run-deferred: current A3 generator only handles planar round groove remove-path profile constraints.");
            return new(
                Success: false,
                FeatureId: plan.FeatureId,
                FeatureKind: plan.FeatureKind,
                Status: SurfaceFeatureDryRunStatus.Deferred,
                CapabilityTier: SurfaceFeatureCapabilityTier.Deferred,
                MaterializationClaimed: false,
                PatchExpectations: [],
                TrimExpectations: [],
                BlockingReasons: ["dry-run-scope-limited"],
                Diagnostics: diagnostics);
        }

        var patches = new List<SurfaceFeaturePatchDryRun>
        {
            new(
                SurfaceFeaturePatchRole.HostRetainedPlanarPatch,
                SurfacePatchFamily.Planar,
                "retained-host-planar-regions-around-groove-annulus",
                SurfaceFeatureCapabilityTier.CoreExact,
                ["host planar retention expected; exact patch decomposition remains non-emitting/planned only"]),
            new(
                SurfaceFeaturePatchRole.GrooveWallPatch,
                SurfacePatchFamily.Toroidal,
                "revolved-round-groove-wall-like-surface",
                SurfaceFeatureCapabilityTier.CoreSplineApprox,
                ["future materializer may represent groove wall with toroidal or spline-approx policy; exact emission not claimed"]),
            new(
                SurfaceFeaturePatchRole.GrooveBottomOrProfilePatch,
                SurfacePatchFamily.Toroidal,
                "circular-arc-profile-bottom-transition",
                SurfaceFeatureCapabilityTier.CoreSplineApprox,
                ["profile/bottom transition expected from circular arc profile; deferred until BRep surface emission milestone"])
        };

        var trims = new List<SurfaceFeatureTrimDryRun>
        {
            new(
                SurfaceFeatureTrimRole.OuterGrooveBoundary,
                TrimCurveFamily.Circle,
                TrimCurveCapability.ExactSupported,
                "exact-when-circle-on-plane-constraints-hold",
                ["outer circular boundary trim expected on planar host"]),
            new(
                SurfaceFeatureTrimRole.InnerGrooveBoundary,
                TrimCurveFamily.Circle,
                TrimCurveCapability.ExactSupported,
                "exact-when-circle-on-plane-constraints-hold",
                ["inner circular boundary trim expected on planar host"]),
            new(
                SurfaceFeatureTrimRole.ProfileBoundary,
                TrimCurveFamily.BSpline,
                TrimCurveCapability.Deferred,
                "approximation-policy-required-for-non-elementary-profile-coupling",
                ["profile/groove-wall trim coupling may require explicit BSpline approximation policy in future"])
        };

        diagnostics.Add("constraint-accepted: planar host + circle-on-plane path + circular-arc profile + remove direction recognized.");
        diagnostics.Add("no-brep-emission: dry-run does not emit faces, edges, loops, or coedges.");
        diagnostics.Add("materialization-not-implemented: this milestone stops at structured expectations and blockers.");

        return new(
            Success: true,
            FeatureId: plan.FeatureId,
            FeatureKind: plan.FeatureKind,
            Status: SurfaceFeatureDryRunStatus.Planned,
            CapabilityTier: SurfaceFeatureCapabilityTier.CoreExact,
            MaterializationClaimed: false,
            PatchExpectations: patches,
            TrimExpectations: trims,
            BlockingReasons: ["brep-materialization-not-implemented", "topology-emission-not-implemented", "step-exactness-not-claimed"],
            Diagnostics: diagnostics);
    }

    private static bool IsPlanarRoundGroove(SurfaceFeatureDescriptor descriptor, SurfaceFeaturePlanningResult plan)
        => plan.FeatureKind == SurfaceFeatureKind.RoundGroove
           && descriptor.HostSurfaceFamily == SurfacePatchFamily.Planar
           && descriptor.PathKind == SurfaceFeaturePathKind.CircleOnPlane
           && descriptor.ProfileKind == SurfaceFeatureProfileKind.CircularArc
           && descriptor.Direction == SurfaceFeatureDirection.Remove;

    private static SurfaceFeatureDryRunStatus MapStatus(SurfaceFeaturePlanningStatus status)
        => status switch
        {
            SurfaceFeaturePlanningStatus.Planned => SurfaceFeatureDryRunStatus.Planned,
            SurfaceFeaturePlanningStatus.Deferred => SurfaceFeatureDryRunStatus.Deferred,
            SurfaceFeaturePlanningStatus.Forge => SurfaceFeatureDryRunStatus.Forge,
            SurfaceFeaturePlanningStatus.Unsupported => SurfaceFeatureDryRunStatus.Unsupported,
            SurfaceFeaturePlanningStatus.Invalid => SurfaceFeatureDryRunStatus.Invalid,
            _ => SurfaceFeatureDryRunStatus.Unsupported
        };
}
