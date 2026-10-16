using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentLanguageOutlineTests
{
    private static string Fixture(string relative) =>
        File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(relative));

    private static HashSet<string> EdgeSet(FirmamentLanguageOutline outline) =>
        outline.Edges.Select(e => $"{e.From} -{e.Kind}/{e.Via}-> {e.To}{(e.Member is null ? "" : "." + e.Member)}").ToHashSet();

    [Fact]
    public void ChimeraBaseline_IsTheMotorMountDataflowGraph()
    {
        var outline = FirmamentLanguageOutlineService.Outline(
            Fixture("fixtures/Regression/FrontendX4/chimera.baseline.firmament"), "chimera.baseline.firmament", "7");

        Assert.Equal(FirmamentLanguageOutlineService.Version, outline.Version);
        Assert.Equal("7", outline.Revision);
        Assert.Empty(outline.Diagnostics);
        Assert.Equal(21, outline.Nodes.Count);
        Assert.Equal(
        [
            "MotorMount/Layout/Outline -reference/field:Vertices-> OutlineVertices",
            "MotorMount/Stock -reference/clause:Using-> MotorMount/Layout",
            "MotorMount/Stock/Outer -reference/body-> MotorMount/Layout/Outline",
            "MotorMount/SlotProfile -reference/clause:Using-> MotorMount/Layout",
            "MotorMount/SlotProfile/Outer -reference/body-> MotorMount/Layout/CableSlot",
            "MotorMount/Compose:Body/Plate -reference/field:Profile-> MotorMount/Stock",
            "MotorMount/Compose:Body/Cable -reference/field:Profile-> MotorMount/SlotProfile",
            "MotorMount/Modify:Body -modifies/target-> MotorMount/Compose:Body",
            "MotorMount/Modify:Body/Motor -reference/clause:Over-> MotorBolts",
            "MotorMount/Modify:Body/Motor -call/body-> M3Clearance",
            "MotorMount/Modify:Body/Frame -reference/clause:Over-> FrameHoles",
            "MotorMount/Modify:Body/Frame -call/body-> M5Clearance",
            "MotorMount/Modify:Body/TopBreak -reference/field:Target-> MotorMount/Stock/Outer",
        ], outline.Edges.Select(e => $"{e.From} -{e.Kind}/{e.Via}-> {e.To}{(e.Member is null ? "" : "." + e.Member)}"));

        var pilot = Assert.Single(outline.Nodes, n => n.Id == "MotorMount/Modify:Body/Pilot");
        Assert.Equal(("Hole", "Shaft", "Hole"), (pilot.Kind, pilot.Variant, pilot.SchemaId));
        Assert.Equal(["On", "Center", "Diameter", "End"], pilot.Fields.Select(f => f.Name));
        Assert.Equal("22mm", Assert.Single(pilot.Fields, f => f.Name == "Diameter").Text);
        var feature = Assert.Single(outline.Nodes, n => n.Id == "M3Clearance");
        Assert.Equal(["center"], feature.Parameters);
        Assert.Equal(["MotorMount/Modify:Body/Pilot", "MotorMount/Modify:Body/Motor", "MotorMount/Modify:Body/Frame", "MotorMount/Modify:Body/TopBreak"],
            Assert.Single(outline.Nodes, n => n.Id == "MotorMount/Modify:Body").Children);
    }

    [Fact]
    public void Spans_PointAtTheAuthoredText()
    {
        var source = Fixture("fixtures/Regression/FrontendX4/chimera.baseline.firmament");
        var outline = FirmamentLanguageOutlineService.Outline(source, "c.firmament", "1");
        var pilot = Assert.Single(outline.Nodes, n => n.Id == "MotorMount/Modify:Body/Pilot");
        Assert.StartsWith("Hole<Shaft> Pilot {", source.Substring(pilot.Span.Start, pilot.Span.Length));
        Assert.Equal("Pilot", source.Substring(pilot.NameSpan!.Start, pilot.NameSpan.Length));
        var diameter = Assert.Single(pilot.Fields, f => f.Name == "Diameter");
        Assert.Equal("22mm", source.Substring(diameter.ValueSpan.Start, diameter.ValueSpan.Length));
        var target = Assert.Single(outline.Edges, e => e.Via == "field:Target");
        Assert.Equal("Stock.Outer", source.Substring(target.Span.Start, target.Span.Length));
    }

    [Fact]
    public void Ids_DependOnNamesNotOffsets()
    {
        var source = Fixture("fixtures/Regression/FrontendX4/chimera.baseline.firmament");
        var before = FirmamentLanguageOutlineService.Outline(source, "c.firmament", "1");
        var shifted = FirmamentLanguageOutlineService.Outline("// a comment\n\n" + source.Replace("Diameter: 22mm", "Diameter: 24mm"), "c.firmament", "2");
        Assert.Equal(before.Nodes.Select(n => n.Id), shifted.Nodes.Select(n => n.Id));
        Assert.Equal(EdgeSet(before), EdgeSet(shifted));
    }

    [Fact]
    public void RepeatedNames_AreQualifiedByKindAndOrdinalWithoutRenamingTheFirst()
    {
        const string one = "Model M {\n  Box Body { Size: [10mm, 10mm, 2mm] }\n  Modify Body { Hole<Shaft> A { On: +Z; Center: Point2(0mm, 0mm); Diameter: 2mm; End: ThroughAll } }\n}\n";
        const string two = "Model M {\n  Box Body { Size: [10mm, 10mm, 2mm] }\n  Modify Body { Hole<Shaft> A { On: +Z; Center: Point2(0mm, 0mm); Diameter: 2mm; End: ThroughAll } }\n  Modify Body { Hole<Shaft> B { On: +Z; Center: Point2(3mm, 0mm); Diameter: 2mm; End: ThroughAll } }\n}\n";
        var a = FirmamentLanguageOutlineService.Outline(one, "a", "1").Nodes.Select(n => n.Id).ToArray();
        var b = FirmamentLanguageOutlineService.Outline(two, "b", "1").Nodes.Select(n => n.Id).ToArray();
        Assert.Contains("M/Box:Body", a);
        Assert.Contains("M/Modify:Body", a);
        Assert.Contains("M/Modify:Body#1/A", b);
        Assert.Contains("M/Modify:Body#2/B", b);
        var edges = FirmamentLanguageOutlineService.Outline(two, "b", "1").Edges.Where(e => e.Kind == "modifies").ToArray();
        Assert.All(edges, e => Assert.Equal("M/Box:Body", e.To));
        Assert.Equal(2, edges.Length);
    }

    [Fact]
    public void VocabularyValues_LoopBindingsAndSelfNames_AreNotDependencies()
    {
        var outline = FirmamentLanguageOutlineService.Outline(
            Fixture("fixtures/Regression/FrontendX4/cases/h00-concept-grid-struct-root.firmament"), "h00", "1");
        var edges = EdgeSet(outline);
        Assert.Contains("GridPlate/Box:Base -reference/field:Bounds-> GridPlate/Design.Bounds", edges);
        Assert.Contains("GridPlate/Modify:Base -modifies/target-> GridPlate/Box:Base", edges);
        Assert.Contains("GridPlate/Modify:Base/Mounts -reference/field:Source-> GridPlate/Design.Points", edges);
        Assert.Contains("GridPlate/Modify:Base/Mounts/Item -reference/field:On-> GridPlate/Box:Base.Top", edges);
        // `Center: Item` names the Pattern's own instance; it is the loop binding, not a wire.
        Assert.DoesNotContain(outline.Edges, e => e.From.EndsWith("/Item", StringComparison.Ordinal) && e.Via == "field:Center");

        var chimera = FirmamentLanguageOutlineService.Outline(
            Fixture("fixtures/Regression/FrontendX4/chimera.baseline.firmament"), "c", "1");
        // `Role: Stock` is vocabulary even though a Profile is named Stock.
        Assert.DoesNotContain(chimera.Edges, e => e.Via == "field:Role");
        // `p => M3Clearance(center: p)`: p is the Pattern's binding.
        Assert.DoesNotContain(chimera.Externals, x => x.Name == "p");
    }

    [Fact]
    public void Templates_LinkSpecializationsArgumentsAndRecordTypes()
    {
        const string source = """
            Record SpacerSpec { OuterDiameter: Length; Height: Length }
            Template<Spec: SpacerSpec> Struct Spacer {
              Box Body { Size: [Spec.OuterDiameter, Spec.OuterDiameter, Spec.Height] }
            }
            Static Short: SpacerSpec = SpacerSpec { OuterDiameter: 10mm; Height: 3mm }
            Model ShortSpacer { Units: mm; Struct Part = Spacer<Spec: Short> }
            """;
        var edges = EdgeSet(FirmamentLanguageOutlineService.Outline(source, "t", "1"));
        Assert.Contains("Spacer -reference/argument:Spec-> SpacerSpec", edges);
        Assert.Contains("Short -reference/annotation-> SpacerSpec", edges);
        Assert.Contains("ShortSpacer/Part -specialization/initializer-> Spacer", edges);
        Assert.Contains("ShortSpacer/Part -reference/initializer-> Short", edges);
    }

    [Fact]
    public void ProjectOutline_ResolvesIncludedDefinitionsAcrossDocuments()
    {
        var root = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas");
        var documents = Directory.GetFiles(root, "*.firmament").ToDictionary(Path.GetFileName, File.ReadAllText);
        var outline = FirmamentLanguageOutlineService.Outline(new FirmamentProjectSnapshot("atlas-industrial.firmament", documents!), "atlas-industrial.firmament", "1");

        var cross = outline.Edges.Where(e => e.ToDocument is not null).Select(e => $"{e.From} -> {e.ToDocument}::{e.To}").ToHashSet();
        Assert.Contains("AtlasIndustrial/AtlasIndustrial/Base -> base.firmament::AtlasBase", cross);
        Assert.Contains("AtlasIndustrial/AtlasIndustrial/Gripper -> gripper.firmament::GripperBody", cross);
        Assert.Contains("AtlasIndustrial/AtlasIndustrial/Gripper -> materials.firmament::BlackPaint", cross);
        Assert.Contains(outline.Edges, e => e.From == "AtlasIndustrial/Shoulder" && e.Via == "field:A"
            && e.To == "AtlasIndustrial/AtlasIndustrial/Base" && e.Member == "Hinge");
        Assert.True(Assert.Single(outline.Nodes, n => n.Id == "AtlasIndustrial/AtlasIndustrial/Gripper").Occurrence);
        Assert.Empty(outline.Externals);
    }

    [Fact]
    public void EveryCanonicalFixture_HasUniqueIdsAndOnlyEdgesBetweenItsNodes()
    {
        var root = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical");
        var files = Directory.GetFiles(root, "*.firm*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".firmament", StringComparison.Ordinal) || f.EndsWith(".firmasm", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var outline = FirmamentLanguageOutlineService.Outline(File.ReadAllText(file), Path.GetFileName(file), "1");
            var ids = outline.Nodes.Select(n => n.Id).ToArray();
            Assert.True(ids.Length == ids.Distinct(StringComparer.Ordinal).Count(), $"duplicate ids in {file}");
            var known = ids.ToHashSet(StringComparer.Ordinal);
            Assert.All(outline.Edges, e => Assert.True(known.Contains(e.From) && known.Contains(e.To), $"{file}: {e.From} -> {e.To}"));
            Assert.All(outline.Nodes.Where(n => n.ParentId is not null), n => Assert.Contains(n.ParentId!, known));
        }
    }
}
