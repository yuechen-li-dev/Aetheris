using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2FeatureFunctionTests
{
    [Fact]
    public void CounterboreFeature_BindsNamedArgumentsDerivesValueAndPreservesProvenance()
    {
        var expansionDiagnostics = new List<string>();
        var expansion = FirmamentV2FeatureExpansion.Expand(CounterboreSource, expansionDiagnostics);
        var parse = FirmamentV2Parser.Parse(CounterboreSource);

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics) + Environment.NewLine + expansion?.Source);
        var definition = Assert.Single(parse.Document!.FeatureDefinitions!);
        Assert.Equal("M8Counterbore", definition.Name);
        Assert.Equal("Hole<Counterbore>", definition.ReturnType);
        Assert.Equal("PureTerminalReturn", definition.Evaluation);
        var invocation = Assert.Single(parse.Document.FeatureInvocations!);
        Assert.Equal("M8Counterbore", invocation.GeneratedByFeature);
        Assert.Equal("Point2(0mm, 0mm)", invocation.Arguments["Center"]);
        Assert.Equal(new FirmamentV2FeatureExpansionMetrics(1, 1, 1, "BoundedSourceOrder"), parse.Document.FeatureExpansion);
        var hole = Assert.Single(parse.Document.ModifyBlocks!.Single().SemanticHoles);
        Assert.Equal(FirmamentV2SemanticHoleVariant.Counterbore, hole.Variant);
        Assert.Equal(14d, hole.CounterboreDiameter);
        Assert.Equal(4d, hole.CounterboreDepth);
    }

    [Fact]
    public void Feature_ComposesWithBoundedPatternWithoutAddingLoopSyntax()
    {
        var parse = FirmamentV2Parser.Parse("""
            Feature MountHole(Center: Point3, Diameter: Length = 6mm) -> Hole<Shaft> {
                return Hole<Shaft> {
                    On: Base.Top
                    Center: Center
                    Diameter: Diameter
                    End: ThroughAll
                }
            }
            Concept Struct Design {
                Bounds: Box3 {
                    Size: [60mm, 40mm, 10mm]
                }
                Points: Grid {
                    Within: Bounds.Face(+Z).Inset(8mm)
                    Columns: 2
                    Rows: 2
                }
            }
            Struct Product {
                Box Base { Bounds: Design.Bounds }
                Modify Base {
                    Pattern Mounts {
                        Source: Design.Points
                        MountHole(Center: Item, Diameter: 6mm)
                    }
                }
            }
            """);

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        Assert.Equal(4, parse.Document!.ModifyBlocks!.Single().SemanticHoles.Count);
        Assert.Equal(4, Assert.Single(parse.Document.ConceptIr!.PatternExpansions!).Count);
        Assert.Single(parse.Document.FeatureInvocations!);
    }

    [Fact]
    public void NestedFeatureCall_IsFiniteAndTyped()
    {
        var source = CounterboreSource.Replace(
            "Feature M8Counterbore(Center: Point2, CounterboreDepth: Length = 4mm) -> Hole<Counterbore> {",
            "Feature CoreCounterbore(Center: Point2, CounterboreDepth: Length) -> Hole<Counterbore> {", StringComparison.Ordinal)
            .Replace("M8Counterbore(Center: Point2(0mm, 0mm))",
                "CoreCounterbore(Center: Point2(0mm, 0mm), CounterboreDepth: 4mm)", StringComparison.Ordinal);
        source = source.Replace("Model FeatureCounterbore {", "Model FeatureCounterbore {\nFeature M8Counterbore(Center: Point2) -> Hole<Counterbore> { return CoreCounterbore(Center: Center, CounterboreDepth: 4mm) }", StringComparison.Ordinal)
            .Replace("CoreCounterbore(Center: Point2(0mm, 0mm), CounterboreDepth: 4mm)", "M8Counterbore(Center: Point2(0mm, 0mm))", StringComparison.Ordinal);

        var parse = FirmamentV2Parser.Parse(source);

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        Assert.Equal(2, parse.Document!.FeatureDefinitions!.Count);
        Assert.Equal(["CoreCounterbore", "M8Counterbore"], parse.Document.FeatureInvocations!.Select(item => item.Feature).Order().ToArray());
    }

    [Theory]
    [InlineData("return Hole<Shaft> { On: +Z Center: Center Diameter: 6mm End: ThroughAll }", "firmament-feature-return-type:Broken")]
    [InlineData("let x: Length = 1mm", "firmament-feature-missing-return:Broken")]
    [InlineData("if true return Hole<Counterbore> { }", "firmament-feature-control-flow-unsupported:if")]
    [InlineData("for Item return Hole<Counterbore> { }", "firmament-feature-control-flow-unsupported:for")]
    public void Feature_RejectsWrongReturnMissingReturnAndGeneralControlFlow(string body, string diagnostic)
    {
        var parse = FirmamentV2Parser.Parse($$"""
            Model InvalidFeature {
                Units: mm
                Feature Broken(Center: Point2) -> Hole<Counterbore> { {{body}} }
                Box Base { Size: [20mm, 20mm, 5mm] }
                Modify Base { Broken([0mm, 0mm]) }
            }
            """);

        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.Diagnostics, item => item.StartsWith(diagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void Feature_RejectsDirectAndIndirectRecursion()
    {
        var direct = FirmamentV2Parser.Parse("Feature A() -> Hole<Shaft> { return A() }");
        Assert.Contains(direct.Diagnostics, item => item == "firmament-feature-recursion:A:A");
        var indirect = FirmamentV2Parser.Parse("Feature A() -> Hole<Shaft> { return B() } Feature B() -> Hole<Shaft> { return A() }");
        Assert.Contains(indirect.Diagnostics, item => item.StartsWith("firmament-feature-recursion:A:B:A", StringComparison.Ordinal));
    }

    [Fact]
    public void FeatureBuild_ExportsAndReimportsEnclosedGeometry()
    {
        var build = FirmamentBuildAndExport.CompileSource("schema Mechanical\n" + CounterboreSource);
        Assert.True(build.IsSuccess, string.Join(Environment.NewLine, build.Diagnostics.Select(item => item.Message)));
        var imported = Step242Importer.ImportBody(build.Value!.StepText);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(item => item.Message)));
        Assert.True(BrepMassProperties.Evaluate(imported.Value!).IsEnclosed);
    }

    [Fact]
    public void FeatureExpansion_HasExactGeometryAndStepParityWithTheConcreteProgram()
    {
        var feature = FirmamentBuildAndExport.CompileSource("schema Mechanical\n" + CounterboreSource);
        var concrete = FirmamentBuildAndExport.CompileSource("""
            schema Mechanical
            Model FeatureCounterbore {
                Units: mm
                Box Plate { Size: [30mm, 20mm, 8mm] }
                Modify Plate {
                    Hole<Counterbore> M8Counterbore__0 {
                        On: +Z
                        Center: Point2(0mm, 0mm)
                        Diameter: 8.5mm
                        CounterboreDiameter: 14mm
                        CounterboreDepth: 4mm
                        End: ThroughAll
                    }
                }
            }
            """);

        Assert.True(feature.IsSuccess);
        Assert.True(concrete.IsSuccess);
        Assert.Equal(concrete.Value!.StepText, feature.Value!.StepText);
        var concreteFeatures = Assert.IsAssignableFrom<IReadOnlyList<FirmamentHoleFeatureReport>>(concrete.Value.Features);
        var expandedFeatures = Assert.IsAssignableFrom<IReadOnlyList<FirmamentHoleFeatureReport>>(feature.Value.Features);
        Assert.Equal(concreteFeatures.Single().Kind, expandedFeatures.Single().Kind);
        Assert.Equal(concreteFeatures.Single().Diameter, expandedFeatures.Single().Diameter);
    }

    [Theory]
    [InlineData("wrong-argument-type.firmament", "firmament-feature-argument-type")]
    [InlineData("missing-argument.firmament", "firmament-feature-argument-count")]
    [InlineData("extra-argument.firmament", "firmament-feature-argument-count")]
    [InlineData("missing-return.firmament", "firmament-feature-missing-return")]
    [InlineData("wrong-return-type.firmament", "firmament-feature-return-type")]
    [InlineData("direct-recursion.firmament", "firmament-feature-recursion")]
    [InlineData("indirect-recursion.firmament", "firmament-feature-recursion")]
    [InlineData("unsupported-conditional.firmament", "firmament-feature-control-flow-unsupported")]
    [InlineData("unsupported-loop.firmament", "firmament-feature-control-flow-unsupported")]
    public void InvalidFeatureFixtures_ProduceSpecificTypedDiagnostics(string file, string diagnostic)
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../fixtures/Invalid/Feature", file)));
        var parse = FirmamentV2Parser.Parse(source);
        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.Diagnostics, item => item.StartsWith(diagnostic, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("WireForm")]
    [InlineData("Sweep")]
    [InlineData("SectionChain")]
    public void Feature_X1IsExplicitlyMechanicalOnly(string schema)
    {
        var selection = FirmamentFrontendSchemas.Select($"schema {schema}\nFeature A() -> Hole<Shaft> {{ return Hole<Shaft> {{ }} }}");
        Assert.False(selection.IsSuccess);
        Assert.Equal($"firmament-feature-schema-unsupported:{schema}", Assert.Single(selection.Diagnostics));
    }

    [Fact]
    public void Feature_CoexistsWithTemplateAndAProfileOfTheSameName()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../fixtures/Canonical/Feature/boss-pocket-feature.firmament"));
        var source = File.ReadAllText(path)
            .Replace("Feature RaisedPad", "Feature PadProfile", StringComparison.Ordinal)
            .Replace("RaisedPad(Shape:", "PadProfile(Shape:", StringComparison.Ordinal)
            .Replace("    Concept Struct Layout On XY", "    Record LegacySpec { Center: Point2 }\n    Template<spec: LegacySpec> LegacyMount { Hole<Shaft> Item { On: +Z Center: spec.Center Diameter: 4mm End: ThroughAll } }\n    Concept Struct Layout On XY", StringComparison.Ordinal);
        var selection = FirmamentFrontendSchemas.Select(source);

        Assert.True(selection.IsSuccess);
        var parse = FirmamentV2Parser.Parse(selection.Source);
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        Assert.Contains(parse.Document!.FeatureDefinitions!, item => item.Name == "PadProfile");
        Assert.Contains(parse.Document.Profiles!, item => item.Name == "PadProfile");
        Assert.Contains(parse.Document.StaticAuthoring!.Templates, item => item.Name == "LegacyMount");
    }

    [Fact]
    public void Feature_ReportsUnknownCallsAndUnsupportedBodyTransformReturns()
    {
        var unknown = FirmamentV2Parser.Parse("""
            Feature Known() -> Hole<Shaft> { return Hole<Shaft> { } }
            Model UnknownCall { Units: mm Box Base { Size: [10mm, 10mm, 2mm] } Modify Base {
                Missing()
            } }
            """);
        Assert.Contains(unknown.Diagnostics, item => item == "firmament-feature-unknown:Missing");

        var bodyTransform = FirmamentV2Parser.Parse("Feature Transform(Input: Body) -> Body { return Input }");
        Assert.Contains(bodyTransform.Diagnostics, item => item == "firmament-feature-return-type:Transform:unsupported-Body");
    }

    private const string CounterboreSource = """
        Model FeatureCounterbore {
            Units: mm
            Feature M8Counterbore(Center: Point2, CounterboreDepth: Length = 4mm) -> Hole<Counterbore> {
                let CounterboreDiameter: Length = 8.5mm + 5.5mm
                return Hole<Counterbore> {
                    On: +Z
                    Center: Center
                    Diameter: 8.5mm
                    CounterboreDiameter: CounterboreDiameter
                    CounterboreDepth: CounterboreDepth
                    End: ThroughAll
                }
            }
            Box Plate { Size: [30mm, 20mm, 8mm] }
            Modify Plate { M8Counterbore(Center: Point2(0mm, 0mm)) }
        }
        """;
}
