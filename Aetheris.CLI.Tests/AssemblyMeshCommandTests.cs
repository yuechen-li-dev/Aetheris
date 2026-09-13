using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class AssemblyMeshCommandTests
{
    [Fact]
    public void AssemblyMeshPreservesSharedDefinitionsAndFailsClosed()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var output = Path.Combine(root!.FullName, "artifacts/local/tests/assembly-mesh", Guid.NewGuid().ToString("N") + ".json");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var fixture = Path.Combine(root.FullName, "fixtures/Canonical/Assembly/annular-pair.firmament");
        var exit = CliRunner.Run(["mesh", fixture, "--format", "assembly-json", "--output", output, "--json"], stdout, stderr);
        Assert.True(exit == 0, stderr.ToString() + stdout);
        using var document = JsonDocument.Parse(File.ReadAllText(output));
        Assert.Equal("aetheris/assembly-display-mesh/1", document.RootElement.GetProperty("schema").GetString());
        Assert.Single(document.RootElement.GetProperty("definitions").EnumerateArray());
        Assert.Equal(3, document.RootElement.GetProperty("occurrences").GetArrayLength());
        var valid = File.ReadAllText(output);
        stdout.GetStringBuilder().Clear(); stderr.GetStringBuilder().Clear();
        exit = CliRunner.Run(["mesh", fixture + ".missing", "--format", "assembly-json", "--output", output, "--json"], stdout, stderr);
        Assert.NotEqual(0, exit);
        Assert.Equal(valid, File.ReadAllText(output));
    }
}
