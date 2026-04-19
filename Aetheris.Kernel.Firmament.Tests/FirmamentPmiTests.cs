using Aetheris.Kernel.Core.Pmi;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentPmiTests
{
    [Fact]
    public void Compile_And_Export_M7_BaselinePmiFixture_Succeeds_With_Structured_Semantic_Entities()
    {
        var compile = CompileFixture("testdata/firmament/examples/m7_semantic_pmi_baseline.firmament");
        Assert.True(compile.Compilation.IsSuccess);
        Assert.NotNull(compile.Compilation.Value.ParsedDocument?.Pmi);
        Assert.Equal(3, compile.Compilation.Value.ParsedDocument!.Pmi!.Entries.Count);

        var export = ExportFixture("testdata/firmament/examples/m7_semantic_pmi_baseline.firmament");
        Assert.True(export.IsSuccess);
        Assert.Contains("firmament-feature:main_hole", export.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("MEASURE_REPRESENTATION_ITEM('diameter',10,#", export.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("firmament-datum:A", export.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("semantic note target=main_hole", export.Value.StepText, StringComparison.Ordinal);
        Assert.DoesNotContain("DRAUGHTING_CALLOUT", export.Value.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_PmiDatumAxis_On_NonCylindrical_Target_IsRejected_With_Bounded_Diagnostic()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7e-invalid-pmi-unsupported-target-kind.firmament");
        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.ValidationFailed, diagnostic.Code);
        Assert.Contains("datum_kind 'axis' target 'base' to come from a cylindrical primitive/feature", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_PmiTarget_UnknownSelectorRoot_IsRejected()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7e-invalid-pmi-unresolved-target.firmament");
        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains("unknown selector root feature 'missing'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreatePlanarDatum_DuplicateLabel_Fails()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7e-invalid-pmi-duplicate-datum-label.firmament");
        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains("reuses label 'A'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreatePlanarDatum_NonPlanar_Fails()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7e-invalid-pmi-nonplanar-datum-target.firmament");
        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains("requires a planar-face selector target", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalyzerDisplaysDatum()
    {
        var export = ExportFixture("testdata/firmament/examples/m7_semantic_pmi_baseline.firmament");
        Assert.True(export.IsSuccess);

        var datum = Assert.Single(export.Value.DatumInspection!);
        Assert.Equal("A", datum.Label);
        Assert.Equal("planar", datum.DatumType);
        Assert.Equal("base.top_face", datum.Target);
    }


    [Fact]
    public void LegacyAutoHoleDemo_BuildsSemanticDiameterDimensions_InKernelPmiModel()
    {
        var compile = CompileFixture("testdata/firmament/examples/boolean_two_cylinder_holes_basic.firmament");
        Assert.True(compile.Compilation.IsSuccess);

        var loweringPlan = Assert.IsType<FirmamentCompilationArtifact>(compile.Compilation.Value).PrimitiveLoweringPlan;
        Assert.NotNull(loweringPlan);

        var model = FirmamentStepExporter.BuildLegacyAutoHolePmiModel(loweringPlan!, selectionFeatureId: "hole_b");

        Assert.Equal(2, model.Dimensions.Count);
        Assert.All(model.Dimensions, d => Assert.Equal(PmiDimensionKind.Diameter, d.Kind));
        Assert.All(model.Dimensions, d => Assert.Equal("legacy-auto-hole-demo", d.SourceTag));
        Assert.Collection(
            model.Dimensions,
            first => Assert.Equal("diameter:hole_a", first.DimensionId),
            second => Assert.Equal("diameter:hole_b", second.DimensionId));
    }

    [Fact]
    public void IntegrationSanity_LegacyAutoHolePmi_StillWorks_WithExplicitDatumsAvailable()
    {
        var baselineWithDatum = ExportFixture("testdata/firmament/examples/m7_semantic_pmi_baseline.firmament");
        Assert.True(baselineWithDatum.IsSuccess);
        Assert.Single(baselineWithDatum.Value.DatumInspection!);

        var legacyAutoHole = CompileFixture("testdata/firmament/examples/boolean_two_cylinder_holes_basic.firmament");
        Assert.True(legacyAutoHole.Compilation.IsSuccess);

        var loweringPlan = Assert.IsType<FirmamentCompilationArtifact>(legacyAutoHole.Compilation.Value).PrimitiveLoweringPlan;
        var model = FirmamentStepExporter.BuildLegacyAutoHolePmiModel(loweringPlan!, selectionFeatureId: "hole_b");
        Assert.Equal(2, model.Dimensions.Count);
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath)
    {
        var sourceText = FirmamentCorpusHarness.ReadFixtureText(fixturePath);
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(sourceText)));
    }

    private static KernelResult<FirmamentStepExportResult> ExportFixture(string fixturePath)
    {
        var sourceText = FirmamentCorpusHarness.ReadFixtureText(fixturePath);
        return FirmamentStepExporter.Export(new FirmamentCompileRequest(new FirmamentSourceDocument(sourceText)));
    }
}
