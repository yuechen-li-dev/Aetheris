using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class DrawingNotesCliTests
{
    [Fact]
    public void InspectAndNotes_NeedNoRendererAndMissingRasterizerFailsClearly()
    {
        using var temp = new TempDirectory();
        var inspect = Run(["drawing", "inspect", Fixture(), "--json"]);
        Assert.Equal(0, inspect.Exit); Assert.Equal(string.Empty, inspect.Error);
        using (var json = JsonDocument.Parse(inspect.Output)) Assert.Equal(1, json.RootElement.GetProperty("document").GetProperty("pageCount").GetInt32());
        var page = Path.Combine(temp.Path, "page.png"); var crop = Path.Combine(temp.Path, "detail.png");
        var render = Run(["drawing", "render", Fixture(), "--page", "1", "--out", page, "--dpi", "96", "--json"]);
        Assert.NotEqual(0, render.Exit); Assert.Contains("drawing-renderer-required", render.Error + render.Output);
        using (var json = JsonDocument.Parse(render.Output))
        {
            Assert.False(json.RootElement.GetProperty("success").GetBoolean());
            var diagnostic = json.RootElement.GetProperty("diagnostics")[0];
            Assert.Equal("drawing-renderer-required", diagnostic.GetProperty("code").GetString());
            Assert.Equal("error", diagnostic.GetProperty("severity").GetString());
            Assert.Contains("--renderer", diagnostic.GetProperty("message").GetString());
        }
        var cropped = Run(["drawing", "crop", Fixture(), "--page", "1", "--bounds", "465,60,180,200", "--name", "DetailD", "--out", crop, "--dpi", "96", "--json"]);
        Assert.NotEqual(0, cropped.Exit); Assert.Contains("drawing-renderer-required", cropped.Error + cropped.Output);
        Assert.False(File.Exists(page)); Assert.False(File.Exists(crop)); Assert.False(File.Exists(Path.ChangeExtension(crop, ".crop.json")));
    }

    [Theory]
    [InlineData("inspect-3dm")]
    [InlineData("recover-3dm")]
    public void CoreCli_ExplainsOptionalThreeDmTool(string command)
    {
        var result = Run([command, "source.3dm"]);
        Assert.NotEqual(0, result.Exit);
        Assert.Contains("aetheris-3dm-required", result.Error);
        Assert.Contains("Aetheris.ThreeDm.CLI", result.Error);
    }

    [Fact]
    public void NotesCommands_CreateExtendRelateExportAndValidate()
    {
        using var temp = new TempDirectory();
        Assert.Equal(0, Run(["drawing", "notes", "create", Fixture(), "--out", temp.Path, "--name", "CLI Notes", "--title", "Synthetic", "--sheets", "SHEET 1 OF 1", "--json"]).Exit);
        var project = Path.Combine(temp.Path, "drawing-notes.json");
        Assert.Equal(0, Run(["drawing", "notes", "add-pass", project, "--id", "Pass.Overall", "--name", "Overall geometry", "--focus", "envelope and datums", "--json"]).Exit);
        Assert.Equal(0, Run(["drawing", "notes", "add-region", project, "--id", "DetailD", "--name", "Detail D", "--page", "1", "--bounds", "465,60,180,200", "--type", "Detail", "--json"]).Exit);
        Assert.Equal(0, Run(["drawing", "notes", "add-note", project, "--id", "DetailD.Origin", "--label", "Detail D origin", "--category", "Datum", "--page", "1", "--bounds", "485,80,25,160", "--region", "DetailD", "--confidence", "High", "--json"]).Exit);
        Assert.Equal(0, Run(["drawing", "notes", "add-dimension", project, "--id", "Camera1.X", "--label", "Camera 1 center X", "--value", "25.00 mm", "--page", "1", "--bounds", "510,95,80,20", "--region", "DetailD", "--type", "Coordinate", "--axis", "X", "--class", "ProductGeometry", "--json"]).Exit);
        Assert.Equal(0, Run(["drawing", "notes", "add-relation", project, "--id", "Camera1.X.From", "--from", "Camera1.X", "--to", "DetailD.Origin", "--kind", "ReferencedFrom", "--json"]).Exit);
        var markdown = Path.Combine(temp.Path, "notes.md");
        Assert.Equal(0, Run(["drawing", "notes", "export", project, "--format", "markdown", "--out", markdown, "--json"]).Exit);
        var validation = Run(["drawing", "notes", "validate", project, "--source", Fixture(), "--json"]);
        Assert.Equal(0, validation.Exit); Assert.Contains("Camera 1 center X", File.ReadAllText(markdown)); Assert.Contains("ReferencedFrom", File.ReadAllText(markdown));
        using var report = JsonDocument.Parse(validation.Output); Assert.True(report.RootElement.GetProperty("success").GetBoolean()); Assert.Equal(1, report.RootElement.GetProperty("relationCount").GetInt32()); Assert.Contains("Pass.Overall", File.ReadAllText(markdown));
    }

    [Fact]
    public void NotesHelpDocumentsAgentCallableSurface()
    {
        var result = Run(["drawing", "notes", "--help"]);
        Assert.Equal(0, result.Exit); Assert.Contains("add-region", result.Output); Assert.Contains("add-dimension", result.Output); Assert.Contains("add-relation", result.Output); Assert.Contains("add-pass", result.Output); Assert.Contains("serve", result.Output);
    }

    private static (int Exit, string Output, string Error) Run(string[] args)
    {
        using var stdout = new StringWriter(); using var stderr = new StringWriter(); var exit = CliRunner.Run(args, stdout, stderr); return (exit, stdout.ToString(), stderr.ToString());
    }

    private static string Fixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root not found."), "fixtures", "DrawingNotes", "synthetic-engineering-drawing.pdf");
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aetheris-drawing-cli-tests-{Guid.NewGuid():N}");
        public TempDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
