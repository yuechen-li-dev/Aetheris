namespace Aetheris.Kernel.Firmament.Execution;

internal enum SurfaceFeaturePlanningStatus
{
    Planned,
    Deferred,
    Forge,
    Unsupported,
    Invalid
}

internal sealed record SurfaceFeatureHostRequirement(
    SurfacePatchFamily HostSurfaceFamily,
    string RequiredSelectorKind,
    string AlignmentConstraint,
    IReadOnlyList<string> Diagnostics);

internal sealed record SurfaceFeaturePathRequirement(
    SurfaceFeaturePathKind PathKind,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<string> Diagnostics);

internal sealed record SurfaceFeatureProfileRequirement(
    SurfaceFeatureProfileKind ProfileKind,
    string ParametersSummary,
    IReadOnlyList<string> Diagnostics);

internal sealed record SurfaceFeaturePatchExpectation(
    SurfacePatchFamily SurfaceFamily,
    string Role,
    SurfaceFeatureCapabilityTier Capability,
    IReadOnlyList<string> Diagnostics);

internal sealed record SurfaceFeatureTrimRequirement(
    TrimCurveFamily CurveFamily,
    TrimCurveCapability Capability,
    string ExactnessPolicy,
    IReadOnlyList<string> Diagnostics);

internal sealed record SurfaceFeaturePlanningResult(
    SurfaceFeaturePlanningStatus Status,
    string FeatureId,
    SurfaceFeatureKind FeatureKind,
    SurfaceFeatureCapabilityTier CapabilityTier,
    bool MaterializationClaimed,
    SurfaceFeatureHostRequirement? HostRequirements,
    SurfaceFeaturePathRequirement? PathRequirements,
    SurfaceFeatureProfileRequirement? ProfileRequirements,
    IReadOnlyList<SurfaceFeaturePatchExpectation> ExpectedPatchFamilies,
    IReadOnlyList<SurfaceFeatureTrimRequirement> RequiredTrimCurveFamilies,
    IReadOnlyList<SurfacePatchFamily> RequiredSurfaceFamilies,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> Diagnostics);

internal static class SurfaceFeaturePlanner
{
    internal static SurfaceFeaturePlanningResult Plan(SurfaceFeatureDescriptor descriptor)
    {
        var validation = SurfaceFeatureValidator.Validate(descriptor);
        var diagnostics = new List<string> { "planning-only: result is non-emitting and does not materialize BRep topology." };
        diagnostics.AddRange(validation.Diagnostics.Select(d => $"{d.Code}: {d.Message}"));

        if (validation.Status == SurfaceFeatureValidationStatus.Invalid)
        {
            return BuildTerminal(SurfaceFeaturePlanningStatus.Invalid, validation, descriptor, ["validation-failed"], diagnostics);
        }

        if (validation.Status == SurfaceFeatureValidationStatus.Unsupported)
        {
            return BuildTerminal(SurfaceFeaturePlanningStatus.Unsupported, validation, descriptor, ["unsupported-intent"], diagnostics);
        }

        if (validation.CapabilityTier == SurfaceFeatureCapabilityTier.Forge)
        {
            diagnostics.Add("forge-route: thread/helical feature planning is routed to Forge in first-wave Core scope.");
            return BuildTerminal(SurfaceFeaturePlanningStatus.Forge, validation, descriptor, ["thread-helical-complexity"], diagnostics);
        }

        if (validation.CapabilityTier == SurfaceFeatureCapabilityTier.Deferred)
        {
            diagnostics.Add("deferred-route: host/path/profile combination requires deferred pipeline or spline approximation policy.");
            return BuildTerminal(SurfaceFeaturePlanningStatus.Deferred, validation, descriptor, ["deferred-capability"], diagnostics);
        }

        var normalizedKind = validation.NormalizedKind;
        var host = new SurfaceFeatureHostRequirement(
            descriptor.HostSurfaceFamily,
            "semantic-host-reference",
            descriptor.HasAlignmentConstraint ? "required-and-present" : "missing",
            [$"host={descriptor.HostSurfaceFamily}"]);

        var pathDiagnostics = descriptor.PathKind switch
        {
            SurfaceFeaturePathKind.CircleOnPlane => new[] { "path-circle-on-plane: concentric/normal alignment expected." },
            SurfaceFeaturePathKind.CircumferentialOnCylinder => new[] { "path-circumferential-on-cylinder: coaxial circumferential alignment expected." },
            _ => new[] { "path-unsupported-for-core-plan" }
        };
        var path = new SurfaceFeaturePathRequirement(descriptor.PathKind, ["closed-path", "bounded-first-wave"], pathDiagnostics);
        var profile = new SurfaceFeatureProfileRequirement(descriptor.ProfileKind, $"radius={descriptor.ProfileRadius:0.###}, depthOrHeight={descriptor.DepthOrHeight:0.###}", ["profile-circular-arc-required-in-first-wave"]);

        var isAdditive = normalizedKind == SurfaceFeatureKind.Ridge || descriptor.Direction == SurfaceFeatureDirection.Add;
        var patches = new List<SurfaceFeaturePatchExpectation>
        {
            new(descriptor.HostSurfaceFamily, "host-replacement-or-retained", SurfaceFeatureCapabilityTier.CoreExact, ["host patch participation expected"]),
            new(descriptor.HostSurfaceFamily, isAdditive ? "additive-feature-wall" : "removal-feature-wall", SurfaceFeatureCapabilityTier.CoreExact, ["local bounded patch set expected"]),
            new(SurfacePatchFamily.Toroidal, "round-profile-transition", SurfaceFeatureCapabilityTier.CoreSplineApprox, ["conceptual toroidal/rolled transition may require spline approximation policy in future materializer"])
        };

        var trims = new List<SurfaceFeatureTrimRequirement>
        {
            new(TrimCurveFamily.Circle, TrimCurveCapability.ExactSupported, "exact-when-circular-constraints-hold", ["circular trim loops expected"]),
            new(TrimCurveFamily.BSpline, TrimCurveCapability.Deferred, "approximation-only-if-policy-allows", ["spline approximation would be explicit and non-exact"])
        };

        diagnostics.Add("core-plan-ready: descriptor is planned under constrained first-wave surface feature policy.");
        diagnostics.Add("materialization-not-implemented: planner does not claim patch emission, topology assembly, or STEP exactness.");

        return new(
            SurfaceFeaturePlanningStatus.Planned,
            descriptor.FeatureId,
            normalizedKind,
            SurfaceFeatureCapabilityTier.CoreExact,
            MaterializationClaimed: false,
            HostRequirements: host,
            PathRequirements: path,
            ProfileRequirements: profile,
            ExpectedPatchFamilies: patches,
            RequiredTrimCurveFamilies: trims,
            RequiredSurfaceFamilies: [descriptor.HostSurfaceFamily, SurfacePatchFamily.Toroidal],
            BlockingReasons: ["materialization-not-implemented"],
            Diagnostics: diagnostics);
    }

    private static SurfaceFeaturePlanningResult BuildTerminal(
        SurfaceFeaturePlanningStatus status,
        SurfaceFeatureValidationResult validation,
        SurfaceFeatureDescriptor descriptor,
        IReadOnlyList<string> blockers,
        IReadOnlyList<string> diagnostics)
        => new(
            status,
            descriptor.FeatureId,
            validation.NormalizedKind,
            validation.CapabilityTier,
            MaterializationClaimed: false,
            HostRequirements: null,
            PathRequirements: null,
            ProfileRequirements: null,
            ExpectedPatchFamilies: [],
            RequiredTrimCurveFamilies: [],
            RequiredSurfaceFamilies: [],
            BlockingReasons: blockers,
            Diagnostics: diagnostics);
}
