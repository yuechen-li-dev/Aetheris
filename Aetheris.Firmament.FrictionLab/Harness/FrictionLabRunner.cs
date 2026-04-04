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

    public FrictionLabSummary Run()
    {
        var paths = FrictionLabPaths.Resolve();
        Directory.CreateDirectory(paths.ReportsDirectory);
        Directory.CreateDirectory(paths.ArtifactsDirectory);

        var caseResults = DiscoverCaseDirectories(paths.CasesDirectory)
            .Select(caseDirectory => RunCase(paths, caseDirectory))
            .ToArray();

        var summaryPath = Path.Combine(paths.ReportsDirectory, "summary.json");
        var summary = FrictionLabSummary.FromResults(caseResults, ToRepoRelativePath(paths.RepoRoot, summaryPath));
        File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, JsonOptions), new UTF8Encoding(false));

        return summary;
    }

    private static IEnumerable<string> DiscoverCaseDirectories(string casesDirectory)
    {
        if (!Directory.Exists(casesDirectory))
        {
            return [];
        }

        return Directory.GetDirectories(casesDirectory)
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static FrictionCaseResult RunCase(FrictionLabPaths paths, string caseDirectory)
    {
        var caseId = Path.GetFileName(caseDirectory);
        var sourcePath = Path.Combine(caseDirectory, "part.firmament");
        if (!File.Exists(sourcePath))
        {
            return new FrictionCaseResult(
                caseId,
                "failure",
                null,
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
                "failure",
                null,
                exportResult.Diagnostics.Select(MapDiagnostic).ToArray());
        }

        File.WriteAllText(artifactPath, exportResult.Value.StepText, new UTF8Encoding(false));
        var status = exportResult.Diagnostics.Any(d => d.Severity != KernelDiagnosticSeverity.Info)
            ? "partial"
            : "success";

        return new FrictionCaseResult(
            caseId,
            status,
            ToRepoRelativePath(paths.RepoRoot, artifactPath),
            exportResult.Diagnostics.Select(MapDiagnostic).ToArray());
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
