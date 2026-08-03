using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentV2AirChamferStepPipelineTests
{
    [Fact]
    public void FirmamentV2_AirChamfer_Matrix_ExportsChangedDeterministicReimportableGeometry()
    {
        var cases = new[] { (10d, 8d, 6d, 1d), (10d, 8d, 6d, 2d), (12d, 5d, 7d, 1d) };
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var test in cases)
        {
            var first = Build(test.Item1, test.Item2, test.Item3, test.Item4);
            var second = Build(test.Item1, test.Item2, test.Item3, test.Item4);
            Assert.Equal(first.Hash, second.Hash);
            Assert.True(hashes.Add(first.Hash), "parameter variations must change STEP geometry/hash");
            Assert.Equal(12, first.Body.Topology.Vertices.Count());
            Assert.Equal(20, first.Body.Topology.Edges.Count());
            Assert.Equal(10, first.Body.Topology.Faces.Count());

            var top = first.Body.Topology.Vertices.Select(v => first.Body.TryGetVertexPoint(v.Id, out var p) ? p : throw new InvalidOperationException()).Where(p => Math.Abs(p.Z - test.Item3) < 1e-9).ToArray();
            Assert.Equal(4, top.Length);
            Assert.Equal((-test.Item1 / 2d) + test.Item4, top.Min(p => p.X), 9);
            Assert.Equal((test.Item1 / 2d) - test.Item4, top.Max(p => p.X), 9);
            Assert.Equal((-test.Item2 / 2d) + test.Item4, top.Min(p => p.Y), 9);
            Assert.Equal((test.Item2 / 2d) - test.Item4, top.Max(p => p.Y), 9);
        }
    }

    [Theory]
    [InlineData(0, "+Z", "Boundary", "air-chamfer-distance-must-be-positive")]
    [InlineData(4, "+Z", "Boundary", "air-chamfer-distance-too-large-rejected")]
    [InlineData(1, "-Z", "Boundary", "air-chamfer-unsupported-face-rejected")]
    [InlineData(1, "+Z", "SingleEdge", "air-chamfer-unsupported-selection-rejected")]
    public void FirmamentV2_AirChamfer_Rejections_DoNotFallBack(double distance, string face, string target, string expected)
    {
        var run = Run(Source(10, 8, 6, distance, face, target));
        Assert.Equal(1, run.Exit);
        Assert.Contains(expected, run.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("legacyFallback\":true", run.Stdout, StringComparison.Ordinal);
    }

    private static (string Hash, Aetheris.Kernel.Core.Brep.BrepBody Body) Build(double width, double depth, double height, double distance)
    {
        var run = Run(Source(width, depth, height, distance));
        Assert.Equal(0, run.Exit);
        using var json = JsonDocument.Parse(run.Stdout);
        var air = json.RootElement.GetProperty("air");
        Assert.True(air.GetProperty("bRepPlan").GetProperty("authoritative").GetBoolean());
        Assert.Equal("AirPrismaticTopFaceBoundaryChamfer", air.GetProperty("materialization").GetProperty("route").GetString());
        Assert.False(air.GetProperty("materialization").GetProperty("legacyFallback").GetBoolean());
        Assert.True(air.GetProperty("materialization").GetProperty("enclosedManifold").GetBoolean());
        Assert.Equal(distance, air.GetProperty("materialization").GetProperty("measuredTopInsetX").GetDouble(), 9);
        Assert.Equal(distance, air.GetProperty("materialization").GetProperty("measuredTopInsetY").GetDouble(), 9);
        Assert.True(air.GetProperty("step").GetProperty("reimportSucceeded").GetBoolean());
        var stepPath = json.RootElement.GetProperty("outputPath").GetString()!;
        var bytes = File.ReadAllBytes(stepPath);
        var imported = Step242Importer.ImportBody(Encoding.UTF8.GetString(bytes));
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(d => d.Message)));
        return (Convert.ToHexString(SHA256.HashData(bytes)), imported.Value);
    }

    private static (int Exit, string Stdout) Run(string source)
    {
        var dir = Path.Combine(Path.GetTempPath(), "aetheris-air-chamfer-m1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var sourcePath = Path.Combine(dir, "case.firmament");
        var stepPath = Path.Combine(dir, "case.step");
        File.WriteAllText(sourcePath, source);
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["build", sourcePath, "--out", stepPath, "--json"], stdout, stderr);
        return (exit, stdout + stderr.ToString());
    }

    private static string Source(double width, double depth, double height, double distance, string face = "+Z", string target = "Boundary") => $$"""
        Model Test mm
        Box Base { Size: [{{width}}mm, {{depth}}mm, {{height}}mm] }
        Modify Base {
            EdgeFinish TopBreak {
                Face: {{face}}
                Target: {{target}}
                Kind: Chamfer
                Distance: {{distance}}mm
            }
        }
        """;
}
