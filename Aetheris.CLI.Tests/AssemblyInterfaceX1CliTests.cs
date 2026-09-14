using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class AssemblyInterfaceX1CliTests
{
    [Fact]
    public void InspectReportsHierarchyExpansionDefinitionsAndDependencyManifest()
    {
        var path = Path.Combine(RepoRoot(), "fixtures", "Canonical", "AssemblyInterfaces", "machine.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = CliRunner.Run(["asm", "inspect", path], stdout, stderr);
        Assert.Equal(0, exit);
        Assert.Empty(stderr.ToString());
        var text = stdout.ToString();
        Assert.Contains("DifferenceEngineReadiness", text, StringComparison.Ordinal);
        Assert.Contains("Interface<Axial>", text, StringComparison.Ordinal);
        Assert.Contains("Expanded Mate AxisCoincident", text, StringComparison.Ordinal);
        Assert.Contains("Shared definitions:", text, StringComparison.Ordinal);
        Assert.Contains("DigitModule: occurrences=4", text, StringComparison.Ordinal);
        Assert.Contains("Source dependencies:", text, StringComparison.Ordinal);

        stdout.GetStringBuilder().Clear();
        exit = CliRunner.Run(["asm", "inspect", path, "--json"], stdout, stderr);
        Assert.Equal(0, exit);
        using var json = JsonDocument.Parse(stdout.ToString());
        var ir = json.RootElement.GetProperty("assemblyIr");
        Assert.Equal(3, ir.GetProperty("sourceDependencies").GetArrayLength());
        Assert.Equal(15, ir.GetProperty("instances").GetArrayLength());
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
