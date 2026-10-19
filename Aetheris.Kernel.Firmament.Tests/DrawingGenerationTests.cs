using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.Drawing;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class DrawingGenerationTests
{
    [Fact]
    public void PartAndAp242ProduceTheSameLocatedPmiAndGeometry()
    {
        using var scratch = new Scratch();
        var part = FirmamentDrawingCompiler.Generate(Fixture(), Path.Combine(scratch.Path, "part"));
        Assert.True(part.IsSuccess, string.Join("\n", part.Diagnostics));
        var compiled = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture()));
        Assert.True(compiled.IsSuccess);
        var readback = Aetheris.Kernel.Core.Step242.Step242SemanticPmiInspector.Inspect(compiled.Value.StepText);
        Assert.All(readback.Items.Where(i => i.Kind == "Diameter"), i => Assert.StartsWith("diameter:", i.Name));
        Assert.Equal(10, readback.Items.Count(i => i.AuthoredName is not null));
        var step = Path.Combine(scratch.Path, "InspectionPlate.step"); File.WriteAllText(step, compiled.Value.StepText);
        var exchanged = FirmamentDrawingCompiler.Generate(step, Path.Combine(scratch.Path, "step"));
        Assert.True(exchanged.IsSuccess, string.Join("\n", exchanged.Diagnostics));
        var drawing = part.Artifacts!.Drawing;
        Assert.Single(drawing.Pages); // a complete coordinate schedule avoids duplicate diameter sheets
        var annotations = drawing.Pages.SelectMany(p => p.Annotations).ToArray();
        Assert.Equal(10, drawing.DefinitionEvidence!.Holes.Count);
        Assert.Equal(10, drawing.DefinitionEvidence.Holes.SelectMany(h => h.SourceReferences).Count());
        Assert.True(drawing.DefinitionEvidence.NominalGeometryComplete);
        Assert.Equal(annotations.Select(a => a.EngineeringDisplay), exchanged.Artifacts!.Drawing.Pages.SelectMany(p => p.Annotations).Select(a => a.EngineeringDisplay));
        Assert.Equal(drawing.Pages.SelectMany(p => p.Views).SelectMany(v => v.Primitives).SelectMany(p => p.Points),
            exchanged.Artifacts.Drawing.Pages.SelectMany(p => p.Views).SelectMany(v => v.Primitives).SelectMany(p => p.Points));
        Assert.Equal(0, drawing.LayoutEvidence.TextModelCollisionsAfter);
        Assert.Equal(0, drawing.LayoutEvidence.TextTextCollisionsAfter);
        Assert.Equal(0, drawing.LayoutEvidence.TextLeaderCollisionsAfter);
        Assert.Equal(0, drawing.LayoutEvidence.FailedAnnotationCount);
        Assert.Contains(annotations, a => a.EngineeringDisplay.Contains("Ø4 +0.05/-0.02", StringComparison.Ordinal));
        Assert.Contains(annotations, a => a.DimensionLines.Count > 0);
        Assert.Contains(drawing.Pages.SelectMany(p => p.Views).SelectMany(v => v.Primitives), p => p.Kind == DrawingPrimitiveKind.CenterMark);
        Assert.True(annotations.Select(a => a.ProjectedAnchor).Distinct().Count() >= 10);
        Assert.All(annotations.Where(a => a.SemanticReference.StartsWith("step:", StringComparison.Ordinal)), a => { Assert.Contains("STEP #", a.Provenance); Assert.Equal(a.ProjectedAnchor, a.SelectedCandidate.Leader[0]); });
        foreach (var page in drawing.Pages)
        {
            var labels = page.Annotations.Select(a => a.SelectedCandidate.Body).ToArray();
            for (var i = 0; i < labels.Length; i++) for (var j = i + 1; j < labels.Length; j++) Assert.False(labels[i].Intersects(labels[j]));
        }
        Assert.True(File.Exists(part.Artifacts.PdfPath)); Assert.True(File.Exists(part.Artifacts.PptxPath));
        var svg = File.ReadAllText(part.Artifacts.SvgPath);
        Assert.Contains("centermark", svg); Assert.Contains("data-semantic-reference", svg);
    }

    [Fact]
    public void UnlocatedPmiRejectsRatherThanInventingACenterLeader()
    {
        using var scratch = new Scratch();
        var compiled = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture())); Assert.True(compiled.IsSuccess);
        var step = Regex.Replace(compiled.Value.StepText, @"(?m)^.*=GEOMETRIC_ITEM_SPECIFIC_USAGE\(.*\r?\n?", "");
        Assert.NotEqual(compiled.Value.StepText, step);
        var source = Path.Combine(scratch.Path, "unlocated.step"); File.WriteAllText(source, step);
        var output = Path.Combine(scratch.Path, "out");
        var result = FirmamentDrawingCompiler.Generate(source, output);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.StartsWith("drawing-pmi-target-unlocated:", StringComparison.Ordinal));
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void GenerationIsDeterministicAndDoesNotMutateSource()
    {
        using var scratch = new Scratch(); var before = File.ReadAllBytes(Fixture());
        var a = FirmamentDrawingCompiler.Generate(Fixture(), Path.Combine(scratch.Path, "a"));
        var b = FirmamentDrawingCompiler.Generate(Fixture(), Path.Combine(scratch.Path, "b"));
        Assert.True(a.IsSuccess, string.Join("\n", a.Diagnostics)); Assert.True(b.IsSuccess, string.Join("\n", b.Diagnostics));
        Assert.Equal(a.Artifacts!.PdfSha256, b.Artifacts!.PdfSha256);
        Assert.Equal(a.Artifacts.DrawingIrSha256, b.Artifacts.DrawingIrSha256);
        Assert.Equal(a.Artifacts.PptxSha256, b.Artifacts.PptxSha256);
        Assert.Equal(before, File.ReadAllBytes(Fixture()));
    }

    [Fact]
    public void GeometryWithoutPmiIsExplicitlyLabeled()
    {
        using var scratch = new Scratch(); var path = Path.Combine(scratch.Path, "plain.firmament");
        File.WriteAllText(path, "Model Plain { units: mm Box Stock { size: [30mm, 20mm, 10mm] } }");
        var result = FirmamentDrawingCompiler.Generate(path, Path.Combine(scratch.Path, "out"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.True(result.Artifacts!.Drawing.DefinitionEvidence!.NominalGeometryComplete);
        Assert.False(result.Artifacts.Drawing.DefinitionEvidence.DimensionalTolerancesComplete);
        Assert.Contains(result.Artifacts.Drawing.DefinitionEvidence.MissingRequirements, n => n.Contains("tolerances are unspecified", StringComparison.Ordinal));
        Assert.Contains(result.Artifacts.Drawing.Pages[0].Notes, n => n.Contains("NOT FOR MANUFACTURE", StringComparison.Ordinal));
    }

    [Fact]
    public void LongCalloutUsesItsActualFontWidth()
    {
        using var scratch = new Scratch(); var path = Path.Combine(scratch.Path, "long.firmament");
        File.WriteAllText(path, File.ReadAllText(Fixture()).Replace("Size1 {", "InspectionRequirementForFirstMountingBoreDiameter {", StringComparison.Ordinal));
        var result = FirmamentDrawingCompiler.Generate(path, Path.Combine(scratch.Path, "out"), null, new(ReferenceOnly: true));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        var label = result.Artifacts!.Drawing.Pages.SelectMany(p => p.Annotations).Single(a => a.EngineeringDisplay.StartsWith("InspectionRequirement", StringComparison.Ordinal));
        Assert.True(label.SelectedCandidate.Body.Width > 58);
    }

    [Fact]
    public void ForeignPmiUnitsCannotBeMislabeledAsMillimetres()
    {
        using var scratch = new Scratch();
        var compiled = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture())); Assert.True(compiled.IsSuccess);
        var text = compiled.Value.StepText.Replace(".MILLI.,.METRE.", ".CENTI.,.METRE.", StringComparison.Ordinal);
        Assert.NotEqual(compiled.Value.StepText, text);
        var path = Path.Combine(scratch.Path, "centimetres.step"); File.WriteAllText(path, text);
        var result = FirmamentDrawingCompiler.Generate(path, Path.Combine(scratch.Path, "out"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.StartsWith("drawing-pmi-units-unsupported:", StringComparison.Ordinal));
    }

    [Fact]
    public void HoleDepthAlreadyPresentInAp242SurvivesTheDrawing()
    {
        using var scratch = new Scratch();
        var compiled = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture())); Assert.True(compiled.IsSuccess);
        var body = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(compiled.Value.StepText).Value;
        var wall = body.Topology.Faces.First(f => body.TryGetFaceSurfaceGeometry(f.Id, out var s) && s?.Cylinder is not null);
        var exported = Aetheris.Kernel.Core.Step242.Step242Exporter.ExportBody(body,
            [new Aetheris.Kernel.Core.Step242.Step242SemanticPmiHole("Stock.H1", 4, 8, "through", .05, .02) { GeometricFaceIds = [wall.Id.Value] }]);
        Assert.True(exported.IsSuccess);
        var path = Path.Combine(scratch.Path, "depth.step"); File.WriteAllText(path, exported.Value);
        var result = FirmamentDrawingCompiler.Generate(path, Path.Combine(scratch.Path, "out"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal(8, result.Artifacts!.Drawing.DefinitionEvidence!.StockDimensions["Thickness"]);
        Assert.All(result.Artifacts.Drawing.DefinitionEvidence.Holes, h => Assert.Equal("THRU", h.Extent));
    }

    [Fact]
    public void SignoffUsesARealTableWithBlankUnperformedApprovals()
    {
        using var scratch = new Scratch();
        var signoff = new DrawingSignoffIr(new("Codex (AI)", "2026-10-18"), new(), new());
        var result = FirmamentDrawingCompiler.Generate(Fixture(), Path.Combine(scratch.Path, "out"), signoff);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        foreach (var page in result.Artifacts!.Drawing.Pages)
        {
            var table = page.InformationBlock!.Table!; Assert.NotNull(table);
            Assert.Equal(5, table.Rows.Count);
            Assert.All(table.Rows, row => Assert.Equal(6, row.Cells.Sum(c => c.ColumnSpan)));
            var cells = table.Rows.SelectMany(r => r.Cells).ToDictionary(c => c.Key);
            Assert.Equal("Codex (AI)", cells["DrawnBy"].Value); Assert.Equal("2026-10-18", cells["DrawnDate"].Value);
            foreach (var key in new[] { "CheckedBy", "CheckedDate", "ApprovedBy", "ApprovedDate" }) Assert.Equal("", cells[key].Value);
        }
        using var archive = System.IO.Compression.ZipFile.OpenRead(result.Artifacts.PptxPath);
        var slides = archive.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)).ToArray();
        foreach (var slide in slides)
        {
            using var stream = slide.Open(); var xml = System.Xml.Linq.XDocument.Load(stream);
            System.Xml.Linq.XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
            var table = Assert.Single(xml.Descendants(a + "tbl"), t => t.Descendants(a + "gridCol").Count() == 6);
            Assert.Equal(6, table.Descendants(a + "gridCol").Count());
            Assert.All(table.Elements(a + "tr"), row => Assert.Equal(6, row.Elements(a + "tc").Count()));
        }
        using var core = new StreamReader(archive.GetEntry("docProps/core.xml")!.Open());
        Assert.Contains("Codex (AI)", core.ReadToEnd(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Checker", null, "drawing-signoff-incomplete")]
    [InlineData(null, "2026-10-18", "drawing-signoff-incomplete")]
    [InlineData("Checker", "yesterday", "drawing-date-iso-invalid")]
    public void SignoffCannotInventAMissingPersonOrDate(string? name, string? date, string code)
    {
        using var scratch = new Scratch();
        var result = FirmamentDrawingCompiler.Generate(Fixture(), Path.Combine(scratch.Path, "out"), new(new(), new(name, date), new()));
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, d => d.StartsWith(code, StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(scratch.Path, "out")));
    }

    [Fact]
    public void LargeDiameterWitnessMeasuresTheActualProjectedCylinder()
    {
        using var scratch = new Scratch(); var path = Path.Combine(scratch.Path, "large-bore.firmament");
        File.WriteAllText(path, """
            Model Bore {
              units: mm
              Box Stock { size: [40mm, 30mm, 8mm] }
              Modify Stock {
                Hole<Shaft> H { on: +Z; center: Point2(0mm, 0mm); diameter: 12mm; end: ThroughAll }
              }
              Pmi {
                HoleDiameter BoreSize {
                  target: face(H.Wall)
                  value: 12mm
                }
              }
            }
            """);
        var result = FirmamentDrawingCompiler.Generate(path, Path.Combine(scratch.Path, "out"), null, new(ReferenceOnly: true));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        var page = result.Artifacts!.Drawing.Pages.Single(p => p.Annotations.Any(a => a.Kind == DrawingAnnotationKind.DiameterDimension));
        var annotation = page.Annotations.Single(a => a.Kind == DrawingAnnotationKind.DiameterDimension);
        var line = Assert.Single(annotation.DimensionLines); var scale = page.Views.Single().Scale;
        Assert.Equal(12, Math.Abs(line.End.X - line.Start.X) / scale, 8);
        Assert.Equal(line.Start.Y, line.End.Y, 8);
        Assert.True(line.ArrowStart && line.ArrowEnd);
    }

    [Fact]
    public void AuthoredMetadataCarriesCheckerAndDateWithoutAnApproval()
    {
        using var scratch = new Scratch();
        var source = File.ReadAllText(Path.Combine(Root(), "fixtures/Canonical/Drawings/bearing-block-production-drawing.firmament"))
            .Replace("Material: String", "Material: String\n        DrawnBy: String\n        DrawnDate: Date\n        CheckedBy: String\n        CheckedDate: Date", StringComparison.Ordinal)
            .Replace("Material: \"PER PRODUCT\"", "Material: \"PER PRODUCT\"\n        DrawnBy: \"Test Preparer\"\n        DrawnDate: 2026-10-18\n        CheckedBy: \"Test Checker\"\n        CheckedDate: 2026-10-18", StringComparison.Ordinal);
        var path = Path.Combine(scratch.Path, "checked.firmament"); File.WriteAllText(path, source);
        var result = FirmamentDrawingCompiler.Compile(path, Path.Combine(scratch.Path, "out"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal("Test Checker", result.Artifacts!.Drawing.Metadata.Signoff!.Checked.Name);
        Assert.Equal("2026-10-18", result.Artifacts.Drawing.Metadata.Signoff.Checked.Date);
        Assert.Null(result.Artifacts.Drawing.Metadata.Signoff.Approved.Name);
        Assert.Equal("Test Preparer", result.Artifacts.Drawing.Metadata.Signoff.Drawn.Name);
        Assert.Contains("/Author (Test Preparer)", File.ReadAllText(result.Artifacts.PdfPath), StringComparison.Ordinal);
    }

    [Fact]
    public void MissingNominalDoesNotBecomeAnImaginaryZeroDimension()
    {
        using var scratch = new Scratch();
        var compiled = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture())); Assert.True(compiled.IsSuccess);
        var text = compiled.Value.StepText.Replace("MEASURE_REPRESENTATION_ITEM('diameter',4,", "MEASURE_REPRESENTATION_ITEM('diameter',$,", StringComparison.Ordinal);
        Assert.NotEqual(compiled.Value.StepText, text);
        var path = Path.Combine(scratch.Path, "missing-nominal.step"); File.WriteAllText(path, text);
        var result = FirmamentDrawingCompiler.Generate(path, Path.Combine(scratch.Path, "out"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.StartsWith("drawing-pmi-value-unavailable:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Angle", false)]
    [InlineData("diameter", true)]
    public void DimensionKindControlsAdmissionAndPresentation(string kind, bool admitted)
    {
        using var scratch = new Scratch();
        var compiled = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture())); Assert.True(compiled.IsSuccess);
        var body = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(compiled.Value.StepText).Value;
        var wall = body.Topology.Faces.First(f => body.TryGetFaceSurfaceGeometry(f.Id, out var s) && s?.Cylinder is not null);
        var exported = Aetheris.Kernel.Core.Step242.Step242Exporter.ExportBody(body,
            [new Aetheris.Kernel.Core.Step242.Step242SemanticPmiDimension("Stock.H1", "Size", kind, 4, null, null) { GeometricFaceIds = [wall.Id.Value] }]);
        Assert.True(exported.IsSuccess);
        var path = Path.Combine(scratch.Path, "kind.step"); File.WriteAllText(path, exported.Value);
        var result = FirmamentDrawingCompiler.Generate(path, Path.Combine(scratch.Path, "out"));
        Assert.Equal(admitted, result.IsSuccess);
        if (admitted)
            Assert.Contains(result.Artifacts!.Drawing.Pages.SelectMany(p => p.Annotations),
                a => a.Kind == DrawingAnnotationKind.DiameterDimension && a.EngineeringDisplay.Contains("Ø4", StringComparison.Ordinal));
        else
        {
            Assert.Contains(result.Diagnostics, d => d.StartsWith("drawing-pmi-family-unsupported:", StringComparison.Ordinal));
            Assert.False(Directory.Exists(Path.Combine(scratch.Path, "out")));
        }
    }

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(System.IO.Path.Combine(d.FullName, "Aetheris.slnx"))) d = d.Parent;
        return d!.FullName;
    }
    private static string Fixture() => System.IO.Path.Combine(Root(), "fixtures/Regression/DrawingGeneration/inspection-plate.firmament");
    private sealed class Scratch : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(Root(), "artifacts/local/tests/drawing-generation", Guid.NewGuid().ToString("N"));
        public Scratch() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
