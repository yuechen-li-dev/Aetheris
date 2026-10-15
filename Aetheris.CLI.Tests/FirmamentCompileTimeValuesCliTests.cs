using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentCompileTimeValuesCliTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));

    [Fact]
    public void NormalValidationBuildsTheRecordTemplateFeatureWitnessAndReportsAuthoredOrigins()
    {
        var path = Path.Combine(Root, "fixtures/Canonical/PMI/record-template-feature-tolerance.firmament");
        var output = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["validate", path, "--json", "--explain"], output, new StringWriter()));
        using var json = JsonDocument.Parse(output.ToString());
        var report = json.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("Built", report.GetProperty("validationLevel").GetString());
        Assert.Equal("UnifiedMechanical", report.GetProperty("parserRoute").GetString());
        var pmi = Assert.Single(report.GetProperty("pmi").EnumerateArray());
        Assert.Equal("standardBore.diameter", pmi.GetProperty("projectionSource").GetString());
        Assert.Contains(pmi.GetProperty("dimensionOrigins").EnumerateArray(), o => o.GetProperty("sourceFile").GetString() == path
            && o.GetProperty("binding").GetString() == "standardBore.diameter");
    }

    [Theory]
    [InlineData("validate")]
    [InlineData("build")]
    [InlineData("parse-only")]
    public void InvalidRecordRequirementRejectsEveryAdmissionCommandAtItsOriginalLine(string command)
    {
        var path = Path.Combine(Root, "fixtures/Regression/CompileTimeValues/record-unit-mismatch.firmament");
        var destination = Path.Combine(Root, "artifacts/local/firmament-value-binding", Guid.NewGuid() + ".step");
        var output = new StringWriter();
        string[] args = command == "build" ? ["build", path, "--output", destination, "--json"]
            : ["validate", path, "--json", .. (command == "parse-only" ? new[] { "--parse-only" } : [])];
        Assert.Equal(1, CliRunner.Run(args, output, new StringWriter()));
        using var json = JsonDocument.Parse(output.ToString());
        var report = command == "build" ? json.RootElement : json.RootElement.GetProperty("firmamentV2Validation");
        var diagnostic = Assert.Single(report.GetProperty("diagnostics").EnumerateArray(),
            d => d.GetProperty("code").GetString() == "firmament-v2-tolerance-unit-mismatch");
        Assert.Equal(path, diagnostic.GetProperty("sourceFile").GetString());
        Assert.Equal(2, diagnostic.GetProperty("line").GetInt32());
        Assert.Contains("mm", diagnostic.GetProperty("message").GetString());
        Assert.False(File.Exists(destination));
    }
}
