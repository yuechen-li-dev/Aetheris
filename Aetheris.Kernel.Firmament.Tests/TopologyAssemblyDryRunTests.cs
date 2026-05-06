using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class TopologyAssemblyDryRunTests
{
    [Fact]
    public void TopologyDryRun_BoxMinusCylinder_PlansFacesAndLoops()
    {
        var result = TopologyAssemblyDryRunPlanner.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.PlannedFaces);
        Assert.Contains(result.PlannedFaces, p => p.SurfaceFamily == SurfacePatchFamily.Cylindrical);
        Assert.Contains(result.PlannedFaces.SelectMany(p => p.LoopGroups), g => g.Readiness is TopologyAssemblyReadiness.ExactPlanReady or TopologyAssemblyReadiness.SpecialCasePlanReady or TopologyAssemblyReadiness.Deferred);
        Assert.False(result.TopologyEmissionImplemented);
    }

    [Fact]
    public void TopologyDryRun_BoxMinusSphere_PlansSphericalPatchWithoutUnsupported()
    {
        var result = TopologyAssemblyDryRunPlanner.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(3)));

        Assert.Contains(result.PlannedFaces, p => p.SurfaceFamily == SurfacePatchFamily.Spherical);
        Assert.Contains(result.PlannedFaces.Where(p => p.SurfaceFamily == SurfacePatchFamily.Spherical).SelectMany(p => p.LoopGroups.SelectMany(g => g.LoopDescriptors)), l => l.TrimCurveFamily == TrimCurveFamily.Circle);
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("generic unsupported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TopologyDryRun_BoxMinusTorus_DefersDueTrimLoops()
    {
        var result = TopologyAssemblyDryRunPlanner.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirTorusNode(4, 1)));

        Assert.Contains(result.PlannedFaces, p => p.SurfaceFamily == SurfacePatchFamily.Toroidal);
        Assert.Equal(TopologyAssemblyReadiness.Deferred, result.Readiness);
        Assert.Contains(result.Diagnostics, d => d.Contains("quartic/algebraic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TopologyDryRun_NonSubtract_NotApplicable()
    {
        var result = TopologyAssemblyDryRunPlanner.Generate(new CirUnionNode(new CirBoxNode(10, 10, 10), new CirSphereNode(2)));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopologyAssemblyReadiness.NotApplicable, result.Readiness);
        Assert.Empty(result.PlannedFaces);
    }

    [Fact]
    public void TopologyDryRun_DeterministicOrdering()
    {
        var node = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var first = TopologyAssemblyDryRunPlanner.Generate(node);
        var second = TopologyAssemblyDryRunPlanner.Generate(node);

        Assert.Equal(first.PlannedFaces.Select(p => p.OrderingKey), second.PlannedFaces.Select(p => p.OrderingKey));
        Assert.Equal(first.PlannedFaces.SelectMany(p => p.LoopGroups.Select(l => l.OrderingKey)), second.PlannedFaces.SelectMany(p => p.LoopGroups.Select(l => l.OrderingKey)));
    }

    [Fact]
    public void TopologyDryRun_DoesNotEmitBRepTopology()
    {
        var result = TopologyAssemblyDryRunPlanner.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));

        Assert.False(result.TopologyEmissionImplemented);
        Assert.Contains(result.Diagnostics, d => d.Contains("topology-emission-not-implemented", StringComparison.Ordinal));
    }
}
