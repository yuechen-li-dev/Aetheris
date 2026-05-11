using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfaceFeatureDryRunGeneratorTests
{
    [Fact]
    public void SurfaceFeatureDryRun_PlanarRoundGroove_ProducesPatchExpectations()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g1", "base.top_face", SurfacePatchFamily.Planar, SurfaceFeaturePathKind.CircleOnPlane, SurfaceFeatureDirection.Remove, 1.2d, 8d, 0.5d, true);

        var result = SurfaceFeatureDryRunGenerator.Generate(descriptor);

        Assert.True(result.Success);
        Assert.Equal(SurfaceFeatureDryRunStatus.Planned, result.Status);
        Assert.False(result.MaterializationClaimed);
        Assert.Contains(result.PatchExpectations, p => p.PatchRole == SurfaceFeaturePatchRole.HostRetainedPlanarPatch && p.SurfaceFamily == SurfacePatchFamily.Planar);
        Assert.Contains(result.PatchExpectations, p => p.PatchRole == SurfaceFeaturePatchRole.GrooveWallPatch);
    }

    [Fact]
    public void SurfaceFeatureDryRun_PlanarRoundGroove_ProducesTrimExpectations()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g2", "base.top_face", SurfacePatchFamily.Planar, SurfaceFeaturePathKind.CircleOnPlane, SurfaceFeatureDirection.Remove, 0.8d, 6d, 0.3d, true);

        var result = SurfaceFeatureDryRunGenerator.Generate(descriptor);

        Assert.True(result.Success);
        Assert.Contains(result.TrimExpectations, t => t.CurveFamily == TrimCurveFamily.Circle && t.TrimRole == SurfaceFeatureTrimRole.OuterGrooveBoundary);
        Assert.Contains(result.TrimExpectations, t => t.CurveFamily == TrimCurveFamily.Circle && t.TrimRole == SurfaceFeatureTrimRole.InnerGrooveBoundary);
        Assert.Contains(result.TrimExpectations, t => t.CurveFamily == TrimCurveFamily.BSpline && t.Capability == TrimCurveCapability.Deferred);
    }

    [Fact]
    public void SurfaceFeatureDryRun_PlanarRoundGroove_DiagnosticsAreExplicit()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g3", "base.top_face", SurfacePatchFamily.Planar, SurfaceFeaturePathKind.CircleOnPlane, SurfaceFeatureDirection.Remove, 0.5d, 4d, 0.2d, true);

        var result = SurfaceFeatureDryRunGenerator.Generate(descriptor);

        Assert.Contains(result.Diagnostics, d => d.Contains("dry-run-only", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("no-brep-emission", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("generic-torus-boolean-not-used", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SurfaceFeatureDryRun_InvalidDescriptor_FailsFromPlanner()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g4", "base.top_face", SurfacePatchFamily.Planar, SurfaceFeaturePathKind.CircleOnPlane, SurfaceFeatureDirection.Remove, 0d, 3d, -0.1d, true);

        var result = SurfaceFeatureDryRunGenerator.Generate(descriptor);

        Assert.False(result.Success);
        Assert.Equal(SurfaceFeatureDryRunStatus.Invalid, result.Status);
        Assert.Empty(result.PatchExpectations);
        Assert.Empty(result.TrimExpectations);
    }

    [Fact]
    public void SurfaceFeatureDryRun_Thread_RemainsForgeDeferred()
    {
        var descriptor = new SurfaceFeatureDescriptor("t1", SurfaceFeatureKind.Thread, "shaft.side_face", SurfacePatchFamily.Cylindrical, SurfaceFeaturePathKind.HelixOnCylinder, SurfaceFeatureProfileKind.VProfile, SurfaceFeatureDirection.Remove, 0.2d, 4d, 0.1d, true, SurfaceFeatureCapabilityTier.Deferred);

        var result = SurfaceFeatureDryRunGenerator.Generate(descriptor);

        Assert.False(result.Success);
        Assert.Equal(SurfaceFeatureDryRunStatus.Forge, result.Status);
        Assert.False(result.MaterializationClaimed);
        Assert.Empty(result.PatchExpectations);
    }
}
