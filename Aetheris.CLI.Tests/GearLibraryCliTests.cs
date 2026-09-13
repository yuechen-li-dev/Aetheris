using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class GearLibraryCliTests
{
    [Fact]
    public void EveryInvalidGearFixtureIsRejectedByTheRealCli()
    {
        var directory = Path.Combine(RepoRoot(), "fixtures", "Invalid", "Gears");
        foreach (var fixture in Directory.GetFiles(directory, "*.firmament").Order(StringComparer.Ordinal))
        {
            var output = new StringWriter(); var error = new StringWriter();
            Assert.Equal(1, CliRunner.Run(["validate", fixture, "--json"], output, error));
            Assert.Contains("firmament-gear-", output.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BuildInspectAndValidateExposeGearEvidence()
    {
        var fixture = Path.Combine(RepoRoot(), "fixtures", "Canonical", "Gears", "spur-basic.firmament");
        var step = Path.Combine(Path.GetTempPath(), $"aetheris-gear-{Guid.NewGuid():N}.step");
        try
        {
            var build = new StringWriter(); var buildError = new StringWriter();
            Assert.Equal(0, CliRunner.Run(["build", fixture, "--out", step, "--json"], build, buildError)); Assert.Empty(buildError.ToString());
            using (var json = JsonDocument.Parse(build.ToString()))
            {
                var report = json.RootElement.GetProperty("gear"); Assert.True(report.GetProperty("stepReimportedManifold").GetBoolean());
                Assert.Equal("SpurGear", report.GetProperty("gears")[0].GetProperty("family").GetString());
            }
            var inspect = new StringWriter(); Assert.Equal(0, CliRunner.Run(["inspect", fixture, "--json"], inspect, new StringWriter()));
            using (var json = JsonDocument.Parse(inspect.ToString())) Assert.Equal("GearLibrary", json.RootElement.GetProperty("domain").GetString());
            var validate = new StringWriter(); Assert.Equal(0, CliRunner.Run(["validate", fixture, "--json"], validate, new StringWriter()));
            using var validation = JsonDocument.Parse(validate.ToString()); Assert.Equal("valid", validation.RootElement.GetProperty("firmamentV2Validation").GetProperty("status").GetString());
        }
        finally { if (File.Exists(step)) File.Delete(step); }
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
