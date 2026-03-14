using Aetheris.Kernel.Core.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentCorpusScaffoldTests
{
    private const string ManifestPath = "testdata/firmament/manifests/m0a.corpus.json";

    [Fact]
    public void Manifest_Exists_And_CanBeLoaded()
    {
        var fullPath = Path.Combine(FirmamentCorpusHarness.RepoRoot(), ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Missing manifest: {ManifestPath}");

        var manifest = FirmamentCorpusHarness.LoadManifest(ManifestPath);
        Assert.Equal("1", manifest.Version);
        Assert.NotEmpty(manifest.Entries);
    }

    [Fact]
    public void Manifest_ListedFixtures_Exist_And_AreReadableDeterministically()
    {
        var manifest = FirmamentCorpusHarness.LoadManifest(ManifestPath);

        foreach (var entry in manifest.Entries)
        {
            var fullPath = FirmamentCorpusHarness.ResolveFixtureFullPath(entry.FixturePath);
            Assert.True(File.Exists(fullPath), $"Missing fixture: {entry.FixturePath}");

            var first = FirmamentCorpusHarness.ReadFixtureText(entry.FixturePath);
            var second = FirmamentCorpusHarness.ReadFixtureText(entry.FixturePath);

            Assert.Equal(first, second);
            Assert.False(string.IsNullOrWhiteSpace(first));
        }
    }

    [Fact]
    public void Fixtures_UseCanonicalEmptyOpsSpelling()
    {
        var manifest = FirmamentCorpusHarness.LoadManifest(ManifestPath);

        foreach (var entry in manifest.Entries)
        {
            var fixtureText = FirmamentCorpusHarness.ReadFixtureText(entry.FixturePath);

            Assert.Contains("ops[0]:", fixtureText, StringComparison.Ordinal);
            Assert.DoesNotContain("ops: []", fixtureText, StringComparison.Ordinal);
            Assert.DoesNotContain("\nops:\n", fixtureText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CuratedCases_Compile_WithExpectedOutcomeAndDiagnostics()
    {
        var manifest = FirmamentCorpusHarness.LoadManifest(ManifestPath);

        foreach (var entry in manifest.Entries.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            var fixtureText = FirmamentCorpusHarness.ReadFixtureText(entry.FixturePath);
            var result = FirmamentCorpusHarness.Compile(fixtureText);

            if (string.Equals(entry.ExpectedOutcome, "success", StringComparison.Ordinal))
            {
                Assert.True(result.Compilation.IsSuccess);
                continue;
            }

            Assert.Equal("failure", entry.ExpectedOutcome);
            Assert.False(result.Compilation.IsSuccess);
            var diagnostic = Assert.Single(result.Compilation.Diagnostics);

            Assert.NotNull(entry.ExpectedDiagnostic);
            Assert.Equal(entry.ExpectedDiagnostic!.Source, diagnostic.Source);
            Assert.Equal(Enum.Parse<KernelDiagnosticCode>(entry.ExpectedDiagnostic.KernelCode), diagnostic.Code);
            Assert.Contains(entry.ExpectedDiagnostic.FirmamentCode, diagnostic.Message, StringComparison.Ordinal);
        }
    }
}
