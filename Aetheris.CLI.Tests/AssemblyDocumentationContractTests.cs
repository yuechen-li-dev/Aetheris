namespace Aetheris.CLI.Tests;

public sealed class AssemblyDocumentationContractTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Assembly_Documentation_Exists_And_Contains_Frozen_Sections()
    {
        var path = Path.Combine(RepoRoot, "docs", "assembly.md");
        Assert.True(File.Exists(path), $"Expected assembly doc at '{path}'.");

        var text = File.ReadAllText(path);
        Assert.Contains("## 1) Core philosophy", text, StringComparison.Ordinal);
        Assert.Contains("## 2) STEP classification rule (ASM-A2.75)", text, StringComparison.Ordinal);
        Assert.Contains("## 3) Assembly pipeline (full flow)", text, StringComparison.Ordinal);
        Assert.Contains("## 4) `.firmasm` contract (ASM-A0)", text, StringComparison.Ordinal);
        Assert.Contains("## 5) Execution model (ASM-A3)", text, StringComparison.Ordinal);
        Assert.Contains("## 6) Roundtrip export model (ASM-A4)", text, StringComparison.Ordinal);
        Assert.Contains("## 7) CLI surface (source of truth)", text, StringComparison.Ordinal);
        Assert.Contains("## 8) Invariants (non-negotiable)", text, StringComparison.Ordinal);
        Assert.Contains("## 9) Non-goals (v0)", text, StringComparison.Ordinal);
        Assert.Contains("## 10) Known limitations", text, StringComparison.Ordinal);
        Assert.Contains("## 11) Future directions (bounded)", text, StringComparison.Ordinal);
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

        throw new InvalidOperationException("Unable to locate repository root for CLI tests.");
    }
}
