using System.Text.Json;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentCompileTimeChoiceTests
{
    private static string Root => FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/CompileTimeChoicesRfc");
    public static IEnumerable<object?[]> Cases()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "cases.json")));
        foreach (var row in manifest.RootElement.EnumerateArray())
            yield return [row.GetProperty("name").GetString()!, row.GetProperty("shouldBuild").GetBoolean(),
                row.TryGetProperty("reference", out var reference) ? reference.GetString() : null,
                row.TryGetProperty("diagnostic", out var diagnostic) ? diagnostic.GetString() : null];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ChoicesAdmitAllArmsBeforeBuildingOnlyTheSelectedValue(string name, bool admitted, string? reference, string? diagnostic)
    {
        var source = File.ReadAllText(Path.Combine(Root, name + ".firmament"));
        var parsed = FirmamentV2Parser.Parse(source);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(parsed.IsSuccess == admitted, name + ": " + string.Join("\n", parsed.Diagnostics) + "\n" + parsed.DiagnosticSource);
        Assert.True(built.IsSuccess == admitted, name + ": " + string.Join("\n", built.Diagnostics));
        if (diagnostic is not null)
        {
            Assert.Contains(parsed.Diagnostics, d => d == diagnostic || d.StartsWith(diagnostic + ":", StringComparison.Ordinal));
            Assert.Contains(parsed.SyntaxDiagnostics, d => d.Code == diagnostic && d.SourceSpan is not null && !string.IsNullOrWhiteSpace(d.Message));
        }
        if (reference is null || !admitted) return;
        var expected = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Path.Combine(Root, reference + ".firmament")));
        Assert.True(expected.IsSuccess, string.Join("\n", expected.Diagnostics));
        Assert.Equal(expected.Value!.StepText, built.Value!.StepText);
    }

    [Fact]
    public void DecodedInvalidEnumArgumentUsesItsOriginalApplicationLocation()
    {
        var source = File.ReadAllText(Path.Combine(Root, "unknown-choice.firmament"));
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        var error = Assert.Single(parsed.SyntaxDiagnostics, d => d.Code == ConceptIrResolver.UnknownEnumVariant);
        var span = Assert.IsType<FirmamentV2SourceSpan>(error.SourceSpan);
        Assert.Contains("Struct Result", source.Substring(span.Start, span.Length), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(error.LocationNote));
    }

    [Fact]
    public void SelectedRecordKeepsToleranceProvenancePmiAndApplicationIdentity()
    {
        var source = File.ReadAllText(Path.Combine(Root, "pmi-selected-record.firmament"));
        var compact = FirmamentV2Parser.Parse(source).Document!;
        var requirement = Assert.Single(compact.BoundPmi!.Dimensions);
        Assert.Equal(0.1, requirement.DimensionTolerance!.Plus);
        Assert.Equal(0.05, requirement.DimensionTolerance.Minus);
        Assert.Contains(requirement.DimensionOrigins, o => o.Binding == "standardBore.diameter"
            && source.Substring(o.SourceSpan.Start, o.SourceSpan.Length).Contains("tol", StringComparison.Ordinal));
        Assert.DoesNotContain(requirement.DimensionOrigins, o => o.Binding.StartsWith("largeBore", StringComparison.Ordinal));
        Assert.Contains(compact.BoundLets!, v => v.Origins.Any(o => o.Binding == "Feature Drill.inspection") && v.Tolerance?.Plus == 0.1);
        var standardSource = source.Replace("choice: BoreChoice = Compact", "choice: BoreChoice = Standard", StringComparison.Ordinal);
        var standard = FirmamentV2Parser.Parse(standardSource).Document!;
        Assert.Equal(80, standard.Solid.Box!.Size[0]);
        Assert.Equal(10, Assert.Single(Assert.Single(standard.ModifyBlocks!).SemanticHoles).ShaftDiameter);
        Assert.Equal(Assert.Single(compact.FeatureInvocations!).ApplicationIdentity, Assert.Single(standard.FeatureInvocations!).ApplicationIdentity);
        var built = FirmamentBuildAndExport.CompileSource(standardSource);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
        var callout = Assert.Single(Step242SemanticPmiInspector.Inspect(built.Value!.StepText).Items, i => i.Kind == "Diameter");
        Assert.Equal(10, callout.Value);
        Assert.Equal(0.2, callout.TolerancePlus);
        Assert.Equal(0.2, callout.ToleranceMinus);
    }

    [Fact]
    public void FormatterOnlyChangesTheMatchKeywordAtItsSyntaxPosition()
    {
        var source = File.ReadAllText(Path.Combine(Root, "template-compact.firmament"));
        var formatted = FirmamentLanguageAnalysisService.Format(source, "choice.firmament", "1").Text;
        Assert.Contains("match choice", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("Match choice", formatted, StringComparison.Ordinal);
        Assert.True(FirmamentBuildAndExport.CompileSource(formatted).IsSuccess);
        Assert.Equal(formatted, FirmamentLanguageAnalysisService.Format(formatted, "choice.firmament", "2").Text);
    }

    [Fact]
    public void OptionsUsesTheSamePublicHostSchemaAndAdmissionAsEnum()
    {
        var source = File.ReadAllText(Path.Combine(Root, "template-compact.firmament"))
            .Replace("Struct Result = Plate<choice: Compact>", "", StringComparison.Ordinal);
        string? schema = null;
        string? step = null;
        foreach (var text in new[] { source, source.Replace("Enum SizeChoice", "Options SizeChoice", StringComparison.Ordinal) })
        {
            var metadata = FirmamentTemplateHostBridge.InspectModule(text, out var diagnostics);
            Assert.Empty(diagnostics);
            Assert.Equal(new[] { "Compact", "Standard" }, Assert.Single(metadata.Enums).Cases);
            var serialized = JsonSerializer.Serialize(metadata);
            if (schema is not null) Assert.Equal(schema, serialized);
            schema = serialized;
            var expansion = FirmamentTemplateHostBridge.Expand(text, "Plate", "Result",
                new Dictionary<string, FirmamentHostArgument> { ["choice"] = new("Compact") }, out diagnostics);
            Assert.NotNull(expansion);
            var built = FirmamentBuildAndExport.CompileSource(expansion.ExpandedSource);
            Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
            if (step is not null) Assert.Equal(step, built.Value!.StepText);
            step = built.Value!.StepText;
        }
    }

    [Fact]
    public void SelectingAToleranceDoesNotCreatePmiWithoutAProjection()
    {
        var source = File.ReadAllText(Path.Combine(Root, "pmi-selected-record.firmament"));
        var syntax = Aetheris.Kernel.Firmament.FirmamentV2.Syntax.FirmamentSyntaxParser.Parse(source).Document;
        var pmi = Assert.Single(Aetheris.Kernel.Firmament.FirmamentV2.Syntax.FirmamentSyntaxTraversal.Declarations(syntax), d => d.Type.Name.Text == "Pmi");
        var withoutPmi = source.Remove(pmi.Span.Start, pmi.Span.Length);
        var baseline = FirmamentBuildAndExport.CompileSource(source);
        var built = FirmamentBuildAndExport.CompileSource(withoutPmi);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
        Assert.Empty(Step242SemanticPmiInspector.Inspect(built.Value!.StepText).Items);
        Assert.Equal(Aetheris.Kernel.Core.Brep.Verification.BrepMassProperties.Evaluate(baseline.Value!.RuntimeBody!).SignedVolume,
            Aetheris.Kernel.Core.Brep.Verification.BrepMassProperties.Evaluate(built.Value.RuntimeBody!).SignedVolume, 9);
    }

    [Theory]
    [InlineData("duplicate-enum-case", "firmament-static-enum-duplicate-variant")]
    [InlineData("missing-unselected", "firmament-static-match-non-exhaustive")]
    public void DomainSourceExpansionCannotEraseInvalidChoices(string name, string code)
    {
        var source = File.ReadAllText(Path.Combine(Root, name + ".firmament"));
        Assert.Null(FirmamentTemplateSourceCompiler.Expand(source, out var diagnostics));
        Assert.Contains(diagnostics, d => d.StartsWith(code, StringComparison.Ordinal));
        Assert.Null(FirmamentTemplateHostBridge.Expand(source.Replace("Struct Result = Plate<choice: Compact>", "", StringComparison.Ordinal),
            "Plate", "Result", new Dictionary<string, FirmamentHostArgument> { ["choice"] = new("Compact") }, out diagnostics));
        Assert.Contains(diagnostics, d => d.StartsWith(code, StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.StartsWith(code + ": ", StringComparison.Ordinal) && d.Length > code.Length + 4);
    }

    [Fact]
    public void ExistingFinitePatternSelectorRejectsAnUnlistedCountWithItsAdmittedCases()
    {
        var source = StandardProductTemplateLibrary.GetTemplateSource("FlangedAdapterTemplate", "FlangedAdapterPolicy");
        var expansion = FirmamentTemplateHostBridge.Expand(source, "FlangedAdapterTemplate", "Flange5",
            new Dictionary<string, FirmamentHostArgument>
            {
                ["P"] = new("", "FlangedAdapterPolicy", new Dictionary<string, string>
                {
                    ["OuterDiameter"] = "80mm", ["BodyThickness"] = "12mm", ["BoreDiameter"] = "30mm",
                    ["BoltHoleDiameter"] = "6.6mm", ["BoltCount"] = "5", ["Material"] = "\"Standard.Materials.Aluminum.6061_T6\""
                })
            }, out var diagnostics);
        Assert.Null(expansion);
        Assert.Contains(diagnostics, d => d.StartsWith(ConceptIrResolver.SelectedBranchEvaluationFailure + ": ", StringComparison.Ordinal)
            && d.Contains("'5'", StringComparison.Ordinal) && d.Contains("4, 6, 8", StringComparison.Ordinal));
    }
}
