using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentCompileTimeValueBindingTests
{
    private static string Source => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(
        "fixtures/Canonical/PMI/record-template-feature-tolerance.firmament"));

    [Fact]
    public void RecordRequirementSurvivesTemplateFeatureAndLocalAliasIntoGeometryAndAp242()
    {
        var source = Source;
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.Equal("TolerancedProduct", parsed.Document!.ModelName);
        Assert.Equal(40, parsed.Document.Solid.Box!.Size[0]);
        Assert.Equal(8, Assert.Single(Assert.Single(parsed.Document.ModifyBlocks!).SemanticHoles).ShaftDiameter);
        var pmi = Assert.Single(parsed.Document.BoundPmi!.Dimensions);
        Assert.Equal("standardBore.diameter", pmi.ProjectionSource);
        var origin = Assert.Single(pmi.DimensionOrigins, o => o.Binding == "standardBore.diameter"
            && source.Substring(o.SourceSpan.Start, o.SourceSpan.Length).Contains("tol", StringComparison.Ordinal));
        Assert.Contains(pmi.DimensionOrigins, o => o.Binding == "Template PlateFamily.spec.diameter");
        var geometryValue = Assert.Single(parsed.Document.BoundLets!, v => v.Origins.Any(o => o.Binding == "Feature Drill.inspection"));
        Assert.Contains(origin, geometryValue.Origins);
        Assert.Equal(0.10, geometryValue.Tolerance!.Plus);
        Assert.Equal(0.05, geometryValue.Tolerance.Minus);
        var compiled = FirmamentBuildAndExport.CompileSource(source, null);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
        var callout = Assert.Single(Step242SemanticPmiInspector.Inspect(compiled.Value.StepText).Items, i => i.Kind == "Diameter");
        Assert.Equal(8, callout.Value);
        Assert.Equal(0.10, callout.TolerancePlus);
        Assert.Equal(0.05, callout.ToleranceMinus);
        Assert.NotEmpty(callout.GeometricFaceEntityIds);
        var imported = Step242Importer.ImportBody(compiled.Value.StepText);
        Assert.True(imported.IsSuccess);
        Assert.Equal(7, imported.Value.Topology.Faces.Count());
    }

    [Fact]
    public void TypedTransportPreservesLiteralGeometryPmiAndStableFeatureIdentities()
    {
        var source = Source;
        var literal = source.Replace("8mm tol +0.10mm -0.05mm", "8mm", StringComparison.Ordinal)
            .Replace("dimension: spec.diameter", "value: 8mm tolerance: PlusMinus(0.10mm, 0.05mm)", StringComparison.Ordinal);
        var before = FirmamentBuildAndExport.CompileSource(literal, null);
        var after = FirmamentBuildAndExport.CompileSource(source, null);
        Assert.True(before.IsSuccess, string.Join("\n", before.Diagnostics.Select(d => d.Message)));
        Assert.True(after.IsSuccess, string.Join("\n", after.Diagnostics.Select(d => d.Message)));
        Assert.Equal(before.Value.StepText, after.Value.StepText);
        Assert.Equal(FirmamentV2Parser.Parse(literal).Document!.TemplateInstantiations![0].SpecializationIdentity,
            FirmamentV2Parser.Parse(source).Document!.TemplateInstantiations![0].SpecializationIdentity);
        Assert.Equal(FirmamentV2Parser.Parse(literal).Document!.ModifyBlocks![0].SemanticHoles[0].Name,
            FirmamentV2Parser.Parse(source).Document!.ModifyBlocks![0].SemanticHoles[0].Name);
    }

    [Fact]
    public void NoPmiMeansNoAutomaticAnnotationAndToleranceChangesDoNotAffectGeometry()
    {
        var source = Source;
        var start = source.IndexOf("    Pmi {", StringComparison.Ordinal);
        var end = source.IndexOf("\n    }", start, StringComparison.Ordinal) + "\n    }".Length;
        source = source.Remove(start, end - start);
        var before = FirmamentBuildAndExport.CompileSource(source, null);
        var after = FirmamentBuildAndExport.CompileSource(source.Replace("+0.10mm -0.05mm", "+0.20mm -0.15mm"), null);
        Assert.True(before.IsSuccess);
        Assert.True(after.IsSuccess);
        Assert.Equal(before.Value.StepText, after.Value.StepText);
        Assert.Equal(0, Step242SemanticPmiInspector.Inspect(after.Value.StepText).DimensionCount);
    }

    [Theory]
    [InlineData("8mm tol 0.1deg", FirmamentV2Parser.ToleranceUnitMismatch)]
    [InlineData("8mm tol -0.1mm", FirmamentV2Parser.ToleranceNegativeBilateral)]
    [InlineData("8mm tol +0.1mm", FirmamentV2Parser.ToleranceMissingMinus)]
    [InlineData("8deg tol 0.1mm", FirmamentV2Parser.ExpressionTypeMismatch)]
    [InlineData("missing tol 0.1mm", FirmamentV2Parser.ExpressionUnknownSymbol)]
    [InlineData("8mm / 0 tol 0.1mm", FirmamentV2Parser.ExpressionDivisionByZero)]
    public void InvalidRecordRequirementRejectsParseAndBuildAtOriginalSource(string initializer, string code)
    {
        var source = Source.Replace("8mm tol +0.10mm -0.05mm", initializer);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(code, parsed.Diagnostics);
        Assert.Contains(parsed.SyntaxDiagnostics, d => d.Code == code && d.SourceSpan is not null);
        Assert.False(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
    }

    [Fact]
    public void DerivedRecordReplacesAllowanceAndPreservesTheOverrideOrigin()
    {
        var source = Source.Replace("Template<spec:", "Static intermediate = standardBore with { width: 40mm }\nStatic revised = intermediate with { diameter: 8mm tol 0.2mm }\nTemplate<spec:")
            .Replace("spec: standardBore>", "spec: revised>");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var pmi = Assert.Single(parsed.Document!.BoundPmi!.Dimensions);
        Assert.Equal(0.2, pmi.DimensionTolerance!.Plus);
        Assert.Equal(0.2, pmi.DimensionTolerance.Minus);
        Assert.Contains(pmi.DimensionOrigins, o => source.Substring(o.SourceSpan.Start, o.SourceSpan.Length).Contains("tol 0.2mm", StringComparison.Ordinal));
        Assert.True(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
    }

    [Fact]
    public void NestedRecordFieldsAndTemplateLocalScalarAliasRetainRequirement()
    {
        var source = Source.Replace("Template<spec: BoreSpec>", "Record ProductSpec { bore: BoreSpec }\nStatic packaged: ProductSpec { bore: standardBore }\nTemplate<spec: ProductSpec>")
            .Replace("spec.width", "spec.bore.width").Replace("spec.diameter", "spec.bore.diameter")
            .Replace("spec: standardBore>", "spec: packaged>")
            .Replace("    Box Body", "    Static drilling: Length = spec.bore.diameter\n    Box Body")
            .Replace("Drill(diameter: spec.bore.diameter)", "Drill(diameter: drilling)");
        var compiled = FirmamentBuildAndExport.CompileSource(source, null);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
        var pmi = Assert.Single(FirmamentV2Parser.Parse(source).Document!.BoundPmi!.Dimensions);
        Assert.Equal(0.10, pmi.DimensionTolerance!.Plus);
        Assert.Equal(0.05, pmi.DimensionTolerance.Minus);
    }

    [Fact]
    public void ScalarTemplateDefaultsUseTheSameCheckedValueEnvironment()
    {
        var source = Source.Replace("Template<spec: BoreSpec>", "Static nominal: Length = 8mm tol +0.10mm -0.05mm\nTemplate<diameter: Length = nominal, width: Length = 40mm>")
            .Replace("spec.diameter", "diameter").Replace("spec.width", "width")
            .Replace("PlateFamily<spec: standardBore>", "PlateFamily<>");
        var compiled = FirmamentBuildAndExport.CompileSource(source, null);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
        Assert.Equal(0.10, Assert.Single(FirmamentV2Parser.Parse(source).Document!.BoundPmi!.Dimensions).DimensionTolerance!.Plus);
    }

    [Fact]
    public void NominalRequireAndArithmeticDoNotPretendToComputeToleranceStacks()
    {
        var source = Source.Replace("    units: mm", "    units: mm\n    Require admitted => spec.diameter >= 8mm")
            .Replace("let inspection: Length = diameter", "let inspection: Length = diameter + 0mm");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.Contains(parsed.SyntaxDiagnostics, d => d.Code == FirmamentV2Parser.ToleranceDroppedThroughArithmetic && d.Severity == "warning");
        Assert.True(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
    }

    [Fact]
    public void GeometryOutsideProjectedRequirementRejectsWithOriginalRequirementProvenance()
    {
        var source = Source.Replace("diameter: inspection", "diameter: 9mm");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        var diagnostic = Assert.Single(parsed.SyntaxDiagnostics, d => d.Code == "firmament-v2-pmi-typed-dimension-unsatisfied");
        Assert.Contains("standardBore.diameter", diagnostic.Message);
        Assert.NotNull(diagnostic.SourceSpan);
        Assert.Contains("tol +0.10mm -0.05mm", source.Substring(diagnostic.SourceSpan.Start, diagnostic.SourceSpan.Length));
        Assert.False(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
    }

    [Fact]
    public void WholeTypedRecordCanCrossTemplateAndFeatureParameterBoundaries()
    {
        var source = Source.Replace("Feature Drill(diameter: Length)", "Feature Drill(spec: BoreSpec)")
            .Replace("let inspection: Length = diameter", "let inspection: Length = spec.diameter")
            .Replace("Drill(diameter: spec.diameter)", "Drill(spec: spec)");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.Contains(parsed.Document!.BoundLets!, v => v.Origins.Any(o => o.Binding == "Template PlateFamily.spec.diameter")
            && v.Origins.Any(o => o.Binding == "Feature Drill.spec.diameter") && v.Tolerance?.Plus == 0.1);
        var arguments = Assert.Single(parsed.Document.FeatureInvocations!).Arguments;
        Assert.Contains("spec", arguments.Keys);
        Assert.Contains("spec.diameter", arguments.Keys);
        Assert.Contains("spec.width", arguments.Keys);
        Assert.True(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
        var wrongType = source.Replace("Feature Drill(spec: BoreSpec)", "Record OtherSpec { diameter: Length }\nFeature Drill(spec: OtherSpec)");
        Assert.False(FirmamentBuildAndExport.CompileSource(wrongType, null).IsSuccess);
    }

    [Fact]
    public void NestedTemplateForwardingPreservesRecordRequirementIdentity()
    {
        var source = Source.Replace("Model TolerancedProduct = PlateFamily<spec: standardBore>", """
            Template<outer: BoreSpec> Model Forward {
                Model Core = PlateFamily<spec: outer>
            }
            Model TolerancedProduct = Forward<outer: standardBore>
            """);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var dimension = Assert.Single(parsed.Document!.BoundPmi!.Dimensions);
        Assert.Equal("standardBore.diameter", dimension.ProjectionSource);
        Assert.Contains(dimension.DimensionOrigins, o => o.Binding == "Template Forward.outer.diameter");
        Assert.Contains(dimension.DimensionOrigins, o => o.Binding == "Template PlateFamily.spec.diameter");
        Assert.True(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
    }

    [Fact]
    public void RecordMembersAlsoBindDirectGeometryAndExplicitPmiWithoutTemplates()
    {
        var source = Source[..Source.IndexOf("Template<", StringComparison.Ordinal)] + """
            Model DirectRecord {
                units: mm
                Box Body { size: [standardBore.width, 30mm, 8mm] }
                Modify Body {
                    Hole<Shaft> Mount { on: +Z center: Point2(0mm,0mm) diameter: standardBore.diameter end: ThroughAll }
                }
                Pmi { HoleDiameter MountSize { target: Mount dimension: standardBore.diameter } }
            }
            """;
        var compiled = FirmamentBuildAndExport.CompileSource(source, null);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
        Assert.Equal(0.05, Assert.Single(FirmamentV2Parser.Parse(source).Document!.BoundPmi!.Dimensions).DimensionTolerance!.Minus);
    }

    [Fact]
    public void ValueSubstitutionPreservesLabelsLocalBindersAndQuotedStrings()
    {
        var source = Source.Replace("Template<spec: BoreSpec>", "Template<spec: BoreSpec, diameter: Length = 8mm, label: String = \"diameter\">")
            .Replace("    units: mm", "    units: mm\n    Static message: String = label");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.Equal("diameter", parsed.Document!.BoundLets!.Single(v => v.Name == "message").Value.Value);
        Assert.True(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
    }

    [Fact]
    public void StructSpecializationRetainsTheSameRequirementAndNominalGeometry()
    {
        var source = Source.Replace("Model PlateFamily", "Struct PlateFamily")
            .Replace("Model TolerancedProduct =", "Struct TolerancedProduct =");
        var compiled = FirmamentBuildAndExport.CompileSource(source, null);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
        Assert.Equal(0.1, Assert.Single(FirmamentV2Parser.Parse(source).Document!.BoundPmi!.Dimensions).DimensionTolerance!.Plus);
    }

    [Fact]
    public void AnExplicitNewAllowanceOwnsItsRequirementWhileKeepingItsInputProvenance()
    {
        var source = Source.Replace("Static standardBore:", "Static nominal: Length = 8mm tol 0.01mm\nStatic standardBore:")
            .Replace("diameter: 8mm tol +0.10mm -0.05mm", "diameter: nominal tol +0.10mm -0.05mm");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var requirement = Assert.Single(parsed.Document!.BoundPmi!.Dimensions);
        Assert.Equal("standardBore.diameter", requirement.ProjectionSource);
        Assert.Contains(requirement.DimensionOrigins, o => o.Binding == "nominal");
        Assert.Equal(0.10, requirement.DimensionTolerance!.Plus);
        Assert.True(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
    }

    [Fact]
    public void LegacyVersionMetadataDoesNotReplaceARealArithmeticWarningWithUnboundSyntaxErrors()
    {
        var source = Source.Replace("    width: Length", "    width: Length\n    revision: Version")
            .Replace("    width: 40mm", "    width: 40mm\n    revision: 1.0.0")
            .Replace("let inspection: Length = diameter", "let inspection: Length = diameter + 0mm");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.Contains(parsed.SyntaxDiagnostics, d => d.Code == FirmamentV2Parser.ToleranceDroppedThroughArithmetic && d.Severity == "warning");
        Assert.DoesNotContain(parsed.SyntaxDiagnostics, d => d.Severity is "error" or "fatal");
        Assert.True(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
    }

    [Fact]
    public void UnrelatedLegacyRecordIntegerDataKeepsItsExistingLiteralRange()
    {
        var source = Source.Replace("    width: Length", "    width: Length\n    serial: Int")
            .Replace("    width: 40mm", "    width: 40mm\n    serial: 3000000000");
        var compiled = FirmamentBuildAndExport.CompileSource(source, null);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
        Assert.Equal(FirmamentBuildAndExport.CompileSource(Source, null).Value.StepText, compiled.Value.StepText);
    }

    [Fact]
    public void DuplicateRecordRequirementsDoNotSilentlySelectTheLastValue()
    {
        var source = Source.Replace("    width: 40mm", "    diameter: 9mm tol 0.2mm\n    width: 40mm");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.SyntaxDiagnostics, d => d.Code == "firmament-template-record-duplicate-field:diameter" && d.SourceSpan is not null);
        Assert.False(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
    }

    [Fact]
    public void LegacyRecordCollectionsCannotEraseAnUnsupportedTolerance()
    {
        var source = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(
            "fixtures/Regression/CompileTimeValues/collection-tolerance.firmament"));
        var before = FirmamentBuildAndExport.CompileSource(source.Replace(" tol 0.1mm", ""), null);
        Assert.True(before.IsSuccess, string.Join("\n", before.Diagnostics.Select(d => d.Message)));
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        var diagnostic = Assert.Single(parsed.SyntaxDiagnostics, d => d.Code == FirmamentV2Parser.ToleranceUnsupported);
        Assert.NotNull(diagnostic.SourceSpan);
        Assert.Equal("tol 0.1mm", source.Substring(diagnostic.SourceSpan.Start, diagnostic.SourceSpan.Length));
        Assert.False(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
    }

    [Fact]
    public void CyclicRecordDataRejectsInsteadOfRecursingDuringArgumentFlattening()
    {
        var source = Source.Replace("    width: Length", "    width: Length\n    next: BoreSpec")
            .Replace("    width: 40mm", "    width: 40mm\n    next: standardBore");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.SyntaxDiagnostics, d => d.Code == "firmament-template-record-cycle:standardBore" && d.SourceSpan is not null);
        Assert.False(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
    }

    [Fact]
    public void WholeNestedRecordViewsRetainEveryTemplateAndFeatureReceipt()
    {
        var source = Source.Replace("Template<spec: BoreSpec>", "Record ProductSpec { bore: BoreSpec }\nStatic packaged: ProductSpec { bore: standardBore }\nTemplate<spec: ProductSpec>")
            .Replace("Feature Drill(diameter: Length)", "Feature Drill(spec: ProductSpec)")
            .Replace("let inspection: Length = diameter", "let inspection: Length = spec.bore.diameter")
            .Replace("spec.width", "spec.bore.width").Replace("dimension: spec.diameter", "dimension: spec.bore.diameter")
            .Replace("Drill(diameter: spec.diameter)", "Drill(spec: spec)").Replace("spec: standardBore>", "spec: packaged>");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.Contains(parsed.Document!.BoundLets!, v => v.Origins.Any(o => o.Binding == "Template PlateFamily.spec.bore.diameter")
            && v.Origins.Any(o => o.Binding == "Feature Drill.spec.bore.diameter") && v.Tolerance?.Plus == 0.10);
        var arguments = Assert.Single(parsed.Document.FeatureInvocations!).Arguments;
        Assert.Contains("spec.bore.diameter", arguments.Keys);
        Assert.Contains("spec.bore.width", arguments.Keys);
        Assert.True(FirmamentBuildAndExport.CompileSource(source, null).IsSuccess);
    }
}
