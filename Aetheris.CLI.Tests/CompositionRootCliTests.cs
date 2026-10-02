using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class CompositionRootCliTests
{
    [Fact]
    public void RootInspectReportsColdWarmDefinitionReuseAndBuildExportsAssembly()
    {
        var root = RepoRoot();
        var source = Path.Combine(root, "fixtures", "Canonical", "AssemblyInterfaces", "AuthoringFoundation", "linear-stations.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["inspect", source, "--json", "--profile", "--repeat", "2"], stdout, stderr));
        Assert.Empty(stderr.ToString());
        using var json = JsonDocument.Parse(stdout.ToString());
        var builds = json.RootElement.GetProperty("builds");
        Assert.Equal(2, builds.GetArrayLength());
        Assert.Equal(2, builds[0].GetProperty("reuse").GetProperty("rebuiltDefinitions").GetInt32());
        Assert.Equal(2, builds[1].GetProperty("reuse").GetProperty("reusedDefinitions").GetInt32());
        Assert.Equal(0, builds[1].GetProperty("reuse").GetProperty("rebuiltDefinitions").GetInt32());
        var output = Path.Combine(root, "artifacts", "local", "owl", "cli-tests", Guid.NewGuid() + ".step");
        stdout.GetStringBuilder().Clear();
        Assert.Equal(0, CliRunner.Run(["build", source, "--output", output, "--json"], stdout, stderr));
        Assert.Contains("ISO-10303-21", File.ReadAllText(output), StringComparison.Ordinal);
    }

    [Fact]
    public void RootInspectReportsGeneratedSectionKeys()
    {
        var path = Path.Combine(RepoRoot(), "fixtures", "Canonical", "AssemblyInterfaces", "GuitarX0", "Neck.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["inspect", path, "--json"], stdout, stderr));
        using var json = JsonDocument.Parse(stdout.ToString());
        var pattern = json.RootElement.GetProperty("patterns")[0];
        Assert.Equal(8, pattern.GetProperty("generatedCount").GetInt32());
        Assert.Equal("Nut", pattern.GetProperty("associations")[7].GetProperty("sourceEntry").GetString());
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
