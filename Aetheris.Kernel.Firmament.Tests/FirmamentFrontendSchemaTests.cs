using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentFrontendSchemaTests
{
    [Fact]
    public void ExplicitWireFormHeaderSelectsAndErasesOnlyTheHeader()
    {
        var selection = FirmamentFrontendSchemas.Select("// witness\nschema WireForm\nModel W { Units: mm WireForm W { Diameter: 1mm } }");
        Assert.True(selection.IsSuccess);
        Assert.Equal(FirmamentFrontendSchema.WireForm, selection.Schema);
        Assert.DoesNotContain("schema WireForm", selection.Source, StringComparison.Ordinal);
        Assert.True(Materializer.WireFormAuthoring.IsWireFormSource(selection.Source));
    }

    [Theory]
    [InlineData("schema Unknown", "firmament-schema-unknown:Unknown")]
    [InlineData("schema WireForm\nschema Sweep", "firmament-schema-duplicate")]
    [InlineData("Model Part { Units: mm }\nschema Mechanical", "firmament-schema-late")]
    [InlineData("schema Mechanical\nModel W { Units: mm WireForm W { Diameter: 1mm } }", "firmament-schema-mismatch:declared=Mechanical:source=WireForm")]
    public void InvalidHeadersFailAtTheFrontendBoundary(string source, string diagnostic)
    {
        var selection = FirmamentFrontendSchemas.Select(source);
        Assert.False(selection.IsSuccess);
        Assert.Contains(diagnostic, selection.Diagnostics.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void LegacySchemaMappingIsNotMisreadAsAFrontendHeader()
    {
        var selection = FirmamentFrontendSchemas.Select("schema:\n  process: CNC");
        Assert.True(selection.IsSuccess);
        Assert.False(selection.IsExplicit);
    }
}
