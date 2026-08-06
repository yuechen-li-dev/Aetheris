using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class LocalFrameHoleBRepPlanTests
{
    [Fact]
    public void PositiveXConstructionPlane_BlindDrillPoint_IsExactPlanOwnedAndSelectable()
    {
        var placement = new AirConstructionPlaneHolePlacement(
            "construction:PositiveXWorkplane", "concept:SideLayout.PositiveXDatum",
            new Point3D(-50, 0, 0), Direction3D.Create(new Vector3D(0, 1, 0)), Direction3D.Create(new Vector3D(0, 0, 1)), Direction3D.Create(new Vector3D(1, 0, 0)),
            10, 6, "fixture", "test");
        var feature = AirHoleFeature.CreateConstructionPlaneSimpleShaft("PositiveXBlind", "base.PositiveXBlind", "base", placement,
            new AirHoleShaft(8), new AirHoleEndCondition.ShaftDepth(30), termination: new AirHoleTermination.DrillPoint());

        var result = AirHoleSimpleShaftMaterializer.Execute(feature, new AirHoleSimpleShaftHost(100, 60, -20, 20));

        Assert.True(result.Succeeded, string.Join(" | ", result.Diagnostics));
        var plan = Assert.IsType<LocalFrameHoleBRepPlan>(result.Plan!.HoleBRepPlan);
        Assert.Equal(HoleHostTraversalClassification.OneContiguousInterval, plan.TraversalEvidence!.Classification);
        Assert.True(plan.ContractEvidence!.ContractSatisfied);
        Assert.True(plan.ContractEvidence.IsBlind);
        Assert.False(plan.ContractEvidence.HasExit);
        Assert.True(plan.ContractEvidence.RemainingWall > 0d);
        Assert.DoesNotContain(plan.Correspondence.Descendants, d => d.Role == SemanticTopologyRole.HoleExitLoop);
        Assert.Contains(plan.Correspondence.Descendants, d => d.Role == SemanticTopologyRole.HoleShaftToDrillPointLoop);
        Assert.Contains(plan.Correspondence.Descendants, d => d.Role == SemanticTopologyRole.HoleDrillPointFace);
        Assert.Contains(plan.Correspondence.Descendants, d => d.Role == SemanticTopologyRole.HoleTipVertex);
        Assert.Contains(plan.Topology.Surfaces, s => s.Geometry.Cylinder is { } c && Math.Abs(c.Axis.ToVector().X - 1d) < 1e-12 && Math.Abs(c.Radius - 4d) < 1e-12);
        Assert.Contains(plan.Topology.Surfaces, s => s.Geometry.Cone is { } c && Math.Abs(c.Axis.ToVector().X + 1d) < 1e-12 && Math.Abs(c.SemiAngleRadians - 59d * Math.PI / 180d) < 1e-12);

        var transition = new SemanticSelectionRequest("selection:transition", "transition", "base", ["base.PositiveXBlind"], SemanticTopologyRole.HoleShaftToDrillPointLoop, SemanticSelectionRequirement.ClosedLoop, "fixture");
        Assert.True(SemanticTopologySelectionResolver.Resolve(result.Body!, plan.Correspondence, transition).Succeeded);
        var mass = BrepMassProperties.Evaluate(result.Body!);
        Assert.True(mass.IsEnclosed);
        Assert.True(mass.IsOrientationConsistent);
        var step = Step242Exporter.ExportBody(result.Body!);
        Assert.True(step.IsSuccess);
        var reimport = Step242Importer.ImportBody(step.Value!);
        Assert.True(reimport.IsSuccess);
        Assert.Contains(reimport.Value!.Geometry.Surfaces, s => s.Value.Cone is not null);
    }

    [Fact]
    public void PositiveXConstructionPlane_ThroughAll_IsExactAndPlanOwned()
    {
        var placement = new AirConstructionPlaneHolePlacement(
            "construction:PositiveXWorkplane", "concept:SideLayout.PositiveXDatum",
            new Point3D(-50, 0, 0), Direction3D.Create(new Vector3D(0, 1, 0)), Direction3D.Create(new Vector3D(0, 0, 1)), Direction3D.Create(new Vector3D(1, 0, 0)),
            0, 0, "fixture", "test");
        var feature = AirHoleFeature.CreateConstructionPlaneSimpleShaft("PositiveXThrough", "base.PositiveXThrough", "base", placement,
            new AirHoleShaft(8), new AirHoleEndCondition.ThroughAll());

        var result = AirHoleSimpleShaftMaterializer.Execute(feature, new AirHoleSimpleShaftHost(100, 60, -6, 6));

        Assert.True(result.Succeeded, string.Join(" | ", result.Diagnostics));
        var plan = Assert.IsType<LocalFrameHoleBRepPlan>(result.Plan!.HoleBRepPlan);
        Assert.Equal((0d, 100d), plan.HostMaterialInterval);
        Assert.Equal("construction:PositiveXWorkplane", plan.Placement.ConstructionPlaneId);
        Assert.Contains(plan.Topology.Surfaces, s => s.Geometry.Cylinder is { } c && Math.Abs(c.Axis.ToVector().X - 1d) < 1e-12 && Math.Abs(c.Radius - 4d) < 1e-12);
        Assert.Contains(plan.Correspondence.Descendants, d => d.Role == SemanticTopologyRole.HoleEntryLoop);
        Assert.Contains(plan.Correspondence.Descendants, d => d.Role == SemanticTopologyRole.HoleExitLoop);
        Assert.Contains(plan.Correspondence.Descendants, d => d.Role == SemanticTopologyRole.HoleWallFace);

        var mass = BrepMassProperties.Evaluate(result.Body!);
        Assert.True(mass.IsEnclosed);
        Assert.True(mass.IsOrientationConsistent);
        var analyticVolume = 100d * 60d * 12d - Math.PI * 4d * 4d * 100d;
        Assert.True(mass.ErrorBound.HasValue);
        Assert.InRange(Math.Abs(mass.AbsoluteVolume - analyticVolume), 0d, mass.ErrorBound!.Value);

        var step = Step242Exporter.ExportBody(result.Body!);
        Assert.True(step.IsSuccess);
        var reimport = Step242Importer.ImportBody(step.Value!);
        Assert.True(reimport.IsSuccess);
        Assert.Contains(reimport.Value!.Geometry.Surfaces, s => s.Value.Cylinder is { } c && Math.Abs(c.Axis.ToVector().X - 1d) < 1e-12);
    }

    [Fact]
    public void PositiveXConstructionPlane_BlindDrillPoint_BreakthroughIsRejectedWithoutReinterpretation()
    {
        var placement = new AirConstructionPlaneHolePlacement(
            "construction:PositiveXWorkplane", "concept:SideLayout.PositiveXDatum",
            new Point3D(-50, 0, 0), Direction3D.Create(new Vector3D(0, 1, 0)), Direction3D.Create(new Vector3D(0, 0, 1)), Direction3D.Create(new Vector3D(1, 0, 0)),
            0, 0, "fixture", "test");
        var feature = AirHoleFeature.CreateConstructionPlaneSimpleShaft("Breakthrough", "base.Breakthrough", "base", placement,
            new AirHoleShaft(8), new AirHoleEndCondition.TotalDepth(100), termination: new AirHoleTermination.DrillPoint());

        var result = AirHoleSimpleShaftMaterializer.Execute(feature, new AirHoleSimpleShaftHost(100, 60, -6, 6));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, d => d.StartsWith("BlindHoleBreakthrough:", StringComparison.Ordinal));
        Assert.Null(result.Body);
        Assert.Null(result.Correspondence);
    }

    [Fact]
    public void ConstructionPlane_MouthAwayFromBoxBoundary_IsRejectedExplicitly()
    {
        var placement = new AirConstructionPlaneHolePlacement(
            "construction:Bad", "concept:Bad", new Point3D(0, 0, 0),
            Direction3D.Create(new Vector3D(0, 1, 0)), Direction3D.Create(new Vector3D(0, 0, 1)), Direction3D.Create(new Vector3D(1, 0, 0)),
            0, 0, "fixture", "test");
        var feature = AirHoleFeature.CreateConstructionPlaneSimpleShaft("Bad", "base.Bad", "base", placement,
            new AirHoleShaft(8), new AirHoleEndCondition.ThroughAll());

        var result = AirHoleSimpleShaftMaterializer.Execute(feature, new AirHoleSimpleShaftHost(100, 60, -6, 6));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, d => d.StartsWith("HoleDirectionDoesNotEnterHost", StringComparison.Ordinal));
    }
}
