using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class AssemblyGlbCommandTests
{
    [Fact]
    public void RealCliExportsAndRejectsBadOptionsWithoutOverwritingOutput()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var fixture = Path.Combine(root!.FullName, "fixtures/Canonical/AssemblyInterfaces/two-link-arm-usd.firmament");
        var output = Path.Combine(root.FullName, "artifacts/local/tests/glb", Guid.NewGuid().ToString("N") + ".glb");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var result = CliRunner.Run(["asm", "export-glb", fixture, output, "--state", "Shoulder=35", "--json"], stdout, stderr);
        Assert.True(result == 0, stderr.ToString());
        var valid = File.ReadAllBytes(output);
        using var report = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(valid.Length, report.RootElement.GetProperty("bytes").GetInt32());
        foreach (var extra in new[] { new[] { "--state", "Typo=1" }, new[] { "--state", "Shoulder=NaN" }, new[] { "--sample", "0:Shoulder=2" } })
        {
            result = CliRunner.Run(["asm", "export-glb", fixture, output, .. extra], stdout, stderr);
            Assert.NotEqual(0, result); Assert.Equal(valid, File.ReadAllBytes(output));
        }
    }
}
