using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Firmament.ParsedModel;
using System.Linq;

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
        Assert.Equal(0, primitive.OpIndex);
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
    public void Compile_Lowers_Cone_Primitive()
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
                op: cone
                id: frustum1
                bottom_radius: 8
                top_radius: 3
                height: 20
            """);

        Assert.True(result.Compilation.IsSuccess);
        var primitive = Assert.Single(result.Compilation.Value.PrimitiveLoweringPlan!.Primitives);
        var parameters = Assert.IsType<FirmamentLoweredConeParameters>(primitive.Parameters);
        Assert.Equal("frustum1", primitive.FeatureId);
        Assert.Equal(FirmamentLoweredPrimitiveKind.Cone, primitive.Kind);
        Assert.Equal(8, parameters.BottomRadius);
        Assert.Equal(3, parameters.TopRadius);
        Assert.Equal(20, parameters.Height);
    }

    [Fact]
    public void Compile_Lowers_PointedCone_Primitive_Without_Forking_The_Cone_Surface()
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
                op: cone
                id: pointed1
                bottom_radius: 8
                top_radius: 0
                height: 20
            """);

        Assert.True(result.Compilation.IsSuccess);
        var primitive = Assert.Single(result.Compilation.Value.PrimitiveLoweringPlan!.Primitives);
        var parameters = Assert.IsType<FirmamentLoweredConeParameters>(primitive.Parameters);
        Assert.Equal("pointed1", primitive.FeatureId);
        Assert.Equal(FirmamentLoweredPrimitiveKind.Cone, primitive.Kind);
        Assert.Equal(8, parameters.BottomRadius);
        Assert.Equal(0, parameters.TopRadius);
        Assert.Equal(20, parameters.Height);
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
    public void Compile_Lowers_Torus_Primitive()
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
                op: torus
                id: donut1
                major_radius: 10
                minor_radius: 3
            """);

        Assert.True(result.Compilation.IsSuccess);
        var primitive = Assert.Single(result.Compilation.Value.PrimitiveLoweringPlan!.Primitives);
        var parameters = Assert.IsType<FirmamentLoweredTorusParameters>(primitive.Parameters);
        Assert.Equal("donut1", primitive.FeatureId);
        Assert.Equal(FirmamentLoweredPrimitiveKind.Torus, primitive.Kind);
        Assert.Equal(10, parameters.MajorRadius);
        Assert.Equal(3, parameters.MinorRadius);
    }

    [Fact]
    public void Compile_Lowers_Add_Boolean_With_Primary_Reference_And_Coarse_Tool()
    {
        var result = Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m1b-valid-add.firmament"));

        Assert.True(result.Compilation.IsSuccess);
        var boolean = Assert.Single(result.Compilation.Value.PrimitiveLoweringPlan!.Booleans);
        Assert.Equal(1, boolean.OpIndex);
        Assert.Equal("add1", boolean.FeatureId);
        Assert.Equal(FirmamentLoweredBooleanKind.Add, boolean.Kind);
        Assert.Equal("to", boolean.PrimaryReferenceField);
        Assert.Equal("base", boolean.PrimaryReferenceFeatureId);
        Assert.Equal("box", boolean.Tool.OpName);
        Assert.Equal("box", boolean.Tool.RawFields["op"]);
        Assert.Equal("[1, 1, 1]", boolean.Tool.RawFields["size"]);
        Assert.Equal("{ op: box, size[3]: [1, 1, 1] }", boolean.Tool.RawValue);
    }

    [Fact]
    public void Compile_Lowers_Subtract_Boolean_With_Primary_Reference_And_Coarse_Tool()
    {
        var result = Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m1b-valid-subtract.firmament"));

        Assert.True(result.Compilation.IsSuccess);
        var boolean = result.Compilation.Value.PrimitiveLoweringPlan!.Booleans.Single(b => b.FeatureId == "sub1");
        Assert.Equal(FirmamentLoweredBooleanKind.Subtract, boolean.Kind);
        Assert.Equal("from", boolean.PrimaryReferenceField);
        Assert.Equal("anchor", boolean.PrimaryReferenceFeatureId);
        Assert.Equal("box", boolean.Tool.OpName);
    }

    [Fact]
    public void Compile_Lowers_Intersect_Boolean_With_Primary_Reference_And_Coarse_Tool()
    {
        var result = Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m1b-valid-intersect.firmament"));

        Assert.True(result.Compilation.IsSuccess);
        var boolean = Assert.Single(result.Compilation.Value.PrimitiveLoweringPlan!.Booleans);
        Assert.Equal(FirmamentLoweredBooleanKind.Intersect, boolean.Kind);
        Assert.Equal("left", boolean.PrimaryReferenceField);
        Assert.Equal("base", boolean.PrimaryReferenceFeatureId);
        Assert.Equal("box", boolean.Tool.OpName);
    }

    [Fact]
    public void Compile_Lowers_Mixed_Primitive_And_Boolean_In_Source_Order_With_Preserved_Ids()
    {
        var result = Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m3b-mixed-primitive-boolean-validation.firmament"));

        Assert.True(result.Compilation.IsSuccess);
        var plan = result.Compilation.Value.PrimitiveLoweringPlan!;

        Assert.Collection(
            plan.Primitives,
            p =>
            {
                Assert.Equal(0, p.OpIndex);
                Assert.Equal("base", p.FeatureId);
            },
            p =>
            {
                Assert.Equal(2, p.OpIndex);
                Assert.Equal("cap", p.FeatureId);
            });

        Assert.Collection(
            plan.Booleans,
            b =>
            {
                Assert.Equal(1, b.OpIndex);
                Assert.Equal("join1", b.FeatureId);
                Assert.Equal("to", b.PrimaryReferenceField);
            },
            b =>
            {
                Assert.Equal(3, b.OpIndex);
                Assert.Equal("cut1", b.FeatureId);
                Assert.Equal("from", b.PrimaryReferenceField);
            });
    }

    [Fact]
    public void Compile_Expands_LinearPattern_Into_Repeated_Primitives_With_Deterministic_Ids()
    {
        var result = Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/p2_linear_hole_row.firmament"));

        Assert.True(result.Compilation.IsSuccess);
        var primitives = result.Compilation.Value.PrimitiveLoweringPlan!.Primitives
            .Where(p => p.Kind == FirmamentLoweredPrimitiveKind.Cylinder)
            .OrderBy(p => p.OpIndex)
            .ToArray();
        Assert.Equal(4, primitives.Length);
        Assert.Equal("hole_marker_1", primitives[0].FeatureId);
        Assert.Equal("hole_marker_1__lin1", primitives[1].FeatureId);
        Assert.Equal("hole_marker_1__lin2", primitives[2].FeatureId);
        Assert.Equal("hole_marker_1__lin3", primitives[3].FeatureId);
    }


    [Fact]
    public void Compile_Lowers_Boolean_Placement_Metadata_Using_Primitive_Placement_Model()
    {
        var result = Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m7d-valid-boolean-origin-placement.firmament"));

        Assert.True(result.Compilation.IsSuccess);
        var boolean = Assert.Single(result.Compilation.Value.PrimitiveLoweringPlan!.Booleans);
        Assert.NotNull(boolean.Placement);
        var origin = Assert.IsType<FirmamentLoweredPlacementOriginAnchor>(boolean.Placement!.On);
        Assert.NotNull(origin);
        Assert.Equal(new[] { 10d, -5d, 3d }, boolean.Placement.Offset);
    }

    [Fact]
    public void Compile_Validation_Family_Ops_Remain_Explicitly_NonLowered()
    {
        var result = Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m3b-mixed-primitive-boolean-validation.firmament"));

        Assert.True(result.Compilation.IsSuccess);
        var skipped = Assert.Single(result.Compilation.Value.PrimitiveLoweringPlan!.SkippedOps);
        Assert.Equal(4, skipped.OpIndex);
        Assert.Equal(FirmamentOpFamily.Validation, skipped.Family);
        Assert.Equal("expect_exists", skipped.OpName);
        Assert.Equal("unsupported-op-in-m3b-boolean-lowering", skipped.Reason);
    }

    [Fact]
    public void Compile_Primitive_And_Boolean_Lowering_Output_Is_Deterministic()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m3b-valid-primitives-and-booleans-lower.firmament");

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
