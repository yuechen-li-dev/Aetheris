using System.Text;
using System.Linq;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentBuildAndExportTests
{
    [Fact]
    public void Run_BoxBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/box_basic.firmament",
            "box_basic.step",
            "base",
            0,
            "primitive",
            "box");
    }

    [Fact]
    public void Run_CylinderBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/cylinder_basic.firmament",
            "cylinder_basic.step",
            "post",
            0,
            "primitive",
            "cylinder");
    }


    [Fact]
    public void Run_SphereBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/sphere_basic.firmament",
            "sphere_basic.step",
            "ball",
            0,
            "primitive",
            "sphere");
    }

    [Fact]
    public void Run_TorusBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/torus_basic.firmament",
            "torus_basic.step",
            "donut1",
            0,
            "primitive",
            "torus");
    }

    [Fact]
    public void Run_ConeFrustumBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/cone_frustum_basic.firmament",
            "cone_frustum_basic.step",
            "frustum1",
            0,
            "primitive",
            "cone");
    }

    [Fact]
    public void Run_ConePointedTopZeroExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/cone_pointed_top_zero.firmament",
            "cone_pointed_top_zero.step",
            "pointed1",
            0,
            "primitive",
            "cone");
    }

    [Fact]
    public void Run_BoxAddBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/box_add_basic.firmament",
            "box_add_basic.step",
            "joined",
            1,
            "boolean",
            "add");
    }

    [Fact]
    public void Run_BooleanAddBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_add_basic.firmament",
            "boolean_add_basic.step",
            "joined",
            2,
            "boolean",
            "add");
    }

    [Fact]
    public void Run_BooleanSubtractBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_subtract_basic.firmament",
            "boolean_subtract_basic.step",
            "carved",
            2,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_BooleanIntersectBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_intersect_basic.firmament",
            "boolean_intersect_basic.step",
            "overlap",
            2,
            "boolean",
            "intersect");
    }

    [Fact]
    public void Run_BooleanBoxCylinderHoleExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_box_cylinder_hole.firmament",
            "boolean_box_cylinder_hole.step",
            "hole",
            1,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_BooleanBoxConeThroughHoleExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_box_cone_throughhole_basic.firmament",
            "boolean_box_cone_throughhole_basic.step",
            "cut",
            1,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_BooleanTwoCylinderHolesExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_two_cylinder_holes_basic.firmament",
            "boolean_two_cylinder_holes_basic.step",
            "hole_b",
            2,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_BooleanCylinderConeHolesExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_cylinder_cone_holes_basic.firmament",
            "boolean_cylinder_cone_holes_basic.step",
            "cut_b",
            2,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_BooleanBoxSphereCavityExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_box_sphere_cavity_basic.firmament",
            "boolean_box_sphere_cavity_basic.step",
            "cavity",
            1,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_RibbedSupportF1Example_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/ribbed_support_f1.firmament",
            "ribbed_support_f1.step",
            "wall",
            1,
            "boolean",
            "add");
    }

    [Fact]
    public void Run_F2FlangeCenterBoreExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/f2_flange_center_bore.firmament",
            "f2_flange_center_bore.step",
            "center_bore",
            1,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_P1BlindHoleOnFaceSemanticExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/p1_blind_hole_on_face_semantic.firmament",
            "p1_blind_hole_on_face_semantic.step",
            "blind_hole_tool",
            1,
            "primitive",
            "cylinder");
    }

    [Fact]
    public void Run_P1FlangeRadialHoleSemanticExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/p1_flange_radial_hole_semantic.firmament",
            "p1_flange_radial_hole_semantic.step",
            "radial_hole_tool",
            1,
            "primitive",
            "cylinder");
    }

    [Fact]
    public void Run_P2LinearHoleRowExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/p2_linear_hole_row.firmament",
            "p2_linear_hole_row.step",
            "hole_cut_1__lin3",
            4,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_P2FlangeBoltCirclePatternExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/p2_flange_bolt_circle_pattern.firmament",
            "p2_flange_bolt_circle_pattern.step",
            "bolt_hole_1__cir5",
            7,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Semantic_Placement_Build_Is_Deterministic()
    {
        var fullSourcePath = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/firmament/examples/p1_flange_radial_hole_semantic.firmament");
        var first = FirmamentBuildAndExport.Run(fullSourcePath);
        var second = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Export.StepText, second.Value.Export.StepText);
    }

    [Fact]
    public void P2_CircularPattern_Build_Is_Deterministic()
    {
        var fullSourcePath = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/firmament/examples/p2_flange_bolt_circle_pattern.firmament");
        var first = FirmamentBuildAndExport.Run(fullSourcePath);
        var second = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Export.StepText, second.Value.Export.StepText);
    }

    [Fact]
    public void Run_UnsupportedBoxWithCylinderHoleFixture_Fails_And_Does_Not_Write_Fallback_Export()
    {
        AssertUnsupportedBuildAndExport(
            "testdata/firmament/fixtures/m10h1-unsupported-box-with-cylinder-hole.firmament",
            "m10h1-unsupported-box-with-cylinder-hole.step",
            "hole",
            "subtract");
    }

    [Theory]
    [InlineData("testdata/firmament/fixtures/m10j-unsupported-box-add-cylinder.firmament", "m10j-unsupported-box-add-cylinder.step", "joined", "add")]
    [InlineData("testdata/firmament/fixtures/m10j-unsupported-box-intersect-cylinder.firmament", "m10j-unsupported-box-intersect-cylinder.step", "overlap", "intersect")]
    [InlineData("testdata/firmament/fixtures/m10l-unsupported-box-add-sphere.firmament", "m10l-unsupported-box-add-sphere.step", "joined", "add")]
    [InlineData("testdata/firmament/fixtures/m10l-unsupported-box-intersect-sphere.firmament", "m10l-unsupported-box-intersect-sphere.step", "overlap", "intersect")]
    [InlineData("testdata/firmament/fixtures/m10m-unsupported-box-subtract-cone.firmament", "m10m-unsupported-box-subtract-cone.step", "tapered_cut", "subtract")]
    [InlineData("testdata/firmament/fixtures/m10m-unsupported-box-add-cone.firmament", "m10m-unsupported-box-add-cone.step", "joined", "add")]
    [InlineData("testdata/firmament/fixtures/m10m-unsupported-box-intersect-cone.firmament", "m10m-unsupported-box-intersect-cone.step", "overlap", "intersect")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-overlapping-composed-holes.firmament", "m13a-unsupported-overlapping-composed-holes.step", "hole_b", "subtract")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-tangent-composed-holes.firmament", "m13a-unsupported-tangent-composed-holes.step", "hole_b", "subtract")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-add-ordering.firmament", "m13a-unsupported-composed-add-ordering.step", "joined", "add")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-subtract-sphere.firmament", "m13a-unsupported-composed-subtract-sphere.step", "cavity", "subtract")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-subtract-box.firmament", "m13a-unsupported-composed-subtract-box.step", "notch", "subtract")]
    [InlineData("testdata/firmament/fixtures/m13b-invalid-composed-reenter-safe-family.firmament", "m13b-invalid-composed-reenter-safe-family.step", "hole", "subtract")]
    [InlineData("testdata/firmament/fixtures/m13c-unsupported-cylinder-root-follow-on-hole.firmament", "m13c-unsupported-cylinder-root-follow-on-hole.step", "bolt_hole", "subtract")]
    public void Run_Unsupported_MixedPrimitive_Fixtures_Fail_And_Do_Not_Write_Fallback_Export(
        string sourcePath,
        string expectedFileName,
        string expectedFeatureId,
        string expectedKind)
    {
        AssertUnsupportedBuildAndExport(sourcePath, expectedFileName, expectedFeatureId, expectedKind);
    }

    private static void AssertExampleBuildAndExport(string sourcePath, string expectedFileName, string expectedFeatureId, int expectedOpIndex, string expectedBodyCategory, string expectedFeatureKind)
    {
        var fullSourcePath = FirmamentCorpusHarness.ResolveFixtureFullPath(sourcePath);
        var expectedOutputPath = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "testdata", "firmament", "exports", expectedFileName);
        if (File.Exists(expectedOutputPath))
        {
            File.Delete(expectedOutputPath);
        }

        var result = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(fullSourcePath, result.Value.SourcePath);
        Assert.Equal(expectedOutputPath, result.Value.OutputPath);
        Assert.Equal(expectedFeatureId, result.Value.Export.ExportedFeatureId);
        Assert.Equal(expectedOpIndex, result.Value.Export.ExportedOpIndex);
        Assert.Equal(expectedBodyCategory, result.Value.Export.ExportedBodyCategory);
        Assert.Equal(expectedFeatureKind, result.Value.Export.ExportedFeatureKind);
        Assert.True(File.Exists(expectedOutputPath));
        Assert.Equal(result.Value.Export.StepText, File.ReadAllText(expectedOutputPath, Encoding.UTF8));
    }

    private static void AssertUnsupportedBuildAndExport(string sourcePath, string expectedFileName, string expectedFeatureId, string expectedKind)
    {
        var fullSourcePath = FirmamentCorpusHarness.ResolveFixtureFullPath(sourcePath);
        var expectedOutputPath = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "testdata", "firmament", "exports", expectedFileName);
        if (File.Exists(expectedOutputPath))
        {
            File.Delete(expectedOutputPath);
        }

        var result = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(expectedOutputPath));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains($"Requested boolean feature '{expectedFeatureId}' ({expectedKind}) could not be executed.", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => HasExpectedMixedPrimitiveFailure(diagnostic.Message));
    }

    private static bool HasExpectedMixedPrimitiveFailure(string message)
        => message.Contains("M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", StringComparison.Ordinal)
           || message.Contains("sequential safe composition only supports subtracting supported cylinder/cone analytic holes", StringComparison.Ordinal)
           || message.Contains("safe subtract", StringComparison.Ordinal)
           || message.Contains("unsupported follow-on tool kind", StringComparison.Ordinal)
           || message.Contains("Boolean feature", StringComparison.Ordinal)
           || message.Contains("analytic hole surface kind", StringComparison.Ordinal)
           || message.Contains("fully enclosed spherical cavity", StringComparison.Ordinal);
}
