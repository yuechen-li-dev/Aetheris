namespace Aetheris.CLI.Tests;

public sealed class AssemblyUsdCommandTests
{
    [Fact]
    public void RealCliExportsPosedSliderAndKeepsExistingOutputOnInvalidState()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var fixture = Path.Combine(root!.FullName, "fixtures/Canonical/AssemblyInterfaces/external-step-slider.firmament");
        var output = Path.Combine(root.FullName, "artifacts/local/tests/usd", Guid.NewGuid().ToString("N") + ".usda");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var result = CliRunner.Run(["asm", "export-usd", fixture, output, "--state", "Travel=24", "--json"], stdout, stderr);
        Assert.True(result == 0, stderr.ToString());
        var valid = File.ReadAllText(output);
        Assert.Contains("PhysicsPrismaticJoint", valid);
        Assert.Contains("aetheris:state = 24", valid);
        foreach (var extra in new[] { new[] { "--state", "Typo=1" }, new[] { "--sample", "0:Travel=NaN" }, new[] { "--evidence", output } })
        {
            result = CliRunner.Run(["asm", "export-usd", fixture, output, .. extra], stdout, stderr);
            Assert.NotEqual(0, result);
            Assert.Equal(valid, File.ReadAllText(output));
        }
    }
}
