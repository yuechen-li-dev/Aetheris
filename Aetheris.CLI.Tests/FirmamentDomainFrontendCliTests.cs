using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentDomainFrontendCliTests
{
    [Theory]
    [InlineData("Canonical/Gears/spur-basic.firmament", "UnifiedGear")]
    [InlineData("Canonical/Revolve/partial-quarter.firmament", "UnifiedRevolve")]
    [InlineData("Canonical/SectionChain/two-section-ruled.firmament", "UnifiedSectionChain")]
    public void NormalValidationReportsActualAdmissionAndStructuredDiagnostics(string relative, string route)
    {
        var output = new StringWriter(); var error = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["validate", Fixture(relative), "--explain", "--json"], output, error));
        Assert.Empty(error.ToString());
        using var json = JsonDocument.Parse(output.ToString());
        var report = json.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("valid", report.GetProperty("status").GetString());
        Assert.Equal("Built", report.GetProperty("validationLevel").GetString());
        Assert.Equal(route, report.GetProperty("parserRoute").GetString());
        Assert.All(report.GetProperty("diagnostics").EnumerateArray(), item => Assert.Equal(JsonValueKind.Object, item.ValueKind));
    }

    [Fact]
    public void GearTrainCannotClaimNormalSingleBodyBuildAdmission()
    {
        var output = new StringWriter();
        Assert.Equal(1, CliRunner.Run(["validate", Fixture("Canonical/GearInterfaces/spur-pair.firmament"), "--json"], output, new StringWriter()));
        using var json = JsonDocument.Parse(output.ToString());
        var report = json.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("invalid", report.GetProperty("status").GetString());
        Assert.Equal("BuildRejected", report.GetProperty("validationLevel").GetString());
        Assert.Contains(report.GetProperty("diagnostics").EnumerateArray(), item =>
            item.GetProperty("code").GetString() == "firmament-gear-build-body-count-invalid"
            && item.GetProperty("message").GetString()!.Contains("use-Assembly", StringComparison.Ordinal));
    }

    private static string Fixture(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException(), "fixtures", relative);
    }
}
