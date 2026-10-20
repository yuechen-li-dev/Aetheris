using System.Text;
using System.Text.Json;

namespace Aetheris.Kernel.Firmament.Tests;

internal static class FirmamentCorpusHarness
{
    public static FirmamentCorpusManifest LoadManifest(string relativePath)
    {
        var fullPath = Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var json = File.ReadAllText(fullPath, Encoding.UTF8);
        var manifest = JsonSerializer.Deserialize<FirmamentCorpusManifest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (manifest is null)
        {
            throw new InvalidOperationException($"Unable to deserialize manifest: {relativePath}");
        }

        return manifest;
    }

    public static string ResolveFixtureFullPath(string fixturePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fixturePath);
        return Path.Combine(RepoRoot(), fixturePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public static string ReadFixtureText(string fixturePath)
    {
        var fullPath = ResolveFixtureFullPath(fixturePath);
        return NormalizeLf(File.ReadAllText(fullPath, Encoding.UTF8));
    }

    // Some historical mutation tests intentionally inject the PascalCase aliases.
    // Normalize their input explicitly; ordinary fixture reads and the CLI corpus
    // continue to exercise the current authored spelling without this adapter.
    public static string CanonicalMutationSource(string source) =>
        FirmamentV2.FirmamentSourceSpelling.Normalize(source);

    public static string ReplaceRequired(this string source, string before, string after,
        StringComparison comparison = StringComparison.Ordinal)
    {
        Assert.True(source.Contains(before, comparison), $"Mutation target was absent: {before}");
        Assert.NotEqual(before, after);
        return source.Replace(before, after, comparison);
    }

    public static string NormalizeLf(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);

    public static string RepoRoot()
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

    public static FirmamentCompileResult Compile(string fixtureText)
    {
        var compiler = new FirmamentCompiler();
        var request = new FirmamentCompileRequest(new FirmamentSourceDocument(fixtureText));
        return compiler.Compile(request);
    }

    // Retain geometry coverage while asserting provable topology, not a mesh-derived volume.
    public static FirmamentStepExportResult InspectFilletSpecimen(string fullPath)
    {
        var source = NormalizeLf(File.ReadAllText(fullPath));
        var accepted = FirmamentBuildAndExport.CompileSource(source, Path.GetDirectoryName(fullPath));
        Assert.True(accepted.IsSuccess, string.Join("; ", accepted.Diagnostics.Select(d => d.Message)));
        var inspected = FirmamentBuildAndExport.CompileForMeasurement(source, Path.GetDirectoryName(fullPath));
        Assert.True(inspected.IsSuccess, string.Join("; ", inspected.Diagnostics.Select(d => d.Message)));
        Assert.Equal(accepted.Value.StepText, inspected.Value.StepText);
        return accepted.Value;
    }

}

internal sealed class FirmamentCorpusManifest
{
    public string Version { get; init; } = string.Empty;

    public IReadOnlyList<FirmamentCorpusEntry> Entries { get; init; } = Array.Empty<FirmamentCorpusEntry>();
}

internal sealed class FirmamentCorpusEntry
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string FixturePath { get; init; } = string.Empty;

    public string ExpectedOutcome { get; init; } = string.Empty;

    public FirmamentExpectedDiagnostic? ExpectedDiagnostic { get; init; }
}

internal sealed class FirmamentExpectedDiagnostic
{
    public string Source { get; init; } = string.Empty;

    public string KernelCode { get; init; } = string.Empty;

    public string FirmamentCode { get; init; } = string.Empty;
}
