using System.Text.Json;
namespace Aetheris.CLI.Tests;

public sealed class FirmamentV2RuntimeConceptValidationCliTests
{
    [Fact]
    public void ValidateJson_InvalidCountersinkFixture_ReturnsNonZeroAndRuntimeDiagnostic()
    {
        var fixturePath = FixturePath("Compatibility/LegacyAliases/Invalid/Language/concept-countersink-diameter-order.invalid.firmfixture");
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
        var fixturePath = FixturePath("Regression/Language/valid/concept-shaft-missing-tolerance-warning.valid.firmfixture");
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
    public void ValidateHelp_AdvertisesTrustedExternalForgePackLoading()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run(["validate", "--help"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Contains("--forge-pack <path>", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("does not sandbox external packs", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateJson_ConceptPmiObligationWarningFixture_ReturnsZeroAndContainsObligationRow()
    {
        var fixturePath = FixturePath("Regression/Language/valid/concept-pmi-obligation-missing-warning.valid.firmfixture");
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

    [Fact]
    public void ValidateJson_MissingForgePackPath_FailsClearly()
    {
        var fixturePath = FixturePath("Regression/Language/valid/concept-applications-forge.valid.firmfixture");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run(["validate", fixturePath, "--forge-pack", "does-not-exist.dll", "--json"], stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("Forge concept pack assembly was not found", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateJson_AssemblyWithoutPacks_FailsClearly()
    {
        var fixturePath = FixturePath("Regression/Language/valid/concept-applications-forge.valid.firmfixture");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run(["validate", fixturePath, "--forge-pack", typeof(CliRunner).Assembly.Location, "--json"], stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("contains no public IForgeConceptPack implementations", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateJson_ExternalPack_ValidatesExternalConceptAndReportsProvenance()
    {
        var fixturePath = FixturePath("Compatibility/LegacyAliases/Invalid/Language/concept-external-boss-hole.invalid.firmfixture");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run(["validate", fixturePath, "--forge-pack", typeof(Aetheris.TestForgePack.TestForgePack).Assembly.Location, "--json"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());

        using var document = JsonDocument.Parse(stdout.ToString());
        var validation = document.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("valid", validation.GetProperty("status").GetString());
        Assert.Contains(
            validation.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "testforge.boss-hole.seen"
                && diagnostic.GetProperty("severity").GetString() == "warning");
        var concept = Assert.Single(validation.GetProperty("concepts").EnumerateArray());
        Assert.Equal("Aetheris.TestForgePack", concept.GetProperty("runtimeValidation").GetProperty("provider").GetString());
        var forgeRuntime = validation.GetProperty("forgeRuntime");
        Assert.Equal("Aetheris.Standard", forgeRuntime.GetProperty("builtInPack").GetString());
        var externalPack = Assert.Single(forgeRuntime.GetProperty("externalPacks").EnumerateArray());
        Assert.Equal("Aetheris.TestForgePack", externalPack.GetProperty("id").GetString());
        Assert.Equal("Aetheris.TestForgePack.dll", externalPack.GetProperty("assembly").GetString());
    }

    [Fact]
    public void ValidateJson_DuplicateConceptPack_FailsClearly()
    {
        var fixturePath = FixturePath("Regression/Language/valid/concept-applications-forge.valid.firmfixture");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var duplicatePackPath = Path.Combine(AppContext.BaseDirectory, "Aetheris.TestForgePack.Duplicate.dll");

        var exitCode = CliRunner.Run(["validate", fixturePath, "--forge-pack", duplicatePackPath, "--json"], stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("duplicate concept 'hole<Countersink>'", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateJson_DefaultRuntime_RemainsBuiltInOnly()
    {
        var fixturePath = FixturePath("Regression/Language/valid/concept-applications-forge.valid.firmfixture");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run(["validate", fixturePath, "--json"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());

        using var document = JsonDocument.Parse(stdout.ToString());
        var forgeRuntime = document.RootElement.GetProperty("firmamentV2Validation").GetProperty("forgeRuntime");
        Assert.Equal("Aetheris.Standard", forgeRuntime.GetProperty("builtInPack").GetString());
        Assert.Empty(forgeRuntime.GetProperty("externalPacks").EnumerateArray());
    }

    private static string FixturePath(string relative) => Path.Combine(FindRepoRoot(), "fixtures", relative);

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
