using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2SemanticHoleTests
{
    [Fact]
    public void FirmamentV2SemanticHole_ParsesShaftCounterboreCountersink()
    {
        AssertHole("Hole/valid/hole-x4-shaft-through.valid.firmfixture", FirmamentV2SemanticHoleVariant.Shaft);
        AssertHole("Hole/valid/hole-x4-counterbore-through.valid.firmfixture", FirmamentV2SemanticHoleVariant.Counterbore);
        AssertHole("Hole/valid/hole-x4-countersink-depth.valid.firmfixture", FirmamentV2SemanticHoleVariant.Countersink);
    }

    [Fact]
    public void FirmamentV2SemanticHole_InvalidFixturesProduceDeterministicDiagnostics()
    {
        Assert.Contains(FirmamentV2Parser.HoleVariantUnknown, Parse("Hole/invalid/hole-x4-unknown.invalid.firmfixture").Diagnostics);
        Assert.Contains(FirmamentV2Parser.HoleCounterboreInvalid, Parse("Hole/invalid/hole-x4-bad-counterbore.invalid.firmfixture").Diagnostics);
        Assert.Contains(FirmamentV2Parser.HoleDepthInvalid, Parse("Hole/invalid/hole-x4-negative-depth.invalid.firmfixture").Diagnostics);
    }

    [Fact]
    public void FirmamentV2SemanticHole_LowersThroughAirHoleFeatureAndPreservesProvenance()
    {
        var features = FirmamentV2SemanticHoleLowering.LowerSemanticHoles(Parse("Hole/valid/hole-x4-counterbore-through.valid.firmfixture").Document!);
        var feature = Assert.Single(features);
        Assert.Equal(nameof(AirHoleFeature), feature.Provenance.RouteName);
        Assert.Equal("mount", feature.Name);
        Assert.Equal("base.mount", feature.FeatureId);
        Assert.Equal(AirHoleStackKind.Counterbore, feature.Stack.Kind);
        Assert.Equal([AirHoleStackComponentKind.Counterbore, AirHoleStackComponentKind.Shaft], feature.Stack.Components.Select(c => c.Kind).ToArray());
        Assert.DoesNotContain("ProfileStackExtrudeSpec", feature.Provenance.ConstructionHistoryKind);
    }

    [Theory]
    [InlineData("Hole/valid/hole-x4-shaft-through.valid.firmfixture", "SimpleShaft")]
    [InlineData("Hole/valid/hole-x4-counterbore-through.valid.firmfixture", "Counterbore")]
    [InlineData("Hole/valid/hole-x4-countersink-depth.valid.firmfixture", "Countersink")]
    public void FirmamentV2SemanticHole_MaterializesViaExistingAirHolePath(string fixture, string kind)
    {
        var feature = Assert.Single(FirmamentV2SemanticHoleLowering.LowerSemanticHoles(Parse(fixture).Document!));
        var result = AirHoleSimpleShaftMaterializer.Execute(feature, new AirHoleSimpleShaftHost(100, 60, -6, 6));
        Assert.True(result.Succeeded, string.Join(" | ", result.Diagnostics));
        Assert.Equal(kind, result.Plan!.StackKind.ToString());
        Assert.Equal(nameof(AirHoleFeature), result.Plan.SemanticSourceKind);
        Assert.Contains(result.Diagnostics, d => d.Contains("semantic AirHoleFeature -> simple shaft materialization plan -> ProfileStackExtrudeExecutor", StringComparison.Ordinal));
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
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2", relative));
        return FirmamentV2Parser.Parse(File.ReadAllText(path));
    }
}
