using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests;

[Trait("Suite", "Import")]
public sealed class ImportedEdgeFinishTests
{
    [Theory]
    [InlineData("imported-single-edge", false, "straight-edge", SurfaceGeometryKind.Cylinder, 1)]
    [InlineData("imported-single-edge", true, "straight-edge", SurfaceGeometryKind.Plane, 1)]
    [InlineData("imported-circular-rim", false, "circular-rim", SurfaceGeometryKind.Cone, 1)]
    [InlineData("imported-circular-rim", true, "circular-rim", SurfaceGeometryKind.Torus, 1)]
    [InlineData("imported-polygon-rim", false, "polygon-rim", SurfaceGeometryKind.Cylinder, 4)]
    [InlineData("imported-polygon-rim", true, "polygon-rim", SurfaceGeometryKind.Plane, 4)]
    public void PinnedFinishBuildsThroughFirmamentAndReimports(string fixture, bool swap,
        string expectedPlan, SurfaceGeometryKind surface, int newFaces)
    {
        var (source, directory, inputPath) = Fixture(fixture);
        var before = File.ReadAllText(inputPath);
        var original = Step242Importer.ImportBody(before).Value;
        if (swap)
            source = source.Contains("<Fillet>", StringComparison.Ordinal)
                ? source.Replace("<Fillet>", "<Chamfer>").Replace("radius:", "distance:")
                : source.Replace("<Chamfer>", "<Fillet>").Replace("distance:", "radius:");

        var build = FirmamentBuildAndExport.CompileSource(source, directory);
        Assert.True(build.IsSuccess, string.Join(" | ", build.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(expectedPlan, build.Value.ExportedFeatureKind);
        Assert.Single(build.Value.MaterializedFeatures);
        var exact = build.Value.RuntimeBody!;
        Assert.Equal(original.Topology.Faces.Count() + newFaces, exact.Topology.Faces.Count());
        var priorFaces = original.Topology.Faces.Select(face => face.Id).ToHashSet();
        Assert.All(exact.Topology.Faces.Where(face => !priorFaces.Contains(face.Id)),
            face => Assert.Equal(surface, exact.GetFaceSurface(face.Id).Kind));
        var reopened = Step242Importer.ImportBody(build.Value.StepText);
        Assert.True(reopened.IsSuccess);
        Assert.Equal(exact.Topology.Faces.Count(), reopened.Value.Topology.Faces.Count());
        Assert.Equal(before, File.ReadAllText(inputPath));
    }

    [Theory]
    [InlineData("Stock.Edge(\"#21\")", "Stock.Edge(\"#9000\")", "edge-unresolved")]
    [InlineData("Stock.Edge(\"#21\")", "Stock.Face(\"#153\")", "imported-finish-selector")]
    [InlineData("radius: 0.1mm", "radius: 2mm", "clearance")]
    [InlineData("geometryOnly: true", "geometryOnly: false", "source-pmi")]
    public void RefusedRevisionReportsTheFailure(string needle, string replacement, string message)
    {
        var (source, directory, _) = Fixture("imported-single-edge");
        var build = FirmamentBuildAndExport.CompileSource(source.Replace(needle, replacement), directory);
        Assert.False(build.IsSuccess);
        Assert.Contains(build.Diagnostics, diagnostic => diagnostic.Message.Contains(message, StringComparison.Ordinal));
    }

    [Fact]
    public void StraightFilletRoundtripQualifiesWithOpposedRetainedPcurves()
    {
        var (source, directory, _) = Fixture("imported-single-edge");
        var build = FirmamentBuildAndExport.CompileSource(source.Replace("#21", "#108"), directory);
        Assert.True(build.IsSuccess, string.Join(" | ", build.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal("straight-edge", build.Value.ExportedFeatureKind);
    }

    private static (string Source, string Directory, string InputPath) Fixture(string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var fixtureDirectory = Path.Combine(root.FullName, "fixtures/Regression/InlineStep/production");
        var input = name == "imported-circular-rim" ? "cylinder.step" : "block-basic.step";
        return (File.ReadAllText(Path.Combine(fixtureDirectory, name + ".firmament")), fixtureDirectory,
            Path.Combine(root.FullName, "testdata/step242/handcrafted/baseline", input));
    }
}
