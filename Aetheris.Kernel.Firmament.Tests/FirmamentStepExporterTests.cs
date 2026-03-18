using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentStepExporterTests
{
    [Fact]
    public void Export_SingleBoxFixture_Produces_NonEmpty_Deterministic_StepText()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m10a-valid-single-box-export.firmament");

        var first = Export(source);
        var second = Export(source);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(FirmamentStepExporter.LastExecutedGeometricBodyPolicy, first.Value.ExportBodyPolicy);
        Assert.Equal("base", first.Value.ExportedFeatureId);
        Assert.NotEmpty(first.Value.StepText);
        Assert.Contains("ISO-10303-21", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", first.Value.StepText, StringComparison.Ordinal);
        Assert.Equal(first.Value.StepText, second.Value.StepText);

        WriteExportArtifact("m10a-box.step", first.Value.StepText);
    }

    [Fact]
    public void Export_PlacedPrimitiveFixture_Exports_Successfully()
    {
        var result = ExportFixture("testdata/firmament/fixtures/m10a-valid-placed-box-export.firmament");

        Assert.True(result.IsSuccess);
        Assert.Equal("placed_box", result.Value.ExportedFeatureId);
        Assert.Contains("CARTESIAN_POINT", result.Value.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_BooleanFixture_Exports_Successfully()
    {
        var result = ExportFixture("testdata/firmament/fixtures/m10a-valid-boolean-export.firmament");

        Assert.True(result.IsSuccess);
        Assert.Equal("joined", result.Value.ExportedFeatureId);
        Assert.Contains("ADVANCED_FACE", result.Value.StepText, StringComparison.Ordinal);

        WriteExportArtifact("m10a-boolean.step", result.Value.StepText);
    }

    [Fact]
    public void Export_MultipleExecutedGeometryBodies_Selects_Last_Executed_Geometric_Feature_Body()
    {
        var compile = CompileFixture("testdata/firmament/fixtures/m3d-mixed-primitive-boolean-validation.firmament");
        Assert.True(compile.Compilation.IsSuccess);

        var export = FirmamentStepExporter.Export(compile.Compilation.Value);
        Assert.True(export.IsSuccess);
        Assert.Equal("cap", export.Value.ExportedFeatureId);
        Assert.Equal(2, export.Value.ExportedOpIndex);

        var expected = Step242Exporter.ExportBody(
            compile.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives.Single(p => p.FeatureId == "cap").Body);
        Assert.True(expected.IsSuccess);
        Assert.Equal(expected.Value, export.Value.StepText);
    }

    [Fact]
    public void Export_ValidationOps_Do_Not_Become_Export_Bodies()
    {
        const string source = """
firmament:
  version: 1

model:
  name: validation_only_after_box
  units: mm

ops[2]:
  -
    op: box
    id: base
    size[3]:
      2
      3
      4
  -
    op: expect_exists
    target: base
""";

        var result = Export(source);

        Assert.True(result.IsSuccess);
        Assert.Equal("base", result.Value.ExportedFeatureId);
    }

    [Fact]
    public void Export_NoExecutedGeometricBody_Fails_Deterministically()
    {
        var first = ExportFixture("testdata/firmament/fixtures/m10a-no-export-body.firmament");
        var second = ExportFixture("testdata/firmament/fixtures/m10a-no-export-body.firmament");

        Assert.False(first.IsSuccess);
        Assert.False(second.IsSuccess);
        var firstDiagnostic = Assert.Single(first.Diagnostics);
        var secondDiagnostic = Assert.Single(second.Diagnostics);
        Assert.Equal(firstDiagnostic, secondDiagnostic);
        Assert.Contains("requires at least one executed primitive or boolean body", firstDiagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_ValidationFailure_Prevents_Export()
    {
        var result = ExportFixture("testdata/firmament/fixtures/m1a-invalid-sphere-missing-radius.firmament");

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("[FIRM-STRUCT-0009]", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_SchemaPresence_Does_Not_Change_Step_Semantics()
    {
        const string baseline = """
firmament:
  version: 1

model:
  name: schema_semantics_baseline
  units: mm

ops[1]:
  -
    op: box
    id: base
    size[3]:
      5
      6
      7
""";

        const string withSchema = """
firmament:
  version: 1

model:
  name: schema_semantics_with_schema
  units: mm

schema:
  process: additive
  printer_resolution: 0.1

ops[1]:
  -
    op: box
    id: base
    size[3]:
      5
      6
      7
""";

        var first = Export(baseline);
        var second = Export(withSchema);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
    }

    private static Aetheris.Kernel.Core.Results.KernelResult<FirmamentStepExportResult> ExportFixture(string fixturePath) =>
        Export(FirmamentCorpusHarness.ReadFixtureText(fixturePath));

    private static Aetheris.Kernel.Core.Results.KernelResult<FirmamentStepExportResult> Export(string source)
    {
        var request = new FirmamentCompileRequest(new FirmamentSourceDocument(source));
        return FirmamentStepExporter.Export(request);
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath) =>
        FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));

    private static void WriteExportArtifact(string fileName, string stepText)
    {
        var path = Path.Combine(
            FirmamentCorpusHarness.RepoRoot(),
            "testdata",
            "firmament",
            "exports",
            fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, stepText);
    }
}
