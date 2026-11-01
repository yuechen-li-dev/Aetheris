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
    public void RoundedStock_RimChamferRetainsBossPocketsAndCounterbores()
    {
        var source = Showcase().Replace(
            "Profile stockOutline = Rect2 { center: [0mm, 0mm]; size: [130mm, 90mm] }",
            "Profile stockOutline = RoundedRect2 { center: [0mm, 0mm]; size: [130mm, 90mm]; radius: 8mm }");
        Assert.Contains("RoundedRect2 { center: [0mm, 0mm]; size: [130mm, 90mm]", source);
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var body = Step242Importer.ImportBody(result.Value.StepText).Value!;
        var kinds = body.Geometry.Surfaces.GroupBy(s => s.Value.Kind).ToDictionary(g => g.Key, g => g.Count());
        // One cone per rounded corner of the 1mm rim chamfer; the retained bore, boss, pocket corners
        // and counterbores are cylinders; nothing degrades to a spline.
        Assert.Equal(4, kinds.GetValueOrDefault(Core.Geometry.SurfaceGeometryKind.Cone));
        Assert.False(kinds.ContainsKey(Core.Geometry.SurfaceGeometryKind.BSplineSurfaceWithKnots));
        Assert.True(BrepExportPreflight.Validate(body).IsValid);
        var pmi = Step242SemanticPmiInspector.Inspect(result.Value.StepText);
        Assert.All(pmi.Items.Where(i => i.Kind != "Annotation"), i => Assert.NotEmpty(i.GeometricFaceEntityIds));
    }

    [Fact]
    public void RimChamfer_RejectsAPocketInsideTheCorridor()
    {
        var source = Showcase()
            .Replace("Profile stockOutline = Rect2 { center: [0mm, 0mm]; size: [130mm, 90mm] }",
                "Profile stockOutline = RoundedRect2 { center: [0mm, 0mm]; size: [130mm, 90mm]; radius: 8mm }")
            .Replace("EdgeFinish<Chamfer> perimeterBreak { target: stockOutline.Outer; on: Top; distance: 1mm }",
                "EdgeFinish<Chamfer> perimeterBreak { target: stockOutline.Outer; on: Top; distance: 9mm }");
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("ProfileRimFinishCavityInterference", StringComparison.Ordinal));
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
        Assert.Equal(3, inspection.DatumCount); Assert.Equal(17, inspection.DimensionCount);
        Assert.Equal(5, inspection.GeometricToleranceCount); Assert.Equal(3, inspection.AnnotationCount);
        Assert.All(inspection.Items.Where(i => i.Kind != "Annotation"), i => Assert.NotEmpty(i.GeometricFaceEntityIds));
        var display = PmiDisplayProjection.FromStep(result.Value.StepText);
        Assert.Equal(inspection.Items.Count, display.Count);
        Assert.All(display, a => Assert.NotEmpty(a.Lines));
        Assert.Contains(display, a => a.Text.StartsWith("4X Ø6.6 ±0.1", StringComparison.Ordinal));
        Assert.Contains(display, a => a.Text == "Position Ø0.25 (M) | A | B | C");
        Assert.Contains(display, a => a.Text == "2X 16 × 40");
        Assert.Contains(display, a => a.Text == "1 × 45°");
    }

    [Fact]
    public void ProjectedDimensions_ComeFromTheBuiltGeometry()
    {
        var step = Build("""
            Datum A { target: face(-Z) }
            Dimension T { target: stock.Height }
            Dimension L { target: stock.Length }
            Dimension W { target: stock.Width }
            Dimension D { target: hub.Diameter }
            Dimension H { target: hub.Height }
            Location FromB { origin: face(-X) to: hub }
            Location HoleFromC { origin: face(-Y) to: a basic: true }
            """).StepText;
        var items = Step242SemanticPmiInspector.Inspect(step).Items.Where(i => i.Kind == "Dimension").ToDictionary(i => i.Name);
        Assert.Equal(8, items["T"].Value); Assert.Equal("thickness", items["T"].DimensionKind);
        Assert.Equal(60, items["L"].Value); Assert.Equal(40, items["W"].Value);
        Assert.Equal(20, items["D"].Value); Assert.Equal(6, items["H"].Value);
        Assert.Equal(30, items["FromB"].Value); Assert.Equal("location -X", items["FromB"].DimensionKind);
        Assert.Equal(8, items["HoleFromC"].Value); Assert.Equal("location -Y basic", items["HoleFromC"].DimensionKind);
        Assert.All(items.Values, i => Assert.NotEmpty(i.GeometricFaceEntityIds));
    }

    [Fact]
    public void Sizes_AreAlsoWrittenInStandardAp242Form_AndReadOnce()
    {
        var step = Build("HoleDiameter S { target: h dimension: d } Dimension D { target: hub.Diameter } Dimension T { target: stock.Height }").StepText;
        Assert.Contains("DIMENSIONAL_SIZE(", step); Assert.Contains("PLUS_MINUS_TOLERANCE(", step); Assert.Contains("DIMENSIONAL_LOCATION(", step);
        Assert.Equal(3, Step242SemanticPmiInspector.Inspect(step).DimensionCount);
        // A reader that only knows the standard entities finds the same sizes and limits.
        var standardOnly = step.Replace("PROPERTY_DEFINITION('diameter:", "PROPERTY_DEFINITION('x:").Replace("PROPERTY_DEFINITION('dimension:", "PROPERTY_DEFINITION('x:")
            .Replace("'firmament-native:", "'x:");
        var dims = Step242SemanticPmiInspector.Inspect(standardOnly).Items.Where(i => i.Kind == "Dimension").ToArray();
        var hole = Assert.Single(dims, i => i.Value == 8 && i.DimensionKind == "diameter");
        Assert.Equal(.1, hole.TolerancePlus); Assert.Equal(.05, hole.ToleranceMinus);
        Assert.Single(dims, i => i.Value == 20 && i.DimensionKind == "diameter");
        Assert.Single(dims, i => i.Value == 8 && i.DimensionKind == "linear distance");
    }

    [Fact]
    public void HoleCallout_ProjectsItsCounterbore()
    {
        var source = Plate.Replace("Hole<Shaft> a { on: +Z; center: [-22mm, -12mm]; diameter: 5mm; end: ThroughAll }",
            "Hole<Counterbore> a { on: +Z; center: [-22mm, -12mm]; diameter: 5mm; counterboreDiameter: 9mm; counterboreDepth: 3mm; end: ThroughAll }")
            .Replace("PMI_BODY", "HoleCallout C { target: a value: 5mm tolerance: PlusMinus(0.1mm) }");
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var items = Step242SemanticPmiInspector.Inspect(result.Value.StepText).Items;
        Assert.Single(items, i => i.Kind == "Diameter" && i.Value == 5);
        Assert.Single(items, i => i.Name == "C.CounterboreDiameter" && i.Value == 9);
        Assert.Single(items, i => i.Name == "C.CounterboreDepth" && i.Value == 3);
        Assert.Contains(PmiDisplayProjection.FromStep(result.Value.StepText), a => a.Text.Contains("Ø5 ±0.1", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Dimension D { target: stock.Volume }")]
    [InlineData("Dimension D { target: [hub.Diameter, stock.Height] }")]
    [InlineData("Location L { origin: face(+Z) to: hub }")]
    [InlineData("Location L { origin: face(-X) to: nothing }")]
    [InlineData("Location L { origin: face(-X) to: hub basic: true tolerance: PlusMinus(0.1mm) }")]
    public void InvalidProjections_Reject(string pmi)
    {
        var result = FirmamentBuildAndExport.CompileSource(Plate.Replace("PMI_BODY", pmi));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void HoleWall_OnAComposeHole_TargetsItsShaft()
    {
        var step = Build("Datum A { target: face(-Z) } Position P { target: face(h.Wall) tolerance: 0.1mm datumRefs: [A] }").StepText;
        Assert.NotEmpty(Assert.Single(Step242SemanticPmiInspector.Inspect(step).Items, i => i.Kind == "Position").GeometricFaceEntityIds);
    }

    [Fact]
    public void PmiRecordErrors_AreLocatedAndExplained()
    {
        var source = Plate.Replace("PMI_BODY", "HoleDiameter S { target: h dimension: nothere }");
        var report = FirmamentV2.FirmamentV2ValidationReportBuilder.Build(FirmamentV2.FirmamentV2Parser.Parse(source), "plate.firmament", sourceText: source);
        var error = Assert.Single(report.Diagnostics, d => d.Code.StartsWith("firmament-v2-pmi-dimension-type-mismatch:", StringComparison.Ordinal));
        Assert.Contains("toleranced Length Static", error.Message);
        Assert.NotNull(error.Line); Assert.NotEqual(1, error.Line);
    }
}
