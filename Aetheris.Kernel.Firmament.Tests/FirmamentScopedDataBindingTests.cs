using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentScopedDataBindingTests
{
    [Fact]
    public void LocalTypedToleranceRetainsGeometryPmiAndAuthoredRequirementLocation()
    {
        var source = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/FrontendX2/scoped-record-pmi.firmament"));
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.Equal(8, Assert.Single(Assert.Single(parsed.Document!.ModifyBlocks!).SemanticHoles).ShaftDiameter);
        var dimension = Assert.Single(parsed.Document.BoundPmi!.Dimensions);
        Assert.Contains(dimension.DimensionOrigins, o => source.Substring(o.SourceSpan.Start, o.SourceSpan.Length).Contains("8mm tol +0.10mm -0.05mm"));
        var compiled = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics));
        var item = Assert.Single(Step242SemanticPmiInspector.Inspect(compiled.Value!.StepText).Items, i => i.Kind == "Diameter");
        Assert.Equal(8, item.Value); Assert.Equal(0.10, item.TolerancePlus); Assert.Equal(0.05, item.ToleranceMinus);
        Assert.NotEmpty(item.GeometricFaceEntityIds);
    }
    [Theory]
    [InlineData("Static width: Length = 30mm", "width")]
    [InlineData("Record Spec { width: Length } Static spec: Spec = Spec { width: 30mm }", "spec.width")]
    [InlineData("Record Spec { width: Length } Static Table choices: Spec { width: [30mm] } Static spec: Spec = choices[0]", "spec.width")]
    [InlineData("Record Spec { width: Length } Static base: Spec = Spec { width: 12mm } Static spec: Spec = base with { width: 30mm }", "spec.width")]
    public void ModelLocalDataShadowsOuterNamesWithoutChangingConcreteGeometry(string declarations, string value)
    {
        var source = $$"""
            Static width: Length = 10mm
            Record Spec { width: Length thickness: Length }
            Static spec: Spec = Spec { width: 10mm thickness: 3mm }
            Static Table choices: Spec { width: [10mm] thickness: [3mm] }
            Model Plate {
                units: mm
                {{declarations}}
                Box stock { size: [{{value}}, 20mm, 6mm] }
            }
            """;
        EqualConcrete(source, 30);
    }

    [Fact]
    public void TemplateDefaultsResolveAtDefinitionAndExplicitArgumentsAtInvocation()
    {
        const string source = """
            Static width: Length = 10mm
            Template<size: Length = width> Struct Family { Box stock { size: [size, 20mm, 6mm] } }
            Model Plate { units: mm Static width: Length = 30mm Struct product = Family<> }
            """;
        EqualConcrete(source, 10, nested: true);
        EqualConcrete(source.Replace("Family<>", "Family<size: width>"), 30, nested: true);
    }

    [Fact]
    public void TypedNestedRecordAliasesRetainTheirMemberBindings()
    {
        const string source = """
            Record Leaf { width: Length }
            Static leaf: Leaf = Leaf { width: 10mm }
            Model Plate {
                units: mm
                Record Leaf { width: Length }
                Record Config { leaf: Leaf }
                Static leaf: Leaf = Leaf { width: 30mm }
                Static config: Config = Config { leaf: leaf }
                Static selected: Config = config
                Box stock { size: [selected.leaf.width, 20mm, 6mm] }
            }
            """;
        EqualConcrete(source, 30);
    }

    [Theory]
    [InlineData("Record Spec() { width: Length }")]
    [InlineData("Static width on XZ: Length = 30mm")]
    [InlineData("Record Spec { width: Length } Static Table choices: Spec on XZ { width: [30mm] }")]
    public void DataHeadersCannotDiscardUnsupportedClauses(string declaration)
    {
        var source = "Model Plate { units: mm " + declaration + " Box stock { size: [30mm, 20mm, 6mm] } }";
        Assert.False(FirmamentV2Parser.Parse(source).IsSuccess);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Theory]
    [InlineData("Record Spec { width: Length }", "Static value: Spec = Spec { width: 30mm }")]
    [InlineData("Static secret: Length = 30mm", "Static value: Length = secret")]
    [InlineData("Record Spec { width: Length } Static Table choices: Spec { width: [30mm] }", "Static value: Length = choices[0].width")]
    public void SiblingDataIsNotVisibleToAConsumer(string declarations, string consumer)
    {
        var source = "Model Other { " + declarations + " } Model Plate { units: mm " + consumer + " Box stock { size: [30mm, 20mm, 6mm] } }";
        var parse = FirmamentV2Parser.Parse(source);
        Assert.False(parse.IsSuccess);
        var errors = parse.SyntaxDiagnostics.Where(d => d.Code.StartsWith("firmament-binding-name-not-visible:")).ToArray();
        Assert.NotEmpty(errors);
        Assert.All(errors, diagnostic =>
        {
            Assert.NotNull(diagnostic.SourceSpan);
            Assert.True(diagnostic.IsOriginalSource);
            Assert.Contains("not visible", diagnostic.Message);
        });
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Theory]
    [InlineData("Record Spec { width: Length } Record Spec { height: Length }")]
    [InlineData("Static width: Length = 10mm Static width: Length = 30mm")]
    [InlineData("Record Spec { width: Length } Static Table choices: Spec { width: [10mm] } Static Table choices: Spec { width: [30mm] }")]
    public void DuplicateDataInOneScopeRejectsWithoutAReaderException(string declarations)
    {
        var source = "Model Plate { units: mm " + declarations + " Box stock { size: [30mm, 20mm, 6mm] } }";
        var parse = FirmamentV2Parser.Parse(source);
        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.SyntaxDiagnostics, d => d.Code.StartsWith("firmament-binding-duplicate-name:") && d.SourceSpan is not null);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void SameSpellingAndShapeDoNotMakeDifferentScopedRecordTypesInterchangeable()
    {
        const string source = """
            Record Spec { width: Length }
            Static outer: Spec = Spec { width: 30mm }
            Model Plate {
                units: mm
                Record Spec { width: Length }
                Template<p: Spec> Struct Family { Box stock { size: [p.width, 20mm, 6mm] } }
                Struct product = Family<p: outer>
            }
            """;
        var parse = FirmamentV2Parser.Parse(source);
        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.Diagnostics, d => d.StartsWith("firmament-template-record-argument-type-mismatch:"));
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void FeatureParametersAndPatternBindersDoNotCaptureAStaticOfTheSameName()
    {
        const string source = """
            Static diameter: Length = 12mm
            Model Plate {
                units: mm
                Static diameter: Length = 6mm
                Feature Drill(diameter: Length) -> Hole<Shaft> {
                    return Hole<Shaft> { on: +Z center: Point2(0mm, 0mm) diameter: diameter end: ThroughAll }
                }
                Box stock { size: [30mm, 20mm, 6mm] }
                Modify stock { Feature bore = Drill(diameter) }
            }
            """;
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.Equal(6, Assert.Single(Assert.Single(parsed.Document!.ModifyBlocks!).SemanticHoles).ShaftDiameter);
        Assert.True(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void SpecializedRecordAndStaticDeclarationsAreBoundBeforeFeatureArguments()
    {
        const string source = """
            Template<diameter: Length = 6mm> Struct Family {
                Record Spec { diameter: Length }
                Static selected: Spec = Spec { diameter: diameter }
                Feature Drill(p: Spec) -> Hole<Shaft> {
                    return Hole<Shaft> { on: +Z center: Point2(0mm, 0mm) diameter: p.diameter end: ThroughAll }
                }
                Box stock { size: [30mm, 20mm, 6mm] }
                Modify stock { Feature bore = Drill(selected) }
            }
            Model Plate { units: mm Struct product = Family<> }
            """;
        var parse = FirmamentV2Parser.Parse(source);
        Assert.True(parse.IsSuccess, string.Join("\n", parse.Diagnostics));
        Assert.Equal(6, Assert.Single(Assert.Single(parse.Document!.ModifyBlocks!).SemanticHoles).ShaftDiameter);
        Assert.True(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void HostInspectionAndInvocationUseTheVisibleRecordSchema()
    {
        const string source = """
            Record Spec { width: Length }
            Static outer: Spec = Spec { width: 10mm }
            Model Library {
                units: mm
                Record Spec { width: Length thickness: Length }
                Static selected: Spec = Spec { width: 30mm thickness: 6mm }
                Template<p: Spec = selected> Struct Family { Box stock { size: [p.width, 20mm, p.thickness] } }
            }
            """;
        var metadata = FirmamentTemplateHostBridge.InspectModule(source, out var diagnostics);
        Assert.Empty(diagnostics);
        var schema = Assert.Single(metadata.Records);
        Assert.Equal("Spec", schema.Name);
        Assert.Equal(["thickness", "width"], schema.Fields.Select(f => f.Name).OrderBy(n => n));
        var expansion = FirmamentTemplateHostBridge.Expand(source, "Family", "product",
            new Dictionary<string, FirmamentHostArgument> { ["p"] = new("", "Spec", new Dictionary<string, string> { ["width"] = "30mm", ["thickness"] = "6mm" }) }, out diagnostics);
        Assert.NotNull(expansion); Assert.Empty(diagnostics);
        var built = FirmamentBuildAndExport.CompileSource(expansion.ExpandedSource);
        Assert.True(built.IsSuccess, expansion.ExpandedSource + "\n" + string.Join("\n", built.Diagnostics));
    }

    [Fact]
    public void HostNestedRecordMarkersResolveTheDeclaredScopedType()
    {
        const string source = """
            Record Leaf { unused: Length }
            Model Library {
                units: mm
                Record Leaf { width: Length }
                Record Spec { leaf: Leaf }
                Template<p: Spec> Struct Family { Box stock { size: [p.leaf.width, 20mm, 6mm] } }
            }
            """;
        var expanded = FirmamentTemplateHostBridge.Expand(source, "Family", "product",
            new Dictionary<string, FirmamentHostArgument> { ["p"] = new("", "Spec", new Dictionary<string, string> { ["leaf"] = "Leaf", ["leaf.width"] = "30mm" }) }, out var diagnostics);
        Assert.NotNull(expanded); Assert.Empty(diagnostics);
        var built = FirmamentBuildAndExport.CompileSource(expanded.ExpandedSource);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
    }

    private static void EqualConcrete(string source, double width, bool nested = false)
    {
        var parse = FirmamentV2Parser.Parse(source);
        Assert.True(parse.IsSuccess, string.Join("\n", parse.Diagnostics));
        var actual = FirmamentBuildAndExport.CompileSource(source);
        var box = $"Box stock {{ size: [{width}mm, 20mm, 6mm] }}";
        var concrete = FirmamentBuildAndExport.CompileSource("Model Plate { units: mm " + (nested ? "Struct product { " + box + " }" : box) + " }");
        Assert.True(actual.IsSuccess, string.Join("\n", actual.Diagnostics));
        Assert.True(concrete.IsSuccess, string.Join("\n", concrete.Diagnostics));
        Assert.Equal(concrete.Value!.StepText, actual.Value!.StepText);
    }
}
