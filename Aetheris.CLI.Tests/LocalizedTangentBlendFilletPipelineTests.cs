using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

/// <summary>Real parser/export/reimport evidence for AIR-FILLET-LOCALIZED-M1.</summary>
public sealed class LocalizedTangentBlendFilletPipelineTests
{
    [Theory]
    [InlineData(10d, 8d, 6d, 1d)]
    [InlineData(10d, 8d, 6d, 2d)]
    [InlineData(12d, 5d, 7d, 1d)]
    public void LocalizedTangentBlend_ExportsExactSingleEdgeFillet(double width, double depth, double height, double radius)
    {
        var first = Run(Source(width, depth, height, radius));
        var second = Run(Source(width, depth, height, radius));
        Assert.Equal(0, first.Exit);
        Assert.Equal(0, second.Exit);
        using var json = JsonDocument.Parse(first.Output);
        var air = json.RootElement.GetProperty("air");
        Assert.Equal("Fillet", air.GetProperty("feature").GetProperty("kind").GetString());
        Assert.Equal("LocalizedTangentBlend", air.GetProperty("construction").GetProperty("kind").GetString());
        Assert.Equal("LocalizedTangentBlend", air.GetProperty("bRepPlan").GetProperty("planKind").GetString());
        Assert.Equal("AirLocalizedTangentBlendSingleEdgeFillet", air.GetProperty("materialization").GetProperty("route").GetString());
        Assert.False(air.GetProperty("materialization").GetProperty("legacyFallback").GetBoolean());
        var localized = air.GetProperty("localizedFillet");
        Assert.Equal("QuarterCircle", localized.GetProperty("profile").GetString());
        Assert.Equal("Linear", localized.GetProperty("sweep").GetString());
        Assert.Equal("Direct", localized.GetProperty("selectionMode").GetString());
        Assert.Equal(2, localized.GetProperty("retainedFaces").GetInt32());
        Assert.Equal(1, localized.GetProperty("replacementFaces").GetInt32());
        Assert.Equal("valid", localized.GetProperty("preflight").GetString());
        var shared = air.GetProperty("localizedEdgeFinish");
        Assert.Equal("Fillet", shared.GetProperty("kind").GetString());
        Assert.Equal("LocalizedEdgeReplacement", shared.GetProperty("construction").GetString());
        Assert.Equal("CylindricalFillet", shared.GetProperty("replacementGeometry").GetString());
        Assert.Equal("ExplicitOwnedEndpoints", shared.GetProperty("endpointPolicy").GetString());
        Assert.True(shared.GetProperty("bRepPlan").GetProperty("authoritative").GetBoolean());
        Assert.True(air.GetProperty("step").GetProperty("reimportSucceeded").GetBoolean());

        var stepPath = json.RootElement.GetProperty("outputPath").GetString()!;
        var bytes = File.ReadAllBytes(stepPath);
        Assert.Contains("TRIMMED_CURVE", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(JsonDocument.Parse(second.Output).RootElement.GetProperty("outputPath").GetString()!))));
        var imported = Step242Importer.ImportBody(Encoding.UTF8.GetString(bytes));
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(d => d.Message)));
        var body = imported.Value;
        Assert.Equal(10, body.Topology.Vertices.Count());
        Assert.Equal(15, body.Topology.Edges.Count());
        Assert.Equal(7, body.Topology.Faces.Count());
        Assert.Equal(6, body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Plane));
        var cylinder = Assert.Single(body.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.Cylinder).Value.Cylinder!.Value;
        Assert.Equal(radius, cylinder.Radius, 9);
        Assert.Equal(0, cylinder.Axis.X, 9); Assert.Equal(1, cylinder.Axis.Y, 9); Assert.Equal(0, cylinder.Axis.Z, 9);
        var points = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : throw new InvalidOperationException()).ToArray();
        Assert.Contains(points, p => Math.Abs(p.X - width / 2d) < 1e-9 && Math.Abs(p.Z - (height / 2d - radius)) < 1e-9);
        Assert.Contains(points, p => Math.Abs(p.X - (width / 2d - radius)) < 1e-9 && Math.Abs(p.Z - height / 2d) < 1e-9);

        // Removed corner area = R² - πR²/4: the square setback less the retained quarter-disc.
        var removedArea = radius * radius * (1d - Math.PI / 4d);
        Assert.True(removedArea > 0d);
    }

    [Theory]
    [InlineData(0d, "+X", "SharedEdgePlusZ", "localized-fillet-radius-must-be-positive")]
    [InlineData(6d, "+X", "SharedEdgePlusZ", "localized-fillet-radius-too-large")]
    [InlineData(1d, "+Z", "SharedEdgePlusZ", "localized-fillet-unsupported-selection")]
    [InlineData(1d, "+X", "SharedEdgePlusY", "localized-fillet-unsupported-selection")]
    public void LocalizedTangentBlend_InvalidInput_FailsBeforeExport(double radius, string face, string target, string expected)
    {
        var run = Run(Source(10, 8, 6, radius, face, target));
        Assert.Equal(1, run.Exit);
        Assert.Contains(expected, run.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalizedTangentBlend_TwoEdgeJunction_ExportsDirectCylinderIntersection()
    {
        var run = Run("""
            Model Localized mm
            Box Base { Size: [10mm, 8mm, 6mm] }
            Modify Base {
                EdgeFinish First { Face: +X Target: SharedEdgePlusZ Kind: Fillet Distance: 1mm }
                EdgeFinish Second { Face: +Y Target: SharedEdgePlusZ Kind: Fillet Distance: 1mm }
            }
            """);
        Assert.Equal(0, run.Exit);
        using var json = JsonDocument.Parse(run.Output);
        var junction = json.RootElement.GetProperty("air").GetProperty("localizedEdgeJunction");
        Assert.Equal("Fillet", junction.GetProperty("finishKind").GetString());
        Assert.Equal("Direct", junction.GetProperty("selectionMode").GetString());
        Assert.Equal(2, junction.GetProperty("replacementFaces").GetInt32());
        Assert.Equal(0, junction.GetProperty("junctionFaces").GetInt32());
        var closure = junction.GetProperty("closure");
        Assert.Equal("DirectIntersection", closure.GetProperty("kind").GetString());
        Assert.Equal("Ellipse", closure.GetProperty("curveKind").GetString());
        Assert.True(closure.GetProperty("exact").GetBoolean());
        Assert.Equal(1, closure.GetProperty("sharedEdges").GetInt32());
    }

    [Theory]
    [InlineData(10d, 8d, 6d, 1d)]
    [InlineData(10d, 8d, 6d, 2d)]
    [InlineData(12d, 5d, 7d, 1d)]
    public void LocalizedTangentBlend_ThreeEdgeTrihedral_ExportsSphericalOctant(double width, double depth, double height, double radius)
    {
        var run = Run($$"""
            Model Trihedral mm
            Box Base { Size: [{{width}}mm, {{depth}}mm, {{height}}mm] }
            Modify Base {
                EdgeFinish XZ { Face: +X Target: SharedEdgePlusZ Kind: Fillet Distance: {{radius}}mm }
                EdgeFinish YZ { Face: +Y Target: SharedEdgePlusZ Kind: Fillet Distance: {{radius}}mm }
                EdgeFinish XY { Face: +X Target: SharedEdgePlusY Kind: Fillet Distance: {{radius}}mm }
            }
            """);
        Assert.Equal(0, run.Exit);
        using var json = JsonDocument.Parse(run.Output);
        var air = json.RootElement.GetProperty("air");
        var junction = air.GetProperty("localizedEdgeJunction");
        Assert.Equal("LocalizedTrihedralFillet", junction.GetProperty("construction").GetString());
        Assert.Equal("SphericalOctant", junction.GetProperty("cornerPatch").GetString());
        Assert.Equal(3, junction.GetProperty("replacementFaces").GetInt32());
        Assert.Equal(1, junction.GetProperty("junctionFaces").GetInt32());
        Assert.Equal(3, junction.GetProperty("closure").GetProperty("sharedEdges").GetInt32());
        Assert.Equal(3, air.GetProperty("materialization").GetProperty("cylindricalFaces").GetInt32());
        Assert.Equal(1, air.GetProperty("materialization").GetProperty("sphericalFaces").GetInt32());
        Assert.True(air.GetProperty("step").GetProperty("reimportSucceeded").GetBoolean());
        var step = File.ReadAllText(json.RootElement.GetProperty("outputPath").GetString()!);
        Assert.Contains("SPHERICAL_SURFACE", step, StringComparison.Ordinal);
        Assert.Contains("CYLINDRICAL_SURFACE", step, StringComparison.Ordinal);
    }

    private static (int Exit, string Output) Run(string source)
    {
        var dir = Path.Combine(Path.GetTempPath(), "aetheris-localized-fillet", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var sourcePath = Path.Combine(dir, "case.firmament");
        var stepPath = Path.Combine(dir, "case.step");
        File.WriteAllText(sourcePath, source);
        var output = new StringWriter(); var error = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["build", sourcePath, "--out", stepPath, "--json"], output, error);
        return (exit, output + error.ToString());
    }

    private static string Source(double width, double depth, double height, double radius, string face = "+X", string target = "SharedEdgePlusZ") => $$"""
        Model Localized mm
        Box Base { Size: [{{width}}mm, {{depth}}mm, {{height}}mm] }
        Modify Base {
            EdgeFinish RoundedEdge {
                Face: {{face}}
                Target: {{target}}
                Kind: Fillet
                Distance: {{radius}}mm
            }
        }
        """;
}
