using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentHeliosAuthoringFeedbackTests
{
    private static string Source(string name) => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(
        $"fixtures/Regression/LanguageService/{name}.firmament"));

    [Theory]
    [InlineData("Shaft", "")]
    [InlineData("Counterbore", "            counterboreDiameter: 14mm\n            counterboreDepth: 3mm\n")]
    [InlineData("Countersink", "            countersinkDiameter: 14mm\n            countersinkAngle: 90deg\n")]
    public void HoleCompletionAndProjectionExposeOnlyItsAdmittedVariant(string variant, string relief)
    {
        var source = Source("hole-variant").Replace("<Shaft>", $"<{variant}>").Replace("            end:", relief + "            end:");
        var offset = source.IndexOf("            on:", StringComparison.Ordinal);
        var completion = FirmamentLanguageService.Complete(source, "bracket.firmament", "1", offset);
        Assert.Equal("Hole", completion.Context);
        Assert.Equal(variant == "Counterbore", completion.Fields.Any(f => f.Name == "CounterboreDepth"));
        Assert.Equal(variant == "Countersink", completion.Fields.Any(f => f.Name == "CountersinkAngle"));
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.NotNull(parsed.Document);
        var hole = Assert.Single(FirmamentFieldProjector.Project(parsed.Document, source, "bracket.firmament", 1), p => p.SemanticId == "Base.CenterMount");
        Assert.Equal(variant == "Counterbore", hole.Fields.Any(f => f.Name == "CounterboreDepth"));
        Assert.Equal(variant == "Countersink", hole.Fields.Any(f => f.Name == "CountersinkAngle"));
        var compiled = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(compiled.IsSuccess, string.Join(';', compiled.Diagnostics.Select(d => d.Message)));
    }

    [Fact]
    public void ShaftReliefFieldsRejectAtTheirSpansWithoutAMisleadingModelError()
    {
        var source = Source("hole-variant").Replace("            end:", "            counterboreDepth: 3mm\n            counterboreDiameter: 14mm\n            end:");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.Null(parsed.Document);
        Assert.DoesNotContain(parsed.Diagnostics, d => d == FirmamentV2Parser.CanonicalDocumentMalformed);
        var analysis = FirmamentLanguageAnalysisService.Analyze(source, "bracket.firmament", "1");
        foreach (var field in new[] { "counterboreDepth", "counterboreDiameter" })
        {
            var diagnostic = Assert.Single(analysis.Diagnostics, d => d.Code.EndsWith(":" + char.ToUpperInvariant(field[0]) + field[1..], StringComparison.Ordinal));
            Assert.Equal(source.IndexOf(field, StringComparison.Ordinal), diagnostic.Start);
        }
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(build.IsSuccess);
        Assert.DoesNotContain(build.Diagnostics, d => d.Message == "firmament-v2-parse-failed");
    }

    [Fact]
    public void EmptyModifyBindsItsTargetAndBuildsExactlyTheUnmodifiedStock()
    {
        var source = Source("empty-modify");
        var actual = FirmamentBuildAndExport.CompileSource(source);
        var stock = FirmamentBuildAndExport.CompileSource(source.Replace("    Modify Base {}", ""));
        Assert.True(actual.IsSuccess, string.Join(';', actual.Diagnostics.Select(d => d.Message)));
        Assert.True(stock.IsSuccess);
        Assert.Equal(stock.Value.StepText, actual.Value.StepText);
        Assert.False(FirmamentBuildAndExport.CompileSource(source.Replace("Modify Base", "Modify Missing")).IsSuccess);
        Assert.DoesNotContain(FirmamentLanguageAnalysisService.Analyze(source, "bracket.firmament", "1").Diagnostics, d => d.Severity == "error");
    }
}
