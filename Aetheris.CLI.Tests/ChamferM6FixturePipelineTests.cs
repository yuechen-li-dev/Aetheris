using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class ChamferM6FixturePipelineTests
{
    [Theory]
    [InlineData("air-cylinder-top-rim-chamfer.valid.firmament", 20, 50, 1)]
    [InlineData("air-cylinder-top-rim-variant.valid.firmament", 7.5, 18, 2)]
    public void CircularRimFixtures_ExportDeterministicChangedReimportableStep(string fixture, double radius, double height, double distance)
    {
        var first = BuildFixture(fixture);
        var second = BuildFixture(fixture);
        Assert.Equal(first.Hash, second.Hash);
        using var json = JsonDocument.Parse(first.Json);
        var air = json.RootElement.GetProperty("air");
        Assert.Equal("RevolutionProfileRewrite", air.GetProperty("construction").GetProperty("kind").GetString());
        Assert.True(air.GetProperty("construction").GetProperty("compilerGeneratedWitness").GetBoolean());
        Assert.Equal("RevolvedProfile", air.GetProperty("bRepPlan").GetProperty("planKind").GetString());
        Assert.True(air.GetProperty("bRepPlan").GetProperty("authoritative").GetBoolean());
        Assert.Equal("AirRevolutionProfileTopRimChamfer", air.GetProperty("materialization").GetProperty("route").GetString());
        Assert.False(air.GetProperty("materialization").GetProperty("legacyFallback").GetBoolean());
        Assert.Equal(1, air.GetProperty("materialization").GetProperty("cylindricalFaces").GetInt32());
        Assert.Equal(1, air.GetProperty("materialization").GetProperty("conicalFaces").GetInt32());
        Assert.Equal(distance, air.GetProperty("materialization").GetProperty("measuredTopInsetX").GetDouble(), 9);

        var imported = Step242Importer.ImportBody(File.ReadAllText(first.StepPath, Encoding.UTF8));
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(d => d.Message)));
        // Periodic rims share seam vertices after the producer topology repair.
        Assert.Equal(3, imported.Value.Topology.Vertices.Count());
        Assert.Equal(5, imported.Value.Topology.Edges.Count());
        Assert.Equal(4, imported.Value.Topology.Faces.Count());
        Assert.Equal(1, imported.Value.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(1, imported.Value.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cone));
        var analysis = StepAnalyzer.Analyze(first.StepPath);
        var bounds = analysis.Summary.BoundingBox!.Value;
        Assert.Equal(-radius, bounds.Min.X, 9);
        Assert.Equal(radius, bounds.Max.Y, 9);
        var volume = StepAnalyzer.AnalyzeVolume(first.StepPath);
        var topRadius = radius - distance;
        var expectedVolume = Math.PI * radius * radius * (height - distance)
            + Math.PI * distance / 3d * (radius * radius + radius * topRadius + topRadius * topRadius);
        Assert.True(volume.Exact);
        Assert.Equal("analytic-piecewise-linear-revolved-profile", volume.Method);
        Assert.Equal(expectedVolume, volume.Volume, 7);
        var replacement = air.GetProperty("construction").GetProperty("replacementProfile");
        Assert.Equal(radius - distance, replacement[2][0].GetDouble(), 9);
        Assert.Equal(height / 2, replacement[2][1].GetDouble(), 9);
    }

    [Theory]
    [InlineData("air-hole-entry-chamfer.valid.firmament", 6, 5)]
    [InlineData("air-hole-entry-chamfer-variant.valid.firmament", 4.5, 4.25)]
    public void InternalHoleEntryFixture_UsesProfileStackAndContainsRealConeAndShaft(string fixture, double shaftDiameter, double transitionZ)
    {
        var built = BuildFixture(fixture);
        var imported = Step242Importer.ImportBody(File.ReadAllText(built.StepPath, Encoding.UTF8));
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(d => d.Message)));
        Assert.Contains(imported.Value.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.Cone);
        Assert.Contains(imported.Value.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.Cylinder);
        Assert.Equal(11, imported.Value.Topology.Vertices.Count());
        var coneBinding = Assert.Single(imported.Value.Bindings.FaceBindings,
            binding => imported.Value.Geometry.GetSurface(binding.SurfaceGeometryId).Kind == SurfaceGeometryKind.Cone);
        var cone = imported.Value.Geometry.GetSurface(coneBinding.SurfaceGeometryId).Cone!.Value;
        Assert.False(coneBinding.SameSense);
        Assert.Equal(transitionZ, cone.PlacementOrigin.Z, 9);
        Assert.Equal(shaftDiameter / 2d, cone.PlacementRadius, 9);
        Assert.Equal(1d, cone.Axis.ToVector().Z, 9);
        using var json = JsonDocument.Parse(built.Json);
        var feature = Assert.Single(json.RootElement.GetProperty("features").EnumerateArray());
        Assert.Equal("AirHoleSimpleShaftMaterializer", feature.GetProperty("materializationRoute").GetString());
        Assert.Equal("HoleProfileStack", feature.GetProperty("constructionKind").GetString());
        Assert.Equal("Countersink", feature.GetProperty("stackKind").GetString());
        Assert.Contains("conical-entry", feature.GetProperty("witnessSummary").GetString(), StringComparison.Ordinal);
        Assert.Equal(1, feature.GetProperty("cylindricalFaces").GetInt32());
        Assert.Equal(1, feature.GetProperty("conicalFaces").GetInt32());
        Assert.True(feature.GetProperty("stepReimportSucceeded").GetBoolean());
        Assert.Equal(shaftDiameter, feature.GetProperty("diameter").GetDouble());
    }

    [Theory]
    [InlineData("air-cylinder-top-rim-zero.invalid.firmament", "chamfer-invalid-distance:must-be-positive")]
    [InlineData("air-cylinder-top-rim-oversized.invalid.firmament", "chamfer-distance-too-large:circular-top-rim")]
    [InlineData("air-hole-entry-chamfer-zero.invalid.firmament", "firmament-v2-hole-countersink-invalid")]
    public void InvalidChamferFixtures_FailBeforeStepAndNeverFallback(string fixture, string expected)
    {
        var sourcePath = FixturePath("invalid", fixture);
        var outputPath = Path.Combine(Path.GetTempPath(), "aetheris-chamfer-m6", Guid.NewGuid().ToString("N"), "invalid.step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = CliRunner.Run(["build", sourcePath, "--out", outputPath, "--json"], stdout, stderr);
        var combined = stdout + stderr.ToString();
        Assert.Equal(1, exit);
        Assert.Contains(expected, combined, StringComparison.Ordinal);
        Assert.DoesNotContain("legacyFallback\":true", combined, StringComparison.Ordinal);
        Assert.False(File.Exists(outputPath));
    }

    private static (string Hash, string Json, string StepPath) BuildFixture(string fixture)
    {
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-chamfer-m6", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var output = Path.Combine(directory, Path.GetFileNameWithoutExtension(fixture) + ".step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = CliRunner.Run(["build", FixturePath("valid", fixture), "--out", output, "--json"], stdout, stderr);
        Assert.Equal(0, exit);
        Assert.True(File.Exists(output));
        return (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output))), stdout + stderr.ToString(), output);
    }

    private static string FixturePath(string category, string fixture) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2/Chamfer", category, fixture));

}
