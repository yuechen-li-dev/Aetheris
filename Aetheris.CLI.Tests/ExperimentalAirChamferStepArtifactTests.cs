using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class ExperimentalAirChamferStepArtifactTests
{
    [Fact]
    public void Experimental_AirChamfer_Cube_Help_Is_Discoverable_And_Explicitly_Lab_Only()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "airchamfer-cube", "--help"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        var text = stdout.ToString();
        Assert.Contains("Usage: aetheris experimental airchamfer-cube --out <path> [--json]", text, StringComparison.Ordinal);
        Assert.Contains("Experimental/lab-only", text, StringComparison.Ordinal);
        Assert.Contains("no production chamfer route replacement", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no 3D Boolean", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Experimental_AirChamfer_Cube_Writes_Candidate_Step_With_Deterministic_Markers_And_Diagnostics()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"edge-x10-airchamfer-cube-one-edge-{Guid.NewGuid():N}.step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "airchamfer-cube", "--out", outputPath, "--json"], stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
            Assert.True(File.Exists(outputPath));

            var stepText = File.ReadAllText(outputPath);
            Assert.False(string.IsNullOrWhiteSpace(stepText));
            Assert.Contains("ISO-10303-21", stepText, StringComparison.Ordinal);
            Assert.Contains("MANIFOLD_SOLID_BREP", stepText, StringComparison.Ordinal);
            Assert.Contains("ADVANCED_FACE", stepText, StringComparison.Ordinal);
            Assert.Contains("PLANE", stepText, StringComparison.Ordinal);
            Assert.DoesNotContain("CYLINDRICAL_SURFACE", stepText, StringComparison.Ordinal);
            Assert.DoesNotContain("BREP_WITH_VOIDS", stepText, StringComparison.Ordinal);

            using var doc = JsonDocument.Parse(stdout.ToString());
            var root = doc.RootElement;
            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal(Path.GetFullPath(outputPath), root.GetProperty("outputPath").GetString());
            Assert.Equal("experimental-cli-airchamfer-cube", root.GetProperty("route").GetString());
            Assert.Equal("AirChamferShadowRoute->AirChamferRealBodyPrototype", root.GetProperty("candidatePath").GetString());

            var diagnostics = root.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.Contains("edge-x10-airchamfer-step-artifact-started", diagnostics);
            Assert.Contains("edge-x10-cli-export-path-used", diagnostics);
            Assert.Contains("edge-x10-air-chamfer-shadow-route-invoked", diagnostics);
            Assert.Contains("edge-x10-candidate-body-created", diagnostics);
            Assert.Contains("edge-x10-step-artifact-written", diagnostics);
            Assert.Contains("edge-x10-step-smoke-succeeded", diagnostics);
            Assert.Contains("edge-x10-legacy-authority-preserved", diagnostics);
            Assert.Contains("edge-x10-no-production-route-replacement", diagnostics);
            Assert.Contains("edge-x10-no-3d-boolean-used", diagnostics);
            Assert.Contains("edge-v3-no-production-route-replacement", diagnostics);
            Assert.Contains("edge-v3-no-3d-boolean-used", diagnostics);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }
}
