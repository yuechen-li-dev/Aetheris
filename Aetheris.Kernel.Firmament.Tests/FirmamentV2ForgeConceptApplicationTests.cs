using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2ForgeConceptApplicationTests
{
    [Fact]
    public void FirmamentV2ForgeConceptApplication_ParsesAndBindsProcessAndHole()
    {
        var result = FirmamentV2Parser.Parse(Source("Language/valid/concept-applications-forge.valid.firmfixture"));

        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        var document = result.Document!;
        var process = Assert.Single(document.ManufacturingConcepts!);
        Assert.Equal("process", process.Application.FamilyName);
        Assert.Equal("CNC", process.Application.ConceptName);
        Assert.Equal(["material", "minimumToolRadius"], process.Fields.Select(f => f.Name).ToArray());
        Assert.True(FirmamentV2ForgeConceptRegistry.TryGet("process", "CNC", out _));
        Assert.DoesNotContain(FirmamentV2Parser.ConceptMissingRequiredField, result.Diagnostics);

        var feature = Assert.Single(document.FeatureConcepts!);
        Assert.Equal("mountHole", feature.Name);
        Assert.Equal("hole", feature.Application.FamilyName);
        Assert.Equal("Countersink", feature.Application.ConceptName);
        Assert.Equal(["target", "diameter", "countersinkDiameter", "angle"], feature.Fields.Select(f => f.Name).ToArray());
        Assert.Equal("part.region(\"mountHoleA\")", feature.BoundFields!.Single(f => f.Name == "target").TargetSource);
        Assert.NotNull(feature.BoundFields!.Single(f => f.Name == "diameter").BoundValue!.AliasTolerance);
        Assert.NotNull(feature.BoundFields!.Single(f => f.Name == "countersinkDiameter").BoundValue!.AliasTolerance);
    }

    [Theory]
    [InlineData("manufacturing banana<CNC> {\n material: \"Aluminum6061\"\n}", FirmamentV2Parser.ConceptUnknownFamily)]
    [InlineData("manufacturing process<LaserGoblin> {\n material: \"Aluminum6061\"\n minimumToolRadius: 1.5mm\n}", FirmamentV2Parser.ConceptUnknownConcept)]
    [InlineData("manufacturing process<CNC> {\n material: \"Aluminum6061\"\n}", FirmamentV2Parser.ConceptMissingRequiredField)]
    [InlineData("manufacturing process<CNC> {\n material: \"Aluminum6061\"\n material: \"Steel\"\n minimumToolRadius: 1.5mm\n}", FirmamentV2Parser.ConceptDuplicateField)]
    [InlineData("manufacturing process<CNC> {\n material: \"Aluminum6061\"\n minimumToolRadius: 1.5\n}", FirmamentV2Parser.ConceptFieldTypeMismatch)]
    [InlineData("feature mountHole: hole<Countersink> {\n target: part.region(\"mountHoleA\")\n diameter: 6.0mm\n countersinkDiameter: 10.0mm\n angle: 90mm\n}", FirmamentV2Parser.ConceptFieldTypeMismatch)]
    public void FirmamentV2ForgeConceptApplication_InvalidConceptsProduceDeterministicDiagnostics(string conceptSource, string diagnostic)
    {
        var source = $$"""
        model InvalidConceptProbe {
            units mm
            solid part: Box { size: [20, 20, 6] }
            {{conceptSource}}
        }
        """;

        var result = FirmamentV2Parser.Parse(source);

        Assert.False(result.IsSuccess);
        Assert.Contains(diagnostic, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2ForgeConceptApplication_DoesNotLowerToDfmPmiOrGeometry()
    {
        var result = FirmamentV2Parser.Parse(Source("Language/valid/concept-applications-forge.valid.firmfixture"));

        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        Assert.Empty(result.Document!.Templates!);
        Assert.Empty(result.Document.Pmi!);
        Assert.Empty(result.Document.ModifyBlocks!);
        Assert.Single(result.Document.Solids);
    }

    private static string Source(string relative) => File.ReadAllText(Path.Combine(FindRepoRoot(), "fixtures/FirmamentV2", relative));

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aetheris.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root.");
    }
}
