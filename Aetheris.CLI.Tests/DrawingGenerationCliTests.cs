using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class DrawingGenerationCliTests
{
    [Fact]
    public void GenerateProducesPdfSvgPptxAndHonestLayoutEvidence()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        var output = Path.Combine(root!.FullName, "artifacts/local/tests/drawing-cli", Guid.NewGuid().ToString("N"));
        try
        {
            using var stdout = new StringWriter(); using var stderr = new StringWriter();
            var source = Path.Combine(root.FullName, "fixtures/Regression/DrawingGeneration/inspection-plate.firmament");
            Assert.Equal(0, CliRunner.Run(["drawing", "generate", source, "--out-dir", output, "--drawn-by", "Codex (AI)", "--date", "2026-10-18", "--linear-tolerance", "0.1", "--angular-tolerance", "0.5", "--material", "6061-T6 aluminium", "--json"], stdout, stderr));
            using var json = JsonDocument.Parse(stdout.ToString()); var result = json.RootElement;
            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.True(result.GetProperty("definition").GetProperty("nominalGeometryComplete").GetBoolean());
            Assert.True(result.GetProperty("definition").GetProperty("dimensionalTolerancesComplete").GetBoolean());
            Assert.False(result.GetProperty("definition").GetProperty("processCapabilityVerified").GetBoolean());
            foreach (var key in new[] { "pdfPath", "svgPath", "pptxPath", "drawingIrPath", "validationPath" }) Assert.True(File.Exists(result.GetProperty(key).GetString()));
            Assert.Equal(0, result.GetProperty("layout").GetProperty("failedAnnotationCount").GetInt32());
            using var ir = JsonDocument.Parse(File.ReadAllText(result.GetProperty("drawingIrPath").GetString()!));
            var roles = ir.RootElement.GetProperty("metadata").GetProperty("signoff");
            Assert.Equal("Codex (AI)", roles.GetProperty("drawn").GetProperty("name").GetString());
            Assert.Equal(JsonValueKind.Null, roles.GetProperty("approved").GetProperty("name").ValueKind);
            Assert.Equal("", stderr.ToString());
        }
        finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    [Fact]
    public void DrawingHelpAdvertisesGenerationAlongsideAuthoredCompilation()
    {
        using var stdout = new StringWriter(); using var stderr = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["drawing", "--help"], stdout, stderr));
        Assert.Contains("generate", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("compile", stdout.ToString(), StringComparison.Ordinal);
    }
}
