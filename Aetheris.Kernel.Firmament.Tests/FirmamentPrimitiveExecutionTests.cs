using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentPrimitiveExecutionTests
{
    [Fact]
    public void Compile_Executes_Box_Primitive_Into_Real_Body()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3c-valid-box-exec.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var executed = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
        Assert.Equal("base", executed.FeatureId);
        Assert.Equal(FirmamentLoweredPrimitiveKind.Box, executed.Kind);
        Assert.NotEmpty(executed.Body.Topology.Bodies);
        Assert.NotEmpty(executed.Body.Topology.Faces);
    }

    [Fact]
    public void Compile_Executes_Cylinder_Primitive_Into_Real_Body()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3c-valid-cylinder-exec.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var executed = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
        Assert.Equal("post", executed.FeatureId);
        Assert.Equal(FirmamentLoweredPrimitiveKind.Cylinder, executed.Kind);
        Assert.NotEmpty(executed.Body.Topology.Bodies);
        Assert.NotEmpty(executed.Body.Topology.Faces);
    }

    [Fact]
    public void Compile_Executes_Sphere_Primitive_Into_Real_Body()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3c-valid-sphere-exec.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var executed = Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
        Assert.Equal("ball", executed.FeatureId);
        Assert.Equal(FirmamentLoweredPrimitiveKind.Sphere, executed.Kind);
        Assert.NotEmpty(executed.Body.Topology.Bodies);
        Assert.NotEmpty(executed.Body.Topology.Faces);
    }

    [Fact]
    public void Compile_Executes_Multiple_Primitives_In_Source_Order_With_Preserved_Ids()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3c-valid-multiple-primitives-exec.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var executed = result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives;

        Assert.Collection(
            executed,
            first =>
            {
                Assert.Equal(0, first.OpIndex);
                Assert.Equal("base", first.FeatureId);
                Assert.Equal(FirmamentLoweredPrimitiveKind.Box, first.Kind);
            },
            second =>
            {
                Assert.Equal(1, second.OpIndex);
                Assert.Equal("post", second.FeatureId);
                Assert.Equal(FirmamentLoweredPrimitiveKind.Cylinder, second.Kind);
            },
            third =>
            {
                Assert.Equal(2, third.OpIndex);
                Assert.Equal("ball", third.FeatureId);
                Assert.Equal(FirmamentLoweredPrimitiveKind.Sphere, third.Kind);
            });
    }

    [Fact]
    public void Compile_Mixed_Document_Executes_Only_Primitives_And_Leaves_Booleans_NonExecuted()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3c-mixed-primitive-boolean-validation.firmament");

        Assert.True(result.Compilation.IsSuccess);
        var artifact = result.Compilation.Value;

        Assert.Equal("firmament-primitives-executed", artifact.ArtifactKind);
        Assert.Equal(2, artifact.PrimitiveLoweringPlan!.Primitives.Count);
        Assert.Equal(2, artifact.PrimitiveLoweringPlan.Booleans.Count);
        Assert.Single(artifact.PrimitiveLoweringPlan.SkippedOps);

        var executed = artifact.PrimitiveExecutionResult!.ExecutedPrimitives;
        Assert.Equal(2, executed.Count);
        Assert.Equal(new[] { "base", "cap" }, executed.Select(p => p.FeatureId).ToArray());
    }

    [Fact]
    public void Compile_ValidationFailure_Still_Fails_Before_Execution()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m1a-invalid-sphere-missing-radius.firmament");

        Assert.False(result.Compilation.IsSuccess);
    }

    [Fact]
    public void Compile_Primitive_Execution_Output_Is_Deterministic_For_Metadata()
    {
        var fixture = "testdata/firmament/fixtures/m3c-valid-multiple-primitives-exec.firmament";

        var first = CompileFixture(fixture);
        var second = CompileFixture(fixture);

        Assert.True(first.Compilation.IsSuccess);
        Assert.True(second.Compilation.IsSuccess);

        var firstMetadata = first.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives
            .Select(p => (p.OpIndex, p.FeatureId, p.Kind, BodyCount: p.Body.Topology.Bodies.Count(), FaceCount: p.Body.Topology.Faces.Count()))
            .ToArray();
        var secondMetadata = second.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives
            .Select(p => (p.OpIndex, p.FeatureId, p.Kind, BodyCount: p.Body.Topology.Bodies.Count(), FaceCount: p.Body.Topology.Faces.Count()))
            .ToArray();

        Assert.Equal(firstMetadata, secondMetadata);
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText(fixturePath);
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
    }
}
