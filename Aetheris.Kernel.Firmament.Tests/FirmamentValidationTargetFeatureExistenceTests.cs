using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed partial class FirmamentValidationTargetFeatureExistenceTests
{
    [Theory]
    [InlineData("testdata/firmament/fixtures/m2c-valid-expect-exists-bare-target-earlier.firmament")]
    [InlineData("testdata/firmament/fixtures/m2c-valid-expect-selectable-bare-target-earlier.firmament")]
    [InlineData("testdata/firmament/fixtures/m4a-valid-mixed-bare-and-selector-targets.firmament")]
    public void Compiler_Accepts_BareValidationTargets_WhenFeatureDefinedEarlier(string fixturePath)
    {
        var result = CompileFixture(fixturePath);

        Assert.True(result.Compilation.IsSuccess);
    }

    [Theory]
    [InlineData("testdata/firmament/fixtures/m2c-invalid-expect-exists-bare-target-nonexistent.firmament", "expect_exists", "missing")]
    [InlineData("testdata/firmament/fixtures/m2c-invalid-expect-selectable-bare-target-nonexistent.firmament", "expect_selectable", "missing")]
    [InlineData("testdata/firmament/fixtures/m2c-invalid-expect-exists-bare-target-forward.firmament", "expect_exists", "later")]
    [InlineData("testdata/firmament/fixtures/m2c-invalid-expect-selectable-bare-target-forward.firmament", "expect_selectable", "later")]
    public void Compiler_Rejects_BareValidationTargets_WhenFeatureMissingOrForward(string fixturePath, string opName, string targetId)
    {
        var first = CompileFixture(fixturePath);
        var second = CompileFixture(fixturePath);

        Assert.False(first.Compilation.IsSuccess);
        Assert.False(second.Compilation.IsSuccess);

        var firstDiagnostic = Assert.Single(first.Compilation.Diagnostics);
        var secondDiagnostic = Assert.Single(second.Compilation.Diagnostics);

        Assert.Equal(firstDiagnostic.Message, secondDiagnostic.Message);
        Assert.Equal(
            $"[{FirmamentDiagnosticCodes.ValidationTargetUnknownFeatureId.Value}] Validation op '{opName}' at index 0 references unknown feature id '{targetId}' via field 'target'.",
            firstDiagnostic.Message);
    }

    [Theory]
    [InlineData("testdata/firmament/fixtures/m4a-valid-expect-exists-selector-target-root-earlier.firmament")]
    [InlineData("testdata/firmament/fixtures/m4a-valid-expect-selectable-selector-target-root-earlier.firmament")]
    [InlineData("testdata/firmament/fixtures/m2c-valid-expect-exists-selector-target-unresolved.firmament")]
    [InlineData("testdata/firmament/fixtures/m2c-valid-expect-selectable-selector-target-unresolved.firmament")]
    [InlineData("testdata/firmament/fixtures/m4b-valid-expect-exists-selector-target-port-surface-valid.firmament")]
    [InlineData("testdata/firmament/fixtures/m4b-valid-expect-selectable-selector-target-port-surface-valid.firmament")]
    [InlineData("testdata/firmament/fixtures/m4b-valid-mixed-bare-and-selector-targets.firmament")]
    [InlineData("testdata/firmament/fixtures/m4c-valid-primitive-selector-contracts.firmament")]
    public void Compiler_Accepts_SelectorShapedValidationTargets_WhenRootFeatureDefinedEarlier(string fixturePath)
    {
        var result = CompileFixture(fixturePath);

        Assert.True(result.Compilation.IsSuccess);
    }

    [Theory]
    [InlineData("testdata/firmament/fixtures/m4a-invalid-expect-exists-selector-target-root-nonexistent.firmament", "expect_exists", "ghost")]
    [InlineData("testdata/firmament/fixtures/m4a-invalid-expect-selectable-selector-target-root-nonexistent.firmament", "expect_selectable", "ghost")]
    [InlineData("testdata/firmament/fixtures/m4a-invalid-expect-exists-selector-target-root-forward.firmament", "expect_exists", "later")]
    [InlineData("testdata/firmament/fixtures/m4a-invalid-expect-selectable-selector-target-root-forward.firmament", "expect_selectable", "later")]
    public void Compiler_Rejects_SelectorShapedValidationTargets_WhenRootMissingOrForward(string fixturePath, string opName, string rootFeatureId)
    {
        var first = CompileFixture(fixturePath);
        var second = CompileFixture(fixturePath);

        Assert.False(first.Compilation.IsSuccess);
        Assert.False(second.Compilation.IsSuccess);

        var firstDiagnostic = Assert.Single(first.Compilation.Diagnostics);
        var secondDiagnostic = Assert.Single(second.Compilation.Diagnostics);

        Assert.Equal(firstDiagnostic.Message, secondDiagnostic.Message);
        Assert.Equal(
            $"[{FirmamentDiagnosticCodes.ValidationTargetUnknownSelectorRootFeatureId.Value}] Validation op '{opName}' at index 0 references unknown selector root feature id '{rootFeatureId}' via field 'target'.",
            firstDiagnostic.Message);
    }



    [Theory]
    [InlineData("testdata/firmament/fixtures/m4b-invalid-expect-exists-selector-target-port-starts-digit.firmament", "expect_exists", "1port")]
    [InlineData("testdata/firmament/fixtures/m4b-invalid-expect-selectable-selector-target-port-invalid-punctuation.firmament", "expect_selectable", "top-face")]
    [InlineData("testdata/firmament/fixtures/m4b-invalid-expect-selectable-selector-target-port-whitespace.firmament", "expect_selectable", "top face")]
    [InlineData("testdata/firmament/fixtures/m4b-invalid-expect-exists-selector-target-port-invalid-symbol.firmament", "expect_exists", "face$")]
    public void Compiler_Rejects_SelectorShapedValidationTargets_WhenPortTokenMalformed(string fixturePath, string opName, string portToken)
    {
        var first = CompileFixture(fixturePath);
        var second = CompileFixture(fixturePath);

        Assert.False(first.Compilation.IsSuccess);
        Assert.False(second.Compilation.IsSuccess);

        var firstDiagnostic = Assert.Single(first.Compilation.Diagnostics);
        var secondDiagnostic = Assert.Single(second.Compilation.Diagnostics);

        Assert.Equal(firstDiagnostic.Message, secondDiagnostic.Message);
        Assert.Equal(
            $"[{FirmamentDiagnosticCodes.ValidationTargetInvalidSelectorPortToken.Value}] Validation op '{opName}' at index 1 has invalid selector port token '{portToken}' via field 'target'.",
            firstDiagnostic.Message);
    }



    [Theory]
    [InlineData("testdata/firmament/fixtures/m4c-invalid-expect-exists-selector-target-port-not-allowed-box.firmament", "expect_exists", "box", "circular_edges", "box", 1)]
    [InlineData("testdata/firmament/fixtures/m4c-invalid-expect-exists-selector-target-port-not-allowed-sphere.firmament", "expect_exists", "sphere", "top_face", "sphere", 1)]
    [InlineData("testdata/firmament/fixtures/m4c-invalid-expect-selectable-selector-target-port-not-allowed-cylinder.firmament", "expect_selectable", "cylinder", "surface", "cylinder", 1)]
    [InlineData("testdata/firmament/fixtures/m4c-mixed-valid-and-invalid-selector-contracts.firmament", "expect_exists", "sph", "top_face", "sphere", 5)]
    public void Compiler_Rejects_SelectorShapedValidationTargets_WhenPortNotAllowedByPrimitiveFeatureContract(string fixturePath, string opName, string featureId, string portToken, string featureKind, int opIndex)
    {
        var first = CompileFixture(fixturePath);
        var second = CompileFixture(fixturePath);

        Assert.False(first.Compilation.IsSuccess);
        Assert.False(second.Compilation.IsSuccess);

        var firstDiagnostic = Assert.Single(first.Compilation.Diagnostics);
        var secondDiagnostic = Assert.Single(second.Compilation.Diagnostics);

        Assert.Equal(firstDiagnostic.Message, secondDiagnostic.Message);
        Assert.Equal(
            $"[{FirmamentDiagnosticCodes.ValidationTargetSelectorPortNotAllowedForFeatureKind.Value}] Validation op '{opName}' at index {opIndex} has selector port '{portToken}' not allowed for feature kind '{featureKind}' on feature id '{featureId}' via field 'target'.",
            firstDiagnostic.Message);
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath)
    {
        var fixtureText = FirmamentCorpusHarness.ReadFixtureText(fixturePath);
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(fixtureText)));
    }
}

public sealed partial class FirmamentValidationTargetFeatureExistenceTests
{
    [Theory]
    [InlineData("base.top_face", "Face", "One")]
    [InlineData("base.side_faces", "FaceSet", "Many")]
    [InlineData("cyl.side_face", "Face", "One")]
    [InlineData("cyl.circular_edges", "EdgeSet", "Many")]
    [InlineData("sphere.surface", "Face", "One")]
    [InlineData("sphere.vertices", "VertexSet", "Many")]
    public void Compiler_Attaches_SelectorContractMetadata_For_Legal_SelectorTargets(string selectorTarget, string expectedResultKind, string expectedCardinality)
    {
        var source = $$"""
firmament:
  version: 1

model:
  name: demo
  units: mm

ops[4]:
  -
    op: box
    id: base
    size[3]:
      10
      20
      30
  -
    op: cylinder
    id: cyl
    radius: 2
    height: 5
  -
    op: sphere
    id: sphere
    radius: 4
  -
    op: expect_exists
    target: {{selectorTarget}}
""";

        var result = new FirmamentCompiler().Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.True(result.Compilation.IsSuccess);
        var validationOp = result.Compilation.Value.ParsedDocument!.Ops.Entries[3];
        Assert.NotNull(validationOp.ClassifiedFields);
        Assert.Equal("SelectorShaped", validationOp.ClassifiedFields!["targetShape"]);
        Assert.Equal(expectedResultKind, validationOp.ClassifiedFields["selectorResultKind"]);
        Assert.Equal(expectedCardinality, validationOp.ClassifiedFields["selectorCardinality"]);
    }

    [Fact]
    public void Compiler_DoesNotAttach_SelectorMetadata_For_BareFeatureIdTarget()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m2c-valid-expect-exists-bare-target-earlier.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validationOp = result.Compilation.Value.ParsedDocument!.Ops.Entries[1];
        Assert.NotNull(validationOp.ClassifiedFields);
        Assert.Equal("FeatureId", validationOp.ClassifiedFields!["targetShape"]);
        Assert.False(validationOp.ClassifiedFields.ContainsKey("selectorResultKind"));
        Assert.False(validationOp.ClassifiedFields.ContainsKey("selectorCardinality"));
    }

    [Fact]
    public void Compiler_DoesNotAttach_SelectorMetadata_For_NonSelectorValidationOps()
    {
        var source = """
firmament:
  version: 1

model:
  name: demo
  units: mm

ops[1]:
  -
    op: expect_manifold
""";

        var result = new FirmamentCompiler().Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.True(result.Compilation.IsSuccess);
        var validationOp = result.Compilation.Value.ParsedDocument!.Ops.Entries[0];
        Assert.Null(validationOp.ClassifiedFields);
    }

    [Theory]
    [InlineData("add", "join", "to", "join.top_face", "Face", "One")]
    [InlineData("subtract", "cut", "from", "cut.side_faces", "FaceSet", "Many")]
    [InlineData("intersect", "clip", "left", "clip.edges", "EdgeSet", "Many")]
    public void Compiler_Attaches_SelectorContractMetadata_For_Legal_BooleanRoot_SelectorTargets(
        string booleanOp,
        string featureId,
        string referenceField,
        string selectorTarget,
        string expectedResultKind,
        string expectedCardinality)
    {
        var source = $$"""
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
      10
      20
      30
  -
    op: {{booleanOp}}
    id: {{featureId}}
    {{referenceField}}: base
    with:
      op: box
      size[3]:
        1
        1
        1
  -
    op: expect_exists
    target: {{selectorTarget}}
""";

        var result = new FirmamentCompiler().Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.True(result.Compilation.IsSuccess);
        var validationOp = result.Compilation.Value.ParsedDocument!.Ops.Entries[2];
        Assert.NotNull(validationOp.ClassifiedFields);
        Assert.Equal("SelectorShaped", validationOp.ClassifiedFields!["targetShape"]);
        Assert.Equal(expectedResultKind, validationOp.ClassifiedFields["selectorResultKind"]);
        Assert.Equal(expectedCardinality, validationOp.ClassifiedFields["selectorCardinality"]);
    }

    [Fact]
    public void Compiler_Rejects_SelectorShapedValidationTargets_WhenPortNotAllowedByBooleanFeatureContract()
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
      10
      20
      30
  -
    op: subtract
    id: cut
    from: base
    with:
      op: box
      size[3]:
        1
        1
        1
  -
    op: expect_exists
    target: cut.circular_edges
""";

        var first = new FirmamentCompiler().Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
        var second = new FirmamentCompiler().Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));

        Assert.False(first.Compilation.IsSuccess);
        Assert.False(second.Compilation.IsSuccess);

        var firstDiagnostic = Assert.Single(first.Compilation.Diagnostics);
        var secondDiagnostic = Assert.Single(second.Compilation.Diagnostics);

        Assert.Equal(firstDiagnostic.Message, secondDiagnostic.Message);
        Assert.Equal(
            $"[{FirmamentDiagnosticCodes.ValidationTargetSelectorPortNotAllowedForFeatureKind.Value}] Validation op 'expect_exists' at index 2 has selector port 'circular_edges' not allowed for feature kind 'subtract' on feature id 'cut' via field 'target'.",
            firstDiagnostic.Message);
    }
}
