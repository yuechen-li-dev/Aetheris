using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class AssemblyMotionCommandTests
{
    [Theory]
    [InlineData("articulated-motion.firmament")]
    [InlineData("articulated-module-motion.firmament")]
    public void NormalValidateAndBuildAdmitTheSameClipAssembly(string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        var fixture = Path.Combine(root!.FullName, "fixtures/Canonical/AssemblyInterfaces", name);
        var output = Path.Combine(root.FullName, "artifacts/local/tests/motion", Guid.NewGuid().ToString("N") + ".step");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["validate", fixture, "--json"], stdout, stderr));
        Assert.Equal(0, CliRunner.Run(["build", fixture, "--output", output, "--json"], stdout, stderr));
        Assert.True(File.Exists(output));
    }

    [Fact]
    public void CliSamplesAdmittedClipAndRejectsMissingMotion()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        var fixture = Path.Combine(root!.FullName, "fixtures/Canonical/AssemblyInterfaces/articulated-motion.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["asm", "motion", fixture, "--clip", "Cycle", "--time", "1.25", "--json"], stdout, stderr));
        using var response = JsonDocument.Parse(stdout.ToString());
        Assert.True(response.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(450, response.RootElement.GetProperty("state").GetProperty("Shoulder").GetDouble());
        Assert.Equal(9, response.RootElement.GetProperty("state").GetProperty("Slide").GetDouble());
        stdout.GetStringBuilder().Clear();
        Assert.Equal(1, CliRunner.Run(["asm", "motion", fixture, "--clip", "Missing", "--time", "0", "--json"], stdout, stderr));
        using var invalid = JsonDocument.Parse(stdout.ToString());
        Assert.False(invalid.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("assembly-motion-invalid-query", invalid.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }
}
