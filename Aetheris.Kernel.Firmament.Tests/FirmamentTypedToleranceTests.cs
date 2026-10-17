using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentTypedToleranceTests
{
    private static string Source => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/PMI/typed-tolerance.firmament"));

    [Fact]
    public void TypedValuesBindNominalGeometryAndProjectAllowanceToRealAp242()
    {
        var source = Source;
        var syntax = FirmamentSyntaxParser.Parse(source);
        Assert.True(syntax.IsSuccess, string.Join("\n", syntax.Diagnostics));
        Assert.Equal(3, FirmamentSyntaxTraversal.Expressions(syntax.Document).OfType<ToleranceSyntax>().Count());
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.Equal("UnifiedMechanical", parsed.ParserRoute);
        var values = parsed.Document!.BoundLets!.ToDictionary(v => v.Name);
        var alias = values["inspectionDiameter"];
        Assert.Equal(0.10, alias.Tolerance!.Plus);
        Assert.Equal(0.05, alias.Tolerance.Minus);
        Assert.Equal(FirmamentV2PrimitiveType.Angle, values["draft"].Tolerance!.Type);
        Assert.Equal(40, parsed.Document.Solid.Box!.Size[0]);
        Assert.Equal(8, Assert.Single(Assert.Single(parsed.Document.ModifyBlocks!).SemanticHoles).ShaftDiameter);
        var compiled = FirmamentBuildAndExport.CompileSource(source, null);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
        var inspection = Step242SemanticPmiInspector.Inspect(compiled.Value.StepText);
        Assert.True(inspection.Success, string.Join("\n", inspection.Diagnostics));
        var callout = Assert.Single(inspection.Items, i => i.Kind == "Diameter");
        Assert.Equal(8, callout.Value);
        Assert.Equal(0.10, callout.TolerancePlus);
        Assert.Equal(0.05, callout.ToleranceMinus);
        Assert.NotEmpty(callout.GeometricFaceEntityIds);
        var imported = Step242Importer.ImportBody(compiled.Value.StepText);
        Assert.True(imported.IsSuccess);
        Assert.Equal(7, imported.Value.Topology.Faces.Count());
    }

    [Fact]
    public void ChangingToleranceDoesNotChangeNominalBrepOrCreateImplicitPmi()
    {
        var withoutPmi = Source[..Source.IndexOf("    Pmi", StringComparison.Ordinal)] + "}";
        var changed = withoutPmi.Replace("tol 0.1mm", "tol 0.2mm").Replace("tol +0.10mm -0.05mm", "tol +0.20mm -0.15mm");
        var before = FirmamentBuildAndExport.CompileSource(withoutPmi, null);
        var after = FirmamentBuildAndExport.CompileSource(changed, null);
        Assert.True(before.IsSuccess, string.Join("\n", before.Diagnostics.Select(d => d.Message)));
        Assert.True(after.IsSuccess, string.Join("\n", after.Diagnostics.Select(d => d.Message)));
        Assert.Equal(before.Value.StepText, after.Value.StepText);
        Assert.Equal(0, Step242SemanticPmiInspector.Inspect(after.Value.StepText).DimensionCount);
        var literals = string.Join('\n', withoutPmi.Split('\n').Where(line => !line.TrimStart().StartsWith("Static ", StringComparison.Ordinal)))
            .Replace("[plateWidth,", "[40mm,", StringComparison.Ordinal)
            .Replace("diameter: drillDiameter", "diameter: 8mm", StringComparison.Ordinal);
        var direct = FirmamentBuildAndExport.CompileSource(literals, null);
        Assert.True(direct.IsSuccess, string.Join("\n", direct.Diagnostics.Select(d => d.Message)));
        Assert.Equal(before.Value.StepText, direct.Value.StepText);
    }

    [Theory]
    [InlineData("Static x: Int = 4 tol 1", FirmamentV2Parser.ToleranceInvalidType)]
    [InlineData("Static x: Length = 8mm tol 0.1deg", FirmamentV2Parser.ToleranceUnitMismatch)]
    [InlineData("Static x: Length = 8mm tol -0.1mm", FirmamentV2Parser.ToleranceNegativeBilateral)]
    [InlineData("Static x: Length = 8mm tol +0.1mm", FirmamentV2Parser.ToleranceMissingMinus)]
    [InlineData("Static x: Length = missing tol 0.1mm", FirmamentV2Parser.ExpressionUnknownSymbol)]
    [InlineData("Static x: Length = x tol 0.1mm", FirmamentV2Parser.ExpressionCycle)]
    [InlineData("Static x: Length = 8mm / 0 tol 0.1mm", FirmamentV2Parser.ExpressionDivisionByZero)]
    [InlineData("Static x: Int = 2147483647 + 1", "firmament-v2-static-scalar-out-of-range")]
    [InlineData("Static x: Length = 1e308mm * 1e308 tol 0.1mm", "firmament-v2-static-scalar-nonfinite")]
    public void InvalidTypedRequirementsRejectAdmissionAndBuildWithLocatedDiagnostics(string declaration, string code)
    {
        var source = "Model Invalid { units: mm " + declaration + "\n Box Body { size: [40mm,30mm,8mm] } }";
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(code, parsed.Diagnostics);
        var diagnostic = Assert.Single(parsed.SyntaxDiagnostics, d => d.Code == code);
        Assert.NotNull(diagnostic.SourceSpan);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Message));
        Assert.False(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
    }

    [Fact]
    public void InlineToleranceIsNotSilentlyDiscardedAndMalformedUnusedPathDoesNotPretendToBind()
    {
        var inline = "Model Inline { units: mm Box Body { size: [40mm tol 0.1mm, 30mm, 8mm] } }";
        Assert.False(FirmamentBuildAndExport.CompileSource(inline, null).IsSuccess);
        var path = "Model Bare { units: mm Box Body { size: [40mm,30mm,8mm] } Path ignored { } }";
        var parsed = FirmamentV2Parser.Parse(path);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.StartsWith("concept-path-start-invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void BarePathAndConceptPathHaveIdenticalGeometryAndPublishedMembers()
    {
        var legacy = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Profiles/concept-path-line-arc-profile.firmament"));
        var canonical = legacy.Replace("Concept Path", "Path", StringComparison.Ordinal);
        var before = FirmamentBuildAndExport.CompileSource(legacy, null);
        var after = FirmamentBuildAndExport.CompileSource(canonical, null);
        Assert.True(before.IsSuccess, string.Join("\n", before.Diagnostics.Select(d => d.Message)));
        Assert.True(after.IsSuccess, string.Join("\n", after.Diagnostics.Select(d => d.Message)));
        Assert.Equal(before.Value.StepText, after.Value.StepText);
        var path = ProfileAuthoringParser.ResolveConceptPath(canonical, "RoundedCorner", out var diagnostics);
        Assert.NotNull(path);
        Assert.Empty(diagnostics);
        Assert.Equal(5, path.Segments.Count);
    }

    [Theory]
    [InlineData("dimension: inspectionDiameter", "dimension: inspectionDiameter tolerance: 0.2mm", "firmament-v2-pmi-typed-dimension-override")]
    [InlineData("diameter: drillDiameter end:", "diameter: 9mm end:", "firmament-v2-pmi-typed-dimension-unsatisfied")]
    [InlineData("dimension: inspectionDiameter", "dimension: draft", FirmamentV2Parser.PmiDimensionTypeMismatch)]
    [InlineData("Static inspectionDiameter: Length = drillDiameter", "Static inspectionDiameter: Length = -1mm tol 10mm", FirmamentV2Parser.PmiDiameterInvalid)]
    public void ProjectionCannotOverrideOrMisrepresentTheTypedRequirement(string from, string to, string code)
    {
        var source = Source.Replace(from, to, StringComparison.Ordinal);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(code, parsed.Diagnostics);
        Assert.False(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
    }

    [Fact]
    public void ScalarArithmeticRetainsNominalPolicyAndAnExplicitResultTolerance()
    {
        const string source = """
            Model Arithmetic {
              units: mm
              Static d: Length = 8mm tol 0.1mm
              Static radius: Length = d / 2
              Static larger: Length = d + 2mm tol 0.2mm
              Box Body { size: [larger, 20mm, 8mm] }
            }
            """;
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var values = parsed.Document!.BoundLets!.ToDictionary(v => v.Name);
        Assert.Null(values["radius"].Tolerance);
        Assert.Equal(4, values["radius"].Value.NumericValue);
        Assert.Equal(10, values["larger"].Value.NumericValue);
        Assert.Equal(0.2, values["larger"].Tolerance!.Plus);
        Assert.Contains(parsed.SyntaxDiagnostics, d => d.Code == FirmamentV2Parser.ToleranceDroppedThroughArithmetic && d.Severity == "warning");
        Assert.Contains(FirmamentLanguageAnalysisService.Analyze(source, "arithmetic.firmament", "1").Diagnostics,
            d => d.Code == FirmamentV2Parser.ToleranceDroppedThroughArithmetic && d.Severity == "warning");
        Assert.True(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
    }

    [Theory]
    [InlineData("fixtures/Canonical/Pipeline/open-path.firmament")]
    [InlineData("fixtures/Canonical/Features/Sweep/circular-planar-path-sweep.firmament")]
    public void BarePathPreservesSweepGeometryAcrossManualAndPipelineForms(string fixture)
    {
        var legacy = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(fixture));
        var canonical = legacy.Replace("Concept Path", "Path", StringComparison.Ordinal);
        var before = FirmamentBuildAndExport.CompileSource(legacy, null);
        var after = FirmamentBuildAndExport.CompileSource(canonical, null);
        Assert.True(before.IsSuccess, string.Join("\n", before.Diagnostics.Select(d => d.Message)));
        Assert.True(after.IsSuccess, string.Join("\n", after.Diagnostics.Select(d => d.Message)));
        Assert.Equal(before.Value.StepText, after.Value.StepText);
    }

    [Theory]
    [InlineData("fixtures/Canonical/Features/Sweep/circular-planar-path-sweep.firmament")]
    [InlineData("fixtures/Canonical/WireForm/axis-coil.firmament")]
    public void UnqualifiedScalarConsumersCannotSilentlyDiscardTheDeclaration(string fixture)
    {
        var sweep = FirmamentCorpusHarness.CanonicalMutationSource(File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(fixture)))
            .ReplaceRequired("Units: mm", "Units: mm\n Static unusedRequirement: Length = 8mm tol 0.1mm", StringComparison.Ordinal);
        var result = FirmamentBuildAndExport.CompileSource(sweep, null);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("static-scalar-consumer-unsupported", StringComparison.Ordinal));
        var nested = "Model Nested { units: mm Struct Part { Static x: Length = 1mm tol 0.1mm Box Body { size: [40mm,30mm,8mm] } } }";
        Assert.False(FirmamentBuildAndExport.CompileSource(nested, null).IsSuccess);
    }

    [Fact]
    public void LanguageServiceUsesTheOriginalToleranceSpanAndCompilerMessage()
    {
        var source = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/TypedTolerances/unit-mismatch.firmament"));
        var result = FirmamentLanguageAnalysisService.Analyze(source, "unit-mismatch.firmament", "1");
        var error = Assert.Single(result.Diagnostics, d => d.Code == FirmamentV2Parser.ToleranceUnitMismatch);
        Assert.Equal("tol 0.1deg", source.Substring(error.Start, error.Length));
        Assert.Contains("mm", error.Message);
        Assert.Equal("error", error.Severity);
    }

    [Fact]
    public void InferredDimensionalValuesAndAliasesRetainTheTypedRequirement()
    {
        var source = Source.Replace("Static drillDiameter: Length =", "Static drillDiameter =", StringComparison.Ordinal)
            .Replace("Static inspectionDiameter: Length =", "Static inspectionDiameter =", StringComparison.Ordinal);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.Equal(FirmamentV2PrimitiveType.Length, parsed.Document!.BoundLets!.Single(v => v.Name == "inspectionDiameter").Type);
        var compiled = FirmamentBuildAndExport.CompileSource(source, null);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
        var reference = FirmamentBuildAndExport.CompileSource(Source, null);
        Assert.True(reference.IsSuccess);
        Assert.Equal(reference.Value.StepText, compiled.Value.StepText);
    }

    [Theory]
    [InlineData("typo: 1mm", "canonical-field-unknown")]
    [InlineData("diameter: 2mm", "field-duplicate")]
    public void SweepFieldAdmissionIsClosedByItsActualAstOwner(string extra, string expected)
    {
        var source = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Features/Sweep/circular-planar-path-sweep.firmament"))
            .Replace("Diameter: 1.2mm", "Diameter: 1.2mm\n" + extra, StringComparison.Ordinal);
        var compiled = FirmamentBuildAndExport.CompileSource(source, null);
        Assert.False(compiled.IsSuccess);
        Assert.Contains(compiled.Diagnostics, d => d.Message.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void CommentTriviaPreservesToleranceLocationsAndDoesNotTruncateStringValues()
    {
        var source = "// Original source coordinates matter.\n/* block comment */\n" +
            File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/TypedTolerances/unit-mismatch.firmament"));
        var parsed = FirmamentV2Parser.Parse(source);
        var diagnostic = Assert.Single(parsed.SyntaxDiagnostics, d => d.Code == FirmamentV2Parser.ToleranceUnitMismatch);
        Assert.Equal("tol 0.1deg", source.Substring(diagnostic.SourceSpan!.Start, diagnostic.SourceSpan.Length));
        const string quoted = "Model Quoted { units: mm Static link: String = \"https://example.test/design\"\n Box Body { size: [40mm,30mm,8mm] } }";
        var bound = FirmamentV2Parser.Parse(quoted);
        Assert.True(bound.IsSuccess, string.Join("\n", bound.Diagnostics));
        Assert.Equal("https://example.test/design", Assert.Single(bound.Document!.BoundLets!).Value.Value);
        Assert.False(FirmamentBuildAndExport.CompileSource("Model Bad { units: mm Box Body { size: [40mm,30mm,8mm] } } /* unclosed", null).IsSuccess);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryPmiSpellingRejectsDatumRelationshipsTheExporterCannotPreserve(bool typed)
    {
        var source = typed ? Source.Replace("dimension: inspectionDiameter", "dimension: inspectionDiameter datum: A", StringComparison.Ordinal)
            : File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/TypedTolerances/legacy-datum-reference.firmament"));
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var report = FirmamentV2ValidationReportBuilder.Build(parsed, "datum-reference.firmament");
        Assert.Contains(report.Pmi, p => p.Kind == "diameter" && p.ExportSupport == "deferred" && p.Reason!.Contains("datum-reference-export-unsupported", StringComparison.Ordinal));
        Assert.Equal("invalid", FirmamentBuildAdmissionValidation.Admit(report, source).Status);
        var compiled = FirmamentBuildAndExport.CompileSource(source, null);
        Assert.False(compiled.IsSuccess);
        Assert.Contains(compiled.Diagnostics, d => d.Message.Contains("firmament-v2-pmi-datum-reference-export-unsupported", StringComparison.Ordinal));
    }
}
