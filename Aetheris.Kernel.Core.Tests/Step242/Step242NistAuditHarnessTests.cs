using System.Text;
using System.Text.Json;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242NistAuditHarnessTests
{
    [Fact]
    [Trait("Category", "SlowCorpus")]
    public void NistCorpus_AuditReport_IsByteStableAcrossConsecutiveRuns_AndMatchesSnapshot()
    {
        var entries = Directory
            .EnumerateFiles(Path.Combine(Step242CorpusManifestRunner.RepoRoot(), "testdata", "step242", "nist"), "*.stp", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Step242CorpusManifestRunner.RepoRoot(), path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new Step242CorpusManifestEntry(FileId(path), path, "deferred", "NIST audit corpus", null, null, null, null))
            .ToArray();

        var first = BuildLegacyAuditJson(entries);
        var second = BuildLegacyAuditJson(entries);
        Assert.Equal(first, second);

        var snapshotPath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), "testdata", "step242", "manifests", "nist.v0.report.json");
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

    private static string BuildLegacyAuditJson(IReadOnlyList<Step242CorpusManifestEntry> entries)
    {
        var report = entries.Select(Step242CorpusManifestRunner.RunOne).Select(r => new LegacyAuditEntry(
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
            CanonicalSha256: r.CanonicalSha256)).ToArray();

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        return Step242CorpusManifestRunner.NormalizeLf(json) + "\n";
    }

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
