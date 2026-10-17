using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

/// <summary>Parameters and reusable Features for visual editors: the outline shows them, and the edit
/// operations that make them keep the model's geometry.</summary>
public sealed class FirmamentLanguageReuseTests
{
    private static readonly string Bracket = """
        Model WebBracket {
            Units: mm
            Box Plate { Size: [50mm, 40mm, 8mm] }
            Modify Plate {
                Hole<Shaft> CenterMount { On: +Z Center: Point2(0mm, 0mm) Diameter: 8mm End: ThroughAll }
            }
        }

        """.ReplaceLineEndings("\n");

    private static FirmamentLanguageEdit Edit(string source, params FirmamentLanguageEditOperation[] operations) =>
        FirmamentLanguageEditService.Apply(source, "bracket.firmament", "1", operations);

    private static string Step(string source)
    {
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join("\n", build.Diagnostics.Select(d => d.Message)) + "\n" + source);
        return build.Value!.StepText;
    }

    [Fact]
    public void Outline_ShowsParameterValuesFeatureSignaturesAndPlacedCalls()
    {
        var source = """
            Feature MountHole(center: Point2, diameter: Length = 3.4mm) -> Hole<Shaft> {
                return Hole<Shaft> { On: +Z Center: center Diameter: diameter End: ThroughAll }
            }
            Model M {
                Units: mm
                Static Thickness: Length = 8mm
                Box Plate { Size: [50mm, 40mm, Thickness] }
                Modify Plate {
                    MountHole(center: Point2(10mm, 0mm))
                    MountHole(center: Point2(-10mm, 0mm), diameter: 5mm)
                }
            }
            """.ReplaceLineEndings("\n");
        var outline = FirmamentLanguageOutlineService.Outline(source, "m", "1");
        var thickness = outline.Nodes.Single(n => n.Id == "M/Thickness");
        Assert.Equal("Length", thickness.Annotation);
        Assert.Equal("8mm", thickness.Value!.Text);
        Assert.Equal(source.IndexOf("8mm", StringComparison.Ordinal), thickness.Value.ValueSpan.Start);

        var feature = outline.Nodes.Single(n => n.Id == "MountHole");
        Assert.Equal([new FirmamentOutlineParameter("center", "Point2", null), new FirmamentOutlineParameter("diameter", "Length", "3.4mm")], feature.Signature);

        var calls = outline.Nodes.Where(n => n.Call).ToArray();
        Assert.Equal(["M/Modify:Plate/MountHole#1", "M/Modify:Plate/MountHole#2"], calls.Select(c => c.Id));
        Assert.Equal(["center"], calls[0].Fields.Select(f => f.Name));
        Assert.Equal(["center", "diameter"], calls[1].Fields.Select(f => f.Name));
        Assert.Equal("5mm", calls[1].Fields[1].Text);
        Assert.Equal(["M/Modify:Plate/MountHole#1", "M/Modify:Plate/MountHole#2"], outline.Nodes.Single(n => n.Id == "M/Modify:Plate").Children);
        // Each placed call depends on its Feature; the Modify no longer carries the call's use itself.
        Assert.Contains(outline.Edges, e => e.From == calls[0].Id && e.To == "MountHole" && e.Kind == "call");
        Assert.DoesNotContain(outline.Edges, e => e.From == "M/Modify:Plate" && e.To == "MountHole");
        Assert.Contains(outline.Edges, e => e.From == "M/Box:Plate" && e.To == "M/Thickness");
    }

    [Fact]
    public void PromotingADimensionToAParameter_KeepsTheGeometry()
    {
        var result = Edit(Bracket,
            new FirmamentLanguageEditOperation("insertDeclaration") { Parent = "WebBracket", First = true, Source = "Static Thickness: Length = 8mm" },
            new FirmamentLanguageEditOperation("setField") { Node = "WebBracket/Box:Plate", Field = "Size", Value = "[50mm, 40mm, Thickness]" });
        Assert.Contains("    Units: mm\n    Static Thickness: Length = 8mm\n    Box Plate { Size: [50mm, 40mm, Thickness] }", result.Text);
        Assert.Equal(["WebBracket/Thickness", "WebBracket/Box:Plate"], result.Nodes);
        Assert.Equal(Step(Bracket), Step(result.Text));
    }

    [Fact]
    public void ExtractFeature_PlacesACallThatBuildsTheSameGeometry()
    {
        var result = Edit(Bracket, new FirmamentLanguageEditOperation("extractFeature") { Node = "WebBracket/Modify:Plate/CenterMount", Name = "MountHole", Parameters = ["Center", "Diameter"] });
        Assert.Equal("""
            Feature MountHole(center: Point2 = Point2(0mm, 0mm), diameter: Length = 8mm) -> Hole<Shaft> {
                return Hole<Shaft> { On: +Z Center: center Diameter: diameter End: ThroughAll }
            }

            Model WebBracket {
                Units: mm
                Box Plate { Size: [50mm, 40mm, 8mm] }
                Modify Plate {
                    Feature CenterMount = MountHole(center: Point2(0mm, 0mm), diameter: 8mm)
                }
            }

            """.ReplaceLineEndings("\n"), result.Text);
        Assert.Equal(["WebBracket/Modify:Plate/CenterMount"], result.Nodes);
        Assert.Equal(Step(Bracket), Step(result.Text));
    }

    [Fact]
    public void PlacedCalls_CanBeAddedMovedAndRemovedLikeFeatures()
    {
        var extracted = Edit(Bracket, new FirmamentLanguageEditOperation("extractFeature") { Node = "WebBracket/Modify:Plate/CenterMount", Name = "MountHole", Parameters = ["Center"] }).Text;
        var two = Edit(extracted,
            new FirmamentLanguageEditOperation("insertFeature") { Node = "WebBracket/Box:Plate", Source = "MountHole(center: Point2(15mm, 0mm))" },
            new FirmamentLanguageEditOperation("insertFeature") { Node = "WebBracket/Box:Plate", Source = "EdgeFinish TopBreak { Face: +Z Target: Boundary Kind: Chamfer Distance: 1mm }" });
        Assert.Equal(["WebBracket/Modify:Plate/mountHole", "WebBracket/Modify:Plate/TopBreak"], two.Nodes);
        Assert.Contains("        Feature CenterMount = MountHole(center: Point2(0mm, 0mm))\n        Feature mountHole = MountHole(center: Point2(15mm, 0mm))\n        EdgeFinish TopBreak", two.Text);
        Step(two.Text);

        var moved = Edit(two.Text, new FirmamentLanguageEditOperation("moveDeclaration") { Node = "WebBracket/Modify:Plate/TopBreak", First = true });
        Assert.Contains("    Modify Plate {\n        EdgeFinish TopBreak { Face: +Z Target: Boundary Kind: Chamfer Distance: 1mm }\n        Feature CenterMount = MountHole(center: Point2(0mm, 0mm))", moved.Text);
        Assert.Equal(["WebBracket/Modify:Plate/TopBreak"], moved.Nodes);
        var down = Edit(moved.Text, new FirmamentLanguageEditOperation("moveDeclaration") { Node = "WebBracket/Modify:Plate/CenterMount", After = "WebBracket/Modify:Plate/mountHole" });
        Assert.Contains("Feature mountHole = MountHole(center: Point2(15mm, 0mm))\n        Feature CenterMount = MountHole(center: Point2(0mm, 0mm))\n    }", down.Text);

        // A placed call's arguments are its fields.
        var argued = Edit(down.Text,
            new FirmamentLanguageEditOperation("setField") { Node = "WebBracket/Modify:Plate/mountHole", Field = "center", Value = "Point2(12mm, 0mm)" },
            new FirmamentLanguageEditOperation("setField") { Node = "WebBracket/Modify:Plate/mountHole", Field = "diameter", Value = "4mm" });
        Assert.Contains("MountHole(center: Point2(12mm, 0mm), diameter: 4mm)", argued.Text);
        Assert.Contains("MountHole(center: Point2(12mm, 0mm))", Edit(argued.Text,
            new FirmamentLanguageEditOperation("removeField") { Node = "WebBracket/Modify:Plate/mountHole", Field = "diameter" }).Text);
        Assert.Contains("MountHole(diameter: 4mm)", Edit(argued.Text,
            new FirmamentLanguageEditOperation("removeField") { Node = "WebBracket/Modify:Plate/mountHole", Field = "center" }).Text);

        var removed = Edit(down.Text, new FirmamentLanguageEditOperation("removeDeclaration") { Node = "WebBracket/Modify:Plate/CenterMount" });
        Assert.DoesNotContain("Point2(0mm, 0mm))\n", removed.Text[removed.Text.IndexOf("Modify", StringComparison.Ordinal)..]);
        Step(removed.Text);
    }

    [Fact]
    public void ExtractFeature_RefusesWhatCannotBecomeAParameterOrAFeature()
    {
        Assert.Equal("edit-extract-parameter", Assert.Throws<FirmamentLanguageEditException>(() =>
            Edit(Bracket, new FirmamentLanguageEditOperation("extractFeature") { Node = "WebBracket/Modify:Plate/CenterMount", Name = "MountHole", Parameters = ["On"] })).Code);
        Assert.Equal("edit-extract-shape", Assert.Throws<FirmamentLanguageEditException>(() =>
            Edit(Bracket, new FirmamentLanguageEditOperation("extractFeature") { Node = "WebBracket/Modify:Plate", Name = "Holes" })).Code);
        Assert.Equal("edit-extract-scope", Assert.Throws<FirmamentLanguageEditException>(() =>
            Edit(Bracket, new FirmamentLanguageEditOperation("extractFeature") { Node = "WebBracket", Name = "Whole" })).Code);
        Assert.Equal("edit-name-taken", Assert.Throws<FirmamentLanguageEditException>(() =>
            Edit(Bracket, new FirmamentLanguageEditOperation("extractFeature") { Node = "WebBracket/Modify:Plate/CenterMount", Name = "WebBracket" })).Code);
        Assert.Equal("edit-call-scope", Assert.Throws<FirmamentLanguageEditException>(() =>
            Edit(Bracket, new FirmamentLanguageEditOperation("insertDeclaration") { Parent = null, Source = "MountHole(center: Point2(0mm, 0mm))" })).Code);
    }

    [Fact]
    public void ExtractFeature_WorksForOtherFeatureKindsAndAngles()
    {
        var source = """
            Model M {
                Units: mm
                Box Plate { Size: [60mm, 40mm, 10mm] }
                Modify Plate {
                    Hole<Countersink> Sink { On: +Z Center: Point2(0mm, 0mm) Diameter: 6mm CountersinkDiameter: 12mm CountersinkAngle: 90deg End: ThroughAll }
                    EdgeFinish Break { Face: +Z Target: Boundary Kind: Chamfer Distance: 1mm }
                }
            }

            """.ReplaceLineEndings("\n");
        var sink = Edit(source, new FirmamentLanguageEditOperation("extractFeature") { Node = "M/Modify:Plate/Sink", Name = "FlatHead", Parameters = ["Center", "CountersinkAngle"] });
        Assert.Contains("Feature FlatHead(center: Point2 = Point2(0mm, 0mm), countersinkAngle: Angle = 90deg) -> Hole<Countersink> {", sink.Text);
        var both = Edit(sink.Text, new FirmamentLanguageEditOperation("extractFeature") { Node = "M/Modify:Plate/Break", Name = "EdgeBreak", Parameters = ["Distance"] });
        Assert.Equal(Step(source), Step(both.Text));
    }

    [Fact]
    public void ExtractFeature_PreservesTheNameAndItsPmiReferences()
    {
        var pmi = """
            Model A3 {
                Units: mm
                Box Plate { Size: [100mm, 70mm, 10mm] }
                Modify Plate {
                    Hole<Shaft> Mount { On: +Z Center: Point2(-35mm, 20mm) Diameter: 8mm End: ThroughAll }
                }
                Pmi {
                    HoleDiameter MountDiameter { Target: Mount Value: 8mm }
                }
            }

            """.ReplaceLineEndings("\n");
        var extracted = Edit(pmi, new FirmamentLanguageEditOperation("extractFeature") { Node = "A3/Modify:Plate/Mount", Name = "MountHole", Parameters = ["Center"] });
        Assert.Contains("Feature Mount = MountHole(", extracted.Text);
        Assert.Contains("Target: Mount", extracted.Text);
        Assert.Equal(Step(pmi), Step(extracted.Text));
        Assert.Empty(extracted.Warnings);

    }
}
