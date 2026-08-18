using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2ConstructionPlaneHoleSourceTests
{
    [Fact]
    public void Source_ConstructionPlaneBlindDrillPoint_LowersWithUnambiguousDepth()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Hole/valid/construction-plane-blind-drillpoint-shaft-depth.firmament")));
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var hole = Assert.Single(Assert.Single(parsed.Document!.ModifyBlocks!).SemanticHoles);
        Assert.Equal(FirmamentV2SemanticHoleEndKind.ShaftDepth, hole.EndCondition.Kind);
        Assert.Equal(30d, hole.EndCondition.Depth);
        Assert.Equal(FirmamentV2SemanticHoleTerminationKind.DrillPoint, hole.Termination!.Kind);
        Assert.Equal(118d, hole.Termination.PointAngleDegrees);
        var air = Assert.Single(FirmamentV2SemanticHoleLowering.LowerSemanticHoles(parsed.Document));
        Assert.IsType<AirHoleEndCondition.ShaftDepth>(air.EndCondition);
        Assert.IsType<AirHoleTermination.DrillPoint>(air.Termination);
        var materialized = AirHoleSimpleShaftMaterializer.Execute(air, new AirHoleSimpleShaftHost(100, 60, -20, 20));
        Assert.True(materialized.Succeeded, string.Join(" | ", materialized.Diagnostics));
        Assert.Contains(materialized.Correspondence!.Descendants, d => d.Role == SemanticTopologyRole.HoleTipVertex);
    }

    [Fact]
    public void Source_ConstructionPlaneHole_LowersDirectlyAndPublishesSelectableDescendants()
    {
        var parsed = FirmamentV2Parser.Parse(File.ReadAllText(Fixture));
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var hole = Assert.Single(Assert.Single(parsed.Document!.ModifyBlocks!).SemanticHoles);
        var placement = Assert.IsType<FirmamentV2ConstructionPlaneHolePlacement>(hole.Placement);
        Assert.Equal("construction:PositiveXWorkplane", placement.Plane.StableId);
        Assert.Equal((10d, 6d), (placement.Center.U, placement.Center.V));

        var air = Assert.Single(FirmamentV2SemanticHoleLowering.LowerSemanticHoles(parsed.Document));
        Assert.IsType<AirConstructionPlaneHolePlacement>(air.Placement);
        Assert.Null(air.Placement.EntryFaceName);
        var materialized = AirHoleSimpleShaftMaterializer.Execute(air, new AirHoleSimpleShaftHost(100, 60, 0, 12));
        Assert.True(materialized.Succeeded, string.Join(" | ", materialized.Diagnostics));
        Assert.IsType<LocalFrameHoleBRepPlan>(materialized.Plan!.HoleBRepPlan);

        var correspondence = Assert.IsType<SemanticTopologyCorrespondence>(materialized.Correspondence);
        var request = new SemanticSelectionRequest("selection:mouth", "mouth", "Base", ["Base.SideMount"], SemanticTopologyRole.HoleEntryLoop, SemanticSelectionRequirement.ClosedLoop, "fixture");
        var selected = SemanticTopologySelectionResolver.Resolve(materialized.Body!, correspondence, request);
        Assert.True(selected.Succeeded, string.Join(" | ", selected.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.Contains(correspondence.Descendants, d => d.Role == SemanticTopologyRole.HoleExitLoop);
        Assert.Contains(correspondence.Descendants, d => d.Role == SemanticTopologyRole.HoleWallFace);
    }

    [Theory]
    [InlineData("From: MissingWorkplane", FirmamentV2Parser.HoleConstructionPlaneNotFound)]
    [InlineData("From: PositiveXWorkplane\n            On: Base.Top", FirmamentV2Parser.HolePlacementMixed)]
    [InlineData("Center: Point2(10mm, 6mm)\n            Diameter: 8mm\n            End: ThroughAll", FirmamentV2Parser.HoleConstructionPlaneCenterMissing)]
    [InlineData("End: Blind", FirmamentV2Parser.HoleConstructionPlaneExtentUnsupported)]
    [InlineData("From: PositiveXWorkplane\n            From: PositiveXWorkplane", FirmamentV2Parser.HolePlacementDuplicate)]
    public void Source_ConstructionPlaneHole_InvalidPlacementIsTyped(string replacement, string diagnostic)
    {
        var source = File.ReadAllText(Fixture).ReplaceLineEndings("\n");
        if (replacement.StartsWith("Center:", StringComparison.Ordinal)) source = source.Replace("Center: Point2(10mm, 6mm)\n            ", string.Empty, StringComparison.Ordinal);
        else if (replacement.StartsWith("End:", StringComparison.Ordinal)) source = source.Replace("End: ThroughAll", replacement, StringComparison.Ordinal);
        else source = source.Replace("From: PositiveXWorkplane", replacement, StringComparison.Ordinal);
        var result = FirmamentV2Parser.Parse(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(diagnostic, result.Diagnostics);
    }

    [Theory]
    [InlineData("construction-plane-orientation.invalid.firmament", "HoleConstructionPlaneOrientationUnsupported")]
    [InlineData("construction-plane-mouth-misses-host.invalid.firmament", "HoleMouthMissesHost")]
    public void Source_ConstructionPlaneHole_MaterializationBoundaryIsTyped(string fixture, string diagnostic)
    {
        var parsed = FirmamentV2Parser.Parse(File.ReadAllText(InvalidFixture(fixture)));
        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));

        var inspection = SemanticHoleInspection.Inspect(parsed.Document!);

        Assert.False(inspection.Succeeded);
        Assert.Contains(inspection.Diagnostics, message => message.StartsWith(diagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void Source_ConstructionPlaneHole_UnsupportedHostIsTyped()
    {
        var parsed = FirmamentV2Parser.Parse(File.ReadAllText(InvalidFixture("construction-plane-host-unsupported.invalid.firmament")));
        Assert.False(parsed.IsSuccess);
        Assert.Contains(FirmamentV2Parser.HoleConstructionPlaneHostUnsupported, parsed.Diagnostics);
    }

    private static string Fixture => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Hole/valid/construction-plane-through-hole.firmament"));
    private static string InvalidFixture(string name) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Hole/invalid", name));
}
