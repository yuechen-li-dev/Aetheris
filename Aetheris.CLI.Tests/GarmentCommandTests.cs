using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class GarmentCommandTests
{
    [Fact]
    public void RealSchemaRoutesThroughValidateInspectBuildAndLanguageFormatting()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        string input = Path.Combine(root!.FullName, "fixtures/Canonical/Garment/skirt.firmament");
        string output = Path.Combine(root.FullName, "artifacts/local/tests/garment", Guid.NewGuid().ToString("N"));
        foreach (string command in new[] { "validate", "inspect" })
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            Assert.True(CliRunner.Run([command, input, "--json"], stdout, stderr) == 0, stderr.ToString());
            using var report = JsonDocument.Parse(stdout.ToString());
            Assert.True(report.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("Garment", report.RootElement.GetProperty("domain").GetString());
        }
        var buildOutput = new StringWriter();
        var errors = new StringWriter();
        Assert.True(CliRunner.Run(["garment", "build", input, "--out-dir", output, "--json"], buildOutput, errors) == 0, errors.ToString());
        foreach (string file in new[] { "garment.json", "garment.obj", "garment.usda", "patterns.svg", "evidence.json" })
            Assert.True(new FileInfo(Path.Combine(output, file)).Length > 0, file);
        using var artifact = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "garment.json")));
        Assert.Equal("aetheris.garment.v1", artifact.RootElement.GetProperty("schema").GetString());
        Assert.NotEqual(0, CliRunner.Run(["build", input], new StringWriter(), errors));
        Assert.Contains("garment-step-export-unsupported", errors.ToString());
        Assert.NotEqual(0, CliRunner.Run(["garment", "drape", input], new StringWriter(), errors));
        Assert.Contains("garment-body-required", errors.ToString());
    }
}
