using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentValidationExecutionTests
{
    [Fact]
    public void Compile_Executes_ExpectExists_For_BareFeatureId_Target()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m2c-valid-expect-exists-bare-target-earlier.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validation = Assert.Single(result.Compilation.Value.ValidationExecutionResult!.Validations);
        Assert.Equal(1, validation.OpIndex);
        Assert.Equal(FirmamentKnownOpKind.ExpectExists, validation.Kind);
        Assert.Equal("base", validation.Target);
        Assert.True(validation.IsExecuted);
        Assert.True(validation.IsSuccess);
        Assert.Null(validation.Reason);
    }

    [Fact]
    public void Compile_Executes_ExpectExists_For_TopologyBacked_SelectorTargets()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m6b-valid-selector-exists.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validations = result.Compilation.Value.ValidationExecutionResult!.Validations;
        Assert.Equal(4, validations.Count);
        Assert.All(validations, validation =>
        {
            Assert.True(validation.IsExecuted);
            Assert.True(validation.IsSuccess);
            Assert.Null(validation.Reason);
        });
    }

    [Fact]
    public void Compile_Executes_ExpectExists_For_Empty_SelectorResolution_As_Deterministic_Failure()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m6b-invalid-selector-empty.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validation = Assert.Single(result.Compilation.Value.ValidationExecutionResult!.Validations);
        Assert.True(validation.IsExecuted);
        Assert.False(validation.IsSuccess);
        Assert.Equal("Selector 'sphere.edges' resolved to no topology elements.", validation.Reason);

    }

    [Fact]
    public void Compile_Executes_ExpectSelectable_One_With_Count_One()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m6a-valid-expect-selectable-one-count1.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validation = Assert.Single(result.Compilation.Value.ValidationExecutionResult!.Validations);
        Assert.True(validation.IsExecuted);
        Assert.True(validation.IsSuccess);
    }

    [Fact]
    public void Compile_Executes_ExpectSelectable_One_With_Count_Greater_Than_One_As_Failure()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m6a-invalid-expect-selectable-one-count2.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validation = Assert.Single(result.Compilation.Value.ValidationExecutionResult!.Validations);
        Assert.True(validation.IsExecuted);
        Assert.False(validation.IsSuccess);
        Assert.Contains("incompatible", validation.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_Executes_ExpectSelectable_Many_With_Plural_Count()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m6a-valid-expect-selectable-many-count2.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validation = Assert.Single(result.Compilation.Value.ValidationExecutionResult!.Validations);
        Assert.True(validation.IsExecuted);
        Assert.True(validation.IsSuccess);
    }

    [Fact]
    public void Compile_Records_Unsupported_For_ExpectSelectable_On_BareFeatureId_Target()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m6a-unsupported-expect-selectable-bare-target.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validation = Assert.Single(result.Compilation.Value.ValidationExecutionResult!.Validations);
        Assert.False(validation.IsExecuted);
        Assert.False(validation.IsSuccess);
        Assert.Contains("does not support bare feature-id", validation.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_Records_Unsupported_For_ExpectManifold_At_M6a()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m6a-unsupported-expect-manifold.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var validation = Assert.Single(result.Compilation.Value.ValidationExecutionResult!.Validations);
        Assert.Equal(FirmamentKnownOpKind.ExpectManifold, validation.Kind);
        Assert.False(validation.IsExecuted);
        Assert.False(validation.IsSuccess);
        Assert.Contains("unsupported", validation.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_Preserves_ValidationExecution_Source_Order_And_OpIndices()
    {
        const string source = """
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
      1
      2
      3
  -
    op: expect_exists
    target: base
  -
    op: expect_selectable
    target: base.top_face
    count: 1
  -
    op: expect_manifold
""";

        var result = Compile(source);

        Assert.True(result.Compilation.IsSuccess);
        var validations = result.Compilation.Value.ValidationExecutionResult!.Validations;
        Assert.Collection(
            validations,
            first =>
            {
                Assert.Equal(1, first.OpIndex);
                Assert.Equal(FirmamentKnownOpKind.ExpectExists, first.Kind);
            },
            second =>
            {
                Assert.Equal(2, second.OpIndex);
                Assert.Equal(FirmamentKnownOpKind.ExpectSelectable, second.Kind);
            },
            third =>
            {
                Assert.Equal(3, third.OpIndex);
                Assert.Equal(FirmamentKnownOpKind.ExpectManifold, third.Kind);
            });
    }

    [Fact]
    public void Compile_Still_Executes_Primitives_And_Booleans_With_M6b_ValidationExecution_Output()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3d-mixed-primitive-boolean-validation.firmament");

        Assert.True(result.Compilation.IsSuccess);
        Assert.NotEmpty(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
        Assert.NotEmpty(result.Compilation.Value.ValidationExecutionResult!.Validations);
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath) =>
        Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));

    private static FirmamentCompileResult Compile(string source)
    {
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
    }
}
