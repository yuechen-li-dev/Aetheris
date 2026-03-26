using System.Linq;
using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentSchemaCncDfmValidationTests
{
    [Fact]
    public void Compile_WithoutSchema_DoesNotApply_CncMinimumToolRadiusRule()
    {
        const string source = """
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
      6
      6
      6
  -
    op: subtract
    id: hole
    from: base
    with:
      op: cylinder
      radius: 0.5
      height: 8
""";

        var result = Compile(source);

        Assert.True(result.Compilation.IsSuccess);
        Assert.Empty(result.Compilation.Diagnostics);
        Assert.DoesNotContain(result.Compilation.Diagnostics, diagnostic => diagnostic.Message.Contains(FirmamentDiagnosticCodes.SchemaCncMinimumToolRadiusViolated.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_WithCncSchema_Allows_SubtractCylinderMeeting_MinimumToolRadius()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m8c-valid-schema-cnc-minimum-tool-radius.firmament");

        Assert.True(result.Compilation.IsSuccess);
        Assert.Empty(result.Compilation.Diagnostics);
    }

    [Fact]
    public void Compile_WithCncSchema_Rejects_SubtractCylinderBelow_MinimumToolRadius_Deterministically()
    {
        var first = CompileFixture("testdata/firmament/fixtures/m8c-invalid-schema-cnc-minimum-tool-radius.firmament");
        var second = CompileFixture("testdata/firmament/fixtures/m8c-invalid-schema-cnc-minimum-tool-radius.firmament");

        Assert.False(first.Compilation.IsSuccess);
        Assert.False(second.Compilation.IsSuccess);

        var firstDiagnostic = Assert.Single(first.Compilation.Diagnostics);
        var secondDiagnostic = Assert.Single(second.Compilation.Diagnostics);

        Assert.Equal(firstDiagnostic.Message, secondDiagnostic.Message);
        Assert.Contains(FirmamentDiagnosticCodes.SchemaCncMinimumToolRadiusViolated.Value, firstDiagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(
            $"[{FirmamentDiagnosticCodes.SchemaCncMinimumToolRadiusViolated.Value}] CNC minimum_tool_radius 2.0 exceeds subtract tool radius 1.0 for feature 'hole'.",
            firstDiagnostic.Message);
    }

    [Theory]
    [InlineData("additive", "printer_resolution: 0.1")]
    [InlineData("injection_molded", "parting_plane: xy\n  gate_location:\n    x: 0\n    y: 0\n    z: 0\n  draft_angle: 2")]
    public void Compile_WithNonCncSchema_DoesNotApply_CncMinimumToolRadiusRule(string process, string schemaFields)
    {
        var result = Compile($$"""
firmament:
  version: 1

model:
  name: demo
  units: mm

schema:
  process: {{process}}
  {{schemaFields}}

ops[2]:
  -
    op: box
    id: base
    size[3]:
      6
      6
      6
  -
    op: subtract
    id: hole
    from: base
    with:
      op: cylinder
      radius: 0.5
      height: 8
""");

        Assert.True(result.Compilation.IsSuccess);
        Assert.Empty(result.Compilation.Diagnostics);
        Assert.DoesNotContain(result.Compilation.Diagnostics, diagnostic => diagnostic.Message.Contains(FirmamentDiagnosticCodes.SchemaCncMinimumToolRadiusViolated.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_WithCncSchema_DoesNotApply_Rule_ToNonCylinderSubtractTools()
    {
        const string source = """
firmament:
  version: 1

model:
  name: demo
  units: mm

schema:
  process: cnc
  minimum_tool_radius: 2

ops[2]:
  -
    op: box
    id: base
    size[3]:
      6
      6
      6
  -
    op: subtract
    id: pocket
    from: base
    with:
      op: sphere
      radius: 1
""";

        var result = Compile(source);

        Assert.True(result.Compilation.IsSuccess);
        Assert.DoesNotContain(result.Compilation.Diagnostics, diagnostic => diagnostic.Message.Contains(FirmamentDiagnosticCodes.SchemaCncMinimumToolRadiusViolated.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_WithValidCncSchema_Preserves_Failure_Truth_For_Unsupported_Boolean_Execution()
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
      8
      8
      8
  -
    op: subtract
    id: hole
    from: base
    with:
      op: cylinder
      radius: 2
      height: 8
""";

        const string withSchema = """
firmament:
  version: 1

model:
  name: demo
  units: mm

schema:
  process: cnc
  minimum_tool_radius: 2

ops[2]:
  -
    op: box
    id: base
    size[3]:
      8
      8
      8
  -
    op: subtract
    id: hole
    from: base
    with:
      op: cylinder
      radius: 2
      height: 8
""";

        var first = Compile(baseline);
        var second = Compile(withSchema);

        Assert.True(first.Compilation.IsSuccess);
        Assert.True(second.Compilation.IsSuccess);
        Assert.Equal(first.Compilation.Diagnostics, second.Compilation.Diagnostics);
        Assert.Empty(first.Compilation.Diagnostics);
    }

    private static FirmamentCompileResult Compile(string source)
    {
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath) =>
        Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));
}
