using System.Linq;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentExamplePackSmokeTests
{
    public static TheoryData<string> ExampleFiles =>
    [
        "testdata/firmament/examples/box_basic.firmament",
        "testdata/firmament/examples/cylinder_basic.firmament",
        "testdata/firmament/examples/cone_frustum_basic.firmament",
        "testdata/firmament/examples/cone_pointed_top_zero.firmament",
        "testdata/firmament/examples/sphere_basic.firmament",
        "testdata/firmament/examples/torus_basic.firmament",
        "testdata/firmament/examples/triangular_prism_basic.firmament",
        "testdata/firmament/examples/hexagonal_prism_basic.firmament",
        "testdata/firmament/examples/straight_slot_basic.firmament",
        "testdata/firmament/examples/box_add_basic.firmament",
        "testdata/firmament/examples/boolean_add_basic.firmament",
        "testdata/firmament/examples/boolean_subtract_basic.firmament",
        "testdata/firmament/examples/boolean_intersect_basic.firmament",
        "testdata/firmament/examples/boolean_two_cylinder_holes_basic.firmament",
        "testdata/firmament/examples/boolean_cylinder_cone_holes_basic.firmament",
        "testdata/firmament/examples/boolean_box_sphere_cavity_basic.firmament",
        "testdata/firmament/examples/p2_mirror_hole_pair.firmament",
        "testdata/firmament/examples/placed_primitive.firmament",
        "testdata/firmament/examples/schema_box_basic.firmament"
    ];

    [Theory]
    [MemberData(nameof(ExampleFiles))]
    public void ExamplePack_Files_Are_Canonical_And_Compile(string fixturePath)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText(fixturePath);

        var formatResult = new FirmamentFormatter().Format(new FirmamentFormatRequest(new FirmamentSourceDocument(source)));
        Assert.True(formatResult.Formatting.IsSuccess);
        Assert.Equal(source, formatResult.Formatting.Value.Text);

        var compileResult = FirmamentCorpusHarness.Compile(source);
        Assert.True(compileResult.Compilation.IsSuccess);
    }

    [Theory]
    [InlineData("testdata/firmament/examples/box_basic.firmament", "base", 0, "primitive", "box")]
    [InlineData("testdata/firmament/examples/cylinder_basic.firmament", "post", 0, "primitive", "cylinder")]
    [InlineData("testdata/firmament/examples/cone_frustum_basic.firmament", "frustum1", 0, "primitive", "cone")]
    [InlineData("testdata/firmament/examples/cone_pointed_top_zero.firmament", "pointed1", 0, "primitive", "cone")]
    [InlineData("testdata/firmament/examples/sphere_basic.firmament", "ball", 0, "primitive", "sphere")]
    [InlineData("testdata/firmament/examples/torus_basic.firmament", "donut1", 0, "primitive", "torus")]
    [InlineData("testdata/firmament/examples/triangular_prism_basic.firmament", "tri1", 0, "primitive", "triangularprism")]
    [InlineData("testdata/firmament/examples/hexagonal_prism_basic.firmament", "hex1", 0, "primitive", "hexagonalprism")]
    [InlineData("testdata/firmament/examples/straight_slot_basic.firmament", "slot1", 0, "primitive", "straightslot")]
    [InlineData("testdata/firmament/examples/box_add_basic.firmament", "joined", 1, "boolean", "add")]
    [InlineData("testdata/firmament/examples/boolean_add_basic.firmament", "joined", 2, "boolean", "add")]
    [InlineData("testdata/firmament/examples/boolean_subtract_basic.firmament", "carved", 2, "boolean", "subtract")]
    [InlineData("testdata/firmament/examples/boolean_intersect_basic.firmament", "overlap", 2, "boolean", "intersect")]
    [InlineData("testdata/firmament/examples/boolean_two_cylinder_holes_basic.firmament", "hole_b", 2, "boolean", "subtract")]
    [InlineData("testdata/firmament/examples/boolean_cylinder_cone_holes_basic.firmament", "cut_b", 2, "boolean", "subtract")]
    [InlineData("testdata/firmament/examples/boolean_box_sphere_cavity_basic.firmament", "cavity", 1, "boolean", "subtract")]
    [InlineData("testdata/firmament/examples/p2_mirror_hole_pair.firmament", "hole_cut_left__mir_yz", 2, "boolean", "subtract")]
    [InlineData("testdata/firmament/examples/placed_primitive.firmament", "post", 1, "primitive", "cylinder")]
    [InlineData("testdata/firmament/examples/schema_box_basic.firmament", "schema_box", 0, "primitive", "box")]
    public void ExamplePack_GoldenPath_Examples_Export(string fixturePath, string expectedFeatureId, int expectedOpIndex, string expectedBodyCategory, string expectedFeatureKind)
    {
        var first = ExportFixture(fixturePath);
        var second = ExportFixture(fixturePath);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(expectedFeatureId, first.Value.ExportedFeatureId);
        Assert.Equal(expectedOpIndex, first.Value.ExportedOpIndex);
        Assert.Equal(expectedBodyCategory, first.Value.ExportedBodyCategory);
        Assert.Equal(expectedFeatureKind, first.Value.ExportedFeatureKind);
        Assert.Equal(FirmamentStepExporter.LastExecutedGeometricBodyPolicy, first.Value.ExportBodyPolicy);
        Assert.Equal(FirmamentStepExporter.LastExecutedGeometricBodySelectionReason, first.Value.ExportBodySelectionReason);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("ISO-10303-21", first.Value.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void ExamplePack_SchemaExample_Attaches_Schema_Without_Changing_Geometric_Export()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/schema_box_basic.firmament"));

        Assert.True(result.Compilation.IsSuccess);
        Assert.NotNull(result.Compilation.Value.CompiledSchema);
        Assert.Empty(result.Compilation.Value.PrimitiveLoweringPlan!.Booleans);
        Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
    }

    private static Aetheris.Kernel.Core.Results.KernelResult<FirmamentStepExportResult> ExportFixture(string fixturePath) =>
        FirmamentStepExporter.Export(new FirmamentCompileRequest(new FirmamentSourceDocument(FirmamentCorpusHarness.ReadFixtureText(fixturePath))));
}
