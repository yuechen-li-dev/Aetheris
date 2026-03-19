using System.Text;

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
    public void Run_BoxWithHoleExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/box_with_hole.firmament",
            "box_with_hole.step",
            "base",
            0,
            "primitive",
            "box");
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

        Assert.True(result.IsSuccess);
        Assert.Equal(fullSourcePath, result.Value.SourcePath);
        Assert.Equal(expectedOutputPath, result.Value.OutputPath);
        Assert.Equal(expectedFeatureId, result.Value.Export.ExportedFeatureId);
        Assert.Equal(expectedOpIndex, result.Value.Export.ExportedOpIndex);
        Assert.Equal(expectedBodyCategory, result.Value.Export.ExportedBodyCategory);
        Assert.Equal(expectedFeatureKind, result.Value.Export.ExportedFeatureKind);
        Assert.True(File.Exists(expectedOutputPath));
        Assert.Equal(result.Value.Export.StepText, File.ReadAllText(expectedOutputPath, Encoding.UTF8));
    }
}
