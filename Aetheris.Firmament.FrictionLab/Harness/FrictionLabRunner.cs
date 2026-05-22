using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Firmament;

namespace Aetheris.Firmament.FrictionLab.Harness;

internal sealed class FrictionLabRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public FrictionLabSummary Run(IReadOnlyList<string>? caseIds = null, string summaryFileName = "summary.json")
    {
        var paths = FrictionLabPaths.Resolve();
        Directory.CreateDirectory(paths.ReportsDirectory);
        Directory.CreateDirectory(paths.ArtifactsDirectory);

        var caseResults = DiscoverCaseDirectories(paths.CasesDirectory, caseIds)
            .Select(caseDirectory => RunCase(paths, caseDirectory))
            .ToArray();

        var summaryPath = Path.Combine(paths.ReportsDirectory, summaryFileName);
        var summary = FrictionLabSummary.FromResults(caseResults, ToRepoRelativePath(paths.RepoRoot, summaryPath));
        File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, JsonOptions), new UTF8Encoding(false));

        return summary;
    }

    private static IEnumerable<string> DiscoverCaseDirectories(string casesDirectory, IReadOnlyList<string>? caseIds)
    {
        if (!Directory.Exists(casesDirectory))
        {
            return [];
        }

        if (caseIds is { Count: > 0 })
        {
            return caseIds.Select(caseId => Path.Combine(casesDirectory, caseId));
        }

        return Directory.GetDirectories(casesDirectory)
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static FrictionCaseResult RunCase(FrictionLabPaths paths, string caseDirectory)
    {
        var caseId = Path.GetFileName(caseDirectory);
        var review = ReadReview(caseDirectory, caseId);

        var sourcePath = Path.Combine(caseDirectory, "part.firmament");
        if (!File.Exists(sourcePath))
        {
            return new FrictionCaseResult(
                caseId,
                ComputeBuildStatus(false, review),
                false,
                null,
                review.Possible,
                review.Awkwardness,
                [new FrictionDiagnostic("ValidationFailed", "Error", "Case is missing required file 'part.firmament'.", "friction-lab")]);
        }

        var sourceText = NormalizeLf(File.ReadAllText(sourcePath, Encoding.UTF8));
        var exportResult = FirmamentStepExporter.Export(new FirmamentCompileRequest(new FirmamentSourceDocument(sourceText)));

        var artifactPath = Path.Combine(paths.ArtifactsDirectory, caseId + ".step");
        if (File.Exists(artifactPath))
        {
            File.Delete(artifactPath);
        }

        if (!exportResult.IsSuccess)
        {
            return new FrictionCaseResult(
                caseId,
                ComputeBuildStatus(false, review),
                false,
                null,
                review.Possible,
                review.Awkwardness,
                exportResult.Diagnostics.Select(MapDiagnostic).ToArray());
        }

        File.WriteAllText(artifactPath, exportResult.Value.StepText, new UTF8Encoding(false));

        return new FrictionCaseResult(
            caseId,
            ComputeBuildStatus(true, review),
            true,
            ToRepoRelativePath(paths.RepoRoot, artifactPath),
            review.Possible,
            review.Awkwardness,
            exportResult.Diagnostics.Select(MapDiagnostic).ToArray());
    }

    private static FrictionCaseReview ReadReview(string caseDirectory, string caseId)
    {
        var reviewPath = Path.Combine(caseDirectory, "review.toon");
        if (!File.Exists(reviewPath))
        {
            return FrictionCaseReview.Missing(caseId);
        }

        var lines = NormalizeLf(File.ReadAllText(reviewPath, Encoding.UTF8)).Split('\n');
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var lists = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        string? currentList = null;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("-", StringComparison.Ordinal) && currentList is not null)
            {
                lists[currentList].Add(Unquote(trimmed[1..].Trim()));
                continue;
            }

            currentList = null;
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (key.Contains('[', StringComparison.Ordinal))
            {
                var listKey = key[..key.IndexOf('[', StringComparison.Ordinal)];
                lists[listKey] = [];
                currentList = listKey;
                continue;
            }

            values[key] = Unquote(value);
        }

        return new FrictionCaseReview(
            values.GetValueOrDefault("case_id", caseId),
            values.GetValueOrDefault("possible", "partial"),
            values.GetValueOrDefault("awkwardness", "high"),
            lists.GetValueOrDefault("pain_points", []),
            lists.GetValueOrDefault("proposed_features", []),
            values.GetValueOrDefault("reviewer_verdict", "No verdict provided."));
    }

    private static string ComputeBuildStatus(bool exportSuccess, FrictionCaseReview review)
    {
        if (!exportSuccess)
        {
            return review.Possible.Equals("partial", StringComparison.Ordinal) ? "partial" : "failure";
        }

        return review.Possible switch
        {
            "true" => "success",
            "partial" => "partial",
            _ => "failure"
        };
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1];
        }

        return value;
    }

    private static FrictionDiagnostic MapDiagnostic(KernelDiagnostic diagnostic) =>
        new(
            diagnostic.Code.ToString(),
            diagnostic.Severity.ToString(),
            diagnostic.Message,
            diagnostic.Source);

    private static string NormalizeLf(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);

    private static string ToRepoRelativePath(string repoRoot, string fullPath) =>
        Path.GetRelativePath(repoRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
}

internal sealed record FrictionLabPaths(string RepoRoot, string ProjectRoot, string CasesDirectory, string ReportsDirectory, string ArtifactsDirectory)
{
    public static FrictionLabPaths Resolve()
    {
        var repoRoot = FindRepoRoot();
        var projectRoot = Path.Combine(repoRoot, "Aetheris.Firmament.FrictionLab");

        return new FrictionLabPaths(
            repoRoot,
            projectRoot,
            Path.Combine(projectRoot, "Cases"),
            Path.Combine(projectRoot, "Reports"),
            Path.Combine(projectRoot, "Reports", "Artifacts"));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
