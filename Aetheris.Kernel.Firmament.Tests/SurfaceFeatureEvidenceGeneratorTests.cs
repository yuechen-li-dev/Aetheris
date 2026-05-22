using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfaceFeatureEvidenceGeneratorTests
{
    [Fact]
    public void SurfaceFeatureEvidence_PlanarRoundGroove_ProducesEvidence()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g1", "base.top_face", SurfacePatchFamily.Planar, SurfaceFeaturePathKind.CircleOnPlane, SurfaceFeatureDirection.Remove, 1.2d, 8d, 0.5d, true);

        var result = SurfaceFeatureEvidenceGenerator.Generate(descriptor);

        Assert.True(result.Success);
        Assert.Equal(SurfaceFeatureEvidenceStatus.Planned, result.Status);
        Assert.False(result.MaterializationClaimed);
        Assert.NotNull(result.Evidence);
        Assert.Equal(SurfaceFeatureGeometryStatus.DescriptorMissing, result.Evidence!.PathEvidence.GeometryStatus);
        Assert.Equal(SurfaceFeatureProfileKind.CircularArc, result.Evidence.ProfileEvidence.ProfileKind);
    }

    [Fact]
    public void SurfaceFeatureEvidence_PlanarRoundGroove_PatchRolesPresent()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g2", "base.top_face", SurfacePatchFamily.Planar, SurfaceFeaturePathKind.CircleOnPlane, SurfaceFeatureDirection.Remove, 0.8d, 6d, 0.3d, true);

        var result = SurfaceFeatureEvidenceGenerator.Generate(descriptor);

        Assert.True(result.Success);
        Assert.Contains(result.Evidence!.PatchRoles, p => p.Role == SurfaceFeaturePatchRole.HostRetainedPlanarPatch && p.SurfaceFamily == SurfacePatchFamily.Planar);
        Assert.Contains(result.Evidence.PatchRoles, p => p.Role == SurfaceFeaturePatchRole.GrooveWallPatch);
    }

    [Fact]
    public void SurfaceFeatureEvidence_PlanarRoundGroove_TrimRolesPresent()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g3", "base.top_face", SurfacePatchFamily.Planar, SurfaceFeaturePathKind.CircleOnPlane, SurfaceFeatureDirection.Remove, 0.5d, 4d, 0.2d, true);

        var result = SurfaceFeatureEvidenceGenerator.Generate(descriptor);

        Assert.True(result.Success);
        Assert.Contains(result.Evidence!.TrimRoles, t => t.Role == SurfaceFeatureTrimRole.OuterGrooveBoundary && t.CurveFamily == TrimCurveFamily.Circle);
        Assert.Contains(result.Evidence.TrimRoles, t => t.Role == SurfaceFeatureTrimRole.InnerGrooveBoundary && t.CurveFamily == TrimCurveFamily.Circle);
        Assert.Contains(result.Evidence.TrimRoles, t => t.Role == SurfaceFeatureTrimRole.ProfileBoundary && t.Capability == TrimCurveCapability.Deferred);
    }

    [Fact]
    public void SurfaceFeatureEvidence_PlanarRoundGroove_DiagnosticsAreExplicit()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g4", "base.top_face", SurfacePatchFamily.Planar, SurfaceFeaturePathKind.CircleOnPlane, SurfaceFeatureDirection.Remove, 0.5d, 4d, 0.2d, true);

        var result = SurfaceFeatureEvidenceGenerator.Generate(descriptor);

        Assert.Contains(result.Diagnostics, d => d.Contains("a3-dry-run-consumed", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("generic-torus-boolean-not-used", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Evidence!.Diagnostics, d => d.Contains("no-brep-emission", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Evidence.PathEvidence.Diagnostics, d => d.Contains("descriptor-missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SurfaceFeatureEvidence_InvalidDescriptor_FailsFromPlannerOrDryRun()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g5", "base.top_face", SurfacePatchFamily.Planar, SurfaceFeaturePathKind.CircleOnPlane, SurfaceFeatureDirection.Remove, 0d, 3d, -0.1d, true);

        var result = SurfaceFeatureEvidenceGenerator.Generate(descriptor);

        Assert.False(result.Success);
        Assert.Equal(SurfaceFeatureEvidenceStatus.Invalid, result.Status);
        Assert.Null(result.Evidence);
    }

    [Fact]
    public void SurfaceFeatureEvidence_Thread_RemainsForgeDeferred()
    {
        var descriptor = new SurfaceFeatureDescriptor("t1", SurfaceFeatureKind.Thread, "shaft.side_face", SurfacePatchFamily.Cylindrical, SurfaceFeaturePathKind.HelixOnCylinder, SurfaceFeatureProfileKind.VProfile, SurfaceFeatureDirection.Remove, 0.2d, 4d, 0.1d, true, SurfaceFeatureCapabilityTier.Deferred);

        var result = SurfaceFeatureEvidenceGenerator.Generate(descriptor);

        Assert.False(result.Success);
        Assert.Equal(SurfaceFeatureEvidenceStatus.Forge, result.Status);
        Assert.False(result.MaterializationClaimed);
        Assert.Null(result.Evidence);
    }
}
