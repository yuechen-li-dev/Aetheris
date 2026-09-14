using System.Security.Cryptography;
using Aetheris.Drawing;

namespace Aetheris.Drawing.Tests;

public sealed class DrawingNotesTests
{
    [Fact]
    public void SyntheticPdf_InspectsAndRendersDeterministically()
    {
        var pdf = Fixture();
        var document = DrawingPdf.Inspect(pdf, "Synthetic Fixture", sheets: ["SHEET 1 OF 1"]);
        Assert.Equal(1, document.PageCount);
        Assert.Equal(792, document.Pages[0].WidthPoints);
        Assert.Equal(612, document.Pages[0].HeightPoints);
        Assert.Equal(64, document.Sha256.Length);
        using var temp = new TempDirectory();
        var first = Path.Combine(temp.Path, "first.png"); var second = Path.Combine(temp.Path, "second.png");
        DrawingPdf.RenderPage(pdf, 1, first, 120); DrawingPdf.RenderPage(pdf, 1, second, 120);
        Assert.Equal(SHA256.HashData(File.ReadAllBytes(first)), SHA256.HashData(File.ReadAllBytes(second)));
    }

    [Fact]
    public void RegionsAnnotationsRelationsAndSourceCoordinates_RoundTrip()
    {
        var project = DrawingPdf.CreateProject(Fixture(), "Synthetic Notes"); var notebook = new DrawingNotebook(project);
        notebook.AddPass(new("Pass1", "Envelope", "Overall product geometry"));
        notebook.AddRegion(new("Front", "Front View", 1, new(80, 70, 250, 270), DrawingRegionType.View));
        notebook.AddRegion(new("Front.Port", "Port", 1, new(225, 140, 60, 60), DrawingRegionType.Detail, "Front"));
        notebook.AddAnnotation(new("Datum.A", "Left datum", DrawingAnnotationCategory.Datum, 1, new(84, 80, 12, 220), "Front"));
        notebook.AddAnnotation(new("ProductWidth", "Product width", DrawingAnnotationCategory.Dimension, 1, new(80, 48, 250, 25), "Front", Confidence: DrawingConfidence.High, SemanticClass: DrawingSemanticClass.ProductGeometry, Dimension: DrawingDimensionParser.Parse("110.00 mm", DrawingDimensionType.Overall, DrawingAxis.X), Pass: "Pass1"));
        notebook.AddRelation(new("ProductWidth.From", "ProductWidth", "Datum.A", DrawingRelationKind.ReferencedFrom));
        using var temp = new TempDirectory(); var json = Path.Combine(temp.Path, "drawing-notes.json"); DrawingNotesPersistence.Save(json, project); var loaded = DrawingNotesPersistence.Load(json);
        Assert.Equal(DrawingNotesPersistence.Serialize(project), DrawingNotesPersistence.Serialize(loaded));
        Assert.Equal(80, loaded.Annotations.Single(x => x.Id == "ProductWidth").Bounds.X);
        Assert.Equal("Front", loaded.Regions.Single(x => x.Id == "Front.Port").ParentRegionId);
        Assert.Equal(DrawingRelationKind.ReferencedFrom, loaded.Relations.Single().Kind);
        Assert.Equal("Envelope", loaded.Passes.Single().Name);
    }

    [Theory]
    [InlineData("77.90", 77.90, "mm", DrawingDimensionType.Unknown)]
    [InlineData("Ø 6.80 mm", 6.80, "mm", DrawingDimensionType.Diameter)]
    [InlineData("R2.55", 2.55, "mm", DrawingDimensionType.Radius)]
    [InlineData("3X Ø16.20 mm", 16.20, "mm", DrawingDimensionType.Diameter)]
    [InlineData("32 deg", 32, "deg", DrawingDimensionType.Angle)]
    public void DimensionParser_PreservesTextAndExtractsUsefulValue(string text, double value, string units, DrawingDimensionType type)
    {
        var parsed = DrawingDimensionParser.Parse(text);
        Assert.Equal(text, parsed.ValueText); Assert.Equal(value, parsed.ParsedValue); Assert.Equal(units, parsed.Units); Assert.Equal(type, parsed.Type);
    }

    [Fact]
    public void QuestionsConfidenceAndKeepoutClassification_RemainExplicitInMarkdown()
    {
        var project = DrawingPdf.CreateProject(Fixture(), "Synthetic Notes"); var notebook = new DrawingNotebook(project);
        notebook.AddRegion(new("Section.SS", "Section S-S", 1, new(80, 340, 260, 190), DrawingRegionType.Section));
        notebook.AddAnnotation(new("SensorCone", "Sensor keepout cone", DrawingAnnotationCategory.Keepout, 1, new(135, 345, 150, 90), "Section.SS", OriginalText: "SENSOR KEEPOUT CONE", Confidence: DrawingConfidence.Medium, SemanticClass: DrawingSemanticClass.SensorKeepout));
        notebook.AddAnnotation(new("Question.Surface", "Surface applicability", DrawingAnnotationCategory.Question, 1, new(465, 430, 270, 30), Note: "Does 12.00 apply at surface?", Confidence: DrawingConfidence.Unresolved));
        var markdown = DrawingNotesMarkdown.Export(project);
        Assert.Contains("Classification: SensorKeepout", markdown);
        Assert.Contains("Original text: “SENSOR KEEPOUT CONE”", markdown);
        Assert.Contains("Confidence: Unresolved", markdown);
        Assert.Contains("Does 12.00 apply at surface?", markdown);
        Assert.Contains("Coordinates: top-left PDF points `[x,y,width,height]`", markdown);
    }

    [Fact]
    public void DuplicateAndConflictingInterpretations_AreReportedWithoutMerging()
    {
        var project = DrawingPdf.CreateProject(Fixture()); var notebook = new DrawingNotebook(project); var bounds = new DrawingBounds(100, 100, 40, 15);
        notebook.AddAnnotation(new("D1", "Width", DrawingAnnotationCategory.Dimension, 1, bounds, Dimension: DrawingDimensionParser.Parse("10.00")));
        notebook.AddAnnotation(new("D1.Copy", "Width", DrawingAnnotationCategory.Dimension, 1, bounds, Dimension: DrawingDimensionParser.Parse("10.00")));
        notebook.AddAnnotation(new("D1.Conflict", "Keepout depth", DrawingAnnotationCategory.Dimension, 1, bounds, SemanticClass: DrawingSemanticClass.AccessoryKeepout, Dimension: DrawingDimensionParser.Parse("10.00", DrawingDimensionType.Depth)));
        var issues = notebook.InspectIssues();
        Assert.Contains(issues, x => x.Code == "drawing-likely-duplicate"); Assert.Contains(issues, x => x.Code == "drawing-interpretation-conflict");
        Assert.Equal(3, project.Annotations.Count);
    }

    [Fact]
    public void HashMismatch_FailsClosed()
    {
        var project = DrawingPdf.CreateProject(Fixture()); var notebook = new DrawingNotebook(project); using var temp = new TempDirectory();
        var changed = Path.Combine(temp.Path, "changed.pdf"); File.Copy(Fixture(), changed); using (var stream = File.OpenWrite(changed)) { stream.Seek(0, SeekOrigin.End); stream.WriteByte(0); }
        var exception = Assert.Throws<DrawingNotesException>(() => notebook.RequireSourceMatch(changed)); Assert.Equal("drawing-source-hash-mismatch", exception.Code);
    }

    [Fact]
    public void ExportFiltering_ProducesCompactSemanticAndUnresolvedSubsets()
    {
        var project = DrawingPdf.CreateProject(Fixture()); var notebook = new DrawingNotebook(project);
        notebook.AddRegion(new("Front", "Front", 1, new(50, 50, 300, 300), DrawingRegionType.View));
        notebook.AddAnnotation(new("Geometry", "Body width", DrawingAnnotationCategory.Dimension, 1, new(60, 60, 100, 20), "Front", SemanticClass: DrawingSemanticClass.ProductGeometry, Dimension: DrawingDimensionParser.Parse("110")));
        notebook.AddAnnotation(new("Question", "Unknown leader", DrawingAnnotationCategory.Question, 1, new(60, 90, 100, 20), "Front", Confidence: DrawingConfidence.Unresolved));
        var geometry = DrawingNotesMarkdown.Export(project, new(SemanticClass: DrawingSemanticClass.ProductGeometry));
        var unresolved = DrawingNotesMarkdown.Export(project, new(UnresolvedOnly: true));
        Assert.Contains("Body width", geometry); Assert.DoesNotContain("Unknown leader", geometry);
        Assert.Contains("Unknown leader", unresolved); Assert.DoesNotContain("Body width", unresolved);
    }

    [Fact]
    public void CropCarriesSourceMetadataAndIsDeterministic()
    {
        using var temp = new TempDirectory(); var output = Path.Combine(temp.Path, "detail.png");
        var metadata = DrawingPdf.Crop(Fixture(), 1, new(465, 60, 180, 200), "Detail D", output, 120);
        Assert.True(File.Exists(output)); Assert.True(File.Exists(Path.ChangeExtension(output, ".crop.json")));
        Assert.Equal(DrawingPdf.Hash(Fixture()), metadata.DocumentSha256); Assert.Equal(1, metadata.Page);
    }

    [Fact]
    public void UiAssetsAndProjectLoadSave_UseTheSameSchema()
    {
        using var temp = new TempDirectory(); var project = DrawingPdf.CreateProject(Fixture()); var path = Path.Combine(temp.Path, "drawing-notes.json");
        DrawingNotesPersistence.Save(path, project); DrawingNotesUi.WriteAssets(Path.Combine(temp.Path, "ui"));
        Assert.Equal(project.Document.Sha256, DrawingNotesPersistence.Load(path).Document.Sha256);
        var html = File.ReadAllText(Path.Combine(temp.Path, "ui", "index.html"));
        Assert.Contains("/api/project", html); Assert.Contains("Fit page", html); Assert.Contains("Drag highlight", html); Assert.Contains("Shift-drag pan", html); Assert.Contains("/api/thumb/", html);
    }

    private static string Fixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root not found."), "fixtures", "DrawingNotes", "synthetic-engineering-drawing.pdf");
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aetheris-drawing-tests-{Guid.NewGuid():N}");
        public TempDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
