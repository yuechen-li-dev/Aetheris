using System.Globalization;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum SurfaceFeatureKind
{
    RoundGroove,
    Ridge,
    Bead,
    Thread,
    Emboss,
    Deboss,
    Knurl,
    Dimple,
    Unsupported
}

internal enum SurfaceFeaturePathKind
{
    CircleOnPlane,
    CircumferentialOnCylinder,
    LatitudeOnSphere,
    HelixOnCylinder,
    CurveOnSurface,
    Unsupported
}

internal enum SurfaceFeatureProfileKind
{
    CircularArc,
    VProfile,
    Trapezoid,
    FlatBottom,
    Custom,
    Unsupported
}

internal enum SurfaceFeatureDirection
{
    Remove,
    Add
}

internal enum SurfaceFeatureCapabilityTier
{
    CoreExact,
    CoreSplineApprox,
    Forge,
    Deferred,
    Unsupported
}

internal enum SurfaceFeatureValidationStatus
{
    Valid,
    WarningDeferred,
    Unsupported,
    Invalid
}

internal record SurfaceFeatureDescriptor(
    string FeatureId,
    SurfaceFeatureKind FeatureKind,
    string HostSurfaceRef,
    SurfacePatchFamily HostSurfaceFamily,
    SurfaceFeaturePathKind PathKind,
    SurfaceFeatureProfileKind ProfileKind,
    SurfaceFeatureDirection Direction,
    double ProfileRadius,
    double CenterlineRadius,
    double DepthOrHeight,
    bool HasAlignmentConstraint,
    SurfaceFeatureCapabilityTier CapabilityTarget,
    IReadOnlyDictionary<string, double>? PathParameters = null,
    IReadOnlyDictionary<string, double>? ProfileParameters = null,
    IReadOnlyDictionary<string, double>? ExtentParameters = null);

internal sealed record RoundGrooveFeatureDescriptor(
    string FeatureId,
    string HostSurfaceRef,
    SurfacePatchFamily HostSurfaceFamily,
    SurfaceFeaturePathKind PathKind,
    SurfaceFeatureDirection Direction,
    double ProfileRadius,
    double CenterlineRadius,
    double DepthOrHeight,
    bool HasAlignmentConstraint,
    SurfaceFeatureCapabilityTier CapabilityTarget = SurfaceFeatureCapabilityTier.CoreExact)
    : SurfaceFeatureDescriptor(
        FeatureId,
        SurfaceFeatureKind.RoundGroove,
        HostSurfaceRef,
        HostSurfaceFamily,
        PathKind,
        SurfaceFeatureProfileKind.CircularArc,
        Direction,
        ProfileRadius,
        CenterlineRadius,
        DepthOrHeight,
        HasAlignmentConstraint,
        CapabilityTarget);

internal sealed record SurfaceFeatureValidationDiagnostic(string Code, string Message);

internal sealed record SurfaceFeatureValidationResult(
    SurfaceFeatureValidationStatus Status,
    SurfaceFeatureCapabilityTier CapabilityTier,
    bool MaterializationClaimed,
    SurfaceFeatureKind NormalizedKind,
    IReadOnlyList<SurfaceFeatureValidationDiagnostic> Diagnostics)
{
    public bool IsSuccess => Status == SurfaceFeatureValidationStatus.Valid;
}

internal static class SurfaceFeatureValidator
{
    public static SurfaceFeatureValidationResult Validate(SurfaceFeatureDescriptor descriptor)
    {
        var diagnostics = new List<SurfaceFeatureValidationDiagnostic>();
        var capability = descriptor.CapabilityTarget;

        if (descriptor.FeatureKind == SurfaceFeatureKind.Thread || descriptor.PathKind == SurfaceFeaturePathKind.HelixOnCylinder)
        {
            diagnostics.Add(new("surface-feature-thread-deferred", "Thread and helical surface features are Forge/deferred due to helix topology and export complexity in first-wave Core scope."));
            return new(SurfaceFeatureValidationStatus.WarningDeferred, SurfaceFeatureCapabilityTier.Forge, false, SurfaceFeatureKind.Thread, diagnostics);
        }

        if (descriptor.FeatureKind == SurfaceFeatureKind.RoundGroove && descriptor.Direction == SurfaceFeatureDirection.Add)
        {
            diagnostics.Add(new("surface-feature-roundgroove-add-normalized", "Round groove descriptor with additive direction normalized to ridge/bead family for first-wave constrained surface features."));
            return Validate(descriptor with { FeatureKind = SurfaceFeatureKind.Ridge }, diagnostics);
        }

        return Validate(descriptor, diagnostics);
    }

    private static SurfaceFeatureValidationResult Validate(SurfaceFeatureDescriptor descriptor, List<SurfaceFeatureValidationDiagnostic> diagnostics)
    {
        if (descriptor.HostSurfaceFamily == SurfacePatchFamily.Toroidal)
        {
            diagnostics.Add(new("surface-feature-generic-torus-unsupported", "Generic torus Boolean remains unsupported for exact materialization and must not be modeled as unconstrained surface feature intent."));
            return new(SurfaceFeatureValidationStatus.Unsupported, SurfaceFeatureCapabilityTier.Unsupported, false, descriptor.FeatureKind, diagnostics);
        }

        if (descriptor.ProfileRadius <= 0d || descriptor.CenterlineRadius <= 0d || descriptor.DepthOrHeight <= 0d)
        {
            diagnostics.Add(new("surface-feature-invalid-extent", string.Format(CultureInfo.InvariantCulture, "Profile radius ({0}), centerline radius ({1}), and depth/height ({2}) must be positive.", descriptor.ProfileRadius, descriptor.CenterlineRadius, descriptor.DepthOrHeight)));
            return new(SurfaceFeatureValidationStatus.Invalid, SurfaceFeatureCapabilityTier.Unsupported, false, descriptor.FeatureKind, diagnostics);
        }

        if (!descriptor.HasAlignmentConstraint)
        {
            diagnostics.Add(new("surface-feature-misaligned", "Surface feature is missing required host/path alignment constraint for constrained first-wave validation."));
            return new(SurfaceFeatureValidationStatus.Invalid, SurfaceFeatureCapabilityTier.Unsupported, false, descriptor.FeatureKind, diagnostics);
        }

        if (descriptor.PathKind == SurfaceFeaturePathKind.CurveOnSurface)
        {
            diagnostics.Add(new("surface-feature-arbitrary-path-deferred", "Arbitrary CurveOnSurface path is out of first-wave constrained scope; defer to Forge/deferred surface feature pipeline."));
            return new(SurfaceFeatureValidationStatus.WarningDeferred, SurfaceFeatureCapabilityTier.Deferred, false, descriptor.FeatureKind, diagnostics);
        }

        var hostPathSupported = descriptor switch
        {
            { HostSurfaceFamily: SurfacePatchFamily.Planar, PathKind: SurfaceFeaturePathKind.CircleOnPlane } => true,
            { HostSurfaceFamily: SurfacePatchFamily.Cylindrical, PathKind: SurfaceFeaturePathKind.CircumferentialOnCylinder } => true,
            _ => false
        };

        if (!hostPathSupported)
        {
            diagnostics.Add(new("surface-feature-host-path-unsupported", $"Unsupported host/path combination for first-wave constrained policy: {descriptor.HostSurfaceFamily} + {descriptor.PathKind}."));
            return new(SurfaceFeatureValidationStatus.Unsupported, SurfaceFeatureCapabilityTier.Unsupported, false, descriptor.FeatureKind, diagnostics);
        }

        if (descriptor.ProfileKind != SurfaceFeatureProfileKind.CircularArc)
        {
            diagnostics.Add(new("surface-feature-profile-deferred", $"Profile kind {descriptor.ProfileKind} is deferred in first-wave constrained round groove/ridge validation."));
            return new(SurfaceFeatureValidationStatus.WarningDeferred, SurfaceFeatureCapabilityTier.Deferred, false, descriptor.FeatureKind, diagnostics);
        }

        var normalized = descriptor.FeatureKind is SurfaceFeatureKind.Bead ? SurfaceFeatureKind.Ridge : descriptor.FeatureKind;
        if (normalized is not (SurfaceFeatureKind.RoundGroove or SurfaceFeatureKind.Ridge))
        {
            diagnostics.Add(new("surface-feature-kind-deferred", $"Feature kind {descriptor.FeatureKind} is not in first-wave Core constrained family and is deferred."));
            return new(SurfaceFeatureValidationStatus.WarningDeferred, SurfaceFeatureCapabilityTier.Deferred, false, descriptor.FeatureKind, diagnostics);
        }

        if (normalized == SurfaceFeatureKind.RoundGroove && descriptor.Direction != SurfaceFeatureDirection.Remove)
        {
            diagnostics.Add(new("surface-feature-direction-invalid", "Round groove requires Remove direction under first-wave constrained policy."));
            return new(SurfaceFeatureValidationStatus.Invalid, SurfaceFeatureCapabilityTier.Unsupported, false, normalized, diagnostics);
        }

        if (normalized == SurfaceFeatureKind.Ridge && descriptor.Direction != SurfaceFeatureDirection.Add)
        {
            diagnostics.Add(new("surface-feature-direction-invalid", "Ridge/bead requires Add direction under first-wave constrained policy."));
            return new(SurfaceFeatureValidationStatus.Invalid, SurfaceFeatureCapabilityTier.Unsupported, false, normalized, diagnostics);
        }

        diagnostics.Add(new("surface-feature-valid-planned", "Descriptor validates as planned constrained surface feature; no materialization is claimed in SURFACE-FEATURE-A1."));
        return new(SurfaceFeatureValidationStatus.Valid, SurfaceFeatureCapabilityTier.CoreExact, false, normalized, diagnostics);
    }
}
