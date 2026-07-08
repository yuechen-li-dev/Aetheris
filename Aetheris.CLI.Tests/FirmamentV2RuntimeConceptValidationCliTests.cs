using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentV2RuntimeConceptValidationCliTests
{
    [Fact]
    public void ValidateJson_InvalidCountersinkFixture_ReturnsNonZeroAndRuntimeDiagnostic()
    {
        var fixturePath = FixturePath("Language/invalid/concept-countersink-diameter-order.invalid.firmfixture");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run(["validate", fixturePath, "--json"], stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());

        using var document = JsonDocument.Parse(stdout.ToString());
        var validation = document.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("invalid", validation.GetProperty("status").GetString());
        Assert.Contains(
            validation.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "forge.hole.countersink.diameter-order"
                && diagnostic.GetProperty("severity").GetString() == "fatal");
    }

    [Fact]
    public void ValidateJson_WarningOnlyFixture_ReturnsZeroAndWarningDiagnostic()
    {
        var fixturePath = FixturePath("Language/valid/concept-shaft-missing-tolerance-warning.valid.firmfixture");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run(["validate", fixturePath, "--json"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());

        using var document = JsonDocument.Parse(stdout.ToString());
        var validation = document.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("valid", validation.GetProperty("status").GetString());
        Assert.Contains(
            validation.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "forge.hole.shaft.diameter-tolerance-recommended"
                && diagnostic.GetProperty("severity").GetString() == "warning");
    }

    [Fact]
    public void ValidateHelp_DoesNotAdvertiseExternalForgePackLoading()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run(["validate", "--help"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.DoesNotContain("--forge-pack", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateJson_ConceptPmiObligationWarningFixture_ReturnsZeroAndContainsObligationRow()
    {
        var fixturePath = FixturePath("Language/valid/concept-pmi-obligation-missing-warning.valid.firmfixture");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run(["validate", fixturePath, "--json"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());

        using var document = JsonDocument.Parse(stdout.ToString());
        var validation = document.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("valid", validation.GetProperty("status").GetString());
        Assert.True(validation.TryGetProperty("conceptPmiObligations", out var obligations));
        Assert.Contains(
            obligations.EnumerateArray(),
            obligation => obligation.GetProperty("kind").GetString() == "diameter"
                && obligation.GetProperty("sourceConcept").GetString() == "hole<Shaft>"
                && obligation.GetProperty("status").GetString() == "missing");
        Assert.Contains(
            validation.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "forge.pmi.obligation.missing"
                && diagnostic.GetProperty("severity").GetString() == "warning");
    }

    private static string FixturePath(string relative) => Path.Combine(FindRepoRoot(), "fixtures", "FirmamentV2", relative);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aetheris.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root.");
    }
}
