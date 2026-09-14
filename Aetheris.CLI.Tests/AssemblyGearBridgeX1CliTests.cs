using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class AssemblyGearBridgeX1CliTests
{
    [Fact]
    public void AssemblyInspectReportsTypedGearAuthorityAndDerivedCompatibility()
    {
        var path = Path.Combine(RepoRoot(), "fixtures", "Canonical", "AssemblyInterfaces", "exposed-gear-port.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = CliRunner.Run(["asm", "inspect", path], stdout, stderr);
        Assert.Equal(0, exit);
        Assert.Empty(stderr.ToString());
        var text = stdout.ToString();
        Assert.Contains("Interface<Gear>", text, StringComparison.Ordinal);
        Assert.Contains("port=Register/Digit0/Output", text, StringComparison.Ordinal);
        Assert.Contains("source=Register/Digit0/OutputGear", text, StringComparison.Ordinal);
        Assert.Contains("ratio=2", text, StringComparison.Ordinal);
        Assert.Contains("center expected=60mm actual=60mm", text, StringComparison.Ordinal);

        stdout.GetStringBuilder().Clear();
        exit = CliRunner.Run(["asm", "inspect", path, "--json"], stdout, stderr);
        Assert.Equal(0, exit);
        using var json = JsonDocument.Parse(stdout.ToString());
        var transfer = json.RootElement.GetProperty("assemblyIr").GetProperty("mates").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "Transfer");
        Assert.Equal("compatible", transfer.GetProperty("gearResult").GetProperty("compatibilityStatus").GetString());
        Assert.Equal(4.5d, transfer.GetProperty("gearResult").GetProperty("a").GetProperty("phaseDegrees").GetDouble());
    }

    [Fact]
    public void AssemblyInspectReturnsExistingGearFailureForHierarchicalMismatch()
    {
        var path = Path.Combine(RepoRoot(), "fixtures", "Invalid", "AssemblyInterfaces", "gear-module-mismatch.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = CliRunner.Run(["asm", "inspect", path], stdout, stderr);
        Assert.Equal(1, exit);
        Assert.Contains("firmament-gear-interface-incompatible", stderr.ToString(), StringComparison.Ordinal);
        Assert.Contains("module-mismatch", stderr.ToString(), StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
