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
        // A 0.005 in flatness and a 2.000 in ±0.010 in bore, written in millimetres.
        Assert.Contains(rows, r => r[1] == "FLATNESS" && r[2] == "0.127");
        Assert.Contains(rows, r => r[2] == "Ø50.8 ±0.254");
    }
}
