using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class CliHelpAndUsageTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Top_Level_Help_Flags_Return_Zero_And_List_Commands(string flag)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run([flag], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        var text = stdout.ToString();
        Assert.Contains("Usage: aetheris <build|analyze|canon|asm>", text, StringComparison.Ordinal);
        Assert.Contains("Commands:", text, StringComparison.Ordinal);
        Assert.Contains("build", text, StringComparison.Ordinal);
        Assert.Contains("analyze", text, StringComparison.Ordinal);
        Assert.Contains("canon", text, StringComparison.Ordinal);
        Assert.Contains("asm", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("build")]
    [InlineData("canon")]
    [InlineData("asm")]
    [InlineData("asm", "export")]
    [InlineData("analyze")]
    [InlineData("analyze", "map")]
    [InlineData("analyze", "section")]
    public void Subcommand_Help_Lists_Usage_And_Examples(params string[] prefix)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var args = prefix.Concat(["--help"]).ToArray();

        var exitCode = Aetheris.CLI.CliRunner.Run(args, stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        var text = stdout.ToString();
        Assert.Contains("Usage:", text, StringComparison.Ordinal);
        Assert.Contains("Example", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_Help_Explains_Default_Text_And_Json_Mode()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "--help"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        var text = stdout.ToString();
        Assert.Contains("default output is human-readable text", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_Map_Missing_Cols_Has_Clear_Diagnostic()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var stepPath = Path.Combine(RepoRoot, "testdata/firmament/exports/box_basic.step");

        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", "map", stepPath, "--top", "--rows", "6", "--json"],
            stdout,
            stderr);

        Assert.Equal(1, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stdout.ToString()));
        Assert.Contains("requires both --rows <N> and --cols <N>", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_Section_Missing_Offset_Has_Clear_Diagnostic()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var stepPath = Path.Combine(RepoRoot, "testdata/firmament/exports/box_basic.step");

        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "section", stepPath, "--xy", "--json"], stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stdout.ToString()));
        Assert.Contains("Analyze section requires --offset <value>", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_Map_And_Section_Still_Return_Structured_Json_Failure_On_Runtime_Error()
    {
        var missingPath = Path.Combine(RepoRoot, "testdata/firmament/exports/does-not-exist.step");

        var mapStdout = new StringWriter();
        var mapStderr = new StringWriter();
        var mapExitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", "map", missingPath, "--top", "--rows", "4", "--cols", "4", "--json"],
            mapStdout,
            mapStderr);

        Assert.Equal(1, mapExitCode);
        Assert.True(string.IsNullOrWhiteSpace(mapStderr.ToString()));
        using var mapDoc = JsonDocument.Parse(mapStdout.ToString());
        Assert.False(mapDoc.RootElement.GetProperty("success").GetBoolean());

        var sectionStdout = new StringWriter();
        var sectionStderr = new StringWriter();
        var sectionExitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", "section", missingPath, "--xy", "--offset", "1.0", "--json"],
            sectionStdout,
            sectionStderr);

        Assert.Equal(1, sectionExitCode);
        Assert.True(string.IsNullOrWhiteSpace(sectionStderr.ToString()));
        using var sectionDoc = JsonDocument.Parse(sectionStdout.ToString());
        Assert.False(sectionDoc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public void Asm_Export_Missing_Out_Has_Clear_Diagnostic()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var manifestPath = Path.Combine(RepoRoot, "testdata/firmasm/examples/occt-nut-bolt/nut-bolt-assembly.firmasm");

        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["asm", "export", manifestPath, "--json"],
            stdout,
            stderr);

        Assert.Equal(1, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stdout.ToString()));
        Assert.Contains("Asm export requires --out <directory>", stderr.ToString(), StringComparison.Ordinal);
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
