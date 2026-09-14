using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class DrawingNotesCliTests
{
    [Fact]
    public void InspectRenderAndCrop_UseCoherentDrawingFamily()
    {
        using var temp = new TempDirectory();
        var inspect = Run(["drawing", "inspect", Fixture(), "--json"]);
        Assert.Equal(0, inspect.Exit); Assert.Equal(string.Empty, inspect.Error);
        using (var json = JsonDocument.Parse(inspect.Output)) Assert.Equal(1, json.RootElement.GetProperty("document").GetProperty("pageCount").GetInt32());
        var page = Path.Combine(temp.Path, "page.png"); var crop = Path.Combine(temp.Path, "detail.png");
        Assert.Equal(0, Run(["drawing", "render", Fixture(), "--page", "1", "--out", page, "--dpi", "96", "--json"]).Exit);
        Assert.Equal(0, Run(["drawing", "crop", Fixture(), "--page", "1", "--bounds", "465,60,180,200", "--name", "DetailD", "--out", crop, "--dpi", "96", "--json"]).Exit);
        Assert.True(File.Exists(page)); Assert.True(File.Exists(crop)); Assert.True(File.Exists(Path.ChangeExtension(crop, ".crop.json")));
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
