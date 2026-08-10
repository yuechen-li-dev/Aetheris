using System.Text.Json;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2LanguageManifestTests
{
    [Fact]
    public void CanonicalLanguageManifest_IsValidClassifiedAndDocumentationTargetsExist()
    {
        var root = FirmamentCorpusHarness.RepoRoot();
        var path = Path.Combine(root, "docs", "firmament-v2", "language-features.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var features = document.RootElement.GetProperty("features").EnumerateArray().ToArray();
        Assert.True(features.Length >= 30);
        var admitted = new HashSet<string>(StringComparer.Ordinal)
        {
            "Supported", "Experimental", "Internal-only", "Legacy", "Deprecated", "Parser-only / dead", "Future / incomplete"
        };
        Assert.All(features, feature => Assert.Contains(feature.GetProperty("status").GetString()!, admitted));
        Assert.Equal(features.Length, features.Select(feature => feature.GetProperty("name").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.True(File.Exists(Path.Combine(root, "docs", "firmament-v2", "language-reference.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "firmament-v2", "quickstart.md")));
    }
}
