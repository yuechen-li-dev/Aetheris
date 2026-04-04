using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentDocumentCoherenceValidationTests
{
    [Fact]
    public void Compiler_Accepts_UniqueFeatureIds()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m2a-valid-two-primitives-distinct-ids.firmament");
        Assert.True(result.Compilation.IsSuccess);
    }

    [Fact]
    public void Compiler_Rejects_DuplicateFeatureId_Deterministically()
    {
        var fixturePath = "testdata/firmament/fixtures/m2a-invalid-duplicate-id-across-primitive-boolean.firmament";
        var first = CompileFixture(fixturePath);
        var second = CompileFixture(fixturePath);

        Assert.False(first.Compilation.IsSuccess);
        Assert.False(second.Compilation.IsSuccess);

        var firstDiagnostic = Assert.Single(first.Compilation.Diagnostics);
        var secondDiagnostic = Assert.Single(second.Compilation.Diagnostics);

        Assert.Equal($"[{FirmamentDiagnosticCodes.ReferenceDuplicateFeatureId.Value}] Feature-producing op 'add' at index 1 reuses duplicate feature id 'base'.", firstDiagnostic.Message);
        Assert.Equal(firstDiagnostic.Message, secondDiagnostic.Message);
        Assert.Equal(firstDiagnostic.Code, secondDiagnostic.Code);
        Assert.Equal(firstDiagnostic.Source, secondDiagnostic.Source);
    }

    [Fact]
    public void Compiler_Accepts_BooleanReferencesToPriorFeatures()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m2a-valid-chained-features.firmament");
        Assert.True(result.Compilation.IsSuccess);
    }

    [Fact]
    public void Compiler_Rejects_PatternCircular_With_Unknown_Axis_Root()
    {
        var result = CompileFixture("testdata/firmament/fixtures/p2_invalid_pattern_unknown_axis.firmament");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Equal(
            $"[{FirmamentDiagnosticCodes.ValidationTargetUnknownSelectorRootFeatureId.Value}] Pattern op 'pattern_circular' at index 2 references unknown selector root feature id 'missing' via field 'axis'.",
            diagnostic.Message);
    }

    [Theory]
    [InlineData("testdata/firmament/fixtures/m2a-invalid-add-to-missing-id.firmament", "add", "to", "missing")]
    [InlineData("testdata/firmament/fixtures/m2a-invalid-subtract-from-missing-id.firmament", "subtract", "from", "missing")]
    [InlineData("testdata/firmament/fixtures/m2a-invalid-intersect-left-missing-id.firmament", "intersect", "left", "missing")]
    [InlineData("testdata/firmament/fixtures/m2a-invalid-forward-reference.firmament", "add", "to", "later")]
    public void Compiler_Rejects_BooleanReference_ToUnknownOrForwardFeatureId(string fixturePath, string opName, string fieldName, string refId)
    {
        var result = CompileFixture(fixturePath);

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Equal(
            $"[{FirmamentDiagnosticCodes.ReferenceUnknownFeatureId.Value}] Boolean op '{opName}' at index 0 references unknown feature id '{refId}' via field '{fieldName}'.",
            diagnostic.Message);
    }

    [Fact]
    public void Compiler_Leaves_SelectorShapedValidationTargets_Unresolved()
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
              2
              3
          -
            op: expect_exists
            target: base.top_face
          -
            op: expect_selectable
            target: base.edges
            count: 1
        """;

        var compiler = new FirmamentCompiler();
        var result = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.True(result.Compilation.IsSuccess);
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath)
    {
        var fixtureText = FirmamentCorpusHarness.ReadFixtureText(fixturePath);
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(fixtureText)));
    }
}
