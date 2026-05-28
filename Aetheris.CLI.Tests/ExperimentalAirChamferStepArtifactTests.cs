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

    [Fact]
    public void Experimental_AirChamfer_Corpus_Help_Is_Discoverable_And_Explicitly_Lab_Only()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "airchamfer-corpus", "--help"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        var text = stdout.ToString();
        Assert.Contains("Usage: aetheris experimental airchamfer-corpus --out-dir <dir> [--json]", text, StringComparison.Ordinal);
        Assert.Contains("EDGE-X11", text, StringComparison.Ordinal);
        Assert.Contains("Experimental/lab-only", text, StringComparison.Ordinal);
        Assert.Contains("no production chamfer route replacement", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no 3D Boolean", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Experimental_AirChamfer_Corpus_Writes_Successful_Artifacts_And_Json_Only_Rejections()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"edge-x11-airchamfer-corpus-{Guid.NewGuid():N}");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "airchamfer-corpus", "--out-dir", outputDir, "--json"], stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));

            var canonicalPath = Path.Combine(outputDir, "edge-x11-airchamfer-cube-canonical.step");
            var nonOrthogonalPath = Path.Combine(outputDir, "edge-x11-airchamfer-cube-nonorthogonal.step");
            var invalidDistancePath = Path.Combine(outputDir, "edge-x11-airchamfer-invalid-distance.step");
            var legacyDependentPath = Path.Combine(outputDir, "edge-x11-airchamfer-triangle-legacy-dependent.step");
            var summaryPath = Path.Combine(outputDir, "edge-x11-airchamfer-corpus.json");

            Assert.True(File.Exists(canonicalPath));
            Assert.True(new FileInfo(canonicalPath).Length > 0);
            Assert.True(File.Exists(nonOrthogonalPath));
            Assert.True(new FileInfo(nonOrthogonalPath).Length > 0);
            Assert.False(File.Exists(invalidDistancePath));
            Assert.False(File.Exists(legacyDependentPath));
            Assert.True(File.Exists(summaryPath));

            AssertStepMarkers(File.ReadAllText(canonicalPath));
            AssertStepMarkers(File.ReadAllText(nonOrthogonalPath));

            using var stdoutDoc = JsonDocument.Parse(stdout.ToString());
            using var fileDoc = JsonDocument.Parse(File.ReadAllText(summaryPath));
            Assert.Equal(stdoutDoc.RootElement.GetProperty("milestone").GetString(), fileDoc.RootElement.GetProperty("milestone").GetString());

            var root = stdoutDoc.RootElement;
            Assert.Equal("EDGE-X11", root.GetProperty("corpusVersion").GetString());
            Assert.Equal("EDGE-X11", root.GetProperty("milestone").GetString());
            Assert.Equal("AirChamferShadowRoute->AirChamferRealBodyPrototype", root.GetProperty("candidatePath").GetString());
            Assert.Equal("experimental-cli-airchamfer-corpus", root.GetProperty("route").GetString());
            Assert.True(root.GetProperty("legacyAuthorityPreserved").GetBoolean());
            Assert.False(root.GetProperty("productionOutputChanged").GetBoolean());
            Assert.True(root.GetProperty("noProductionRouteReplacement").GetBoolean());
            Assert.True(root.GetProperty("no3DBooleanUsed").GetBoolean());

            var cases = root.GetProperty("cases").EnumerateArray().ToDictionary(x => x.GetProperty("caseName").GetString()!);
            Assert.Equal("succeeded", cases["canonical"].GetProperty("status").GetString());
            Assert.Equal("succeeded", cases["nonorthogonal"].GetProperty("status").GetString());
            Assert.Equal("rejected", cases["invalid-distance"].GetProperty("status").GetString());
            Assert.Equal("deferred", cases["triangle-legacy-dependent"].GetProperty("status").GetString());
            Assert.Equal("AirChamferShadowRoute->AirChamferRealBodyPrototype", cases["canonical"].GetProperty("candidatePath").GetString());
            Assert.Equal("experimental-cli-airchamfer-corpus", cases["canonical"].GetProperty("route").GetString());
            Assert.Equal(canonicalPath, cases["canonical"].GetProperty("artifactPath").GetString());
            Assert.Equal(nonOrthogonalPath, cases["nonorthogonal"].GetProperty("artifactPath").GetString());
            Assert.Equal(JsonValueKind.Null, cases["invalid-distance"].GetProperty("artifactPath").ValueKind);
            Assert.Equal(JsonValueKind.Null, cases["triangle-legacy-dependent"].GetProperty("artifactPath").ValueKind);

            var canonicalMarkers = cases["canonical"].GetProperty("stepMarkerSummary");
            Assert.True(canonicalMarkers.GetProperty("requiredPresentSatisfied").GetBoolean());
            Assert.True(canonicalMarkers.GetProperty("forbiddenAbsentSatisfied").GetBoolean());
            Assert.NotEqual(JsonValueKind.Null, cases["canonical"].GetProperty("topologySummary").ValueKind);

            var diagnostics = root.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.Contains("edge-x11-legacy-authority-preserved", diagnostics);
            Assert.Contains("edge-x11-no-production-route-replacement", diagnostics);
            Assert.Contains("edge-x11-no-3d-boolean-used", diagnostics);
            Assert.Contains("edge-x11-step-smoke-succeeded:canonical", diagnostics);
            Assert.Contains("edge-x11-step-smoke-succeeded:nonorthogonal", diagnostics);
            Assert.Contains("edge-x11-case-rejected:invalid-distance:reject-invalid-distance", diagnostics);
            Assert.Contains("edge-x11-case-deferred:triangle-legacy-dependent:legacy-dependent-fallback", diagnostics);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    private static void AssertStepMarkers(string stepText)
    {
        Assert.False(string.IsNullOrWhiteSpace(stepText));
        Assert.Contains("ISO-10303-21", stepText, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", stepText, StringComparison.Ordinal);
        Assert.Contains("ADVANCED_FACE", stepText, StringComparison.Ordinal);
        Assert.Contains("PLANE", stepText, StringComparison.Ordinal);
        Assert.DoesNotContain("CYLINDRICAL_SURFACE", stepText, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", stepText, StringComparison.Ordinal);
    }

}
