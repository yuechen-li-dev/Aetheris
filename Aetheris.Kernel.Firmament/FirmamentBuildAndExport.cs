using System.Text;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Firmament;

public static class FirmamentBuildAndExport
{
    public static KernelResult<FirmamentBuildAndExportResult> Run(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullSourcePath = Path.GetFullPath(sourcePath);
        var sourceText = NormalizeLf(File.ReadAllText(fullSourcePath, Encoding.UTF8));
        var exportResult = FirmamentStepExporter.Export(new FirmamentCompileRequest(new FirmamentSourceDocument(sourceText)));
        if (!exportResult.IsSuccess)
        {
            return KernelResult<FirmamentBuildAndExportResult>.Failure(exportResult.Diagnostics);
        }

        var outputPath = ResolveDefaultOutputPath(fullSourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, exportResult.Value.StepText, new UTF8Encoding(false));

        return KernelResult<FirmamentBuildAndExportResult>.Success(
            new FirmamentBuildAndExportResult(
                fullSourcePath,
                outputPath,
                exportResult.Value));
    }

    private static string ResolveDefaultOutputPath(string fullSourcePath)
    {
        var root = FindRepositoryRoot(Path.GetDirectoryName(fullSourcePath)!);
        var sourceFileName = Path.GetFileNameWithoutExtension(fullSourcePath);
        return Path.Combine(root, "testdata", "firmament", "exports", sourceFileName + ".step");
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root for Firmament export output.");
    }

    private static string NormalizeLf(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
}
