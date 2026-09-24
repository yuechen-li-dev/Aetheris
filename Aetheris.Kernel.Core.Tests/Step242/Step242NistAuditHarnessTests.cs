using System.Text;
using System.Text.Json;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242NistAuditHarnessTests
{
    [Theory]
    [MemberData(nameof(NistCorpusEntries))]
    [Trait("Category", "SlowCorpus")]
    public void NistCorpus_PerFile_AuditReport_MatchesSnapshot(string relativePath)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("AETHERIS_UPDATE_STEP242_SNAPSHOT"), "1", StringComparison.Ordinal)) return;
        var entry = BuildNistEntry(relativePath);

        var actual = ExecutePerFileLegacyAudit(entry);

        var expectedByPath = LoadLegacySnapshotEntriesByPath();
        Assert.True(expectedByPath.TryGetValue(entry.Path, out var expected), $"Missing snapshot entry for '{entry.Path}'.");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NistCorpus_SnapshotCoversEveryFile()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("AETHERIS_UPDATE_STEP242_SNAPSHOT"), "1", StringComparison.Ordinal))
        {
            var reports = GetNistCorpusRelativePaths().Select(path => ExecutePerFileLegacyAudit(BuildNistEntry(path))).ToArray();
            var json = JsonSerializer.Serialize(reports, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            File.WriteAllText(NistSnapshotPath(), Step242CorpusManifestRunner.NormalizeLf(json) + "\n", new UTF8Encoding(false));
            return;
        }
        Assert.Equal(GetNistCorpusRelativePaths(), LoadLegacySnapshotEntriesByPath().Keys.OrderBy(path => path, StringComparer.Ordinal));
    }

    [Fact]
    public void NistCorpus_RepresentativeAp242Lane_DoesNotRequireDisplayAudit()
    {
        const string relativePath = "testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp";
        var entry = BuildNistEntry(relativePath);

        var first = Step242CorpusManifestRunner.RunOne(entry);

        Assert.Equal("success", first.Status);
        Assert.Equal(string.Empty, first.FirstFailureLayer);
        Assert.Equal("notRun", first.DisplayStatus);
        Assert.Equal(string.Empty, first.DisplayFirstFailureLayer);
    }

    [Fact]
    public void NistCorpus_FormerDisplayBlocker_Ftc11_NowPassesDisplayLane()
    {
        const string relativePath = "testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp";
        var entry = BuildNistEntry(relativePath);

        var first = Step242CorpusManifestRunner.RunOne(entry, includeDisplayAudit: true);

        Assert.Equal("success", first.Status);
        Assert.Equal(string.Empty, first.FirstFailureLayer);
        Assert.Equal("success", first.DisplayStatus);
        Assert.Equal(string.Empty, first.DisplayFirstFailureLayer);
    }

    [Fact]
    public void NistCorpus_Ftc08Tg_IsExplicitlyClassifiedAsUnsupportedTessellationOnly()
    {
        const string relativePath = "testdata/step242/nist/FTC/nist_ftc_08_asme1_ap242-e1-tg.stp";
        var entry = BuildNistEntry(relativePath);

        var report = Step242CorpusManifestRunner.RunOne(entry);

        Assert.Equal("importFail", report.Status);
        Assert.Equal("importer-representation", report.FirstFailureLayer);
        Assert.Equal("Importer.Representation.Unsupported", report.FirstDiagnostic.Source);
        Assert.StartsWith("Unsupported tessellation-only STEP geometry", report.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.Null(report.CanonicalSha256);
    }

    public static IEnumerable<object[]> NistCorpusEntries() => GetNistCorpusRelativePaths().Select(path => new object[] { path });

    private static IReadOnlyList<string> GetNistCorpusRelativePaths()
    {
        return Directory
            .EnumerateFiles(Path.Combine(Step242CorpusManifestRunner.RepoRoot(), "testdata", "step242", "nist"), "*.stp", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Step242CorpusManifestRunner.RepoRoot(), path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static Step242CorpusManifestEntry BuildNistEntry(string relativePath)
        => new(FileId(relativePath), relativePath, "deferred", "NIST audit corpus", null, null, null, null);

    private static LegacyAuditEntry ExecutePerFileLegacyAudit(Step242CorpusManifestEntry entry)
    {
        var r = Step242CorpusManifestRunner.RunOne(entry);
        return new LegacyAuditEntry(
            FileId: r.Id,
            Path: r.Path,
            SizeBytes: r.SizeBytes,
            Status: r.Status,
            FirstFailureLayer: r.FirstFailureLayer,
            FirstDiagnostic: r.FirstDiagnostic,
            DiagnosticCount: r.DiagnosticCount,
            ExceptionEscaped: r.ExceptionEscaped,
            TopologyCounts: r.TopologyCounts,
            CanonicalSha256: r.CanonicalSha256);
    }

    private static Dictionary<string, LegacyAuditEntry> LoadLegacySnapshotEntriesByPath()
    {
        var snapshotPath = NistSnapshotPath();
        var snapshotJson = Step242CorpusManifestRunner.NormalizeLf(File.ReadAllText(snapshotPath, Encoding.UTF8));
        var entries = JsonSerializer.Deserialize<LegacyAuditEntry[]>(snapshotJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(entries);

        return entries.ToDictionary(e => e.Path, StringComparer.Ordinal);
    }

    private static string NistSnapshotPath() => Path.Combine(Step242CorpusManifestRunner.RepoRoot(), "testdata", "step242", "manifests", "nist.v0.report.json");

    private static string FileId(string relativePath)
    {
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var folder = Path.GetDirectoryName(relativePath)?.Replace('\\', '/').Replace('/', '_') ?? string.Empty;
        return string.IsNullOrWhiteSpace(folder) ? stem : $"{folder}_{stem}";
    }

    private sealed record LegacyAuditEntry(
        string FileId,
        string Path,
        int SizeBytes,
        string Status,
        string FirstFailureLayer,
        Step242AuditDiagnostic FirstDiagnostic,
        int DiagnosticCount,
        bool ExceptionEscaped,
        Step242TopologyCounts TopologyCounts,
        string? CanonicalSha256);
}
