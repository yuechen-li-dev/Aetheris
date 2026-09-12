using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2SemanticHoleTests
{
    [Fact]
    public void CounterboreStockFrame_IsBottomAnchored()
    {
        const string source = """
            Model Mount {
              Units: mm
              Box Body { Size: [60mm,40mm,12mm] }
              Modify Body { Hole<Counterbore> Mount { On: +Z; Center: Point2(0mm,0mm); Diameter: 6mm; CounterboreDiameter: 12mm; CounterboreDepth: 4mm; End: ThroughAll } }
            }
            """;
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Diagnostics));

        var frame = FirmamentStockFrame.ForBox(60, 40, 12);
        var feature = Assert.Single(FirmamentV2SemanticHoleLowering.LowerSemanticHoles(parsed.Document!));
        var materialized = AirHoleSimpleShaftMaterializer.Execute(feature, frame.CreateHoleHost());

        Assert.True(materialized.Succeeded, string.Join(" | ", materialized.Diagnostics));
        Assert.Equal(0d, materialized.Plan!.Host.ZMin, 9);
        Assert.Equal(12d, materialized.Plan.Host.ZMax, 9);
        Assert.Equal("FirmamentBoxBottomAnchoredV2", frame.Authority);
        Assert.Equal(0d, frame.ZMin, 9);
        Assert.Equal(12d, frame.ZMax, 9);
        var z = materialized.Body!.Topology.Vertices.Select(vertex =>
        {
            Assert.True(materialized.Body.TryGetVertexPoint(vertex.Id, out var point));
            return point.Z;
        }).ToArray();
        Assert.Equal(0d, z.Min(), 9);
        Assert.Equal(12d, z.Max(), 9);
    }

    [Fact]
    public void FirmamentV2SemanticHole_ParsesShaftCounterboreCountersink()
    {
        AssertHole("Regression/Hole/valid/hole-x4-shaft-through.valid.firmfixture", FirmamentV2SemanticHoleVariant.Shaft);
        AssertHole("Regression/Hole/valid/hole-x4-counterbore-through.valid.firmfixture", FirmamentV2SemanticHoleVariant.Counterbore);
        AssertHole("Regression/Hole/valid/hole-x4-countersink-depth.valid.firmfixture", FirmamentV2SemanticHoleVariant.Countersink);
    }

    [Fact]
    public void FirmamentV2SemanticHole_InvalidFixturesProduceDeterministicDiagnostics()
    {
        Assert.Contains(FirmamentV2Parser.HoleVariantUnknown, Parse("Compatibility/LegacyAliases/Invalid/Hole/hole-x4-unknown.invalid.firmfixture").Diagnostics);
        Assert.Contains(FirmamentV2Parser.HoleCounterboreInvalid, Parse("Compatibility/LegacyAliases/Invalid/Hole/hole-x4-bad-counterbore.invalid.firmfixture").Diagnostics);
        Assert.Contains(FirmamentV2Parser.HoleDepthInvalid, Parse("Compatibility/LegacyAliases/Invalid/Hole/hole-x4-negative-depth.invalid.firmfixture").Diagnostics);
    }

    [Fact]
    public void FirmamentV2SemanticHole_LowersThroughAirHoleFeatureAndPreservesProvenance()
    {
        var features = FirmamentV2SemanticHoleLowering.LowerSemanticHoles(Parse("Regression/Hole/valid/hole-x4-counterbore-through.valid.firmfixture").Document!);
        var feature = Assert.Single(features);
        Assert.Equal(nameof(AirHoleFeature), feature.Provenance.RouteName);
        Assert.Equal("mount", feature.Name);
        Assert.Equal("base.mount", feature.FeatureId);
        Assert.Equal(AirHoleStackKind.Counterbore, feature.Stack.Kind);
        Assert.Equal([AirHoleStackComponentKind.Counterbore, AirHoleStackComponentKind.Shaft], feature.Stack.Components.Select(c => c.Kind).ToArray());
        Assert.DoesNotContain("ProfileStackExtrudeSpec", feature.Provenance.ConstructionHistoryKind);
        Assert.DoesNotContain("CylinderCut", feature.Provenance.ConstructionHistoryKind);
        Assert.DoesNotContain("ConeCut", feature.Provenance.ConstructionHistoryKind);
    }

    [Theory]
    [InlineData("Regression/Hole/valid/hole-x4-shaft-through.valid.firmfixture", "SimpleShaft")]
    [InlineData("Regression/Hole/valid/hole-x4-counterbore-through.valid.firmfixture", "Counterbore")]
    [InlineData("Regression/Hole/valid/hole-x4-countersink-depth.valid.firmfixture", "Countersink")]
    public void FirmamentV2SemanticHole_MaterializesViaExistingAirHolePath(string fixture, string kind)
    {
        var feature = Assert.Single(FirmamentV2SemanticHoleLowering.LowerSemanticHoles(Parse(fixture).Document!));
        var result = AirHoleSimpleShaftMaterializer.Execute(feature, new AirHoleSimpleShaftHost(100, 60, -6, 6));
        Assert.True(result.Succeeded, string.Join(" | ", result.Diagnostics));
        Assert.Equal(kind, result.Plan!.StackKind.ToString());
        Assert.Equal(nameof(AirHoleFeature), result.Plan.SemanticSourceKind);
        if (kind == "SimpleShaft")
        {
            Assert.Contains(result.Diagnostics, d => d.Contains("semantic AirHoleFeature -> ThroughHoleRecipeRequest -> ThroughHoleConstructionRecipe", StringComparison.Ordinal));
        }
        else
        {
            Assert.Contains(result.Diagnostics, d => d.Contains("semantic AirHoleFeature -> simple shaft materialization plan -> ProfileStackExtrudeExecutor", StringComparison.Ordinal));
        }
    }

    private static void AssertHole(string fixture, FirmamentV2SemanticHoleVariant variant)
    {
        var result = Parse(fixture);
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var hole = Assert.Single(Assert.Single(result.Document!.ModifyBlocks!).SemanticHoles);
        Assert.Equal(variant, hole.Variant);
        Assert.Equal("face(+Z)", hole.EntryFace.Source);
    }

    private static FirmamentV2ParseResult Parse(string relative)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures", relative));
        return FirmamentV2Parser.Parse(File.ReadAllText(path));
    }
}
