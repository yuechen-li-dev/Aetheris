using System.Text.Json;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.CLI.Tests;

public sealed class ReconstructionPrimitiveClosureTests
{
    [Fact]
    public void MajorArc_BoundsAndSectionRetainTrimmedAnalyticExtrema()
    {
        var loop = new LineArcProfileLoop2D([
            new LineArcCircularArc2D((0, 0), 5, 0, 3d * Math.PI / 2d),
            new LineArcLineSegment2D((0, -5), (0, 0)),
            new LineArcLineSegment2D((0, 0), (5, 0))], false);
        var emitted = LineArcProfileExtrudeEmitter.TryEmit(new LineArcProfileExtrudeRequest([loop], 2));
        var body = Assert.IsType<BrepBody>(emitted.Body);
        var path = WriteStep(body, "major-arc");
        try
        {
            var analysis = StepAnalyzer.Analyze(path);
            Assert.Equal(-5d, analysis.Summary.BoundingBox!.Value.Min.X, 8);
            Assert.Equal(5d, analysis.Summary.BoundingBox.Value.Max.Y, 8);
            var cylindricalFace = body.Topology.Faces.Single(face =>
                body.TryGetFaceSurface(face.Id, out var surface) && surface?.Cylinder is not null);
            var faceAnalysis = StepAnalyzer.Analyze(path, faceId: cylindricalFace.Id.Value);
            Assert.Equal(2d, faceAnalysis.Face!.AxialExtent!.Value, 8);
            Assert.Equal(3d * Math.PI / 2d, faceAnalysis.Face.BoundaryAngularExtentRadians!.Value, 8);
            Assert.Equal(-5d, faceAnalysis.Face.BoundingBox!.Value.Min.X, 8);
            Assert.Equal(5d, faceAnalysis.Face.BoundingBox.Value.Max.Y, 8);
            var section = StepAnalyzer.AnalyzeSection(path, SectionPlaneFamily.XY, 0d);
            var arc = Assert.Single(section.Loops.SelectMany(loopResult => loopResult.Segments), segment => segment.Kind == "arc");
            Assert.Equal(3d * Math.PI / 2d, Math.Abs(arc.SweepRadians!.Value), 8);
            Assert.Equal(-5d, section.Summary.SectionBoundingBox2D!.Min.U, 8);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ArbitraryPlaneSection_WritesDeterministicSvg()
    {
        var path = WriteStep(BrepPrimitives.CreateBox(10, 8, 6).Value, "arbitrary-section");
        var svg = Path.Combine(Path.GetTempPath(), "aetheris-arbitrary-section.svg");
        try
        {
            string Run()
            {
                var stdout = new StringWriter(); var stderr = new StringWriter();
                var code = CliRunner.Run(["analyze", "section", path, "--origin", "0,0,0", "--normal", "1,1,0", "--out", svg, "--json"], stdout, stderr);
                Assert.Equal(0, code); Assert.True(File.Exists(svg), stderr.ToString());
                using var json = JsonDocument.Parse(stdout.ToString());
                Assert.Equal("Custom", json.RootElement.GetProperty("metadata").GetProperty("planeFamily").GetString());
                return File.ReadAllText(svg);
            }
            var first = Run(); var second = Run();
            Assert.Equal(first, second);
            Assert.Contains("<path data-loop=", first, StringComparison.Ordinal);
        }
        finally { File.Delete(path); if (File.Exists(svg)) File.Delete(svg); }
    }

    [Fact]
    public void CompoundAnalysis_ReportsEverySolidAndAggregatePlacement()
    {
        var exported = Step242AssemblyExporter.Export(new Step242AssemblyExportModel("Pair", "root", [
            new("box-def", "Box", BrepPrimitives.CreateBox(2, 4, 6).Value),
            new("sphere-def", "Sphere", BrepPrimitives.CreateSphere(3).Value)], [
            new("root", "Pair", null, null, Identity()),
            new("box-occ", "Box", "root", "box-def", Identity()),
            new("sphere-occ", "Sphere", "root", "sphere-def", Translated(0, 0, 10))]));
        Assert.True(exported.IsSuccess);
        var path = Path.Combine(Path.GetTempPath(), $"aetheris-compound-{Guid.NewGuid():N}.step");
        File.WriteAllText(path, exported.Value);
        try
        {
            var result = StepAnalyzer.AnalyzeCompound(path);
            Assert.Equal(2, result.RootCount);
            Assert.Equal(2, result.SolidCount);
            Assert.NotNull(result.AggregateVolume);
            Assert.Equal(0d, result.Solids[1].OffsetFromFirst.Z, 8);
            Assert.Equal(3d, result.AggregateBoundingBox!.Value.Max.Z, 8);
        }
        finally { File.Delete(path); }
    }

    private static string WriteStep(BrepBody body, string name)
    {
        var exported = Step242Exporter.ExportBody(body); Assert.True(exported.IsSuccess);
        var path = Path.Combine(Path.GetTempPath(), $"aetheris-{name}-{Guid.NewGuid():N}.step");
        File.WriteAllText(path, exported.Value); return path;
    }

    private static double[] Identity() => Translated(0, 0, 0);
    private static double[] Translated(double x, double y, double z) => [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, x, y, z, 1];
}
