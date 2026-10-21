using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentDirectiveAndCasingTests
{
    [Theory]
    [InlineData("ignored-include", "firmament-v2-directive-unconsumed")]
    [InlineData("malformed-include", "firmament-v2-directive-malformed")]
    [InlineData("schema-inline-unknown", "firmament-schema-unknown")]
    [InlineData("schema-comment-unknown", "firmament-schema-unknown")]
    [InlineData("schema-inline-wire", "firmament-schema-mismatch")]
    [InlineData("schema-template-mismatch", "firmament-schema-mismatch")]
    public void DirectiveIntentCannotDisappear(string fixture, string code)
    {
        var source = File.ReadAllText(Fixture(fixture));
        var parsed = FirmamentV2Parser.Parse(source);
        var report = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(parsed, Fixture(fixture)), source);
        Assert.False(parsed.IsSuccess);
        Assert.Equal("invalid", report.Status);
        var build = FirmamentBuildAndExport.CompileSource(source, Path.GetDirectoryName(Fixture(fixture)));
        Assert.False(build.IsSuccess);
        if (fixture == "schema-template-mismatch")
        {
            Assert.True(FirmamentBuildAndExport.CompileSource(source.Replace("schema WireForm", "schema Mechanical", StringComparison.Ordinal)).IsSuccess);
            var erased = FirmamentFrontendSchemas.Select(source).Source;
            Assert.True(FirmamentV2Parser.Parse(erased, null, null, FirmamentFrontendSchema.Mechanical).IsSuccess);
            Assert.False(FirmamentV2Parser.Parse(erased, null, null, FirmamentFrontendSchema.WireForm).IsSuccess);
            Assert.False(FirmamentV2Parser.Parse(source, null, null, FirmamentFrontendSchema.Mechanical).IsSuccess);
        }
        Assert.Contains(report.Diagnostics, d => d.Code.StartsWith(code, StringComparison.Ordinal));
        if (fixture.StartsWith("schema-", StringComparison.Ordinal))
        {
            var original = Assert.Single(parsed.SyntaxDiagnostics);
            var presented = FirmamentBuildAdmissionValidation.Present(Assert.Single(build.Diagnostics), Fixture(fixture), source);
            Assert.Equal(original.SourceSpan, presented.SourceSpan);
            Assert.Equal(original.Message, presented.Message);
        }
        if (fixture == "ignored-include")
        {
            var located = Assert.Single(parsed.SyntaxDiagnostics);
            Assert.Equal("include \"__missing_syntax_review__.firmament\"", source.Substring(located.SourceSpan!.Start, located.SourceSpan.Length));
            Assert.Contains(build.Diagnostics, d => d.Message.Contains("assembly-include-file-not-found", StringComparison.Ordinal));
            var marker = Assert.Single(FirmamentLanguageAnalysisService.Analyze(source, Fixture(fixture), "1").Diagnostics);
            Assert.Equal(located.SourceSpan.Start, marker.Start);
            Assert.Contains("__missing_syntax_review__.firmament", marker.Message, StringComparison.Ordinal);
            Assert.False(FirmamentV2Parser.Parse(source.Replace("include", "Include", StringComparison.Ordinal)).IsSuccess);
        }
    }

    [Fact]
    public void SchemaCommentsAndInlineSpacingPreserveGeometryAndOffsets()
    {
        var source = File.ReadAllText(Fixture("schema-block-comment")).Replace("\r\n", "\n", StringComparison.Ordinal);
        var selection = FirmamentFrontendSchemas.Select(source);
        Assert.True(selection.IsSuccess);
        Assert.Equal(source.Length, selection.Source.Length);
        Assert.Equal(source.IndexOf("Box", StringComparison.Ordinal), selection.Source.IndexOf("Box", StringComparison.Ordinal));
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join(";", built.Diagnostics));
        var inline = source.Replace("schema Mechanical // a trailing comment is also trivia\n", "schema Mechanical ", StringComparison.Ordinal);
        Assert.Equal(built.Value.StepText, FirmamentBuildAndExport.CompileSource(inline).Value.StepText);
        Assert.Equal(built.Value.StepText, FirmamentBuildAndExport.CompileSource(selection.Source).Value.StepText);
    }

    [Theory]
    [InlineData("mirrored-hole")]
    [InlineData("mirrored-feature-with-override")]
    [InlineData("mirrored-profile")]
    [InlineData("mirrored-revolve")]
    [InlineData("radial-bolt-circle")]
    [InlineData("partial-radial-pattern")]
    public void SymmetryUsesAstAliasesWithoutChangingStepOrIdentities(string name)
    {
        var source = File.ReadAllText(Canonical("Symmetry/" + name));
        var preferred = FirmamentSourceSpelling.Prefer(source);
        Assert.Equal(preferred, FirmamentSourceSpelling.Prefer(preferred));
        Assert.True(FirmamentSyntaxParser.Parse(preferred).IsSuccess);
        var directDiagnostics = new List<string>();
        Assert.NotNull(SemanticSymmetryAuthoring.Expand(preferred, directDiagnostics));
        Assert.Empty(directDiagnostics);
        var before = FirmamentV2Parser.Parse(source); var after = FirmamentV2Parser.Parse(preferred);
        Assert.True(after.IsSuccess, string.Join(";", after.Diagnostics));
        Assert.Equal(before.Document!.ModelName, after.Document!.ModelName);
        Assert.Equal((before.Document.StaticAuthoring?.Mirrors ?? []).Select(m => (m.Destination, m.Source)), (after.Document.StaticAuthoring?.Mirrors ?? []).Select(m => (m.Destination, m.Source)));
        Assert.Equal(FirmamentBuildAndExport.CompileSource(source).Value.StepText, FirmamentBuildAndExport.CompileSource(preferred).Value.StepText);
        Assert.DoesNotContain("Across:", preferred);
        Assert.DoesNotContain("Count:", preferred);
    }

    [Fact]
    public void DenseSeedFieldsAndCamelCaseOverrideRetainAllRequestedGeometry()
    {
        var source = File.ReadAllText(Canonical("Symmetry/mirrored-feature-with-override"));
        var dense = source.Replace(";", "", StringComparison.Ordinal).Replace("From LeftMount", "from LeftMount", StringComparison.Ordinal)
            .Replace("Diameter: 8mm", "diameter: 8mm", StringComparison.Ordinal);
        var baseline = FirmamentBuildAndExport.CompileSource(source);
        var changed = FirmamentBuildAndExport.CompileSource(dense);
        Assert.True(changed.IsSuccess, string.Join(";", changed.Diagnostics));
        Assert.Equal(baseline.Value.StepText, changed.Value.StepText);
        var holes = FirmamentV2Parser.Parse(dense).Document!.ModifyBlocks!.Single().SemanticHoles;
        Assert.Equal(2, holes.Count);
        Assert.Contains(holes, h => h.Name == "RightMountCustom" && h.ShaftDiameter == 8);
    }

    [Theory]
    [InlineData("Across: CenterPlane", "acros: CenterPlane", "acros")]
    [InlineData("Across: CenterPlane", "Across: CenterPlane; across: CenterPlane", "field-duplicate")]
    [InlineData("Normal: [1,0,0]", "Normal: [1,0,0]; typo: 3", "typo")]
    public void AstSymmetryClosesItsConsumedScopes(string oldText, string newText, string code)
    {
        var source = FirmamentCorpusHarness.CanonicalMutationSource(File.ReadAllText(Canonical("Symmetry/mirrored-hole"))).ReplaceRequired(oldText, newText);
        Assert.False(FirmamentV2Parser.Parse(source).IsSuccess);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        Assert.Contains(FirmamentV2Parser.Parse(source).Diagnostics, d => d.Contains(code, StringComparison.Ordinal));
    }

    [Fact]
    public void EndpointLinksAreAstBoundAndPreservePublishedNames()
    {
        var source = File.ReadAllText(Canonical("Revolve/sphere"));
        var preferred = FirmamentSourceSpelling.Prefer(source);
        Assert.Contains("Meridian from South to North as Arc", preferred);
        Assert.Contains("Line2 Diameter { from: North; to: South }", preferred);
        var before = FirmamentBuildAndExport.CompileSource(source); var after = FirmamentBuildAndExport.CompileSource(preferred);
        Assert.True(after.IsSuccess, string.Join(";", after.Diagnostics));
        Assert.Equal(before.Value.StepText, after.Value.StepText);
        var profile = ProfileAuthoringParser.ResolveNamedProfile(preferred, "SphereSection", out var diagnostics);
        Assert.NotNull(profile);
        Assert.Empty(diagnostics);
        Assert.Equal(new[] { "Arc", "AxisClosure" }, profile!.Loops.Single().Segments.Select(s => s.Name));
        const string authored = "Static from: String = \"From To Across\"; Static spec: Spec { From: 1 To: 2 }";
        Assert.True(FirmamentSyntaxParser.Parse(authored).IsSuccess);
        Assert.Equal(authored, FirmamentSourceSpelling.Prefer(authored));
        var namedFrom = File.ReadAllText(Fixture("schema-block-comment")).Replace("stock", "from", StringComparison.Ordinal);
        Assert.True(FirmamentBuildAndExport.CompileSource(namedFrom).IsSuccess);
        Assert.Equal("from", FirmamentV2Parser.Parse(namedFrom).Document!.Solids.Single().Name);
        var profileNamedFrom = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/FrontendMigration/preferred-profile-links.firmament"))
            .Replace("outline", "from", StringComparison.Ordinal);
        Assert.True(FirmamentBuildAndExport.CompileSource(profileNamedFrom).IsSuccess);
    }

    private static string Fixture(string name) => FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/FrontendDirectives/" + name + ".firmament");
    private static string Canonical(string name) => FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/" + name + ".firmament");
}
