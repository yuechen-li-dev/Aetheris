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
    public void ExplicitDiameterAndLinearDistanceToDatum_AreAuthoredAndInspectable()
    {
        var compile = CompileFixture("testdata/firmament/examples/m7_pmi_explicit_dimensions.firmament");
        Assert.True(compile.Compilation.IsSuccess);

        var export = ExportFixture("testdata/firmament/examples/m7_pmi_explicit_dimensions.firmament");
        Assert.True(export.IsSuccess);

        Assert.NotNull(export.Value.DimensionInspection);
        Assert.Contains(export.Value.DimensionInspection!, dimension =>
            string.Equals(dimension.Kind, nameof(PmiDimensionKind.Diameter), StringComparison.Ordinal)
            && string.Equals(dimension.Target, "main_hole", StringComparison.Ordinal)
            && Math.Abs(dimension.Value - 10d) < 1e-9d);
        Assert.Contains(export.Value.DimensionInspection!, dimension =>
            string.Equals(dimension.Kind, nameof(PmiDimensionKind.LinearDistanceToDatum), StringComparison.Ordinal)
            && string.Equals(dimension.Target, "main_hole", StringComparison.Ordinal)
            && string.Equals(dimension.Datum, "A", StringComparison.Ordinal)
            && Math.Abs(dimension.Value - 12d) < 1e-9d);
    }

    [Fact]
    public void Compile_PmiDimension_LinearDistanceToDatum_MissingDatum_IsRejected()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7e-invalid-pmi-dimension-missing-datum.firmament");
        Assert.False(result.Compilation.IsSuccess);

        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains("references missing datum label 'Z'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_PmiDimension_Diameter_OnNonCylindricalTarget_IsRejected()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7e-invalid-pmi-dimension-invalid-target.firmament");
        Assert.False(result.Compilation.IsSuccess);

        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains("dimension_kind 'diameter' target 'base' to be a cylindrical primitive/feature", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoPmi_SimpleHole_SubtractCylinder_EmitsDiameter()
    {
        var compile = CompileFixture("testdata/firmament/examples/boolean_two_cylinder_holes_basic.firmament");
        Assert.True(compile.Compilation.IsSuccess);

        var execution = Assert.IsType<FirmamentCompilationArtifact>(compile.Compilation.Value).PrimitiveExecutionResult;
        var loweringPlan = Assert.IsType<FirmamentCompilationArtifact>(compile.Compilation.Value).PrimitiveLoweringPlan;
        Assert.NotNull(loweringPlan);

        var model = FirmamentStepExporter.BuildLegacyAutoHolePmiModel(
            loweringPlan!,
            execution!,
            PmiModel.Empty("hole_b"),
            selectionFeatureId: "hole_b");

        Assert.True(model.Dimensions.Count >= 1);
        Assert.All(model.Dimensions, d => Assert.Equal(PmiDimensionKind.Diameter, d.Kind));
        Assert.Contains(model.Dimensions, d => string.Equals(d.SourceTag, "auto-hole-pmi:simple_hole_callout", StringComparison.Ordinal));
    }

    [Fact]
    public void AutoPmi_NonHoleCylindricalSubtract_IsRejected()
    {
        var export = ExportFixture("testdata/firmament/fixtures/m0d-cylinder-root-cylindrical-subtract.firmament");
        Assert.True(export.IsSuccess);
        Assert.DoesNotContain(export.Value.DimensionInspection!, d => string.Equals(d.SourceTag, "auto-hole-pmi:simple_hole_callout", StringComparison.Ordinal));
    }


    [Fact]
    public void AutoPmi_Counterbore_SubtractStack_EmitsBoundedCounterboreDimensions()
    {
        var export = ExportFixture("testdata/firmament/fixtures/m0e-auto-pmi-counterbore-success.firmament");
        Assert.True(export.IsSuccess, string.Join(Environment.NewLine, export.Diagnostics.Select(d => d.Message)));

        Assert.NotNull(export.Value.DimensionInspection);
        var autoDimensions = export.Value.DimensionInspection!
            .Where(d => string.Equals(d.SourceTag, "auto-hole-pmi:counterbore_callout", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(2, autoDimensions.Length);
        Assert.All(autoDimensions, dimension =>
        {
            Assert.Equal(nameof(PmiDimensionKind.Diameter), dimension.Kind);
            Assert.Equal("counterbore_through", dimension.Target);
            Assert.Equal("counterbore_callout", dimension.CandidateName);
        });

        Assert.Contains(autoDimensions, d => Math.Abs(d.Value - 14d) < 1e-9d);
        Assert.Contains(autoDimensions, d => Math.Abs(d.Value - 7d) < 1e-9d);
    }

    [Fact]
    public void AutoPmi_Countersink_SubtractStack_EmitsBoundedCountersinkDimensions()
    {
        var export = ExportFixture("testdata/firmament/fixtures/m0e-auto-pmi-countersink-success.firmament");
        Assert.True(export.IsSuccess, string.Join(Environment.NewLine, export.Diagnostics.Select(d => d.Message)));

        Assert.NotNull(export.Value.DimensionInspection);
        var autoDimensions = export.Value.DimensionInspection!
            .Where(d => string.Equals(d.SourceTag, "auto-hole-pmi:countersink_callout", StringComparison.Ordinal))
            .ToArray();

        var dimension = Assert.Single(autoDimensions);
        Assert.Equal(nameof(PmiDimensionKind.Diameter), dimension.Kind);
        Assert.Equal("countersink_through", dimension.Target);
        Assert.Equal("countersink_callout", dimension.CandidateName);
        Assert.True(dimension.Value > 0d);
    }

    [Fact]
    public void AutoPmi_CounterboreLikePartialCylinderSubtract_IsRejected()
    {
        var export = ExportFixture("testdata/firmament/fixtures/m0d-cylinder-root-cylindrical-subtract.firmament");
        Assert.True(export.IsSuccess, string.Join(Environment.NewLine, export.Diagnostics.Select(d => d.Message)));

        Assert.NotNull(export.Value.DimensionInspection);
        Assert.DoesNotContain(export.Value.DimensionInspection!, dimension =>
            !string.IsNullOrWhiteSpace(dimension.SourceTag)
            && dimension.SourceTag.StartsWith("auto-hole-pmi:", StringComparison.Ordinal));
    }

    [Fact]
    public void AutoPmi_CountersinkLikeConeOnlySubtract_IsRejected()
    {
        var export = ExportFixture("testdata/firmament/fixtures/m0e-auto-pmi-countersink-reject-cone-only.firmament");
        Assert.True(export.IsSuccess, string.Join(Environment.NewLine, export.Diagnostics.Select(d => d.Message)));

        Assert.NotNull(export.Value.DimensionInspection);
        Assert.DoesNotContain(export.Value.DimensionInspection!, dimension =>
            !string.IsNullOrWhiteSpace(dimension.SourceTag)
            && dimension.SourceTag.StartsWith("auto-hole-pmi:", StringComparison.Ordinal));
    }
    [Fact]
    public void AutoPmi_ExplicitPmiSuppressesEquivalentAutoDimension()
    {
        var export = ExportFixture("testdata/firmament/examples/m7_pmi_explicit_and_legacy_coexist.firmament");
        Assert.True(export.IsSuccess);

        Assert.NotNull(export.Value.DimensionInspection);
        Assert.Single(export.Value.DimensionInspection!);
        Assert.Contains(export.Value.DimensionInspection, dimension => string.Equals(dimension.SourceTag, "explicit-dimension", StringComparison.Ordinal));
        Assert.DoesNotContain(export.Value.DimensionInspection, dimension => string.Equals(dimension.SourceTag, "auto-hole-pmi:simple_hole_callout", StringComparison.Ordinal));
    }

    [Fact]
    public void AutoPmi_InspectionIncludesSourceAndCandidateName()
    {
        var export = ExportFixture("testdata/firmament/examples/boolean_two_cylinder_holes_basic.firmament");
        Assert.True(export.IsSuccess);

        Assert.Contains(export.Value.DimensionInspection!, dimension =>
            string.Equals(dimension.SourceTag, "auto-hole-pmi:simple_hole_callout", StringComparison.Ordinal)
            && string.Equals(dimension.CandidateName, "simple_hole_callout", StringComparison.Ordinal));
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
