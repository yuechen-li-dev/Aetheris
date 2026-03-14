using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Diagnostics;

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

    public static KernelDiagnostic CompileFirstDiagnostic(string fixtureText)
    {
        var compiler = new FirmamentCompiler();
        var request = new FirmamentCompileRequest(new FirmamentSourceDocument(fixtureText));
        var result = compiler.Compile(request);
        Assert.False(result.Compilation.IsSuccess);
        return Assert.Single(result.Compilation.Diagnostics);
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

    public string Code { get; init; } = string.Empty;
}
