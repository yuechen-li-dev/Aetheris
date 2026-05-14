using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfaceFeatureDescriptorValidationTests
{
    [Fact]
    public void SurfaceFeature_RoundGroove_PlanarCircle_Validates()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g1", "base.top_face", SurfacePatchFamily.Planar, SurfaceFeaturePathKind.CircleOnPlane, SurfaceFeatureDirection.Remove, 1.2d, 8d, 0.5d, true);

        var result = SurfaceFeatureValidator.Validate(descriptor);

        Assert.True(result.IsSuccess);
        Assert.Equal(SurfaceFeatureValidationStatus.Valid, result.Status);
        Assert.Equal(SurfaceFeatureCapabilityTier.CoreExact, result.CapabilityTier);
        Assert.False(result.MaterializationClaimed);
    }

    [Fact]
    public void SurfaceFeature_RoundGroove_CylindricalCircumferential_Validates()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g2", "shaft.side_face", SurfacePatchFamily.Cylindrical, SurfaceFeaturePathKind.CircumferentialOnCylinder, SurfaceFeatureDirection.Remove, 0.6d, 4d, 0.4d, true);
        var result = SurfaceFeatureValidator.Validate(descriptor);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SurfaceFeature_RidgeOrBead_ValidatesAsAdditiveSurfaceFeature()
    {
        var descriptor = new SurfaceFeatureDescriptor("r1", SurfaceFeatureKind.Bead, "shaft.side_face", SurfacePatchFamily.Cylindrical, SurfaceFeaturePathKind.CircumferentialOnCylinder, SurfaceFeatureProfileKind.CircularArc, SurfaceFeatureDirection.Add, 0.4d, 5d, 0.3d, true, SurfaceFeatureCapabilityTier.CoreExact);
        var result = SurfaceFeatureValidator.Validate(descriptor);
        Assert.True(result.IsSuccess);
        Assert.Equal(SurfaceFeatureKind.Ridge, result.NormalizedKind);
    }

    [Fact]
    public void SurfaceFeature_Thread_ClassifiedForgeOrDeferred()
    {
        var descriptor = new SurfaceFeatureDescriptor("t1", SurfaceFeatureKind.Thread, "shaft.side_face", SurfacePatchFamily.Cylindrical, SurfaceFeaturePathKind.HelixOnCylinder, SurfaceFeatureProfileKind.VProfile, SurfaceFeatureDirection.Remove, 0.2d, 4d, 0.1d, true, SurfaceFeatureCapabilityTier.Deferred);
        var result = SurfaceFeatureValidator.Validate(descriptor);
        Assert.Equal(SurfaceFeatureValidationStatus.WarningDeferred, result.Status);
        Assert.Equal(SurfaceFeatureCapabilityTier.Forge, result.CapabilityTier);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("helix", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SurfaceFeature_GenericTorusBoolean_IsRejectedOrDeferred()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g3", "body.surface", SurfacePatchFamily.Toroidal, SurfaceFeaturePathKind.CurveOnSurface, SurfaceFeatureDirection.Remove, 0.2d, 2d, 0.1d, true);
        var result = SurfaceFeatureValidator.Validate(descriptor);
        Assert.Equal(SurfaceFeatureValidationStatus.Unsupported, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Generic torus Boolean remains unsupported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SurfaceFeature_InvalidParameters_Rejected()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g4", "base.top_face", SurfacePatchFamily.Planar, SurfaceFeaturePathKind.CircleOnPlane, SurfaceFeatureDirection.Remove, 0d, 3d, -1d, true);
        var result = SurfaceFeatureValidator.Validate(descriptor);
        Assert.Equal(SurfaceFeatureValidationStatus.Invalid, result.Status);
    }

    [Fact]
    public void SurfaceFeature_ArbitraryCurvePath_RejectedOrDeferred()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g5", "base.top_face", SurfacePatchFamily.Planar, SurfaceFeaturePathKind.CurveOnSurface, SurfaceFeatureDirection.Remove, 0.5d, 3d, 0.2d, true);
        var result = SurfaceFeatureValidator.Validate(descriptor);
        Assert.Equal(SurfaceFeatureValidationStatus.WarningDeferred, result.Status);
        Assert.Equal(SurfaceFeatureCapabilityTier.Deferred, result.CapabilityTier);
    }
}
