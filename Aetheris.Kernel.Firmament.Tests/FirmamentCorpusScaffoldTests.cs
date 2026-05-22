using Aetheris.Kernel.Core.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentCorpusScaffoldTests
{
    private const string ManifestPath = "testdata/firmament/manifests/firmament.corpus.json";

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
    public void Fixtures_AreCanonicalToonStyle_NotJsonObjectLiterals()
    {
        var fixturesRoot = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "testdata", "firmament", "fixtures");
        var fixtureFiles = Directory.GetFiles(fixturesRoot, "*.firmament", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(fixtureFiles);

        foreach (var fixturePath in fixtureFiles)
        {
            var fixtureText = FirmamentCorpusHarness.NormalizeLf(File.ReadAllText(fixturePath));
            Assert.False(
                fixtureText.TrimStart().StartsWith("{", StringComparison.Ordinal),
                $"Fixture '{Path.GetFileName(fixturePath)}' must use canonical TOON-style Firmament syntax, not a JSON object literal.");
        }
    }

    [Fact]
    public void ToonFixtures_UseCanonicalOpsHeaderSpelling()
    {
        var manifest = FirmamentCorpusHarness.LoadManifest(ManifestPath);

        foreach (var entry in manifest.Entries)
        {
            var fixtureText = FirmamentCorpusHarness.ReadFixtureText(entry.FixturePath);
            if (fixtureText.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.Contains("ops[", fixtureText, StringComparison.Ordinal);
            Assert.DoesNotContain("ops: []", fixtureText, StringComparison.Ordinal);
            Assert.DoesNotContain("\nops:\n", fixtureText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FirmamentTestSourceLiterals_AvoidJsonShapedExamples()
    {
        var testsRoot = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "Aetheris.Kernel.Firmament.Tests");
        var testFiles = Directory.GetFiles(testsRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(testFiles);

        foreach (var testFile in testFiles)
        {
            var contents = FirmamentCorpusHarness.NormalizeLf(File.ReadAllText(testFile));
            Assert.DoesNotContain("\"firmament\":", contents, StringComparison.Ordinal);
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
