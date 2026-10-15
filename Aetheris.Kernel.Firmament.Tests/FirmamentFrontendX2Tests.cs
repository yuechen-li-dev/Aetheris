using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentFrontendX2Tests
{
    [Fact]
    public void ExistingPanelRadianAdmissionPreservesSurfaceCoordinates()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../fixtures/Regression/FrontendX2/panel-radians.firmament")));
        var parse = FirmamentV2Parser.Parse(source);
        Assert.True(parse.IsSuccess, string.Join("\n", parse.Diagnostics));
        var panel = Assert.Single(parse.Document!.Panels!);
        var start = panel.SurfaceConstruction.Evaluate(0, 0);
        var end = panel.SurfaceConstruction.Evaluate(1, 1);
        Assert.Equal(10 * Math.Cos(0.25), start.X, 10); Assert.Equal(10 * Math.Sin(0.25), start.Y, 10); Assert.Equal(0, start.Z, 10);
        Assert.Equal(10 * Math.Cos(0.75), end.X, 10); Assert.Equal(10 * Math.Sin(0.75), end.Y, 10); Assert.Equal(5, end.Z, 10);
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(build.IsSuccess);
        Assert.Contains(build.Diagnostics, d => d.Message.Contains("firmament-panel-only-step-materialization-unsupported"));
    }

    [Fact]
    public void NamedInvocationCannotDiscardPlacementFromAnOtherwiseBuildablePart()
    {
        const string source = """
            Model Plate {
                units: mm
                Feature Drill() -> Hole<Shaft> {
                    return Hole<Shaft> { on: +Z center: Point2(0mm, 0mm) diameter: 6mm end: ThroughAll }
                }
                Box stock { size: [30mm, 20mm, 8mm] }
                Modify stock { Feature bore = Drill() }
            }
            """;
        var ordinary = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(ordinary.IsSuccess, string.Join("\n", ordinary.Diagnostics));
        var unsupported = source.Replace("Feature bore =", "Feature bore on XZ =");
        var parse = FirmamentV2Parser.Parse(unsupported);
        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.SyntaxDiagnostics, d => d.Code == "firmament-feature-body-unsupported:invocation-header"
            && d.Message.Contains("placement") && d.SourceSpan is not null);
        Assert.False(FirmamentBuildAndExport.CompileSource(unsupported).IsSuccess);
    }

    [Theory]
    [InlineData("Feature<> Make() -> Hole<Shaft> { return Hole<Shaft> { diameter: 6mm } } Make()")]
    [InlineData("Feature Make(p: \"Profile\") -> Boss { return Boss { Profile: p Height: 2mm } } Make(Shape)")]
    [InlineData("Feature Make() -> Hole<Shaft> { let p: \"Length\" = 6mm; return Hole<Shaft> { diameter: p } } Make()")]
    [InlineData("Feature Make() -> Hole<Shaft> { return Hole<Shaft> Named { diameter: 6mm } } Make()")]
    [InlineData("Feature Make() -> Hole<Shaft> { return Hole<Shaft> { diameter: 6mm } } Feature result on XZ = Make()")]
    [InlineData("Feature Make() -> Hole<Shaft> { return Hole<Shaft> { diameter: 6mm } } Feature result() = Make()")]
    [InlineData("Feature Make() -> Hole<Shaft> { Circle2 Outline() { radius: 2mm } return Hole<Shaft> { diameter: 6mm } } Make()")]
    public void MalformedFeatureTypeHeadersCannotBecomeIgnoredIntent(string body)
    {
        var source = "Model P { units: mm " + body + " }";
        var parse = FirmamentV2Parser.Parse(source);
        Assert.False(parse.IsSuccess);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        Assert.Contains(parse.Diagnostics, d => d.StartsWith("firmament-feature-"));
    }

    [Fact]
    public void TemplateShadowingBindsTheNearestDefinitionAndPreservesConcreteGeometry()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../fixtures/Regression/FrontendX2/template-shadowing.firmament")));
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var instance = Assert.Single(parsed.Document!.TemplateInstantiations!);
        Assert.Equal("30mm", instance.ValueArguments["width"]);
        Assert.StartsWith("document/Model LocalFamily", instance.DefinitionScope);
        Assert.Equal(instance.DefinitionScope, instance.BindingScope);
        Assert.Equal(source.LastIndexOf("Template<", StringComparison.Ordinal), instance.TemplateSourceSpan.Start);
        var actual = FirmamentBuildAndExport.CompileSource(source);
        var concrete = FirmamentBuildAndExport.CompileSource("Model LocalFamily { units: mm Struct product { Box stock { size: [30mm, 20mm, 6mm] } } }");
        Assert.True(actual.IsSuccess, string.Join("\n", actual.Diagnostics));
        Assert.True(concrete.IsSuccess, string.Join("\n", concrete.Diagnostics));
        Assert.Equal(concrete.Value!.StepText, actual.Value!.StepText);
    }

    [Fact]
    public void NestedTemplateCallsResolveAtDefinitionScopeRatherThanCallerScope()
    {
        const string source = """
            Template<> Struct Family { Box stock { size: [10mm, 20mm, 6mm] } }
            Template<> Struct Outer { Struct nested = Family<> }
            Model Plate {
                units: mm
                Template<> Struct Family { Box stock { size: [30mm, 20mm, 6mm] } }
                Struct product = Outer<>
            }
            """;
        var actual = FirmamentBuildAndExport.CompileSource(source);
        var concrete = FirmamentBuildAndExport.CompileSource("Model Plate { units: mm Struct product { Box stock { size: [10mm, 20mm, 6mm] } } }");
        Assert.True(actual.IsSuccess, string.Join("\n", actual.Diagnostics));
        Assert.True(concrete.IsSuccess, string.Join("\n", concrete.Diagnostics));
        Assert.Equal(concrete.Value!.StepText, actual.Value!.StepText);
        var parse = FirmamentV2Parser.Parse(source);
        Assert.Equal(["Outer", "Family"], parse.Document!.TemplateInstantiations!.Select(i => i.Template));
    }

    [Fact]
    public void TemplateSiblingVisibilityAndSameScopeDuplicatesRejectInProduction()
    {
        const string sibling = """
            Model Other { Template<> Struct Family { Box stock { size: [10mm, 20mm, 6mm] } } }
            Model Plate { units: mm Struct product = Family<> }
            """;
        var parsed = FirmamentV2Parser.Parse(sibling);
        Assert.False(parsed.IsSuccess);
        Assert.Contains("firmament-template-not-found:Family", parsed.Diagnostics);
        var located = Assert.Single(parsed.SyntaxDiagnostics, d => d.Code == "firmament-template-not-found:Family");
        Assert.Equal("Template binding", located.Stage);
        Assert.True(located.IsOriginalSource);
        Assert.Equal(sibling.IndexOf("Struct product", StringComparison.Ordinal), located.SourceSpan!.Start);
        Assert.False(FirmamentBuildAndExport.CompileSource(sibling).IsSuccess);
        const string duplicate = "Template<> Struct Family {} Template<> Struct Family {} Struct product = Family<>";
        Assert.Contains("firmament-template-duplicate-name:Family", FirmamentV2Parser.Parse(duplicate).Diagnostics);
        Assert.False(FirmamentBuildAndExport.CompileSource(duplicate).IsSuccess);
    }

    [Fact]
    public void TemplateCycleDetectionUsesLexicalDeclarationsNotUnqualifiedNames()
    {
        const string source = """
            Template<> Struct Family { Box stock { size: [10mm, 20mm, 6mm] } }
            Template<> Struct Outer {
                Template<> Struct Family { Struct next = Outer<> }
                Struct nested = Family<>
            }
            Model Plate { units: mm Struct product = Outer<> }
            """;
        var parse = FirmamentV2Parser.Parse(source);
        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.Diagnostics, d => d.StartsWith("firmament-template-recursive-specialization:"));
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Theory]
    [InlineData("Template<> Struct Family on XZ { Box stock { size: [10mm, 20mm, 6mm] } } Struct product = Family<>")]
    [InlineData("Template<> Struct Family { Box stock { size: [10mm, 20mm, 6mm] } } Struct product on XZ = Family<>")]
    [InlineData("Template<> Template<> Struct Family {} Struct product = Family<>")]
    [InlineData("Template<> Struct<> Family {} Struct product = Family<>")]
    public void TemplateHeadersCannotDiscardPlacement(string source)
    {
        Assert.False(FirmamentV2Parser.Parse(source).IsSuccess);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void ScopedRecordSchemasDoNotCaptureAnUnrelatedOuterValue()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../fixtures/Regression/FrontendX2/record-shadowing.firmament")));
        var parse = FirmamentV2Parser.Parse(source);
        Assert.True(parse.IsSuccess, string.Join("\n", parse.Diagnostics));
        var actual = FirmamentBuildAndExport.CompileSource(source);
        var concrete = FirmamentBuildAndExport.CompileSource("Model LocalRecord { units: mm Struct product { Box stock { size: [30mm, 20mm, 6mm] } } }");
        Assert.True(actual.IsSuccess, string.Join("\n", actual.Diagnostics));
        Assert.True(concrete.IsSuccess, string.Join("\n", concrete.Diagnostics));
        Assert.Equal(concrete.Value!.StepText, actual.Value!.StepText);
    }

    [Fact]
    public void HostTemplateExportsAreDocumentScopedAndUseTheSameLexicalBinder()
    {
        const string source = """
            Template<width: Length = 10mm> Struct Family { Box stock { size: [width, 20mm, 6mm] } }
            Model Other { Template<width: Length = 30mm> Struct Family { Box stock { size: [width, 20mm, 6mm] } } }
            """;
        var exported = FirmamentTemplateHostBridge.Inspect(source, out var diagnostics);
        Assert.Empty(diagnostics);
        Assert.Equal("10mm", Assert.Single(Assert.Single(exported).Parameters).DefaultExpression);
        var expanded = FirmamentTemplateHostBridge.Expand(source, "Family", "product", new Dictionary<string, FirmamentHostArgument>(), out diagnostics);
        Assert.NotNull(expanded);
        Assert.Empty(diagnostics);
        Assert.Contains("10mm", expanded.ExpandedSource);
        Assert.DoesNotContain("30mm", expanded.ExpandedSource);
    }

    [Fact]
    public void AstTemplateParametersPreserveQuotedCommasAndIgnoreCommentDeclarations()
    {
        const string source = """
            // Template<> Struct Family { misleading { braces }
            Template<label: String = "left, right", width: Length = 10mm> Struct Family {
                Box stock { size: [width, 20mm, 6mm] }
            }
            Model Plate { units: mm Struct product = Family<> }
            """;
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\\n", parsed.Diagnostics));
        Assert.Equal("\"left, right\"", Assert.Single(parsed.Document!.TemplateInstantiations!).ValueArguments["label"]);
        var actual = FirmamentBuildAndExport.CompileSource(source);
        var concrete = FirmamentBuildAndExport.CompileSource("Model Plate { units: mm Struct product { Box stock { size: [10mm, 20mm, 6mm] } } }");
        Assert.True(actual.IsSuccess, string.Join("\\n", actual.Diagnostics));
        Assert.True(concrete.IsSuccess, string.Join("\\n", concrete.Diagnostics));
        Assert.Equal(concrete.Value!.StepText, actual.Value!.StepText);
    }

    [Fact]
    public void SameNamedTemplatesInDifferentScopesHaveDistinctDeterministicIdentities()
    {
        const string source = """
            Template<> Struct Family { Box stock { size: [10mm, 20mm, 6mm] } }
            Struct product = Family<>
            Model Plate {
                Template<> Struct Family { Box stock { size: [10mm, 20mm, 7mm] } }
                Struct product = Family<>
            }
            """;
        var first = FirmamentTemplateSourceCompiler.Expand(source, out var diagnostics);
        Assert.Empty(diagnostics); Assert.NotNull(first);
        var identities = first.Instantiations.Select(i => i.SpecializationIdentity).ToArray();
        Assert.Equal(2, identities.Distinct().Count());
        var second = FirmamentTemplateSourceCompiler.Expand(source, out diagnostics);
        Assert.Empty(diagnostics); Assert.NotNull(second);
        Assert.Equal(identities, second.Instantiations.Select(i => i.SpecializationIdentity));
    }

    [Theory]
    [InlineData("Template<> Struct Product { Construct Company.Geometry.Part body { width: 10mm } }")]
    [InlineData("Template<force: float> Model Product { units mm analysis LinearElastic Study { force Load { vector: [force N, 0N, 0N] } } }")]
    public void SharedSyntaxRepresentsExistingHostCapabilityAndAnalysisProductions(string source)
    {
        var syntax = FirmamentSyntaxParser.Parse(source);
        Assert.True(syntax.IsSuccess, string.Join("\n", syntax.Diagnostics));
        Assert.Single(FirmamentTemplateHostBridge.Inspect(source, out var diagnostics));
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void MalformedTemplatePrefixCannotDisappearWhenStructuralRecoveryLosesItsDeclaration()
    {
        const string source = "Template<width: Length Struct Family {} Struct product = Family<>";
        Assert.False(FirmamentSyntaxParser.Parse(source).IsSuccess);
        Assert.Null(FirmamentTemplateSourceCompiler.Expand(source, out var diagnostics));
        Assert.Contains(diagnostics, d => d.StartsWith("firmament-template-syntax:"));
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void PatternSymbolCannotRewriteFieldLabelsOrGeneratedIdentity()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../fixtures/Regression/FrontendX2/pattern-symbol-collision.firmament")));
        var parse = FirmamentV2Parser.Parse(source);
        Assert.True(parse.IsSuccess, string.Join("\n", parse.Diagnostics));
        var pattern = Assert.Single(parse.Document!.StaticAuthoring!.Patterns);
        Assert.Equal(["holes.left", "holes.right"], pattern.GeneratedIds);
        var holes = Assert.Single(parse.Document.ModifyBlocks!).SemanticHoles;
        Assert.Equal(["holes_left", "holes_right"], holes.Select(h => h.Name));
        Assert.All(holes, h => Assert.Equal(6, h.ShaftDiameter));
        var build = FirmamentBuildAndExport.CompileSource(source);
        var renamed = FirmamentBuildAndExport.CompileSource(source.Replace("Diameter => Hole<Shaft> Diameter", "point => Hole<Shaft> seed")
            .Replace("center: Diameter", "center: point"));
        Assert.True(build.IsSuccess, string.Join("\n", build.Diagnostics));
        Assert.True(renamed.IsSuccess, string.Join("\n", renamed.Diagnostics));
        Assert.Equal(renamed.Value!.StepText, build.Value!.StepText);
        var variantCollision = FirmamentBuildAndExport.CompileSource(source.Replace("Diameter => Hole<Shaft> Diameter", "Shaft => Hole<Shaft> seed")
            .Replace("center: Diameter", "center: Shaft"));
        Assert.True(variantCollision.IsSuccess, string.Join("\n", variantCollision.Diagnostics));
        Assert.Equal(build.Value.StepText, variantCollision.Value!.StepText);
    }

    [Fact]
    public void SemanticPointPatternSymbolCannotRewriteDiameterField()
    {
        const string source = """
            Concept Struct Layout {
                Bounds: Box3 { Size: [60mm, 40mm, 10mm] }
                Points: Grid {
                    Within: Bounds.Face(+Z).Inset(8mm)
                    Columns: 2
                    Rows: 2
                }
            }
            Struct Product {
                Box Base { Bounds: Layout.Bounds }
                Modify Base {
                    Pattern Mounts {
                        Source: Layout.Points
                        Hole<Shaft> Diameter { On: Base.Top Center: Diameter Diameter: 6mm End: ThroughAll }
                    }
                }
            }
            """;
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var holes = Assert.Single(parsed.Document!.ModifyBlocks!).SemanticHoles;
        Assert.Equal(4, holes.Count); Assert.All(holes, h => Assert.Equal(6, h.ShaftDiameter));
        var actual = FirmamentBuildAndExport.CompileSource(source);
        var renamed = FirmamentBuildAndExport.CompileSource(source.Replace("Hole<Shaft> Diameter", "Hole<Shaft> Item").Replace("Center: Diameter", "Center: Item"));
        Assert.True(actual.IsSuccess, string.Join("\n", actual.Diagnostics)); Assert.True(renamed.IsSuccess);
        Assert.Equal(renamed.Value!.StepText, actual.Value!.StepText);
    }

    [Fact]
    public void FeatureLookupUsesNearestLexicalDefinitionAndPreservesConcreteGeometry()
    {
        var source = """
            Feature Drill(diameter: Length = 2mm) -> Hole<Shaft> {
                return Hole<Shaft> { on: +Z center: Point2(0mm, 0mm) diameter: diameter end: ThroughAll }
            }
            Model Plate {
                units: mm
                Feature Drill(diameter: Length = 6mm) -> Hole<Shaft> {
                    return Hole<Shaft> { on: +Z center: Point2(0mm, 0mm) diameter: diameter end: ThroughAll }
                }
                Box stock { size: [30mm, 20mm, 8mm] }
                Modify stock { Drill() }
            }
            """;
        var parse = FirmamentV2Parser.Parse(source);
        Assert.True(parse.IsSuccess, string.Join("\n", parse.Diagnostics));
        var call = Assert.Single(parse.Document!.FeatureInvocations!);
        Assert.Equal("document/Model Plate", call.BindingScope);
        Assert.Equal(6, Assert.Single(Assert.Single(parse.Document.ModifyBlocks!).SemanticHoles).ShaftDiameter);
        var build = FirmamentBuildAndExport.CompileSource(source);
        var concrete = FirmamentBuildAndExport.CompileSource("""
            Model Plate { units: mm
                Box stock { size: [30mm, 20mm, 8mm] }
                Modify stock { Hole<Shaft> Drill__0 { on: +Z center: Point2(0mm, 0mm) diameter: 6mm end: ThroughAll } }
            }
            """);
        Assert.True(build.IsSuccess, string.Join("\n", build.Diagnostics.Select(d => d.Message)));
        Assert.True(concrete.IsSuccess);
        Assert.Equal(concrete.Value!.StepText, build.Value!.StepText);
    }

    [Fact]
    public void FeatureInSiblingScopeCannotCaptureAnUnresolvedCall()
    {
        const string source = """
            Struct Other { Feature Local() -> Hole<Shaft> { return Hole<Shaft> { diameter: 9mm } } }
            Model Plate { units: mm Box stock { size: [20mm, 20mm, 5mm] } Modify stock { Local() } }
            """;
        var codes = new List<string>();
        Assert.Null(FirmamentV2FeatureExpansion.Expand(source, codes));
        Assert.Contains("firmament-feature-unknown:Local", codes);
        var parsed = FirmamentV2Parser.Parse(source);
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(parsed.IsSuccess); Assert.False(build.IsSuccess);
        Assert.Contains(parsed.SyntaxDiagnostics, d => d.Message.Contains("not visible") && d.SourceSpan is not null);
    }

    [Fact]
    public void DeclarationLabelsMembersStringsAndSimilarIdentifiersAreNotVariableReferences()
    {
        var parsed = FirmamentSyntaxParser.Parse("""
            Circle2 Ring { radius: size center: [size, 0mm] }
            Profile Outline { Loop Outer { Ring |> TraceLoop } }
            Other Unrelated { size: size sizeExtra: sizeExtra path: Other.size text: "size Ring Outline" }
            Feature Nested(size: Length) -> Hole<Shaft> { return Hole<Shaft> { diameter: size } }
            Pattern P over Items { size => Hole<Shaft> Item { diameter: size } }
            """);
        Assert.True(parsed.IsSuccess);
        var value = FirmamentSyntaxParser.ParseExpression("8mm").Expression!;
        var rewrite = new FirmamentSyntaxSubstitution(new Dictionary<string, ExpressionSyntax> { ["size"] = value },
            new Dictionary<string, string> { ["Ring"] = "Call__Ring", ["Outline"] = "Call__Outline" });
        var statements = parsed.Document.Statements.Select(rewrite.Statement).ToArray();
        var circle = Assert.IsType<DeclarationSyntax>(statements[0]);
        Assert.Equal("Call__Ring", circle.Name!.Text);
        Assert.Equal("8mm", FirmamentSyntaxWriter.Expression(Assert.IsType<FieldSyntax>(circle.Body!.Statements[0]).Value));
        var unrelated = Assert.IsType<DeclarationSyntax>(statements[2]);
        var fields = unrelated.Body!.Statements.Cast<FieldSyntax>().ToDictionary(f => f.Name.Text, f => f.Value);
        Assert.Equal("sizeExtra", Assert.IsType<ReferenceSyntax>(fields["sizeExtra"]).Name.Text);
        Assert.Equal("Other.size", FirmamentSyntaxWriter.Expression(fields["path"]));
        Assert.Equal("\"size Ring Outline\"", Assert.IsType<LiteralSyntax>(fields["text"]).Raw);
        var nested = Assert.IsType<DeclarationSyntax>(statements[3]);
        var returned = Assert.IsType<ObjectSyntax>(Assert.IsType<ReturnSyntax>(Assert.Single(nested.Body!.Statements)).Value);
        Assert.Equal("size", Assert.IsType<ReferenceSyntax>(Assert.IsType<FieldSyntax>(Assert.Single(returned.Body.Statements)).Value).Name.Text);
        var pattern = Assert.IsType<DeclarationSyntax>(statements[4]);
        var mapping = Assert.IsType<MappingSyntax>(Assert.Single(pattern.Body!.Statements));
        var hole = Assert.IsType<DeclarationSyntax>(mapping.Value);
        Assert.Equal("Item", hole.Name!.Text);
        Assert.Equal("size", Assert.IsType<ReferenceSyntax>(Assert.IsType<FieldSyntax>(Assert.Single(hole.Body!.Statements)).Value).Name.Text);
    }

    [Theory]
    [InlineData("(20mm + 4mm) / 3", "mm", 8)]
    [InlineData("(90deg - 30deg) * 2", "deg", 120)]
    [InlineData("12mm / 3mm + 2", "", 6)]
    [InlineData("2 + 3 * 4", "", 14)]
    [InlineData("1e-2mm * 100", "mm", 1)]
    [InlineData("0.25rad * 2", "rad", 0.5)]
    public void SharedNumericEvaluationHasOnePrecedenceAndUnitPolicy(string expression, string unit, double expected)
    {
        var ast = FirmamentSyntaxParser.ParseExpression(expression).Expression!;
        Assert.True(FirmamentNumericEvaluation.TryEvaluate(ast, out var result));
        Assert.Equal(unit, result.Unit); Assert.Equal(expected, result.Number, 10);
        Assert.True(FirmamentSyntaxValues.TryMeasure(ast, unit, out var field));
        Assert.Equal(expected, field, 10);
        Assert.True(FirmamentNumericEvaluation.TryEvaluateScalar(expression, out var adapter, out var adapterUnit));
        Assert.Equal(result.Number, adapter); Assert.Equal(unit, adapterUnit);
    }

    [Theory]
    [InlineData("1mm + 1deg")]
    [InlineData("1deg + 1rad")]
    [InlineData("1mm * 1mm")]
    [InlineData("1 / 1mm")]
    [InlineData("1mm / 0")]
    [InlineData("missing + 1mm")]
    [InlineData("Pow(1mm, 2)")]
    [InlineData("1e999")]
    [InlineData("(width: 1mm)")]
    public void SharedNumericEvaluationRejectsInvalidValues(string expression) => Assert.False(FirmamentNumericEvaluation.TryEvaluate(expression, out _));

    [Fact]
    public void DefaultsResolveTypedDependenciesAndRejectCycles()
    {
        const string source = """
            Feature Drill(diameter: Length = base * 2, base: Length = 3mm) -> Hole<Shaft> {
                return Hole<Shaft> { diameter: diameter }
            }
            Model P { Modify stock { Drill() } }
            """;
        var codes = new List<string>();
        var result = FirmamentV2FeatureExpansion.Expand(source, codes);
        Assert.NotNull(result); Assert.Empty(codes);
        Assert.Equal("6mm", Assert.Single(result.Invocations).Arguments["diameter"]);
        codes.Clear();
        Assert.Null(FirmamentV2FeatureExpansion.Expand(source.Replace("base: Length = 3mm", "base: Length = diameter"), codes));
        Assert.Contains(codes, c => c.StartsWith("firmament-feature-default-cycle:"));
    }

    [Fact]
    public void QuotedCommentBracesCannotChangeTerminalReturnOrConstructionNames()
    {
        var codes = new List<string>();
        var expanded = FirmamentV2FeatureExpansion.Expand("""
            Feature Drill() -> Hole<Shaft> {
                // "}" return Missing() { fake
                return Hole<Shaft> { diameter: 6mm }
            }
            Model P { Modify stock { Drill() } }
            """, codes);
        Assert.NotNull(expanded); Assert.Empty(codes);
        Assert.Single(expanded.Invocations);
        Assert.DoesNotContain(FirmamentSyntaxTraversal.Declarations(expanded.Document!), d => d.Type.Name.Text == "Feature");
    }

    [Fact]
    public void ValueParameterCannotRenameAConstructionTypeOrReturnConstructor()
    {
        const string source = """
            Feature Make(Profile: Profile, Boss: Plane) -> Boss {
                Circle2 Outline { Center: [0mm, 0mm] Radius: 2mm }
                Profile Section { Loop Outer { Outline |> TraceLoop } }
                return Boss { On: Boss Profile: Profile Height: 2mm }
            }
            Model P { Compose Body { Make(Profile: ExternalProfile, Boss: Top) } }
            """;
        var codes = new List<string>();
        var expanded = FirmamentV2FeatureExpansion.Expand(source, codes);
        Assert.NotNull(expanded); Assert.Empty(codes);
        var nodes = FirmamentSyntaxTraversal.Declarations(expanded.Document!).ToArray();
        Assert.Contains(nodes, d => d.Type.Name.Text == "Profile" && d.Name?.Text == "Make__0__Section");
        var boss = Assert.Single(nodes, d => d.Type.Name.Text == "Boss");
        var fields = boss.Body!.Statements.Cast<FieldSyntax>().ToDictionary(f => f.Name.Text, f => f.Value);
        Assert.Equal("Top", Assert.IsType<ReferenceSyntax>(fields["On"]).Name.Text);
        Assert.Equal("ExternalProfile", Assert.IsType<ReferenceSyntax>(fields["Profile"]).Name.Text);
    }

    [Fact]
    public void TransportSerializationPreservesIdentityBearingApplicationSpelling()
    {
        const string source = "<Part fret = Panel<L:site.Width,W:1.2mm,H:1mm,R:0.5mm>> Placement { From: Origin; To: World; } </Part>";
        var parsed = FirmamentSyntaxParser.Parse(source);
        Assert.True(parsed.IsSuccess);
        var width = FirmamentSyntaxParser.ParseExpression("43.697597898337534mm").Expression!;
        var transform = new FirmamentSyntaxSubstitution(new Dictionary<string, ExpressionSyntax> { ["site.Width"] = width });
        var part = (DeclarationSyntax)transform.Statement(Assert.Single(parsed.Document.Statements));
        var serialized = FirmamentSyntaxWriter.Statement(part, source);
        var rebound = (DeclarationSyntax)Assert.Single(FirmamentSyntaxParser.Parse(serialized).Document.Statements);
        var application = rebound.Initializer!;
        Assert.Equal("Panel<L:43.697597898337534mm,W:1.2mm,H:1mm,R:0.5mm>", serialized.Substring(application.Span.Start, application.Span.Length));
    }

    [Fact]
    public void ExpansionLimitsRejectRatherThanTruncateAndRetainLocatedDiagnostics()
    {
        var source = "Feature Drill() -> Hole<Shaft> { return Hole<Shaft> { diameter: 6mm } }\nModel P { Modify stock {\n"
            + string.Join("\n", Enumerable.Repeat("Drill()", FirmamentV2FeatureExpansion.MaximumInvocations + 1)) + "\n} }";
        var codes = new List<string>(); var values = new FirmamentCompileTimeValues(source);
        Assert.Null(FirmamentV2FeatureExpansion.Expand(source, codes, values));
        Assert.Contains("firmament-feature-expansion-limit", codes);
        Assert.Contains(values.Diagnostics, d => d.Code == "firmament-feature-expansion-limit" && d.SourceSpan is not null && d.Severity == "fatal");
    }

    [Fact]
    public void InvocationReceiptsAreDeterministicAndSeparateDefinitionFromUse()
    {
        const string source = """
            Feature Drill(diameter: Length = 6mm) -> Hole<Shaft> { return Hole<Shaft> { diameter: diameter } }
            Model P { Modify stock { Drill() Drill(8mm) } }
            """;
        var first = FirmamentV2FeatureExpansion.Expand(source, []);
        var second = FirmamentV2FeatureExpansion.Expand(source, []);
        Assert.NotNull(first); Assert.NotNull(second);
        Assert.Equal(first.Source, second.Source);
        Assert.Equal([0, 1], first.Invocations.Select(i => i.Ordinal));
        foreach (var invocation in first.Invocations)
        {
            Assert.Equal("Drill", invocation.DefinitionOrigin!.Binding);
            Assert.StartsWith("Feature Drill", source.Substring(invocation.DefinitionOrigin.SourceSpan.Start, invocation.DefinitionOrigin.SourceSpan.Length));
            Assert.StartsWith("Drill(", source.Substring(invocation.InvocationOrigin!.SourceSpan.Start, invocation.InvocationOrigin.SourceSpan.Length));
            Assert.Equal(["Drill"], invocation.ExpansionChain);
        }
    }
}
