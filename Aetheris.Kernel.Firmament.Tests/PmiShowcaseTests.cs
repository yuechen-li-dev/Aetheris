using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Visualization;

namespace Aetheris.Kernel.Firmament.Tests;

/// <summary>PMI on a composed machined part, as an engineer would specify it for manufacture.</summary>
public sealed class PmiShowcaseTests
{
    private static string Fixture(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return Path.Combine(directory!.FullName, relative);
    }

    private static string Showcase() => File.ReadAllText(Fixture("fixtures/Canonical/PMI/showcase-bearing-mount.firmament"));

    private const string Plate = """
        Model P {
          units: mm
          Static d: Length = 8mm tol +0.1mm -0.05mm
          Profile s = Rect2 { center: [0mm, 0mm]; size: [60mm, 40mm] }
          Profile pad = Circle2 { center: [0mm, 0mm]; radius: 10mm }
          Compose body {
            Base stock { profile: s; from: 0mm; to: 8mm; role: Stock }
            Boss hub { on: Top; profile: pad; height: 6mm }
            Hole<Shaft> h { on: +Z; center: [0mm, 0mm]; diameter: d; end: ThroughAll }
            Hole<Shaft> a { on: +Z; center: [-22mm, -12mm]; diameter: 5mm; end: ThroughAll }
            Hole<Shaft> b { on: +Z; center: [22mm, -12mm]; diameter: 5mm; end: ThroughAll }
          }
          Pmi {
            PMI_BODY
          }
        }
        """;

    private static FirmamentStepExportResult Build(string pmi)
    {
        var result = FirmamentBuildAndExport.CompileSource(Plate.Replace("PMI_BODY", pmi));
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        return result.Value;
    }

    [Fact]
    public void TypedToleranceDimension_BindsOnAComposedBody()
    {
        var step = Build("HoleDiameter S { target: h dimension: d }").StepText;
        var size = Assert.Single(Step242SemanticPmiInspector.Inspect(step).Items, i => i.Kind == "Diameter");
        Assert.Equal(8, size.Value); Assert.Equal(.1, size.TolerancePlus); Assert.Equal(.05, size.ToleranceMinus);
    }

    [Fact]
    public void ComposeOperationCap_IsANamedPmiTarget()
    {
        var step = Build("Datum A { target: face(-Z) } Parallelism Top { target: face(hub.Top) tolerance: 0.02mm datumRefs: [A] }").StepText;
        var control = Assert.Single(Step242SemanticPmiInspector.Inspect(step).Items, i => i.Kind == "Parallelism");
        Assert.Single(control.GeometricFaceEntityIds);
        // The ambiguous axis spelling is not silently resolved to some +Z face of a stepped body.
        var body = Core.Step242.Step242Importer.ImportBody(step).Value;
        var face = body.Bindings.FaceBindings.Single(b => b.SourceStepEntityId == control.GeometricFaceEntityIds[0]);
        Assert.Equal(14, body.GetFaceSurface(face.FaceId).Plane!.Value.Origin.Z, 9);
    }

    [Fact]
    public void AxisDatum_OnASplitFace_AttachesToEveryCoplanarPatch()
    {
        var step = Build("Datum A { target: face(-Z) } Datum B { target: face(-X) } Datum C { target: face(-Y) }").StepText;
        var datums = Step242SemanticPmiInspector.Inspect(step).Items.Where(i => i.Kind == "Datum").ToArray();
        Assert.Equal(3, datums.Length);
        Assert.All(datums, d => Assert.NotEmpty(d.GeometricFaceEntityIds));
    }

    [Fact]
    public void HoleList_IsOneRequirementWithItsQuantity_OnShaftWallsOnly()
    {
        var step = Build("Datum A { target: face(-Z) } Datum B { target: face(-X) } Datum C { target: face(-Y) }\n"
            + "HoleDiameter S { target: [a, b] value: 5mm tolerance: PlusMinus(0.1mm) }\n"
            + "Position L { target: [a, b] tolerance: 0.2mm zone: Cylindrical datumRefs: [A, B, C] modifiers: [MaximumMaterial] }").StepText;
        var items = Step242SemanticPmiInspector.Inspect(step).Items;
        var size = Assert.Single(items, i => i.Kind == "Diameter");
        var position = Assert.Single(items, i => i.Kind == "Position");
        Assert.Equal(2, size.Quantity); Assert.Equal(2, position.Quantity);
        Assert.Equal(size.GeometricFaceEntityIds.Order(), position.GeometricFaceEntityIds.Order());
        var display = PmiDisplayProjection.FromStep(step);
        Assert.StartsWith("2X Ø5 ±0.1", Assert.Single(display, a => a.Kind == "Diameter").Text);
    }

    [Theory]
    [InlineData("HoleDiameter S { target: [a] value: 5mm tolerance: PlusMinus(0.1mm) }")]
    [InlineData("HoleDiameter S { target: [a, missing] value: 5mm tolerance: PlusMinus(0.1mm) }")]
    [InlineData("Datum A { target: [a, b] }")]
    public void InvalidHoleLists_Reject(string pmi)
    {
        var result = FirmamentBuildAndExport.CompileSource(Plate.Replace("PMI_BODY", pmi));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void PartNotes_ExportAndReadBack()
    {
        var step = Build("Note Material { text: \"MATERIAL: 6061-T6\" }").StepText;
        var note = Assert.Single(Step242SemanticPmiInspector.Inspect(step).Items, i => i.Kind == "Annotation");
        Assert.Equal("Material", note.Name); Assert.Equal("MATERIAL: 6061-T6", note.Text);
        var result = FirmamentBuildAndExport.CompileSource(Plate.Replace("PMI_BODY", "Note Empty { text: \"\" }"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Showcase_SpecifiesThePartCompletely_AndEveryRecordIsDrawn()
    {
        var result = FirmamentBuildAndExport.CompileSource(Showcase());
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var inspection = Step242SemanticPmiInspector.Inspect(result.Value.StepText);
        Assert.Equal(3, inspection.DatumCount); Assert.Equal(2, inspection.DimensionCount);
        Assert.Equal(5, inspection.GeometricToleranceCount); Assert.Equal(3, inspection.AnnotationCount);
        Assert.All(inspection.Items.Where(i => i.Kind != "Annotation"), i => Assert.NotEmpty(i.GeometricFaceEntityIds));
        var display = PmiDisplayProjection.FromStep(result.Value.StepText);
        Assert.Equal(inspection.Items.Count, display.Count);
        Assert.All(display.Where(a => a.Kind != "Note"), a => Assert.NotNull(a.TextFill));
        Assert.Contains(display, a => a.Text.StartsWith("4X Ø6.6 ±0.1", StringComparison.Ordinal));
        Assert.Contains(display, a => a.Text == "Position Ø0.25 (M) | A | B | C");
    }
}
