using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentBooleanCanonicalSuccessTests
{
    public static TheoryData<string, string, FirmamentLoweredBooleanKind> CanonicalBooleanExamples =>
        new()
        {
            { "testdata/firmament/examples/boolean_add_basic.firmament", "joined", FirmamentLoweredBooleanKind.Add },
            { "testdata/firmament/examples/boolean_subtract_basic.firmament", "carved", FirmamentLoweredBooleanKind.Subtract },
            { "testdata/firmament/examples/boolean_intersect_basic.firmament", "overlap", FirmamentLoweredBooleanKind.Intersect },
            { "testdata/firmament/examples/boolean_box_cylinder_hole.firmament", "hole", FirmamentLoweredBooleanKind.Subtract }
        };

    [Theory]
    [MemberData(nameof(CanonicalBooleanExamples))]
    public void CanonicalBooleanExamples_Compile_Execute_WithoutDiagnostics(string fixturePath, string expectedFeatureId, FirmamentLoweredBooleanKind expectedKind)
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));

        Assert.True(result.Compilation.IsSuccess);
        Assert.Empty(result.Compilation.Diagnostics);

        var execution = result.Compilation.Value.PrimitiveExecutionResult!;
        var executedBoolean = Assert.Single(execution.ExecutedBooleans, boolean => boolean.FeatureId == expectedFeatureId);
        Assert.Equal(expectedKind, executedBoolean.Kind);
        Assert.NotEmpty(executedBoolean.Body.Topology.Bodies);
        Assert.NotEmpty(executedBoolean.Body.Topology.Faces);
        Assert.DoesNotContain(result.Compilation.Diagnostics, diagnostic => diagnostic.Message.Contains("Requested boolean feature", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("testdata/firmament/examples/boolean_add_basic.firmament", "joined", 2, "add")]
    [InlineData("testdata/firmament/examples/boolean_subtract_basic.firmament", "carved", 2, "subtract")]
    [InlineData("testdata/firmament/examples/boolean_intersect_basic.firmament", "overlap", 2, "intersect")]
    [InlineData("testdata/firmament/examples/boolean_box_cylinder_hole.firmament", "hole", 1, "subtract")]
    public void CanonicalBooleanExamples_Export_Deterministically_WithExpectedMarkers(string fixturePath, string expectedFeatureId, int expectedOpIndex, string expectedKind)
    {
        var first = ExportFixture(fixturePath);
        var second = ExportFixture(fixturePath);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(expectedFeatureId, first.Value.ExportedFeatureId);
        Assert.Equal(expectedOpIndex, first.Value.ExportedOpIndex);
        Assert.Equal("boolean", first.Value.ExportedBodyCategory);
        Assert.Equal(expectedKind, first.Value.ExportedFeatureKind);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("ISO-10303-21", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("ADVANCED_FACE", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("PLANE", first.Value.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void OutsideCanonicalSubset_RemainsRejected_WithoutSilentFallback()
    {
        var unsupportedSingleBoxSubtract = FirmamentCorpusHarness.Compile(
            FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m3d-valid-subtract-exec.firmament"));
        var supportedCylinderHoleExport = ExportFixture("testdata/firmament/examples/boolean_box_cylinder_hole.firmament");
        var unsupportedCylinderHoleExport = ExportFixture("testdata/firmament/fixtures/m10h1-unsupported-box-with-cylinder-hole.firmament");

        Assert.False(unsupportedSingleBoxSubtract.Compilation.IsSuccess);
        Assert.Contains(unsupportedSingleBoxSubtract.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && diagnostic.Message.Contains("Requested boolean feature 'cut' (subtract) could not be executed.", StringComparison.Ordinal));

        Assert.True(supportedCylinderHoleExport.IsSuccess);
        Assert.Contains("CYLINDRICAL_SURFACE", supportedCylinderHoleExport.Value.StepText, StringComparison.Ordinal);

        Assert.False(unsupportedCylinderHoleExport.IsSuccess);
        Assert.Contains(unsupportedCylinderHoleExport.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && diagnostic.Message.Contains("Requested boolean feature 'hole' (subtract) could not be executed.", StringComparison.Ordinal));
        Assert.DoesNotContain(unsupportedCylinderHoleExport.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("requires at least one executed primitive or boolean body", StringComparison.Ordinal));
    }

    public static TheoryData<string, string, string> UnsupportedBoxCylinderBooleanFixtures =>
        new()
        {
            { "testdata/firmament/fixtures/m10j-unsupported-box-add-cylinder.firmament", "joined", "add" },
            { "testdata/firmament/fixtures/m10h1-unsupported-box-with-cylinder-hole.firmament", "hole", "subtract" },
            { "testdata/firmament/fixtures/m10j-unsupported-box-intersect-cylinder.firmament", "overlap", "intersect" }
        };

    [Theory]
    [MemberData(nameof(UnsupportedBoxCylinderBooleanFixtures))]
    public void BoxCylinderBooleans_Remain_Unsupported_And_Fail_Loudly(string fixturePath, string expectedFeatureId, string expectedKind)
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));

        Assert.False(result.Compilation.IsSuccess);
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && diagnostic.Source == "firmament"
            && diagnostic.Message.Contains($"Requested boolean feature '{expectedFeatureId}' ({expectedKind}) could not be executed.", StringComparison.Ordinal));
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && (diagnostic.Message.Contains("M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", StringComparison.Ordinal)
                || diagnostic.Message.Contains("strict Z-aligned through-hole subset", StringComparison.Ordinal)));
    }

    private static Aetheris.Kernel.Core.Results.KernelResult<FirmamentStepExportResult> ExportFixture(string fixturePath) =>
        FirmamentStepExporter.Export(new FirmamentCompileRequest(new FirmamentSourceDocument(FirmamentCorpusHarness.ReadFixtureText(fixturePath))));
}
