using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentEditorExperienceTests
{
    private static string Fixture(string path = "fixtures/Language/Editor/parameterized-plate.firmament") =>
        File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(path)).ReplaceLineEndings("\n");
    private static FirmamentLanguageCompletion Complete(string source, int? offset = null) => FirmamentLanguageService.Complete(source, "editor.firmament", "1", offset ?? source.Length);

    [Fact]
    public void EmptyDocumentOffersAnExecutableModelTemplateWithANameTabStop()
    {
        var entry = Assert.Single(Complete("mo").Entries!);
        Assert.Equal("Model", entry.Name);
        Assert.Contains("${1:part}", entry.Snippet);
        Assert.True(FirmamentBuildAndExport.CompileSource(entry.Source).IsSuccess);
        var garment = Assert.Single(Complete("Gar").Entries!);
        Assert.Contains("${1:Outfit}", garment.Snippet);
    }

    [Theory]
    [InlineData("Cylinder")]
    [InlineData("Cone")]
    [InlineData("Frustum")]
    [InlineData("Sphere")]
    [InlineData("Torus")]
    [InlineData("RoundedBox")]
    public void NewPrimitiveEntriesBuildThroughTheirExistingGeometryOwners(string name)
    {
        var entry = FirmamentSemanticSchemas.Get(name)!.Entry!;
        var build = FirmamentBuildAndExport.CompileSource("Model M { units: mm " + entry + " }");
        Assert.True(build.IsSuccess, string.Join(';', build.Diagnostics.Select(d => d.Message)));
    }

    [Theory]
    [InlineData("01-plate")]
    [InlineData("02-bracket-profile")]
    [InlineData("03-machined-block")]
    [InlineData("04-plate-family")]
    [InlineData("05-checked-plate")]
    public void FormatterKeepsCurrentLessonGeometryAndIsIdempotent(string name)
    {
        var source = Fixture("fixtures/Canonical/Learn/" + name + ".firmament");
        var formatted = FirmamentLanguageAnalysisService.Format(source, "lesson.firmament", "1");
        Assert.Equal(formatted.Text, FirmamentLanguageAnalysisService.Format(formatted.Text, "lesson.firmament", "2").Text);
        var before = FirmamentBuildAndExport.CompileSource(source);
        var after = FirmamentBuildAndExport.CompileSource(formatted.Text);
        Assert.True(before.IsSuccess, string.Join(';', before.Diagnostics.Select(d => d.Message)));
        Assert.True(after.IsSuccess, string.Join(';', after.Diagnostics.Select(d => d.Message)));
        Assert.Equal(before.Value!.StepText, after.Value!.StepText);
    }

    [Theory]
    [InlineData("Cylinder", "radius")]
    [InlineData("Cone", "bottomRadius")]
    [InlineData("Frustum", "topRadius")]
    [InlineData("Sphere", "radius")]
    [InlineData("Torus", "majorRadius")]
    [InlineData("RoundedBox", "cornerRadius")]
    [InlineData("Circle2", "diameter")]
    [InlineData("Rib", "thickness")]
    public void AllRegisteredConstructsUseTheirActualOwnerFields(string name, string field)
    {
        var source = FirmamentSourceSpelling.Prefer(FirmamentSemanticSchemas.Get(name)!.Entry!);
        var offset = source.IndexOf(field + ":", StringComparison.Ordinal) + Math.Min(2, field.Length);
        Assert.True(offset >= 2, source);
        var result = Complete(source, offset);
        var found = Assert.Single(result.Fields);
        Assert.Equal(field, found.Spelling);
        Assert.Equal(field, source.Substring(result.ReplaceStart, result.ReplaceLength));
        Assert.True(found.Required || name == "Circle2");
    }

    [Fact]
    public void ValuesAndFieldsAreSeparateEvenOnOneLineOrAfterAnUnfinishedColon()
    {
        var source = Fixture();
        var units = source.IndexOf("units: ", StringComparison.Ordinal) + "units: ".Length;
        Assert.Equal(["mm"], Complete(source[..units]).Values);
        Assert.Empty(Complete(source[..units]).Fields);
        var end = source.IndexOf("end: ", StringComparison.Ordinal) + "end: ".Length;
        Assert.Contains("ThroughAll", Complete(source, end).Values!);
        Assert.Empty(Complete(source, end).Fields);
    }

    [Fact]
    public void CommentsAndStringsNeverOfferConstructsAndBracesDoNotLeakScope()
    {
        var source = Fixture();
        var comment = source.IndexOf("radius:", StringComparison.Ordinal);
        Assert.Equal("Literal", Complete(source, comment + 3).Context);
        var body = source.IndexOf("  Box", StringComparison.Ordinal);
        var entries = Complete(source, body).Entries!;
        Assert.Contains(entries, e => e.Name == "Cylinder");
        Assert.DoesNotContain(entries, e => e.Name is "Garment" or "Room" or "Fabric" or "Hole" or "Model");
        var literal = "\"brace } radius: ";
        Assert.Equal("Literal", Complete(literal).Context);
    }

    [Fact]
    public void ModifyEntriesInsertTheFeatureInsteadOfAnotherModifyWrapper()
    {
        var source = Fixture();
        var offset = source.IndexOf("    mountHole", StringComparison.Ordinal);
        var entry = Assert.Single(Complete(source, offset).Entries!, e => e.Name == "Hole");
        Assert.StartsWith("Hole<Shaft>", entry.InsertText);
        Assert.DoesNotContain("Modify", entry.InsertText);
        Assert.Contains("${1:H}", entry.Snippet);
    }

    [Fact]
    public void ValueCompletionAndDefinitionsIncludeLocalParametersAndReusableFeatures()
    {
        var source = Fixture();
        var thickness = source.LastIndexOf("thickness", StringComparison.Ordinal);
        Assert.Contains(Complete(source, thickness + 3).Symbols!, s => s.Name == "thickness" && s.Detail == "Static: Length");
        var parameter = source.IndexOf("center: center", StringComparison.Ordinal) + "center: ".Length;
        Assert.Contains(Complete(source, parameter + 3).Symbols!, s => s.Name == "center" && s.Kind == "variable");
        Assert.Equal(source.IndexOf("center:", StringComparison.Ordinal), FirmamentLanguageAnalysisService.Definition(source, "editor.firmament", parameter + 2)!.Start);
        Assert.Contains("Point2", FirmamentLanguageAnalysisService.Hover(source, "editor.firmament", "1", parameter + 2)!.Description);
        var call = source.LastIndexOf("mountHole", StringComparison.Ordinal);
        Assert.Contains(Complete(source, call + 5).Symbols!, s => s.Name == "mountHole" && s.Kind == "function");
        Assert.Equal(source.IndexOf("mountHole", StringComparison.Ordinal), FirmamentLanguageAnalysisService.Definition(source, "editor.firmament", call + 2)!.Start);
    }

    [Fact]
    public void ValidDeclarationModulesAndAssembliesUseTheConservativeFormattingPass()
    {
        var source = Fixture();
        var module = source[..source.IndexOf("Model ", StringComparison.Ordinal)];
        foreach (var text in new[] { module, Fixture("fixtures/Canonical/Learn/06-post-on-plate.firmament") })
        {
            var formatted = FirmamentLanguageAnalysisService.Format(text, "module.firmament", "1");
            Assert.Equal(FirmamentSourceSpelling.Normalize(text), FirmamentSourceSpelling.Normalize(formatted.Text));
            Assert.Equal(formatted.Text, FirmamentLanguageAnalysisService.Format(formatted.Text, "module.firmament", "2").Text);
        }
    }

    [Fact]
    public void CurrentKeywordsHoverAndFormattingPreserveTheParameterizedPart()
    {
        var source = Fixture();
        var analysis = FirmamentLanguageAnalysisService.Analyze(source, "editor.firmament", "1");
        Assert.DoesNotContain(analysis.Diagnostics, d => d.Severity == "error");
        Assert.Contains(analysis.Tokens, t => t.Kind == "keyword" && source.Substring(t.Start, t.Length) == "Feature");
        var on = source.IndexOf("on:", StringComparison.Ordinal);
        Assert.Contains("Entry face", FirmamentLanguageAnalysisService.Hover(source, "editor.firmament", "1", on + 1)!.Description);
        var formatted = FirmamentLanguageAnalysisService.Format(source, "editor.firmament", "1");
        Assert.Equal(formatted.Text, FirmamentLanguageAnalysisService.Format(formatted.Text, "editor.firmament", "2").Text);
        var before = FirmamentBuildAndExport.CompileSource(source);
        var after = FirmamentBuildAndExport.CompileSource(formatted.Text);
        Assert.True(before.IsSuccess, string.Join(';', before.Diagnostics.Select(d => d.Message)));
        Assert.True(after.IsSuccess, string.Join(';', after.Diagnostics.Select(d => d.Message)));
        Assert.Equal(before.Value!.StepText, after.Value!.StepText);
    }
}
