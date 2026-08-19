using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class DrawingCliTests
{
    [Fact]
    public void DrawingCompile_ReportsRealArtifactsAndCollisionEvidence()
    {
        var output = Path.Combine(Path.GetTempPath(), $"aetheris-drawing-cli-{Guid.NewGuid():N}");
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var exit = CliRunner.Run(["drawing", "compile", Fixture(), "--out-dir", output, "--json"], stdout, stderr);

            Assert.Equal(0, exit);
            using var json = JsonDocument.Parse(stdout.ToString());
            Assert.True(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(2, json.RootElement.GetProperty("pageCount").GetInt32());
            Assert.Equal(0, json.RootElement.GetProperty("layout").GetProperty("textModelCollisionsAfter").GetInt32());
            Assert.True(File.Exists(json.RootElement.GetProperty("pdfPath").GetString()));
            Assert.True(File.Exists(json.RootElement.GetProperty("drawingIrPath").GetString()));
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
    }

    [Fact]
    public void TopLevelHelp_AdvertisesDrawingCompiler()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["--help"], stdout, stderr));
        Assert.Contains("drawing", stdout.ToString(), StringComparison.Ordinal);
    }

    private static string Fixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root not found."), "fixtures", "Canonical", "Drawings", "bearing-block-production-drawing.firmament");
    }
}
