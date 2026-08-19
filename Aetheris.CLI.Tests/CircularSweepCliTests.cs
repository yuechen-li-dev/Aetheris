using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class CircularSweepCliTests
{
    [Fact]
    public void PaperclipValidateAndInspectUseSweepSemanticRoute()
    {
        var fixture = Path.Combine(RepoRoot(), "fixtures", "Canonical", "Templates", "paperclip.firmament");
        var validation = new StringWriter(); var validationError = new StringWriter();
        Assert.Equal(0, Aetheris.CLI.CliRunner.Run(["validate", fixture, "--json"], validation, validationError));
        Assert.Empty(validationError.ToString());
        using (var json = JsonDocument.Parse(validation.ToString()))
        {
            var report = json.RootElement.GetProperty("firmamentV2Validation");
            Assert.Equal("valid", report.GetProperty("status").GetString());
            Assert.Equal("CircularSweep", report.GetProperty("domain").GetString());
            Assert.Equal(7, report.GetProperty("summary").GetProperty("segmentCount").GetInt32());
        }
        var inspection = new StringWriter(); var inspectionError = new StringWriter();
        Assert.Equal(0, Aetheris.CLI.CliRunner.Run(["inspect", fixture, "--json"], inspection, inspectionError));
        Assert.Empty(inspectionError.ToString());
        using var inspected = JsonDocument.Parse(inspection.ToString());
        Assert.True(inspected.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Standard.Materials.StainlessSteel.304_Annealed", inspected.RootElement.GetProperty("sweep").GetProperty("material").GetString());
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
