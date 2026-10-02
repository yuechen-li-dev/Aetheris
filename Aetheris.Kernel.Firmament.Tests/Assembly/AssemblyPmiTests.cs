using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyPmiTests
{
    private const string Source = """
        Record ReleaseInfo { Author: String Date: Date Version: Version Description: String Tolerance: Length }
        Static Release: ReleaseInfo {
         Author: "GPT 6.1 Sol Codex"
         Date: 2026-10-01
         Version: 0.1.0
         Description: "AI-authored presentation witness"
         Tolerance: 0.1mm
        }
        Template<H: Length> Struct Pad {
         Rect2 R { Center: [0mm,0mm]; Size: [10mm,10mm] }
         Profile P { Loop Outer { R |> TraceLoop } }
         Extrude Body { Profile: P From: 0mm To: H }
        }
        Assembly Product {
         Provenance: Release;
         Pmi {
          Note Intent { Target: Product.A; Text: "Designer's note: {not geometry} #17; https://example.test/a // literal text"; }
         }
         <Assembly Product>
          <Part A = Pad<H:2mm>> Placement { From: Origin; To: World; } </Part>
          <Part B = Pad<H:2mm>> Placement { From: Origin; To: World; TranslateLocal: [20mm,0mm,0mm]; } </Part>
         </Assembly>
         Anchor: Product;
        }
        """;

    [Fact]
    public void TypedReleaseAndOccurrenceNoteSurviveRealAp242AndUsdExport()
    {
        var result = new AssemblyM1Pipeline().Compile(Source);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal("GPT 6.1 Sol Codex", result.Ir!.Annotations!.Release!.Fields["Author"]);
        var export = AssemblyIrAp242Exporter.Export(result);
        Assert.True(export.IsSuccess, string.Join("\n", export.Diagnostics));
        Assert.Contains("'2026-10-01T00:00:00',('GPT 6.1 Sol Codex')", export.Value);
        var inspected = Step242SemanticPmiInspector.Inspect(export.Value);
        Assert.True(inspected.Success, string.Join("\n", inspected.Diagnostics));
        Assert.Equal(6, inspected.AnnotationCount);
        var note = Assert.Single(inspected.Items, n => n.Name == "Intent");
        Assert.Equal("Product.A", note.Target);
        Assert.Equal("Designer's note: {not geometry} #17; https://example.test/a // literal text", note.Text);
        Assert.All(inspected.Items, n => Assert.Empty(n.GeometricFaceEntityIds));
        var imported = Step242AssemblyImporter.Import(export.Value);
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics));
        Assert.Single(imported.Value.Definitions, d => d.Geometry is not null);
        Assert.Equal(2, imported.Value.Occurrences.Count(o => o.Name is "A" or "B"));
        var usd = AssemblyUsdExporter.Export(result);
        Assert.Contains("aetheris:provenance:Author = \"GPT 6.1 Sol Codex\"", usd);
        Assert.Contains("aetheris:pmi:Intent:target = \"Product.A\"", usd);
        Assert.Equal(export.Value, AssemblyIrAp242Exporter.Export(result).Value);
    }

    [Theory]
    [InlineData("Date: 2026-10-01", "Date: 2026-02-30", "provenance-record-invalid")]
    [InlineData("Version: 0.1.0", "Version: 0.1", "provenance-record-invalid")]
    [InlineData("Tolerance: 0.1mm", "Tolerance: 0.1deg", "provenance-record-invalid")]
    [InlineData("Author: String", "Author: Length", "provenance-field-invalid")]
    [InlineData("Provenance: Release;", "Provenance: Missing;", "provenance-record-unresolved")]
    [InlineData("Provenance: Release;", "Provenance: Release; Provenance: Release;", "provenance-selection-invalid")]
    [InlineData("Target: Product.A", "Target: Product.Missing", "pmi-target-unresolved")]
    [InlineData("Note Intent", "Datum Intent", "pmi-kind-unsupported")]
    [InlineData("Text:", "Unknown:", "pmi-note-invalid")]
    public void InvalidMetadataStopsCompilation(string before, string after, string diagnostic)
    {
        var result = new AssemblyM1Pipeline().Compile(Source.Replace(before, after));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "assembly-" + diagnostic);
    }

    [Fact]
    public void NoteTextCannotDeclareRecordsOrGeometry()
    {
        var source = Source.Replace("Designer's note: {not geometry} #17; https://example.test/a // literal text",
            "Record Spoof { Value: Length } Static Trap: Spoof { Value: invalid } Box Fake { Size: [9mm,9mm,9mm] }");
        var result = new AssemblyM1Pipeline().Compile(source);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Single(result.Geometry!.DefinitionBodies);
        Assert.Equal(2, result.Geometry.InstanceBodies.Count);
        var inspection = Step242SemanticPmiInspector.Inspect(AssemblyIrAp242Exporter.Export(result).Value);
        Assert.StartsWith("Record Spoof", inspection.Items.Single(n => n.Name == "Intent").Text);
    }

    [Fact]
    public void MetadataEditRefreshesStepAndKeepsSharedGeometryCache()
    {
        using var session = new FirmamentCompilationSession();
        var before = session.Compile(Source);
        var after = session.Compile(Source.Replace("Version: 0.1.0", "Version: 0.1.1").Replace("Designer", "Reviewer"));
        Assert.True(before.IsSuccess && after.IsSuccess, string.Join("\n", after.Diagnostics));
        Assert.Equal(1, after.Reuse!.ReusedDefinitions);
        Assert.Equal(0, after.Reuse.RebuiltDefinitions);
        Assert.Equal(before.Geometry!.Artifact.DeterministicSha256, after.Geometry!.Artifact.DeterministicSha256);
        var inspected = Step242SemanticPmiInspector.Inspect(AssemblyIrAp242Exporter.Export(after).Value);
        Assert.Equal("0.1.1", inspected.Items.Single(n => n.Name == "Release.Version").Text);
        Assert.StartsWith("Reviewer's note", inspected.Items.Single(n => n.Name == "Intent").Text);
    }

    [Fact]
    public void GuitarReleaseExportsAllNotesWithoutChangingProductStructure()
    {
        var fixture = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm");
        var result = new AssemblyM1Pipeline().CompileFile(fixture);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal(99, result.Geometry!.InstanceBodies.Count);
        Assert.Equal(56, result.Geometry.DefinitionBodies.Count);
        Assert.Equal("0.1mm", result.Ir!.Annotations!.Release!.Fields["AuthoringTolerance"]);
        var export = AssemblyIrAp242Exporter.Export(result);
        Assert.True(export.IsSuccess, string.Join("\n", export.Diagnostics));
        var inspected = Step242SemanticPmiInspector.Inspect(export.Value);
        Assert.True(inspected.Success, string.Join("\n", inspected.Diagnostics));
        Assert.Equal(18, inspected.AnnotationCount);
        Assert.Equal("GPT 6.1 Sol Codex", inspected.Items.Single(n => n.Name == "GuitarRelease.Author").Text);
        Assert.Equal("GuitarX0.Bridge", inspected.Items.Single(n => n.Name == "BridgeHardware").Target);
    }
}
