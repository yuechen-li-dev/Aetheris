using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentSchemaValidationTests
{
    [Fact]
    public void Compile_WithoutSchema_StillSucceeds()
    {
        var result = Compile(BasicOpsSource);

        Assert.True(result.Compilation.IsSuccess);
        Assert.Null(result.Compilation.Value.ParsedDocument!.Schema);
    }

    [Fact]
    public void Compile_WithValidCncSchema_Succeeds_AndPreservesSchemaMetadata()
    {
        var result = Compile($"""
{Header}

schema:
  process: cnc
  minimum_tool_radius: 1.5

{OpsSingleBox}
""");

        Assert.True(result.Compilation.IsSuccess);
        var schema = result.Compilation.Value.ParsedDocument!.Schema;
        Assert.NotNull(schema);
        Assert.Equal("cnc", schema!.ProcessRaw);
        Assert.Equal(1.5d, schema.MinimumToolRadius);
    }

    [Fact]
    public void Compile_WithValidInjectionMoldedSchema_Succeeds()
    {
        var result = Compile($"""
{Header}

schema:
  process: injection_molded
  parting_plane: xy
  gate_location:
    x: 0
    y: 0
    z: 0
  draft_angle: 2

{OpsSingleBox}
""");

        Assert.True(result.Compilation.IsSuccess);
        Assert.Equal("xy", result.Compilation.Value.ParsedDocument!.Schema!.PartingPlane);
    }

    [Fact]
    public void Compile_WithValidAdditiveSchema_Succeeds()
    {
        var result = Compile($"""
{Header}

schema:
  process: additive
  printer_resolution: 0.1

{OpsSingleBox}
""");

        Assert.True(result.Compilation.IsSuccess);
        Assert.Equal(0.1d, result.Compilation.Value.ParsedDocument!.Schema!.PrinterResolution);
    }

    [Fact]
    public void Compile_Rejects_UnknownSchemaProcess()
        => AssertSchemaError("""
firmament:
  version: 1

model:
  name: demo
  units: mm

schema:
  process: laser

ops[0]:
""", FirmamentDiagnosticCodes.SchemaUnknownProcess);

    [Fact]
    public void Compile_Rejects_MissingRequiredCncField()
        => AssertSchemaError("""
firmament:
  version: 1

model:
  name: demo
  units: mm

schema:
  process: cnc

ops[0]:
""", FirmamentDiagnosticCodes.SchemaMissingRequiredField);

    [Fact]
    public void Compile_Rejects_MissingRequiredInjectionField()
        => AssertSchemaError("""
firmament:
  version: 1

model:
  name: demo
  units: mm

schema:
  process: injection_molded
  parting_plane: xy
  draft_angle: 2

ops[0]:
""", FirmamentDiagnosticCodes.SchemaMissingRequiredField);

    [Fact]
    public void Compile_Rejects_MissingRequiredAdditiveField()
        => AssertSchemaError("""
firmament:
  version: 1

model:
  name: demo
  units: mm

schema:
  process: additive

ops[0]:
""", FirmamentDiagnosticCodes.SchemaMissingRequiredField);

    [Fact]
    public void Compile_Rejects_InvalidPartingPlane()
        => AssertSchemaError("""
firmament:
  version: 1

model:
  name: demo
  units: mm

schema:
  process: injection_molded
  parting_plane: zx
  gate_location:
    x: 0
    y: 0
    z: 0
  draft_angle: 2

ops[0]:
""", FirmamentDiagnosticCodes.SchemaInvalidFieldValue);

    [Fact]
    public void Compile_Rejects_InvalidGateLocationCoordinateType()
        => AssertSchemaError("""
firmament:
  version: 1

model:
  name: demo
  units: mm

schema:
  process: injection_molded
  parting_plane: xy
  gate_location:
    x: left
    y: 0
    z: 0
  draft_angle: 2

ops[0]:
""", FirmamentDiagnosticCodes.SchemaInvalidFieldTypeOrShape);

    [Fact]
    public void Compile_Rejects_NonPositiveSchemaNumericValues()
        => AssertSchemaError("""
firmament:
  version: 1

model:
  name: demo
  units: mm

schema:
  process: additive
  printer_resolution: 0

ops[0]:
""", FirmamentDiagnosticCodes.SchemaInvalidFieldValue);

    [Fact]
    public void Compile_WithSchema_DoesNotChangeGeometryOrValidationExecution()
    {
        const string baseline = """
firmament:
  version: 1

model:
  name: demo
  units: mm

ops[2]:
  -
    op: box
    id: base
    size[3]:
      4
      2
      1
  -
    op: expect_exists
    target: base
""";

        const string withSchema = """
firmament:
  version: 1

model:
  name: demo
  units: mm

schema:
  process: cnc
  minimum_tool_radius: 1

ops[2]:
  -
    op: box
    id: base
    size[3]:
      4
      2
      1
  -
    op: expect_exists
    target: base
""";

        var first = Compile(baseline);
        var second = Compile(withSchema);

        Assert.True(first.Compilation.IsSuccess);
        Assert.True(second.Compilation.IsSuccess);
        Assert.Equal(first.Compilation.Value.PrimitiveLoweringPlan!.Primitives.Count, second.Compilation.Value.PrimitiveLoweringPlan!.Primitives.Count);
        Assert.Equal(first.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives.Count, second.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives.Count);
        Assert.Equal(first.Compilation.Value.ValidationExecutionResult!.Validations.Count, second.Compilation.Value.ValidationExecutionResult!.Validations.Count);
    }

    private static void AssertSchemaError(string source, FirmamentDiagnosticCode expectedCode)
    {
        var result = Compile(source);
        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains(expectedCode.Value, diagnostic.Message, StringComparison.Ordinal);
    }

    private static FirmamentCompileResult Compile(string source)
    {
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
    }

    private const string Header = """
firmament:
  version: 1

model:
  name: demo
  units: mm
""";

    private const string OpsSingleBox = """
ops[1]:
  -
    op: box
    id: base
    size[3]:
      1
      2
      3
""";

    private const string BasicOpsSource = Header + "\n" + OpsSingleBox;
}
