using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentSchemaEnclosedVoidValidationTests
{
    [Fact]
    public void Compile_WithoutSchema_Rejects_FullyEnclosedVoid_ByDefault()
    {
        var result = Compile("""
firmament:
  version: 1

model:
  name: enclosed_void_default
  units: mm

ops[2]:
  -
    op: box
    id: root
    size[3]:
      30
      20
      12
  -
    op: subtract
    id: cavity
    from: root
    with:
      op: sphere
      radius: 4
""");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains(FirmamentDiagnosticCodes.SchemaEnclosedVoidsNotAllowed.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("process 'default'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("fully enclosed internal void", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_WithCncSchema_Rejects_FullyEnclosedVoid()
    {
        var result = Compile("""
firmament:
  version: 1

model:
  name: enclosed_void_cnc
  units: mm

schema:
  process: cnc
  minimum_tool_radius: 1

ops[2]:
  -
    op: box
    id: root
    size[3]:
      30
      20
      12
  -
    op: subtract
    id: cavity
    from: root
    with:
      op: sphere
      radius: 4
""");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains(FirmamentDiagnosticCodes.SchemaEnclosedVoidsNotAllowed.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("process 'cnc'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_BlindHole_Remains_Allowed_UnderDefaultPolicy()
    {
        var result = CompileFixture("testdata/firmament/examples/p1_blind_hole_on_face_semantic.firmament");

        Assert.True(result.Compilation.IsSuccess);
        Assert.DoesNotContain(
            result.Compilation.Diagnostics,
            diagnostic => diagnostic.Message.Contains(FirmamentDiagnosticCodes.SchemaEnclosedVoidsNotAllowed.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_ThroughHole_Remains_Allowed_UnderDefaultPolicy()
    {
        var result = CompileFixture("testdata/firmament/examples/boolean_box_cylinder_hole.firmament");

        Assert.True(result.Compilation.IsSuccess);
        Assert.DoesNotContain(
            result.Compilation.Diagnostics,
            diagnostic => diagnostic.Message.Contains(FirmamentDiagnosticCodes.SchemaEnclosedVoidsNotAllowed.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_Counterbore_Composition_Remains_Allowed_UnderDefaultPolicy()
    {
        var result = CompileFixture("Aetheris.Firmament.FrictionLab/Cases/counterbore-hole/part.firmament");

        Assert.True(result.Compilation.IsSuccess);
        Assert.DoesNotContain(
            result.Compilation.Diagnostics,
            diagnostic => diagnostic.Message.Contains(FirmamentDiagnosticCodes.SchemaEnclosedVoidsNotAllowed.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_WithAdditiveSchema_Allows_FullyEnclosedVoid()
    {
        var result = Compile("""
firmament:
  version: 1

model:
  name: enclosed_void_additive
  units: mm

schema:
  process: additive
  printer_resolution: 0.1

ops[2]:
  -
    op: box
    id: root
    size[3]:
      30
      20
      12
  -
    op: subtract
    id: cavity
    from: root
    with:
      op: sphere
      radius: 4
""");

        Assert.True(result.Compilation.IsSuccess);
        Assert.DoesNotContain(
            result.Compilation.Diagnostics,
            diagnostic => diagnostic.Message.Contains(FirmamentDiagnosticCodes.SchemaEnclosedVoidsNotAllowed.Value, StringComparison.Ordinal));
    }

    private static FirmamentCompileResult Compile(string source)
    {
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath)
        => Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));
}
