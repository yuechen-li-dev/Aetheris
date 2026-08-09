using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SteppedHolePlacementSemanticsTests
{
    [Fact]
    public void SteppedPlan_ProfileSegmentsCarryExplicitPlacement()
    {
        var plan = BuildPlan();

        Assert.Equal(3, plan.ProfileStack.Count);
        Assert.All(plan.ProfileStack, segment =>
        {
            Assert.NotEqual(HoleTierAnchorSide.Unknown, segment.AnchorSide);
            Assert.False(double.IsNaN(segment.ZMin));
            Assert.False(double.IsNaN(segment.ZMax));
            Assert.True(segment.ZMax >= segment.ZMin);
            Assert.NotNull(segment.PlacementDiagnostics);
            Assert.NotEmpty(segment.PlacementDiagnostics!);
        });
    }

    [Fact]
    public void SteppedPlan_TopEntryZSpansMatchFrictionLabRoute()
    {
        var plan = BuildPlan();
        var hostMinZ = plan.HostTranslation.Z - (plan.HostSizeZ * 0.5d);
        var hostMaxZ = plan.HostTranslation.Z + (plan.HostSizeZ * 0.5d);
        var large = plan.ProfileStack[0];
        var medium = plan.ProfileStack[1];
        var small = plan.ProfileStack[2];

        Assert.Equal(HoleTierAnchorSide.Through, small.AnchorSide);
        Assert.True(small.IsThrough);
        Assert.True(small.ZMin <= hostMinZ + 1e-9);
        Assert.True(small.ZMax >= hostMaxZ - 1e-9);

        Assert.Equal(HoleTierAnchorSide.Top, medium.AnchorSide);
        Assert.False(medium.IsThrough);
        Assert.Equal(6d, medium.DepthFromAnchor, 10);
        Assert.Equal(hostMaxZ - medium.DepthFromAnchor, medium.ZMin, 10);
        Assert.Equal(hostMaxZ, medium.ZMax, 10);

        Assert.Equal(HoleTierAnchorSide.Top, large.AnchorSide);
        Assert.False(large.IsThrough);
        Assert.Equal(4d, large.DepthFromAnchor, 10);
        Assert.Equal(hostMaxZ - large.DepthFromAnchor, large.ZMin, 10);
        Assert.Equal(hostMaxZ, large.ZMax, 10);

        Assert.True(small.RadiusStart < medium.RadiusStart && medium.RadiusStart < large.RadiusStart);
    }

    [Fact]
    public void SteppedPlan_PlacementNoLongerMissingPolarity()
    {
        var exec = HoleRecoveryExecutor.Execute(BuildPlan());
        Assert.DoesNotContain(exec.Diagnostics, d => d.Contains("missing-stepped-entry-side-polarity", StringComparison.Ordinal));
        Assert.Contains(exec.Diagnostics, d => d.Contains("no-hidden-placement-inference", StringComparison.Ordinal));
    }

    [Fact]
    public void SteppedExecution_UsesExplicitPlacementRouteInV13_3()
    {
        var exec = HoleRecoveryExecutor.Execute(BuildPlan());
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, exec.Status);
        Assert.NotNull(exec.Body);
        Assert.Contains(exec.Diagnostics, d => d.Contains("stepped executor route: profile-stack-extrude", StringComparison.Ordinal));
        Assert.Contains(exec.Diagnostics, d => d.Contains("No STEP export attempted", StringComparison.Ordinal));
    }

    private static HoleRecoveryPlan BuildPlan()
        => (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped())).Plan!;

    private static CirNode BuildStepped()
    {
        var host = new CirBoxNode(20, 20, 10);
        var small = new CirCylinderNode(2, 20);
        var medium = new CirTransformNode(new CirCylinderNode(3, 6), Transform3D.CreateTranslation(new Vector3D(0, 0, 2)));
        var large = new CirTransformNode(new CirCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, 3)));
        return new CirSubtractNode(new CirSubtractNode(new CirSubtractNode(host, small), medium), large);
    }
}
