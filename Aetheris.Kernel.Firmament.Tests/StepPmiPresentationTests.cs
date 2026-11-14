using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Drawing;
using Aetheris.Kernel.Firmament.Export;

namespace Aetheris.Kernel.Firmament.Tests;

/// <summary>A GLB and a PDF drawing made from the semantic PMI an existing AP242 file carries (NIST MBE test cases).</summary>
public sealed class StepPmiPresentationTests
{
    private static string Nist(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return Path.Combine(directory!.FullName, "testdata", "step242", "nist", relative);
    }

    private sealed class Scratch : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aetheris-step-pmi-" + Guid.NewGuid().ToString("N"));
        public Scratch() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, true); } catch (IOException) { } }
    }

    [Fact]
    public void SchemaReadControlsCarryTheirDatumsAndDatumFeaturesTheirFaces()
    {
        var items = Step242SemanticPmiInspector.Inspect(File.ReadAllText(Nist("CTC/nist_ctc_01_asme1_ap242-e1.stp"))).Items;
        // PERPENDICULARITY_TOLERANCE as a plain instance names its datum system after the four inherited attributes.
        Assert.Equal(["A"], Assert.Single(items, i => i.Kind == "Perpendicularity").DatumReferences);
        // Datum features B and C reach their faces through ITEM_IDENTIFIED_REPRESENTATION_USAGE.
        Assert.All(items.Where(i => i.Kind == "Datum"), d => Assert.NotEmpty(d.GeometricFaceEntityIds));
        Assert.Equal(["A", "B", "C"], items.Where(i => i.Kind == "Datum").Select(i => i.Name).Order());
    }

    [Fact]
    public void ANistPartBecomesAGlbWithEveryRequirementAsACallout()
    {
        var step = File.ReadAllText(Nist("CTC/nist_ctc_01_asme1_ap242-e1.stp"));
        var display = StepPmiPresentation.Display(step, "ctc01");
        Assert.Equal("mm", display.Units);
        Assert.Equal(Step242SemanticPmiInspector.Inspect(step).Items.Count, display.Annotations.Count);
        Assert.Contains(display.Annotations, a => a.Text == "60° ±0.5°");
        // A location the file states without a value is drawn with the model nominal, in parentheses.
        Assert.Contains(display.Annotations, a => a.Text == "(85)");
        Assert.Contains(display.Annotations, a => a.Text.StartsWith("Perpendicularity 1.5 | A", StringComparison.Ordinal));
        var glb = StepPmiPresentation.Glb(step, "ctc01");
        Assert.Equal("glTF"u8.ToArray(), glb[..4]);
    }

    [Fact]
    public void ANistPartBecomesAReferenceDrawingWithAPmiSchedule()
    {
        using var scratch = new Scratch();
        var result = FirmamentDrawingCompiler.Generate(Nist("CTC/nist_ctc_01_asme1_ap242-e1.stp"), scratch.Path, null, new(ReferenceOnly: true));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        var rows = result.Artifacts!.Drawing.Pages.SelectMany(p => p.Tables).Where(t => t.Identity.StartsWith("PMI SCHEDULE", StringComparison.Ordinal))
            .SelectMany(t => t.Rows).ToArray();
        Assert.Equal(21, rows.Length);
        Assert.Contains(rows, r => r[1] == "ANGLE" && r[2] == "60° ±0.5°");
        Assert.Contains(rows, r => r[1] == "SIZE (DIAMETER)" && r[2] == "Ø35 0/-0.2");
        Assert.Contains(rows, r => r[1] == "DISTANCE" && r[2] == "(85) NO VALUE IN FILE");
        Assert.Contains(rows, r => r[1] == "POSITION" && r[2] == "0.75" && r[3] == "A | B | C");
        Assert.True(File.Exists(result.Artifacts.PdfPath));
    }

    [Fact]
    public void AnInchFileIsDrawnInMillimetres()
    {
        using var scratch = new Scratch();
        var result = FirmamentDrawingCompiler.Generate(Nist("CTC/nist_ctc_03_asme1_ap242-e2.stp"), scratch.Path, null, new(ReferenceOnly: true));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        var rows = result.Artifacts!.Drawing.Pages.SelectMany(p => p.Tables).SelectMany(t => t.Rows).ToArray();
        // A 0.005 in flatness per 0.25 in square and a 2.000 in ±0.010 in bore, written in millimetres.
        Assert.Contains(rows, r => r[1] == "FLATNESS" && r[2] == "0.127 /6.35×6.35");
        Assert.Contains(rows, r => r[2] == "Ø50.8 ±0.254");
    }

    [Fact]
    public void AdvancedToleranceParametersAreReadInMillimetres()
    {
        static IReadOnlyList<Step242SemanticPmiInspectionItem> Items(string file) => Step242SemanticPmiInspector.Inspect(File.ReadAllText(Nist(file))).Items;
        // Per unit area (0.005 in per 0.25 in square), projected zone, unequally disposed, common datum, value qualifiers.
        Assert.Equal(["/6.35×6.35"], Assert.Single(Items("CTC/nist_ctc_03_asme1_ap242-e2.stp"), i => i.Kind == "Flatness").ToleranceParameters);
        Assert.Equal(["P 50"], Assert.Single(Items("CTC/nist_ctc_04_asme1_ap242-e1.stp"), i => i.EntityId == 18891).ToleranceParameters);
        Assert.Equal(["U 0.5"], Assert.Single(Items("CTC/nist_ctc_02_asme1_ap242-e2.stp"), i => i.EntityId == 57).ToleranceParameters);
        Assert.Equal(["A", "B"], Items("CTC/nist_ctc_05_asme1_ap242-e1.stp").First(i => i.Kind == "CircularRunout").DatumReferenceFrames.Single().Labels);
        Assert.Contains(Items("FTC/nist_ftc_10_asme1_ap242-e2.stp"), i => i.Kind == "Symmetry" && i.Value == .5);
        Assert.Equal(["MAX"], Assert.Single(Items("FTC/nist_ftc_11_asme1_ap242-e2.stp"), i => i.EntityId == 113).ValueQualifiers);
        // Every record of every NIST test case is now drawable in full.
        foreach (var file in Directory.GetFiles(Nist(""), "*.stp", SearchOption.AllDirectories))
            Assert.All(Step242SemanticPmiInspector.Inspect(File.ReadAllText(file)).Items, i => Assert.True(i.PresentationSupported, $"{Path.GetFileName(file)} #{i.EntityId}: {string.Join(", ", i.PresentationGaps)}"));
    }

    [Theory]
    [InlineData("CTC/nist_ctc_04_asme1_ap242-e1.stp")] // composite datum features, projected zone
    [InlineData("CTC/nist_ctc_05_asme1_ap242-e1.stp")] // auxiliary curves outside the part: listed, not located
    public void NistPartsBecomeAGlbAndAReferenceDrawingWithEveryRecord(string file)
    {
        var step = File.ReadAllText(Nist(file));
        var count = Step242SemanticPmiInspector.Inspect(step).Items.Count;
        Assert.Equal(count, StepPmiPresentation.Display(step, "part").Annotations.Count);
        using var scratch = new Scratch();
        var result = FirmamentDrawingCompiler.Generate(Nist(file), scratch.Path, null, new(ReferenceOnly: true));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal(count, result.Artifacts!.Drawing.Pages.SelectMany(p => p.Tables).Where(t => t.Identity.StartsWith("PMI SCHEDULE", StringComparison.Ordinal)).Sum(t => t.Rows.Count));
    }

    [Fact]
    public void BalloonsRingTheViewWithoutCrossingLeaders()
    {
        using var scratch = new Scratch();
        var result = FirmamentDrawingCompiler.Generate(Nist("CTC/nist_ctc_01_asme1_ap242-e1.stp"), scratch.Path, null, new(ReferenceOnly: true));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        foreach (var page in result.Artifacts!.Drawing.Pages.Where(p => p.Views.Count == 1 && p.Annotations.Count > 0))
        {
            var view = page.Views[0];
            Assert.All(page.Annotations, a =>
            {
                Assert.Contains(a.Shape, new[] { DrawingAnnotationShape.Balloon, DrawingAnnotationShape.DatumFeature });
                // Outside the geometry, never on it.
                Assert.False(a.SelectedCandidate.Body.Intersects(view.GeometryBounds), a.EngineeringDisplay);
            });
            var leaders = page.Annotations.Select(a => a.SelectedCandidate.Leader).ToArray();
            for (var i = 0; i < leaders.Length; i++)
                for (var j = i + 1; j < leaders.Length; j++)
                    Assert.False(Crosses(leaders[i][0], leaders[i][^1], leaders[j][0], leaders[j][^1]), $"{page.Annotations[i].EngineeringDisplay} x {page.Annotations[j].EngineeringDisplay}");
        }
        // The top face's flatness ends on the face, with a dot.
        Assert.True(result.Artifacts.Drawing.Pages.SelectMany(p => p.Annotations).Single(a => a.EngineeringDisplay == "9").DotAnchor);
    }

    [Fact]
    public void LabelsInTheFirmamentSourcePlaceBalloons()
    {
        var path = Path.Combine(Nist(""), "..", "..", "..", "fixtures", "Canonical", "PMI", "nist-ctc01-drawing-labels.firmament");
        using var scratch = new Scratch();
        var result = FirmamentDrawingCompiler.Generate(Path.GetFullPath(path), scratch.Path, null, new(ReferenceOnly: true));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        var pages = result.Artifacts!.Drawing.Pages;
        DrawingAnnotationIr Balloon(string display) => pages.SelectMany(p => p.Annotations).Single(a => a.EngineeringDisplay == display);
        var top = pages.Single(p => p.Views.Count == 1 && p.Views[0].Identity == "TOP" && p.Annotations.Count > 0).Views[0];
        Assert.True(Balloon("9").SelectedCandidate.Body.Bottom < top.GeometryBounds.Y);
        Assert.Equal(120, Balloon("A").SelectedCandidate.Body.Center.X, 6);
        Assert.Equal(34, Balloon("A").SelectedCandidate.Body.Center.Y, 6);
        var slot = Balloon("3");
        Assert.Equal("FRONT", slot.AssignedView);
        Assert.True(slot.SelectedCandidate.Body.Y > pages.Single(p => p.Views.Count == 1 && p.Views[0].Identity == "FRONT").Views[0].GeometryBounds.Bottom);
        Assert.Equal("authored", slot.SelectedCandidate.Lane);
    }

    [Theory]
    [InlineData("Label X { record: \"#57\" side: Inside }", "side is Left, Right, Above or Below")]
    [InlineData("Label X { record: Nowhere side: Left }", "record 'Nowhere' is not a PMI record")]
    [InlineData("Label X { record: \"#57\" }", "a Label sets view, side or at")]
    [InlineData("Label X { record: \"#57\" at: [1, 2] }", "at is [x mm, y mm]")]
    public void AMalformedLabelIsRejectedWithItsReason(string label, string reason)
    {
        var root = Path.GetFullPath(Path.Combine(Nist(""), "..", "..", ".."));
        var source = $$"""
            Model L {
                units: mm
                InlineStep Source {
                    path: "{{Path.Combine(root, "testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp").Replace('\\', '/')}}"
                    textSha256: "341cea888fb316673ed683e59391c11b4f96cff2542d9387e135f06682dbd745"
                    importTolerance: 0.1mm
                }
                Pmi {
                    {{label}}
                }
            }
            """;
        var parse = FirmamentV2.FirmamentV2Parser.Parse(source, root);
        Assert.Contains(parse.Diagnostics, d => d.Contains(DrawingLabelDirectives.Invalid, StringComparison.Ordinal) && d.Contains(reason, StringComparison.Ordinal));
    }

    private static bool Crosses(DrawingPoint2 a, DrawingPoint2 b, DrawingPoint2 c, DrawingPoint2 d)
    {
        static double Side(DrawingPoint2 p, DrawingPoint2 q, DrawingPoint2 r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        return Side(a, b, c) * Side(a, b, d) < -1e-8 && Side(c, d, a) * Side(c, d, b) < -1e-8;
    }
}
