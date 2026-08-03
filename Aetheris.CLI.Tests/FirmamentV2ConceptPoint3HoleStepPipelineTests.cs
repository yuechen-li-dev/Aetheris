using System.Text.Json;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentV2ConceptPoint3HoleStepPipelineTests
{
    [Fact]
    public void ConceptPoint3_DrivesTwoSemanticHolesThroughStepAndIndependentAnalysis()
    {
        var dir = Path.Combine(Path.GetTempPath(), "aetheris-concept-materialization-m2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var sourcePath = Path.Combine(dir, "concept-point-holes.firmament");
        var stepPath = Path.Combine(dir, "concept-point-holes.step");
        File.WriteAllText(sourcePath, Source);
        var stdout = new StringWriter(); var stderr = new StringWriter();

        var exit = CliRunner.Run(["build", sourcePath, "--out", stepPath, "--json"], stdout, stderr);

        Assert.Equal(0, exit);
        using var report = JsonDocument.Parse(stdout.ToString());
        var root = report.RootElement;
        Assert.Equal("Valid", root.GetProperty("conceptIr").GetProperty("materializedStruct").GetProperty("conformance").GetString());
        Assert.False(root.GetProperty("conceptIr").GetProperty("structs")[0].GetProperty("materialized").GetBoolean());
        Assert.Equal(4, root.GetProperty("conceptIr").GetProperty("materializedStruct").GetProperty("exposedMembers").GetArrayLength());
        var features = root.GetProperty("features");
        Assert.Equal(2, features.GetArrayLength());
        Assert.Equal("concept:BracketConcept.MountPoints[0]", features[0].GetProperty("centerStableId").GetString());
        Assert.Equal(-30d, features[0].GetProperty("resolvedPoint3")[0].GetDouble());
        Assert.Equal(30d, features[1].GetProperty("resolvedPoint3")[0].GetDouble());
        Assert.All(features.EnumerateArray(), f =>
        {
            Assert.Equal(8.5d, f.GetProperty("diameter").GetDouble());
            Assert.Equal("AirHoleCompositeMaterializer", f.GetProperty("materializationRoute").GetString());
        });

        var import = Step242Importer.ImportBody(File.ReadAllText(stepPath));
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));
        var body = import.Value;
        var cylinders = body.Topology.Faces.Select(f => body.TryGetFaceSurface(f.Id, out var surface) ? surface : null)
            .Where(s => s?.Kind == SurfaceGeometryKind.Cylinder).Select(s => s!.Cylinder!.Value).OrderBy(c => c.Origin.X).ToArray();
        Assert.Equal(2, cylinders.Length);
        Assert.Equal([-30d, 30d], cylinders.Select(c => c.Origin.X));
        Assert.All(cylinders, cylinder =>
        {
            Assert.Equal(0d, cylinder.Origin.Y);
            Assert.Equal(4.25d, cylinder.Radius);
            Assert.Equal(1d, cylinder.Axis.Z);
        });
        var points = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var point) ? point : throw new InvalidOperationException()).ToArray();
        Assert.Equal((-40d, -25d, 0d, 40d, 25d, 25d),
            (points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z), points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z)));
        var topCircleCenters = body.Geometry.Curves.Select(entry => entry.Value).Where(c => c.Kind == CurveGeometryKind.Circle3 && Math.Abs(c.Circle3!.Value.Center.Z - 25d) < 1e-9)
            .Select(c => c.Circle3!.Value.Center).OrderBy(p => p.X).ToArray();
        Assert.Contains(topCircleCenters, p => Math.Abs(p.X + 30d) < 1e-9 && Math.Abs(p.Y) < 1e-9);
        Assert.Contains(topCircleCenters, p => Math.Abs(p.X - 30d) < 1e-9 && Math.Abs(p.Y) < 1e-9);

        var analyzeOut = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["analyze", stepPath, "--json"], analyzeOut, new StringWriter()));
        using var analysis = JsonDocument.Parse(analyzeOut.ToString());
        var summary = analysis.RootElement.GetProperty("summary");
        Assert.Equal(1, summary.GetProperty("bodyCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("shellCount").GetInt32());
        Assert.Equal("enclosed-manifold", summary.GetProperty("structuralAssessment").GetString());
        Assert.Equal(2, summary.GetProperty("surfaceFamilies").GetProperty("cylinder").GetInt32());

        var volume = StepAnalyzer.AnalyzeVolume(stepPath);
        Assert.True(volume.Success);
        Assert.True(volume.Exact);
        Assert.Equal("analytic-box-minus-z-hole", volume.Method);
        Assert.InRange(Math.Abs(volume.Volume - (80d * 50d * 25d - 2d * Math.PI * 4.25d * 4.25d * 25d)), 0d, 1e-8);
    }

    [Fact]
    public void ConceptHolesCombinedWithProductionChamfer_FailsExplicitly()
    {
        var combined = Source.Replace("        }\r\n    }\r\n    Expose {", "        }\r\n        EdgeFinish TopBreak {\r\n            Face: BracketConcept.TopPlane\r\n            Target: Boundary\r\n            Kind: Chamfer\r\n            Distance: 1.5mm\r\n        }\r\n    }\r\n    Expose {", StringComparison.Ordinal)
            .Replace("        }\n    }\n    Expose {", "        }\n        EdgeFinish TopBreak {\n            Face: BracketConcept.TopPlane\n            Target: Boundary\n            Kind: Chamfer\n            Distance: 1.5mm\n        }\n    }\n    Expose {", StringComparison.Ordinal);
        var dir = Path.Combine(Path.GetTempPath(), "aetheris-concept-materialization-m2-combined", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var sourcePath = Path.Combine(dir, "combined.firmament"); File.WriteAllText(sourcePath, combined);
        var stdout = new StringWriter();

        var exit = CliRunner.Run(["build", sourcePath, "--out", Path.Combine(dir, "combined.step"), "--json"], stdout, new StringWriter());

        Assert.Equal(1, exit);
        Assert.Contains("air-chamfer-production-route-requires-one-box-and-one-edge-finish", stdout.ToString(), StringComparison.Ordinal);
    }

    private static string Source => File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../demos/concept-materialization-m2.firmament")));
}
