using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FrepMaterializerPolicyCatalogTests
{
    [Fact]
    public void Catalog_Default_IncludesThroughHolePolicy()
    {
        var policies = FrepMaterializerPolicyCatalog.Default();
        Assert.Contains(policies, p => p.Name == nameof(ThroughHoleRecoveryPolicy));
    }

    [Fact]
    public void Catalog_Default_OrderIsDeterministic()
    {
        var first = FrepMaterializerPolicyCatalog.Default().Select(p => p.Name).ToArray();
        var second = FrepMaterializerPolicyCatalog.Default().Select(p => p.Name).ToArray();
        Assert.Equal(first, second);
    }

    [Fact]
    public void Planner_WithDefaultCatalog_SelectsThroughHoleForBoxCylinder()
    {
        var context = new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20, 10, 8), new CirCylinderNode(2, 20)));
        var decision = FrepMaterializerPlanner.Decide(context, FrepMaterializerPolicyCatalog.Default());

        Assert.Equal(FrepMaterializerDecisionStatus.Selected, decision.Status);
        Assert.Equal(nameof(ThroughHoleRecoveryPolicy), decision.SelectedPolicyName);
    }

    [Fact]
    public void Planner_WithDefaultCatalog_UsesFallbackForUnsupported()
    {
        var context = new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(2)));
        var decision = FrepMaterializerPlanner.Decide(context, FrepMaterializerPolicyCatalog.Default());

        Assert.Equal(FrepMaterializerDecisionStatus.Selected, decision.Status);
        Assert.Equal(nameof(CirOnlyFallbackPolicy), decision.SelectedPolicyName);
        Assert.Equal(FrepMaterializerCapability.CirOnly, decision.Capability);
    }

    [Fact]
    public void Catalog_Snapshot_ReportsPolicyNamesAndOrder()
    {
        var snapshot = FrepMaterializerPolicyCatalog.SnapshotDefault();

        Assert.Equal(new[] { nameof(ThroughHoleRecoveryPolicy), nameof(CirOnlyFallbackPolicy) }, snapshot.PolicyNames);
        Assert.Contains(snapshot.Diagnostics, d => d.Contains("default catalog built", StringComparison.Ordinal));
    }
}
