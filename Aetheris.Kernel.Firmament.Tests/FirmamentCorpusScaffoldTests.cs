using Aetheris.Kernel.Core.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentCorpusScaffoldTests
{
    private const string ManifestPath = "testdata/firmament/manifests/pre-m0.corpus.json";

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
    public void CuratedCases_InvokeStubCompiler_WithExpectedCoarseOutcome()
    {
        var manifest = FirmamentCorpusHarness.LoadManifest(ManifestPath);

        foreach (var entry in manifest.Entries.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            Assert.Equal("stubNotImplemented", entry.ExpectedOutcome);

            var fixtureText = FirmamentCorpusHarness.ReadFixtureText(entry.FixturePath);
            var diagnostic = FirmamentCorpusHarness.CompileFirstDiagnostic(fixtureText);

            Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
            if (entry.ExpectedDiagnostic is not null)
            {
                Assert.Equal(entry.ExpectedDiagnostic.Source, diagnostic.Source);
                Assert.Equal(entry.ExpectedDiagnostic.Code, diagnostic.Code.ToString());
            }
        }
    }
}
