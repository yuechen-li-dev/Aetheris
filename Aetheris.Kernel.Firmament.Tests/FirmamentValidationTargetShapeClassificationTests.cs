using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentValidationTargetShapeClassificationTests
{
    [Theory]
    [InlineData("expect_exists", "base", "FeatureId")]
    [InlineData("expect_exists", "base.top_face", "SelectorShaped")]
    [InlineData("expect_selectable", "base", "FeatureId")]
    [InlineData("expect_selectable", "mount_hole.entry_face", "SelectorShaped")]
    public void Compiler_Classifies_ValidationTargets_BySurfaceShape(string opName, string target, string expectedShape)
    {
        var selectorRoot = target.Split('.', 2, StringSplitOptions.None)[0];
        var source = string.Equals(expectedShape, "FeatureId", StringComparison.Ordinal)
            ? $$"""
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
                  1
                  1
                  1
              -
                op: {{opName}}
                target: {{target}}
                {{(opName == "expect_selectable" ? "count: 1" : string.Empty)}}
            """
            : $$"""
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[2]:
              -
                op: box
                id: {{selectorRoot}}
                size[3]:
                  1
                  1
                  1
              -
                op: {{opName}}
                target: {{target}}
                {{(opName == "expect_selectable" ? "count: 1" : string.Empty)}}
            """;

        var result = Compile(source);

        Assert.True(result.Compilation.IsSuccess);
        var op = result.Compilation.Value.ParsedDocument!.Ops.Entries[^1];
        Assert.NotNull(op.ClassifiedFields);
        Assert.Equal(expectedShape, op.ClassifiedFields!["targetShape"]);
    }

    [Theory]
    [InlineData("expect_exists", ".top_face")]
    [InlineData("expect_exists", "base.")]
    [InlineData("expect_exists", "base.top.face")]
    [InlineData("expect_selectable", "base top_face")]
    [InlineData("expect_selectable", "1base")]
    public void Compiler_Rejects_MalformedValidationTargetShape_Deterministically(string opName, string target)
    {
        var source = $$"""
        firmament:
          version: 1
        
        model:
          name: demo
          units: mm
        
        ops[1]:
          -
            op: {{opName}}
            target: {{target}}
            {{(opName == "expect_selectable" ? "count: 1" : string.Empty)}}
        """;

        var first = Compile(source);
        var second = Compile(source);

        Assert.False(first.Compilation.IsSuccess);
        Assert.False(second.Compilation.IsSuccess);

        var firstDiagnostic = Assert.Single(first.Compilation.Diagnostics);
        var secondDiagnostic = Assert.Single(second.Compilation.Diagnostics);

        Assert.Equal(firstDiagnostic.Message, secondDiagnostic.Message);
        Assert.Contains($"[{FirmamentDiagnosticCodes.ValidationInvalidTargetShape.Value}]", firstDiagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compiler_DoesNotResolve_SelectorPorts_Semantically()
    {
        const string source = """
        firmament:
          version: 1

        model:
          name: demo
          units: mm

        ops[3]:
          -
            op: box
            id: base
            size[3]:
              1
              1
              1
          -
            op: expect_exists
            target: base.nonexistent_port_name
          -
            op: expect_selectable
            target: base.any_other_port
            count: 1
        """;

        var result = Compile(source);

        Assert.True(result.Compilation.IsSuccess);
    }

    private static FirmamentCompileResult Compile(string source)
    {
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
    }
}
