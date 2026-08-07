using System.Text.Json;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class Preview1CapabilityManifestTests
{
    [Fact]
    public void FreezeManifest_MapsSupportedFeaturesAndInvalidPoliciesToExistingFixtures()
    {
        var root = FirmamentCorpusHarness.RepoRoot();
        var manifestPath = Path.Combine(root, "artifacts", "release", "preview1-capabilities.json");
        Assert.True(File.Exists(manifestPath), "Missing Preview 1 capability manifest.");

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var manifest = document.RootElement;
        Assert.Equal("Preview 1 feature freeze", manifest.GetProperty("version").GetString());

        foreach (var feature in manifest.GetProperty("features").EnumerateArray())
        {
            var status = feature.GetProperty("status").GetString();
            if (status is not ("Supported" or "SupportedBounded" or "SupportedWithExplicitCompatibilityPolicy")) continue;
            var fixtures = feature.GetProperty("fixturePaths").EnumerateArray().Select(item => item.GetString()).Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
            Assert.NotEmpty(fixtures);
            foreach (var fixture in fixtures) Assert.True(File.Exists(Path.Combine(root, fixture!.Replace('/', Path.DirectorySeparatorChar))), $"Supported feature '{feature.GetProperty("id").GetString()}' references missing fixture '{fixture}'.");
            if (feature.TryGetProperty("stepArtifacts", out var artifacts))
                foreach (var artifact in artifacts.EnumerateArray())
                {
                    var path = artifact.GetString();
                    Assert.True(!string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))), $"Supported feature '{feature.GetProperty("id").GetString()}' references missing STEP artifact '{path}'.");
                }
        }

        foreach (var policy in manifest.GetProperty("edgeFinish").GetProperty("invalidPolicies").EnumerateArray())
        {
            Assert.Equal("Invalid", policy.GetProperty("status").GetString());
            var fixture = policy.GetProperty("fixturePath").GetString();
            Assert.False(string.IsNullOrWhiteSpace(fixture));
            Assert.True(File.Exists(Path.Combine(root, fixture!.Replace('/', Path.DirectorySeparatorChar))), $"Invalid policy fixture is missing: {fixture}");
        }

        foreach (var blocker in manifest.GetProperty("releaseBlockers").EnumerateArray())
        {
            Assert.NotEqual("Supported", blocker.GetProperty("affectedFeatureStatus").GetString());
        }
    }
}
