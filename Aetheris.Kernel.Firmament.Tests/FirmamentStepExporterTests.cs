using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentStepExporterTests
{
    [Fact]
    public void Export_SingleBoxFixture_Returns_Explicit_Metadata_And_Persisted_Artifact()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m10a-valid-single-box-export.firmament");

        var first = Export(source);
        var second = Export(source);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(FirmamentStepExporter.LastExecutedGeometricBodyPolicy, first.Value.ExportBodyPolicy);
        Assert.Equal(FirmamentStepExporter.LastExecutedGeometricBodySelectionReason, first.Value.ExportBodySelectionReason);
        Assert.Equal("base", first.Value.ExportedFeatureId);
        Assert.Equal(0, first.Value.ExportedOpIndex);
        Assert.Equal("primitive", first.Value.ExportedBodyCategory);
        Assert.Equal("box", first.Value.ExportedFeatureKind);
        Assert.NotEmpty(first.Value.StepText);
        Assert.Contains("ISO-10303-21", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", first.Value.StepText, StringComparison.Ordinal);
        Assert.Equal(first.Value.StepText, second.Value.StepText);

        var artifactPath = WriteExportArtifact("m10a-box.step", first.Value.StepText);
        Assert.True(File.Exists(artifactPath));
        Assert.Equal(first.Value.StepText, File.ReadAllText(artifactPath));
    }

    [Fact]
    public void Export_PlacedPrimitiveFixture_Exports_Successfully_With_Metadata_And_Deterministic_Text()
    {
        var first = ExportFixture("testdata/firmament/fixtures/m10a-valid-placed-box-export.firmament");
        var second = ExportFixture("testdata/firmament/fixtures/m10a-valid-placed-box-export.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("placed_box", first.Value.ExportedFeatureId);
        Assert.Equal(0, first.Value.ExportedOpIndex);
        Assert.Equal("primitive", first.Value.ExportedBodyCategory);
        Assert.Equal("box", first.Value.ExportedFeatureKind);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("CARTESIAN_POINT", first.Value.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_BooleanFixture_Exports_Successfully_With_Metadata_And_Deterministic_Text()
    {
        var first = ExportFixture("testdata/firmament/fixtures/m10a-valid-boolean-export.firmament");
        var second = ExportFixture("testdata/firmament/fixtures/m10a-valid-boolean-export.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("joined", first.Value.ExportedFeatureId);
        Assert.Equal(1, first.Value.ExportedOpIndex);
        Assert.Equal("boolean", first.Value.ExportedBodyCategory);
        Assert.Equal("add", first.Value.ExportedFeatureKind);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("ADVANCED_FACE", first.Value.StepText, StringComparison.Ordinal);

        var artifactPath = WriteExportArtifact("m10a-boolean.step", first.Value.StepText);
        Assert.True(File.Exists(artifactPath));
        Assert.Equal(first.Value.StepText, File.ReadAllText(artifactPath));
    }

    [Fact]
    public void Export_SchemaPresent_Model_Still_Exports_With_Stable_Metadata_And_Deterministic_Text()
    {
        var first = ExportFixture("testdata/firmament/fixtures/m10b-valid-schema-box-export.firmament");
        var second = ExportFixture("testdata/firmament/fixtures/m10b-valid-schema-box-export.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("schema_box", first.Value.ExportedFeatureId);
        Assert.Equal(0, first.Value.ExportedOpIndex);
        Assert.Equal("primitive", first.Value.ExportedBodyCategory);
        Assert.Equal("box", first.Value.ExportedFeatureKind);
        Assert.Equal(first.Value.ExportBodyPolicy, second.Value.ExportBodyPolicy);
        Assert.Equal(first.Value.ExportBodySelectionReason, second.Value.ExportBodySelectionReason);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
    }

    [Fact]
    public void Export_CylinderExample_Exports_Successfully_With_Metadata_Deterministic_Text_And_Persisted_Artifact()
    {
        var first = ExportFixture("testdata/firmament/examples/cylinder_basic.firmament");
        var second = ExportFixture("testdata/firmament/examples/cylinder_basic.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("post", first.Value.ExportedFeatureId);
        Assert.Equal(0, first.Value.ExportedOpIndex);
        Assert.Equal("primitive", first.Value.ExportedBodyCategory);
        Assert.Equal("cylinder", first.Value.ExportedFeatureKind);
        Assert.NotEmpty(first.Value.StepText);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("CYLINDRICAL_SURFACE", first.Value.StepText, StringComparison.Ordinal);

        var artifactPath = WriteExportArtifact("m10c-cylinder.step", first.Value.StepText);
        Assert.True(File.Exists(artifactPath));
        Assert.Equal(first.Value.StepText, File.ReadAllText(artifactPath));
    }


    [Fact]
    public void Export_SphereExample_Exports_Successfully_With_Metadata_Deterministic_Text_And_Persisted_Artifact()
    {
        var first = ExportFixture("testdata/firmament/examples/sphere_basic.firmament");
        var second = ExportFixture("testdata/firmament/examples/sphere_basic.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("ball", first.Value.ExportedFeatureId);
        Assert.Equal(0, first.Value.ExportedOpIndex);
        Assert.Equal("primitive", first.Value.ExportedBodyCategory);
        Assert.Equal("sphere", first.Value.ExportedFeatureKind);
        Assert.NotEmpty(first.Value.StepText);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("SPHERICAL_SURFACE", first.Value.StepText, StringComparison.Ordinal);

        var artifactPath = WriteExportArtifact("m10d-sphere.step", first.Value.StepText);
        Assert.True(File.Exists(artifactPath));
        Assert.Equal(first.Value.StepText, File.ReadAllText(artifactPath));
    }

    [Fact]
    public void Export_MultipleExecutedGeometryBodies_Selects_Last_Executed_Geometric_Feature_Body()
    {
        var compile = CompileFixture("testdata/firmament/fixtures/m3d-mixed-primitive-boolean-validation.firmament");
        Assert.True(compile.Compilation.IsSuccess);

        var export = FirmamentStepExporter.Export(compile.Compilation.Value);
        Assert.True(export.IsSuccess);
        Assert.Equal(FirmamentStepExporter.LastExecutedGeometricBodyPolicy, export.Value.ExportBodyPolicy);
        Assert.Equal(FirmamentStepExporter.LastExecutedGeometricBodySelectionReason, export.Value.ExportBodySelectionReason);
        Assert.Equal("cap", export.Value.ExportedFeatureId);
        Assert.Equal(2, export.Value.ExportedOpIndex);
        Assert.Equal("primitive", export.Value.ExportedBodyCategory);
        Assert.Equal("box", export.Value.ExportedFeatureKind);

        var expected = Step242Exporter.ExportBody(
            compile.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives.Single(p => p.FeatureId == "cap").Body);
        Assert.True(expected.IsSuccess);
        Assert.Equal(expected.Value, export.Value.StepText);
    }

    [Fact]
    public void Export_ValidationOps_Do_Not_Become_Export_Bodies_When_Final_Geometric_Body_Precedes_Validation()
    {
        const string source = """
firmament:
  version: 1

model:
  name: validation_after_geometry
  units: mm

ops[3]:
  -
    op: box
    id: base
    size[3]:
      2
      3
      4

  -
    op: box
    id: cap
    size[3]:
      5
      6
      7

  -
    op: expect_exists
    target: cap
""";

        var result = Export(source);

        Assert.True(result.IsSuccess);
        Assert.Equal("cap", result.Value.ExportedFeatureId);
        Assert.Equal(1, result.Value.ExportedOpIndex);
        Assert.Equal("primitive", result.Value.ExportedBodyCategory);
        Assert.Equal("box", result.Value.ExportedFeatureKind);
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
    public void Export_SchemaPresence_Does_Not_Change_Step_Semantics_For_Equivalent_Geometry()
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
        Assert.Equal(first.Value.ExportedFeatureId, second.Value.ExportedFeatureId);
        Assert.Equal(first.Value.ExportedOpIndex, second.Value.ExportedOpIndex);
        Assert.Equal(first.Value.ExportedBodyCategory, second.Value.ExportedBodyCategory);
        Assert.Equal(first.Value.ExportedFeatureKind, second.Value.ExportedFeatureKind);
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

    private static string WriteExportArtifact(string fileName, string stepText)
    {
        var path = Path.Combine(
            FirmamentCorpusHarness.RepoRoot(),
            "testdata",
            "firmament",
            "exports",
            fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, stepText);
        return path;
    }
}
