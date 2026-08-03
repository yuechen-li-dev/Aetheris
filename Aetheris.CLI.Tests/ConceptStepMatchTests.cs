using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class ConceptStepMatchTests
{
    [Fact]
    public void Match_StandardBracket_ReportsTypedAnalyticEvidence()
    {
        var root = RepositoryRoot();
        var step = BuildStandardStep(root);
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = CliRunner.Run(["match", step, Path.Combine(root, "demos", "concept-step-match-standard.firmament"), "--json"], stdout, stderr);
        Assert.Equal(0, exit);
        using var json = JsonDocument.Parse(stdout.ToString());
        var report = json.RootElement.GetProperty("conceptStepMatch");
        Assert.Equal("Matched", report.GetProperty("status").GetString());
        Assert.Equal(3, report.GetProperty("summary").GetProperty("matched").GetInt32());
        Assert.Equal("DerivedAnalytic", report.GetProperty("members")[1].GetProperty("evidenceQuality").GetString());
    }

    [Fact]
    public void Match_WrongBounds_IsConflicted_And_PartialConcept_RemainsUseful()
    {
        var root = RepositoryRoot(); var source = File.ReadAllText(Path.Combine(root, "demos", "concept-step-match-standard.firmament"));
        var dir = Path.Combine(Path.GetTempPath(), "aetheris-match", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var wrong = Path.Combine(dir, "wrong.firmament"); File.WriteAllText(wrong, source.Replace("Width: 80mm", "Width: 81mm", StringComparison.Ordinal));
        var step = BuildStandardStep(root);
        Assert.Equal(1, CliRunner.Run(["match", step, wrong, "--json"], new StringWriter(), new StringWriter()));
        var partial = Path.Combine(dir, "partial.firmament");
        // The full point-set declaration is retained for parser validity; omitting its role produces nonfatal evidence.
        File.WriteAllText(partial, source.Replace("Match {\n    MountPoints As HoleCenters {\n        Diameter: 8.5mm\n        Axis: +Z\n        Kind: Through\n    }\n}\n", string.Empty, StringComparison.Ordinal));
        Assert.Equal(0, CliRunner.Run(["match", step, partial, "--json"], new StringWriter(), new StringWriter()));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return Assert.IsType<DirectoryInfo>(directory).FullName;
    }

    private static string BuildStandardStep(string root)
    {
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-match-step", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var step = Path.Combine(directory, "standard.step");
        var output = new StringWriter(); var errors = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["build", Path.Combine(root, "demos", "concept-step-match-standard.firmament"), "--out", step, "--json"], output, errors));
        return step;
    }
}
