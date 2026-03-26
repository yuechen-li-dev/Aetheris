using System.Text;
using System.Text.Json;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242NistAuditHarnessTests
{
    [Theory]
    [MemberData(nameof(NistCorpusEntries))]
    [Trait("Category", "SlowCorpus")]
    public void NistCorpus_PerFile_AuditReport_IsStable_AndMatchesSnapshot(string relativePath)
    {
        var entry = BuildNistEntry(relativePath);

        var first = ExecutePerFileLegacyAudit(entry);
        var second = ExecutePerFileLegacyAudit(entry);

        Assert.Equal(first, second);

        var expectedByPath = LoadLegacySnapshotEntriesByPath();
        Assert.True(expectedByPath.TryGetValue(entry.Path, out var expected), $"Missing snapshot entry for '{entry.Path}'.");
        Assert.Equal(expected, first);
    }

    [Fact]
    [Trait("Category", "SlowCorpus")]
    public void NistCorpus_AggregateAuditReport_IsByteStableAcrossConsecutiveRuns_AndMatchesSnapshot()
    {
        var entries = GetNistCorpusEntries();

        var first = BuildLegacyAuditJson(entries);
        var second = BuildLegacyAuditJson(entries);
        Assert.Equal(first, second);

        var snapshotPath = NistSnapshotPath();
        var expected = Step242CorpusManifestRunner.NormalizeLf(File.ReadAllText(snapshotPath, Encoding.UTF8));
        if (!string.Equals(expected, first, StringComparison.Ordinal))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("AETHERIS_UPDATE_STEP242_SNAPSHOT"), "1", StringComparison.Ordinal))
            {
                File.WriteAllText(snapshotPath, first, new UTF8Encoding(false));
                return;
            }

            Assert.Fail("NIST audit snapshot mismatch.");
        }
    }

    public static IEnumerable<object[]> NistCorpusEntries() => GetNistCorpusRelativePaths().Select(path => new object[] { path });

    private static IReadOnlyList<Step242CorpusManifestEntry> GetNistCorpusEntries() => GetNistCorpusRelativePaths().Select(BuildNistEntry).ToArray();

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

    private static string BuildLegacyAuditJson(IReadOnlyList<Step242CorpusManifestEntry> entries)
    {
        var report = entries.Select(ExecutePerFileLegacyAudit).ToArray();

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        return Step242CorpusManifestRunner.NormalizeLf(json) + "\n";
    }

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
            TessellationCounts: r.TessellationCounts,
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
        Step242TessellationCounts TessellationCounts,
        string? CanonicalSha256);
}
