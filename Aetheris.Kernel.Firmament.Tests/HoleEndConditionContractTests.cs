using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class HoleEndConditionContractTests
{
    [Fact]
    public void ThroughAll_DisconnectedTraversal_IsRejectedRatherThanSelectingAnInterval()
    {
        var feature = ThroughAllFeature();
        var traversal = Evidence(feature, HoleHostTraversalClassification.DisconnectedMaterialIntervals,
            new(0, 20, "compose:slab-a", "Base", true, "Mouth", "AirGap"),
            new(30, 50, "compose:slab-b", "Add", true, "ReEntry", "FarBoundary"));

        var contract = HoleEndConditionContract.Evaluate(feature, traversal);

        Assert.False(contract.ContractSatisfied);
        Assert.Contains(contract.Diagnostics, d => d.StartsWith("HoleHostTraversalDisconnected:", StringComparison.Ordinal));
        Assert.True(contract.IsThroughAll);
    }

    [Fact]
    public void BlindDrillPoint_ReportsRemainingWallFromObservedMaterialSpan()
    {
        var feature = AirHoleFeature.CreateConstructionPlaneSimpleShaft("Blind", "Base.Blind", "Base", Placement(),
            new AirHoleShaft(8), new AirHoleEndCondition.ShaftDepth(30), termination: new AirHoleTermination.DrillPoint());
        var traversal = Evidence(feature, HoleHostTraversalClassification.MultipleContiguousPartitionsOfOneMaterialSpan,
            new(0, 50, "compose:base", "Base", true, "Mouth", "Transition"),
            new(50, 100, "compose:add", "Add", true, "Transition", "FarBoundary"));

        var contract = HoleEndConditionContract.Evaluate(feature, traversal);

        Assert.True(contract.ContractSatisfied);
        Assert.InRange(contract.RemainingWall!.Value, 67.59, 67.60);
        Assert.True(contract.TipInsideMaterial);
        Assert.False(contract.HasExit);
    }

    private static AirHoleFeature ThroughAllFeature() => AirHoleFeature.CreateConstructionPlaneSimpleShaft("Through", "Base.Through", "Base", Placement(), new AirHoleShaft(8), new AirHoleEndCondition.ThroughAll());
    private static AirConstructionPlaneHolePlacement Placement() => new("construction:PositiveX", "concept:Plane", new Point3D(-50, 0, 0),
        Direction3D.Create(new Vector3D(0, 1, 0)), Direction3D.Create(new Vector3D(0, 0, 1)), Direction3D.Create(new Vector3D(1, 0, 0)), 0, 0, "fixture", "test");
    private static HoleHostTraversalEvidence Evidence(AirHoleFeature feature, HoleHostTraversalClassification classification, params HoleHostMaterialIntervalEvidence[] intervals) =>
        new(feature.FeatureId, "Base", "construction:PositiveX", [-50, 0, 0], [1, 0, 0], feature.Shaft.Radius, classification, intervals, []);
}
