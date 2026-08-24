using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class CircularSweepCliTests
{
    [Fact]
    public void PaperclipValidateAndInspectUseWireFormSemanticRoute()
    {
        var fixture = Path.Combine(RepoRoot(), "fixtures", "Canonical", "Templates", "paperclip.firmament");
        var validation = new StringWriter(); var validationError = new StringWriter();
        Assert.Equal(0, Aetheris.CLI.CliRunner.Run(["validate", fixture, "--json"], validation, validationError));
        Assert.Empty(validationError.ToString());
        using (var json = JsonDocument.Parse(validation.ToString()))
        {
            var report = json.RootElement.GetProperty("firmamentV2Validation");
            Assert.Equal("valid", report.GetProperty("status").GetString());
            Assert.Equal("WireForm", report.GetProperty("domain").GetString());
            Assert.Equal(7, report.GetProperty("summary").GetProperty("operationCount").GetInt32());
        }
        var inspection = new StringWriter(); var inspectionError = new StringWriter();
        Assert.Equal(0, Aetheris.CLI.CliRunner.Run(["inspect", fixture, "--json"], inspection, inspectionError));
        Assert.Empty(inspectionError.ToString());
        using var inspected = JsonDocument.Parse(inspection.ToString());
        Assert.True(inspected.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("WireForm", inspected.RootElement.GetProperty("domain").GetString());
        var wire = inspected.RootElement.GetProperty("wireForm");
        Assert.Equal("Standard.Materials.StainlessSteel.304_Annealed", wire.GetProperty("material").GetString());
        Assert.Equal(95.6991118431, wire.GetProperty("totalWireLength").GetDouble(), 9);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
