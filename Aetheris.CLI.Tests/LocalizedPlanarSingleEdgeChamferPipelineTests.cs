using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

/// <summary>Real parser/export/reimport evidence for AIR-CHAMFER-LOCALIZED-PLAN-A1.</summary>
public sealed class LocalizedPlanarSingleEdgeChamferPipelineTests
{
    [Theory]
    [InlineData(10d, 8d, 6d, 1d)]
    [InlineData(10d, 8d, 6d, 2d)]
    [InlineData(12d, 5d, 7d, 1d)]
    public void LocalizedPlanarReplacement_ExportsRealSingleEdgeChamfer(double width, double depth, double height, double distance)
    {
        var first = Run(Source(width, depth, height, distance));
        var second = Run(Source(width, depth, height, distance));
        Assert.Equal(0, first.Exit);
        Assert.Equal(0, second.Exit);
        using var json = JsonDocument.Parse(first.Output);
        var air = json.RootElement.GetProperty("air");
        Assert.Equal("SharedEdge(+X,+Z)", air.GetProperty("feature").GetProperty("selection").GetString());
        Assert.Equal("LocalizedPlanarReplacement", air.GetProperty("construction").GetProperty("kind").GetString());
        Assert.Equal("ExplicitOwnedEndpoints", air.GetProperty("construction").GetProperty("splitPolicy").GetString());
        Assert.True(air.GetProperty("bRepPlan").GetProperty("authoritative").GetBoolean());
        Assert.Equal("LocalizedPlanarReplacement", air.GetProperty("bRepPlan").GetProperty("planKind").GetString());
        Assert.Equal("AirLocalizedPlanarSingleEdgeChamfer", air.GetProperty("materialization").GetProperty("route").GetString());
        Assert.False(air.GetProperty("materialization").GetProperty("legacyFallback").GetBoolean());
        Assert.True(air.GetProperty("step").GetProperty("reimportSucceeded").GetBoolean());
        var localized = air.GetProperty("localizedChamfer");
        Assert.Equal("Direct", localized.GetProperty("selectionMode").GetString());
        Assert.Equal(2, localized.GetProperty("retainedFaces").GetInt32());
        Assert.Equal(1, localized.GetProperty("replacementFaces").GetInt32());
        Assert.Equal("valid", localized.GetProperty("preflight").GetString());

        var stepPath = json.RootElement.GetProperty("outputPath").GetString()!;
        var bytes = File.ReadAllBytes(stepPath);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(JsonDocument.Parse(second.Output).RootElement.GetProperty("outputPath").GetString()!))));
        var imported = Step242Importer.ImportBody(Encoding.UTF8.GetString(bytes));
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(d => d.Message)));
        var body = imported.Value;
        Assert.Equal(10, body.Topology.Vertices.Count());
        Assert.Equal(15, body.Topology.Edges.Count());
        Assert.Equal(7, body.Topology.Faces.Count());
        Assert.Equal(7, body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Plane));
        var points = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : throw new InvalidOperationException()).ToArray();
        Assert.Contains(points, p => Math.Abs(p.X - width / 2d) < 1e-9 && Math.Abs(p.Z - (height / 2d - distance)) < 1e-9);
        Assert.Contains(points, p => Math.Abs(p.X - (width / 2d - distance)) < 1e-9 && Math.Abs(p.Z - height / 2d) < 1e-9);
        Assert.Equal(-width / 2d, points.Min(p => p.X), 9);
        Assert.Equal(width / 2d, points.Max(p => p.X), 9);
        Assert.Equal(-height / 2d, points.Min(p => p.Z), 9);
        Assert.Equal(height / 2d, points.Max(p => p.Z), 9);
    }

    [Theory]
    [InlineData(0d, "+X", "SharedEdgePlusZ", "localized-chamfer-distance-must-be-positive")]
    [InlineData(6d, "+X", "SharedEdgePlusZ", "localized-chamfer-distance-too-large")]
    [InlineData(1d, "+Z", "SharedEdgePlusZ", "air-chamfer-unsupported-selection-rejected")]
    [InlineData(1d, "+X", "SharedEdgePlusY", "air-chamfer-unsupported-face-rejected")]
    public void LocalizedPlanarReplacement_InvalidInput_FailsBeforeExport(double distance, string face, string target, string expected)
    {
        var run = Run(Source(10, 8, 6, distance, face, target));
        Assert.Equal(1, run.Exit);
        Assert.Contains(expected, run.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("legacyFallback\":true", run.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalizedPlanarReplacement_TwoEdgeJunction_RequiresExplicitConstructionWitness()
    {
        var run = Run("""
            Model Localized mm
            Box Base { Size: [10mm, 8mm, 6mm] }
            Modify Base {
                EdgeFinish First { Face: +X Target: SharedEdgePlusZ Kind: Chamfer Distance: 1mm }
                EdgeFinish Second { Face: +Y Target: SharedEdgePlusZ Kind: Chamfer Distance: 1mm }
            }
            """);
        Assert.Equal(1, run.Exit);
        Assert.Contains("localized-chamfer-construction-witness-required:two-edge-junction", run.Output, StringComparison.Ordinal);
    }

    private static (int Exit, string Output) Run(string source)
    {
        var dir = Path.Combine(Path.GetTempPath(), "aetheris-localized-plan", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var sourcePath = Path.Combine(dir, "case.firmament");
        var stepPath = Path.Combine(dir, "case.step");
        File.WriteAllText(sourcePath, source);
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["build", sourcePath, "--out", stepPath, "--json"], output, error);
        return (exit, output + error.ToString());
    }

    private static string Source(double width, double depth, double height, double distance, string face = "+X", string target = "SharedEdgePlusZ") => $$"""
        Model Localized mm
        Box Base { Size: [{{width}}mm, {{depth}}mm, {{height}}mm] }
        Modify Base {
            EdgeFinish Break {
                Face: {{face}}
                Target: {{target}}
                Kind: Chamfer
                Distance: {{distance}}mm
            }
        }
        """;
}
