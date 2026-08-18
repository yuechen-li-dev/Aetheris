using System.Linq;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentExamplePackSmokeTests
{
    public static TheoryData<string> ExampleFiles =>
    [
        "fixtures/LegacyV1/Examples/box_basic.firmament",
        "fixtures/LegacyV1/Examples/cylinder_basic.firmament",
        "fixtures/LegacyV1/Examples/cone_frustum_basic.firmament",
        "fixtures/LegacyV1/Examples/cone_pointed_top_zero.firmament",
        "fixtures/LegacyV1/Examples/sphere_basic.firmament",
        "fixtures/LegacyV1/Examples/torus_basic.firmament",
        "fixtures/LegacyV1/Examples/triangular_prism_basic.firmament",
        "fixtures/LegacyV1/Examples/hexagonal_prism_basic.firmament",
        "fixtures/LegacyV1/Examples/straight_slot_basic.firmament",
        "fixtures/LegacyV1/Examples/rounded_corner_box_basic.firmament",
        "fixtures/LegacyV1/Examples/slot_cut_basic.firmament",
        "fixtures/LegacyV1/Examples/library_part_cube_with_hole_basic.firmament",
        "fixtures/LegacyV1/Examples/box_add_basic.firmament",
        "fixtures/LegacyV1/Examples/boolean_add_basic.firmament",
        "fixtures/LegacyV1/Examples/boolean_subtract_basic.firmament",
        "fixtures/LegacyV1/Examples/boolean_intersect_basic.firmament",
        "fixtures/LegacyV1/Examples/boolean_two_cylinder_holes_basic.firmament",
        "fixtures/LegacyV1/Examples/boolean_cylinder_cone_holes_basic.firmament",
        "fixtures/LegacyV1/Examples/boolean_box_sphere_cavity_basic.firmament",
        "fixtures/LegacyV1/Examples/p2_mirror_hole_pair.firmament",
        "fixtures/LegacyV1/Examples/placed_primitive.firmament",
        "fixtures/LegacyV1/Examples/schema_box_basic.firmament"
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
    [InlineData("fixtures/LegacyV1/Examples/box_basic.firmament", "base", 0, "primitive", "box")]
    [InlineData("fixtures/LegacyV1/Examples/cylinder_basic.firmament", "post", 0, "primitive", "cylinder")]
    [InlineData("fixtures/LegacyV1/Examples/cone_frustum_basic.firmament", "frustum1", 0, "primitive", "cone")]
    [InlineData("fixtures/LegacyV1/Examples/cone_pointed_top_zero.firmament", "pointed1", 0, "primitive", "cone")]
    [InlineData("fixtures/LegacyV1/Examples/sphere_basic.firmament", "ball", 0, "primitive", "sphere")]
    [InlineData("fixtures/LegacyV1/Examples/torus_basic.firmament", "donut1", 0, "primitive", "torus")]
    [InlineData("fixtures/LegacyV1/Examples/triangular_prism_basic.firmament", "tri1", 0, "primitive", "triangularprism")]
    [InlineData("fixtures/LegacyV1/Examples/hexagonal_prism_basic.firmament", "hex1", 0, "primitive", "hexagonalprism")]
    [InlineData("fixtures/LegacyV1/Examples/straight_slot_basic.firmament", "slot1", 0, "primitive", "straightslot")]
    [InlineData("fixtures/LegacyV1/Examples/rounded_corner_box_basic.firmament", "rbox1", 0, "primitive", "roundedcornerbox")]
    [InlineData("fixtures/LegacyV1/Examples/slot_cut_basic.firmament", "slot_cut_1", 0, "primitive", "slotcut")]
    [InlineData("fixtures/LegacyV1/Examples/library_part_cube_with_hole_basic.firmament", "lib_part_1", 0, "primitive", "librarypart")]
    [InlineData("fixtures/LegacyV1/Examples/box_add_basic.firmament", "joined", 1, "boolean", "add")]
    [InlineData("fixtures/LegacyV1/Examples/boolean_add_basic.firmament", "joined", 2, "boolean", "add")]
    [InlineData("fixtures/LegacyV1/Examples/boolean_subtract_basic.firmament", "carved", 2, "boolean", "subtract")]
    [InlineData("fixtures/LegacyV1/Examples/boolean_intersect_basic.firmament", "overlap", 2, "boolean", "intersect")]
    [InlineData("fixtures/LegacyV1/Examples/boolean_two_cylinder_holes_basic.firmament", "hole_b", 2, "boolean", "subtract")]
    [InlineData("fixtures/LegacyV1/Examples/boolean_cylinder_cone_holes_basic.firmament", "cut_b", 2, "boolean", "subtract")]
    [InlineData("fixtures/LegacyV1/Examples/boolean_box_sphere_cavity_basic.firmament", "cavity", 1, "boolean", "subtract")]
    [InlineData("fixtures/LegacyV1/Examples/p2_mirror_hole_pair.firmament", "hole_cut_left__mir_yz", 2, "boolean", "subtract")]
    [InlineData("fixtures/LegacyV1/Examples/placed_primitive.firmament", "post", 1, "primitive", "cylinder")]
    [InlineData("fixtures/LegacyV1/Examples/schema_box_basic.firmament", "schema_box", 0, "primitive", "box")]
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
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("fixtures/LegacyV1/Examples/schema_box_basic.firmament"));

        Assert.True(result.Compilation.IsSuccess);
        Assert.NotNull(result.Compilation.Value.CompiledSchema);
        Assert.Empty(result.Compilation.Value.PrimitiveLoweringPlan!.Booleans);
        Assert.Single(result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
    }

    [Theory]
    [InlineData("fixtures/LegacyV1/Examples/p1_blind_hole_on_face_semantic.firmament")]
    [InlineData("fixtures/LegacyV1/Examples/p1_flange_radial_hole_semantic.firmament")]
    [InlineData("fixtures/LegacyV1/Examples/w2_cylinder_root_blind_bore_semantic.firmament")]
    [InlineData("fixtures/LegacyV1/Examples/w2_box_sphere_exterior_opening_pocket_semantic.firmament")]
    [InlineData("fixtures/LegacyV1/Examples/placed_primitive.firmament")]
    public void CanonicalExamples_StillBuild(string fixturePath)
    {
        var compileResult = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));
        Assert.True(compileResult.Compilation.IsSuccess);

        var exportResult = ExportFixture(fixturePath);
        Assert.True(exportResult.IsSuccess);
        Assert.Contains("ISO-10303-21", exportResult.Value.StepText, StringComparison.Ordinal);
    }

    private static Aetheris.Kernel.Core.Results.KernelResult<FirmamentStepExportResult> ExportFixture(string fixturePath) =>
        FirmamentStepExporter.Export(new FirmamentCompileRequest(new FirmamentSourceDocument(FirmamentCorpusHarness.ReadFixtureText(fixturePath))));
}
