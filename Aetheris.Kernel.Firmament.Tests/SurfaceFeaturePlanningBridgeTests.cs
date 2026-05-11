using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfaceFeaturePlanningBridgeTests
{
    [Fact]
    public void SurfaceFeaturePlan_PlanarRoundGroove_ProducesPlanningArtifacts()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g1", "base.top_face", SurfacePatchFamily.Planar, SurfaceFeaturePathKind.CircleOnPlane, SurfaceFeatureDirection.Remove, 1.2d, 8d, 0.5d, true);
        var result = SurfaceFeaturePlanner.Plan(descriptor);

        Assert.Equal(SurfaceFeaturePlanningStatus.Planned, result.Status);
        Assert.Equal(SurfacePatchFamily.Planar, result.HostRequirements!.HostSurfaceFamily);
        Assert.Equal(SurfaceFeaturePathKind.CircleOnPlane, result.PathRequirements!.PathKind);
        Assert.Equal(SurfaceFeatureProfileKind.CircularArc, result.ProfileRequirements!.ProfileKind);
        Assert.Contains(result.RequiredTrimCurveFamilies, t => t.CurveFamily == TrimCurveFamily.Circle);
        Assert.False(result.MaterializationClaimed);
    }

    [Fact]
    public void SurfaceFeaturePlan_CylindricalRoundGroove_ProducesPlanningArtifacts()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g2", "shaft.side_face", SurfacePatchFamily.Cylindrical, SurfaceFeaturePathKind.CircumferentialOnCylinder, SurfaceFeatureDirection.Remove, 0.6d, 4d, 0.4d, true);
        var result = SurfaceFeaturePlanner.Plan(descriptor);

        Assert.Equal(SurfaceFeaturePlanningStatus.Planned, result.Status);
        Assert.Equal(SurfacePatchFamily.Cylindrical, result.HostRequirements!.HostSurfaceFamily);
        Assert.Equal(SurfaceFeaturePathKind.CircumferentialOnCylinder, result.PathRequirements!.PathKind);
        Assert.Contains(result.PathRequirements.Diagnostics, d => d.Contains("coaxial", StringComparison.OrdinalIgnoreCase));
        Assert.False(result.MaterializationClaimed);
    }

    [Fact]
    public void SurfaceFeaturePlan_RidgeBead_AdditivePlanning()
    {
        var descriptor = new SurfaceFeatureDescriptor("r1", SurfaceFeatureKind.Bead, "shaft.side_face", SurfacePatchFamily.Cylindrical, SurfaceFeaturePathKind.CircumferentialOnCylinder, SurfaceFeatureProfileKind.CircularArc, SurfaceFeatureDirection.Add, 0.4d, 5d, 0.3d, true, SurfaceFeatureCapabilityTier.CoreExact);
        var result = SurfaceFeaturePlanner.Plan(descriptor);

        Assert.Equal(SurfaceFeaturePlanningStatus.Planned, result.Status);
        Assert.Equal(SurfaceFeatureKind.Ridge, result.FeatureKind);
        Assert.Contains(result.ExpectedPatchFamilies, p => p.Role.Contains("additive", StringComparison.OrdinalIgnoreCase));
        Assert.False(result.MaterializationClaimed);
    }

    [Fact]
    public void SurfaceFeaturePlan_Thread_RoutesToForgeOrDeferred()
    {
        var descriptor = new SurfaceFeatureDescriptor("t1", SurfaceFeatureKind.Thread, "shaft.side_face", SurfacePatchFamily.Cylindrical, SurfaceFeaturePathKind.HelixOnCylinder, SurfaceFeatureProfileKind.VProfile, SurfaceFeatureDirection.Remove, 0.2d, 4d, 0.1d, true, SurfaceFeatureCapabilityTier.Deferred);
        var result = SurfaceFeaturePlanner.Plan(descriptor);

        Assert.Equal(SurfaceFeaturePlanningStatus.Forge, result.Status);
        Assert.False(result.MaterializationClaimed);
        Assert.Contains(result.Diagnostics, d => d.Contains("helix", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SurfaceFeaturePlan_GenericTorusBoolean_Rejected()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g3", "body.surface", SurfacePatchFamily.Toroidal, SurfaceFeaturePathKind.CurveOnSurface, SurfaceFeatureDirection.Remove, 0.2d, 2d, 0.1d, true);
        var result = SurfaceFeaturePlanner.Plan(descriptor);

        Assert.Equal(SurfaceFeaturePlanningStatus.Unsupported, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Contains("Generic torus Boolean", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SurfaceFeaturePlan_InvalidDescriptor_ReturnsInvalidPlan()
    {
        var descriptor = new RoundGrooveFeatureDescriptor("g4", "base.top_face", SurfacePatchFamily.Planar, SurfaceFeaturePathKind.CircleOnPlane, SurfaceFeatureDirection.Remove, 0d, 3d, -1d, true);
        var result = SurfaceFeaturePlanner.Plan(descriptor);

        Assert.Equal(SurfaceFeaturePlanningStatus.Invalid, result.Status);
        Assert.False(result.MaterializationClaimed);
    }

    [Fact]
    public void SurfaceFeaturePlan_DoesNotClaimMaterialization()
    {
        var descriptors = new SurfaceFeatureDescriptor[]
        {
            new RoundGrooveFeatureDescriptor("g1", "base.top_face", SurfacePatchFamily.Planar, SurfaceFeaturePathKind.CircleOnPlane, SurfaceFeatureDirection.Remove, 1.2d, 8d, 0.5d, true),
            new RoundGrooveFeatureDescriptor("g2", "shaft.side_face", SurfacePatchFamily.Cylindrical, SurfaceFeaturePathKind.CircumferentialOnCylinder, SurfaceFeatureDirection.Remove, 0.6d, 4d, 0.4d, true),
            new SurfaceFeatureDescriptor("r1", SurfaceFeatureKind.Bead, "shaft.side_face", SurfacePatchFamily.Cylindrical, SurfaceFeaturePathKind.CircumferentialOnCylinder, SurfaceFeatureProfileKind.CircularArc, SurfaceFeatureDirection.Add, 0.4d, 5d, 0.3d, true, SurfaceFeatureCapabilityTier.CoreExact)
        };

        var results = descriptors.Select(SurfaceFeaturePlanner.Plan).ToArray();
        Assert.All(results, plan => Assert.False(plan.MaterializationClaimed));
    }
}
