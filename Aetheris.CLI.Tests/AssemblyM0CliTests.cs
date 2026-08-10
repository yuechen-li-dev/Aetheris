using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class AssemblyM0CliTests
{
    [Fact]
    public void AsmInspect_EmitsMachineReadableAssemblyIr()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "fixtures", "AssemblyM0", "bearing-module.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["asm", "inspect", path, "--json"], stdout, stderr);
        Assert.Equal(0, exit);
        using var json = JsonDocument.Parse(stdout.ToString());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        var ir = json.RootElement.GetProperty("assemblyIr");
        Assert.Equal("aetheris/assembly-ir/m0", ir.GetProperty("schema").GetString());
        Assert.Equal(2, ir.GetProperty("mates").GetArrayLength());
        Assert.Equal("passed", ir.GetProperty("toleranceStackups")[0].GetProperty("status").GetString());
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void AsmInspect_FailingAssertReturnsFailureJsonWithChain()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "fixtures", "AssemblyM0", "bearing-module-failing.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["asm", "inspect", path, "--json"], stdout, stderr);
        Assert.Equal(1, exit);
        using var json = JsonDocument.Parse(stdout.ToString());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(5, json.RootElement.GetProperty("assemblyIr").GetProperty("toleranceStackups")[0].GetProperty("contributions").GetArrayLength());
        Assert.Contains(json.RootElement.GetProperty("diagnostics").EnumerateArray(), x => x.GetProperty("code").GetString() == "assembly-tolerance-assertion-failure");
    }

    [Fact]
    public void AsmInspect_TemplateAssembly_EmitsExecutableGeometryResidualsAndDefinitionReuseSeam()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "fixtures", "AssemblyM1", "template-block-pair.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["asm", "inspect", path, "--json"], stdout, stderr);
        Assert.Equal(0, exit);
        using var json = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("aetheris/assembly-ir/m1", json.RootElement.GetProperty("assemblyIr").GetProperty("schema").GetString());
        var geometry = json.RootElement.GetProperty("geometryArtifact");
        Assert.Equal("aetheris/assembly-geometry/m1", geometry.GetProperty("schema").GetString());
        Assert.Equal(2, geometry.GetProperty("definitions").GetArrayLength());
        Assert.Equal(2, geometry.GetProperty("instances").GetArrayLength());
        Assert.All(geometry.GetProperty("mateResiduals").EnumerateArray(), residual => Assert.True(residual.GetProperty("passed").GetBoolean()));
        Assert.Empty(stderr.ToString());
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        { if (File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) return directory.FullName; directory = directory.Parent; }
        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
