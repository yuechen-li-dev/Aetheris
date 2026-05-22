using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Core.Brep.Queries;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentSchemaCncDfmValidationTests
{
    [Fact]
    public void Compile_WithCncSchema_CanonicalPassBody_Succeeds()
    {
        const string source = """
firmament:
  version: 1

model:
  name: demo
  units: mm

schema:
  process: cnc
  minimum_tool_radius: 0.25
  minimum_wall_thickness: 0.5

ops[1]:
  -
    op: box
    id: base
    size[3]:
      6
      4
      2
""";

        var result = Compile(source);

        Assert.True(result.Compilation.IsSuccess);
        Assert.Empty(result.Compilation.Diagnostics);
    }

    [Fact]
    public void Compile_WithCncSchema_FailsForInternalCornerToolRadiusViolation()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m8c-invalid-schema-cnc-minimum-tool-radius.firmament");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains(FirmamentDiagnosticCodes.SchemaCncManufacturabilityViolated.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains(CncManufacturabilityRuleIds.MinimumInternalCornerRadius, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_WithCncSchema_FailsForWallThicknessViolation()
    {
        const string source = """
firmament:
  version: 1

model:
  name: demo
  units: mm

schema:
  process: cnc
  minimum_tool_radius: 0.25
  minimum_wall_thickness: 1

ops[1]:
  -
    op: box
    id: thin
    size[3]:
      8
      0.5
      2
""";

        var result = Compile(source);

        Assert.False(result.Compilation.IsSuccess);
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Message.Contains(FirmamentDiagnosticCodes.SchemaCncManufacturabilityViolated.Value, StringComparison.Ordinal)
            && diagnostic.Message.Contains(CncManufacturabilityRuleIds.MinimumWallThickness, StringComparison.Ordinal)
            && diagnostic.Message.Contains("required>=1", StringComparison.Ordinal));
    }

    private static FirmamentCompileResult Compile(string source)
    {
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath) =>
        Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));
}
