using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentConceptRequirementsCliTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));

    [Theory]
    [InlineData("requirements")]
    [InlineData("shape-prefix")]
    public void NormalValidationActuallyBuildsTheConceptWitness(string name)
    {
        var path = Path.Combine(Root, "fixtures/Canonical/Concepts", name + ".firmament");
        var output = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["validate", path, "--json", "--explain"], output, new StringWriter()));
        using var json = JsonDocument.Parse(output.ToString());
        var report = json.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("Built", report.GetProperty("validationLevel").GetString());
        Assert.Equal("UnifiedMechanical", report.GetProperty("parserRoute").GetString());
    }

    [Theory]
    [InlineData("validate")]
    [InlineData("build")]
    [InlineData("parse-only")]
    public void FalseBundleRejectsAllAdmissionCommandsWithItsLabeledFailure(string command)
    {
        var path = Path.Combine(Root, "fixtures/Regression/Concepts/failed-requirement.firmament");
        var destination = Path.Combine(Root, "artifacts/local/firmament-concepts", Guid.NewGuid() + ".step");
        var output = new StringWriter();
        string[] args = command == "build" ? ["build", path, "--output", destination, "--json"]
            : ["validate", path, "--json", .. (command == "parse-only" ? new[] { "--parse-only" } : [])];
        Assert.Equal(1, CliRunner.Run(args, output, new StringWriter()));
        using var json = JsonDocument.Parse(output.ToString());
        var report = command == "build" ? json.RootElement : json.RootElement.GetProperty("firmamentV2Validation");
        var diagnostic = Assert.Single(report.GetProperty("diagnostics").EnumerateArray(),
            d => d.GetProperty("code").GetString() == "firmament-concept-requirement-failed");
        Assert.Equal(path, diagnostic.GetProperty("sourceFile").GetString());
        Assert.Equal(9, diagnostic.GetProperty("line").GetInt32());
        Assert.Contains("PlateRequirements.aspect", diagnostic.GetProperty("message").GetString());
        Assert.False(File.Exists(destination));
    }
}
