using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentPrimitiveLoweringTests
{
    [Fact]
    public void Compile_Lowers_Box_Primitive()
    {
        var result = Compile(
            """
            firmament:
              version: 1

            model:
              name: demo
              units: mm

            ops[1]:
              -
                op: box
                id: base
                size[3]:
                  10
                  20
                  30
            """);

        Assert.True(result.Compilation.IsSuccess);
        var primitive = Assert.Single(result.Compilation.Value.PrimitiveLoweringPlan!.Primitives);
        var parameters = Assert.IsType<FirmamentLoweredBoxParameters>(primitive.Parameters);
        Assert.Equal("base", primitive.FeatureId);
        Assert.Equal(FirmamentLoweredPrimitiveKind.Box, primitive.Kind);
        Assert.Equal(10, parameters.SizeX);
        Assert.Equal(20, parameters.SizeY);
        Assert.Equal(30, parameters.SizeZ);
    }

    [Fact]
    public void Compile_Lowers_Cylinder_Primitive()
    {
        var result = Compile(
            """
            firmament:
              version: 1

            model:
              name: demo
              units: mm

            ops[1]:
              -
                op: cylinder
                id: hole
                radius: 3.5
                height: 8
            """);

        Assert.True(result.Compilation.IsSuccess);
        var primitive = Assert.Single(result.Compilation.Value.PrimitiveLoweringPlan!.Primitives);
        var parameters = Assert.IsType<FirmamentLoweredCylinderParameters>(primitive.Parameters);
        Assert.Equal("hole", primitive.FeatureId);
        Assert.Equal(3.5, parameters.Radius);
        Assert.Equal(8, parameters.Height);
    }

    [Fact]
    public void Compile_Lowers_Sphere_Primitive()
    {
        var result = Compile(
            """
            firmament:
              version: 1

            model:
              name: demo
              units: mm

            ops[1]:
              -
                op: sphere
                id: ball
                radius: 2
            """);

        Assert.True(result.Compilation.IsSuccess);
        var primitive = Assert.Single(result.Compilation.Value.PrimitiveLoweringPlan!.Primitives);
        var parameters = Assert.IsType<FirmamentLoweredSphereParameters>(primitive.Parameters);
        Assert.Equal("ball", primitive.FeatureId);
        Assert.Equal(2, parameters.Radius);
    }

    [Fact]
    public void Compile_Lowers_Multiple_Primitives_In_Source_Order()
    {
        var result = Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m3a-valid-multiple-primitives-lower.firmament"));

        Assert.True(result.Compilation.IsSuccess);
        var plan = result.Compilation.Value.PrimitiveLoweringPlan!;
        Assert.Collection(
            plan.Primitives,
            p => Assert.Equal("b1", p.FeatureId),
            p => Assert.Equal("c1", p.FeatureId),
            p => Assert.Equal("s1", p.FeatureId));
        Assert.Empty(plan.SkippedOps);
    }

    [Fact]
    public void Compile_Mixed_Primitive_And_Boolean_Produces_Explicit_SkippedOps()
    {
        var result = Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m3a-mixed-primitive-boolean.firmament"));

        Assert.True(result.Compilation.IsSuccess);
        var plan = result.Compilation.Value.PrimitiveLoweringPlan!;
        var primitive = Assert.Single(plan.Primitives);
        var skipped = Assert.Single(plan.SkippedOps);

        Assert.Equal("base", primitive.FeatureId);
        Assert.Equal(1, skipped.OpIndex);
        Assert.Equal("add", skipped.OpName);
        Assert.Equal("unsupported-op-in-m3a-primitive-only-lowering", skipped.Reason);
    }

    [Fact]
    public void Compile_Primitive_Lowering_Output_Is_Deterministic()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m3a-valid-primitive-only-lower.firmament");

        var first = Compile(source);
        var second = Compile(source);

        Assert.True(first.Compilation.IsSuccess);
        Assert.True(second.Compilation.IsSuccess);
        Assert.Equivalent(first.Compilation.Value.PrimitiveLoweringPlan, second.Compilation.Value.PrimitiveLoweringPlan);
    }

    [Fact]
    public void Compile_ValidationFailure_Prevents_Lowering()
    {
        var result = Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m1a-invalid-sphere-missing-radius.firmament"));

        Assert.False(result.Compilation.IsSuccess);
    }

    private static FirmamentCompileResult Compile(string source)
    {
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
    }
}
