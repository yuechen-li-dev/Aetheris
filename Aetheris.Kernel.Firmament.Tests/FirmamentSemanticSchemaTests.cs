using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentSemanticSchemaTests
{
    [Fact]
    public void GeneratedRegistry_IsStableOptInAndCoversFirstConstructs()
    {
        Assert.Equal("firmament-semantic-schema/1", FirmamentSemanticSchemas.Version);
        Assert.Equal(["Box", "Concept", "Helix", "Hole", "Loft"], FirmamentSemanticSchemas.All.Select(s => s.Name));
        var hole = Assert.IsType<FirmamentConstructSchema>(FirmamentSemanticSchemas.Get("Hole"));
        var diameter = Assert.Single(hole.Fields, f => f.Name == "Diameter");
        Assert.Equal("Hole.Diameter", diameter.Id.Value);
        Assert.Equal(FirmamentSchemaValueKind.Length, diameter.Kind);
        Assert.Equal(FirmamentUnitKind.Length, diameter.Unit);
        Assert.True(diameter.Required);
        var wall = Assert.Single(hole.Outputs);
        Assert.Equal("Hole.Wall", wall.Id.Value);
        Assert.Equal("HoleWallFace", wall.SourceRole);
        Assert.True(wall.SourceAddressable);
    }

    [Fact]
    public void ExistingLanguageHelp_ConsumesGeneratedHelixAndLoftFields()
    {
        var helix = FirmamentSemanticSchemas.Get("Helix")!;
        Assert.Equal("WireForm", helix.Context);
        Assert.Equal("AxisCoil", helix.CompatibilityAlias);
        Assert.Equal(helix.Fields.Select(f => f.Name), FirmamentSchemaAuthoringFields.For("Helix").Select(f => f.Name));
        Assert.Equal(helix.Fields.Select(f => f.Name), LoftOrHelixCompletion("Model M { WireForm Spring { Helix Winding {\n ", "Helix").Fields.Select(f => f.Name));

        var loft = FirmamentSemanticSchemas.Get("Loft")!;
        Assert.Contains("Loft Body", loft.Entry);
        Assert.Equal(loft.Fields.Select(f => f.Name), LoftAuthoringParser.SolidFields.Select(f => f.Name));
        Assert.Equal(loft.Fields.Select(f => f.Name), LoftOrHelixCompletion("Model M { Loft Body {\n ", "Loft").Fields.Select(f => f.Name));
    }

    [Fact]
    public void ConceptAndBox_KeepTheirSemanticBoundaries()
    {
        Assert.Empty(FirmamentSemanticSchemas.Get("Concept")!.Fields);
        var box = FirmamentSemanticSchemas.Get("Box")!;
        Assert.Contains(box.Fields, f => f.Name == "Size" && f.Kind == FirmamentSchemaValueKind.Vector && f.Unit == FirmamentUnitKind.Length);
        Assert.Contains(box.Fields, f => f.Name == "Bounds" && f.Kind == FirmamentSchemaValueKind.Box3);
        Assert.All(box.Fields, f => Assert.False(f.Required)); // Size or Bounds is required, not either field alone.
    }

    [Theory]
    [InlineData("Helix", "WireForm Spring")]
    [InlineData("Loft", "Construction Plane LowerFrame")]
    public void BareConstructSearchReturnsGeneratedLegalEntry(string name, string requiredContext)
    {
        var source = $"Model Draft {{ Units: mm\n {name}";
        var completion = FirmamentLanguageService.Complete(source, "draft.firmament", "1", source.Length);
        Assert.Equal("ConstructEntry", completion.Context);
        var entry = Assert.Single(completion.Entries!);
        Assert.Equal(name, entry.Name);
        Assert.Contains(requiredContext, entry.Source);
        Assert.Equal(FirmamentSemanticSchemas.Get(name)!.Entry, entry.Source);
    }

    [Theory]
    [InlineData("Helix")]
    [InlineData("Loft")]
    public void GeneratedEntryIsBuildable(string name)
    {
        var result = FirmamentBuildAndExport.CompileSource(FirmamentSemanticSchemas.Get(name)!.Entry!);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(item => item.Message)));
    }

    private static FirmamentLanguageCompletion LoftOrHelixCompletion(string source, string expected)
    {
        var result = FirmamentLanguageService.Complete(source, "draft.firmament", "1", source.Length);
        Assert.Equal(expected, result.Context);
        return result;
    }
}
