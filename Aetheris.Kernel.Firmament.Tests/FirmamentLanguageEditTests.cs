using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentLanguageEditTests
{
    private const string Bracket = """
        // Mounting plate
        Model WebBracket {
            Units: mm
            Box Plate { Size: [50mm, 40mm, 8mm] }
            Modify Plate {
                Hole<Shaft> CenterMount { On: +Z Center: Point2(0mm, 0mm) Diameter: 8mm End: ThroughAll }
            }
        }

        """;

    private static FirmamentLanguageEdit Edit(string source, params FirmamentLanguageEditOperation[] operations) =>
        FirmamentLanguageEditService.Apply(source, "bracket.firmament", "1", operations);

    private static FirmamentLanguageEditOperation Op(string kind) => new(kind);

    private static string Fixture(string relative) =>
        File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(relative));

    [Fact]
    public void SetField_ReplacesAnAuthoredValue()
    {
        var result = Edit(Bracket, Op("setField") with { Node = "WebBracket/Box:Plate", Field = "Size", Value = "[60mm, 40mm, 8mm]" });
        Assert.Equal(Bracket.Replace("[50mm, 40mm, 8mm]", "[60mm, 40mm, 8mm]"), result.Text);
        Assert.Equal(["WebBracket/Box:Plate"], result.Nodes);
        Assert.Equal(new FirmamentTextEdit(Bracket.IndexOf("50mm", StringComparison.Ordinal), 1, "6"), result.Edit);
    }

    [Fact]
    public void SetField_AddsToAOneLineBodyOnTheSameLine()
    {
        var result = Edit(Bracket, Op("setField") with { Node = "WebBracket/Modify:Plate/CenterMount", Field = "CounterboreDepth", Value = "2mm" });
        Assert.Contains("Diameter: 8mm End: ThroughAll CounterboreDepth: 2mm }", result.Text);
    }

    [Fact]
    public void SetField_AddsAfterTheLastFieldOfAMultilineBodyAtItsIndent()
    {
        var result = Edit(Bracket, Op("setField") with { Node = "WebBracket", Field = "Tolerance", Value = "0.01mm" });
        Assert.Contains("    Units: mm\n    Tolerance: 0.01mm\n    Box Plate", result.Text);
    }

    [Fact]
    public void SetField_OpensABodyAndFieldsLeadDeclarations()
    {
        var bare = "Model M {\n  Box A;\n  Modify A {\n    Hole<Shaft> H { Diameter: 3mm }\n  }\n}\n";
        Assert.Contains("Box A { Size: [1mm, 1mm, 1mm] }\n",
            Edit(bare, Op("setField") with { Node = "M/Box:A", Field = "Size", Value = "[1mm, 1mm, 1mm]" }).Text);
        Assert.Contains("Modify A {\n    Note: Draft\n    Hole<Shaft> H",
            Edit(bare, Op("setField") with { Node = "M/Modify:A", Field = "Note", Value = "Draft" }).Text);
    }

    [Fact]
    public void RemoveField_TakesItsLineOrJustItsPlaceOnTheLine()
    {
        Assert.Equal(Bracket.Replace("    Units: mm\n", ""),
            Edit(Bracket, Op("removeField") with { Node = "WebBracket", Field = "Units" }).Text);
        Assert.Contains("{ On: +Z Diameter: 8mm End: ThroughAll }",
            Edit(Bracket, Op("removeField") with { Node = "WebBracket/Modify:Plate/CenterMount", Field = "Center" }).Text);
        Assert.Contains("Diameter: 8mm }",
            Edit(Bracket, Op("removeField") with { Node = "WebBracket/Modify:Plate/CenterMount", Field = "End" }).Text);
    }

    [Fact]
    public void InsertDeclaration_WritesHouseStyleAndReturnsTheNewId()
    {
        var spec = new FirmamentDeclarationSpec("Box", Name: "Rib", Fields: [new("Size", "[4mm, 40mm, 12mm]")]);
        var end = Edit(Bracket, Op("insertDeclaration") with { Parent = "WebBracket", Declaration = spec });
        Assert.Contains("    }\n    Box Rib { Size: [4mm, 40mm, 12mm] }\n}", end.Text);
        Assert.Equal(["WebBracket/Rib"], end.Nodes);

        var after = Edit(Bracket, Op("insertDeclaration") with { Parent = "WebBracket", After = "WebBracket/Box:Plate", Declaration = spec });
        Assert.Contains("Box Plate { Size: [50mm, 40mm, 8mm] }\n    Box Rib { Size: [4mm, 40mm, 12mm] }\n    Modify Plate", after.Text);

        var first = Edit(Bracket, Op("insertDeclaration") with { Parent = "WebBracket", First = true, Declaration = spec });
        Assert.Contains("Units: mm\n    Box Rib { Size: [4mm, 40mm, 12mm] }\n    Box Plate", first.Text);
    }

    [Fact]
    public void InsertDeclaration_IntoAOneLineBodyStaysOnTheLine()
    {
        var source = "Model M {\n  Box A { Size: [1mm, 1mm, 1mm] }\n  Modify A { Hole<Shaft> H { Diameter: 1mm } }\n}\n";
        var result = Edit(source, Op("insertDeclaration") with { Parent = "M/Modify:A",
            Declaration = new("Hole", Variant: "Shaft", Name: "J", Fields: [new("Diameter", "2mm")]) });
        Assert.Contains("Modify A { Hole<Shaft> H { Diameter: 1mm } Hole<Shaft> J { Diameter: 2mm } }", result.Text);
        Assert.Equal(["M/Modify:A/J"], result.Nodes);
    }

    [Fact]
    public void InsertDeclaration_BreaksLongDeclarationsOverLines()
    {
        var fields = Enumerable.Range(1, 6).Select(i => new FirmamentFieldSpec($"Field{i}", $"{i * 1000}mm")).ToArray();
        var result = Edit(Bracket, Op("insertDeclaration") with { Parent = "WebBracket", Declaration = new("Thing", Name: "Long", Fields: fields) });
        Assert.Contains("    Thing Long {\n        Field1: 1000mm\n", result.Text);
        Assert.Contains("        Field6: 6000mm\n    }\n}", result.Text);
    }

    [Fact]
    public void RemoveDeclaration_ReportsUsesThatStopResolving()
    {
        var result = Edit(Bracket, Op("removeDeclaration") with { Node = "WebBracket/Box:Plate" });
        Assert.DoesNotContain("Box Plate", result.Text);
        Assert.Contains("    Units: mm\n    Modify Plate", result.Text);
        Assert.Contains(result.Warnings, w => w.Contains("WebBracket/Modify:Plate", StringComparison.Ordinal));
    }

    [Fact]
    public void RenameDeclaration_UpdatesEveryUseInTheDocument()
    {
        var source = Fixture("fixtures/Regression/FrontendX4/chimera.baseline.firmament");
        var result = Edit(source, Op("renameDeclaration") with { Node = "MotorMount/Stock", Name = "Blank" });
        var before = FirmamentLanguageOutlineService.Outline(source, "c", "1");
        var after = FirmamentLanguageOutlineService.Outline(result.Text, "c", "2");
        Assert.Equal(["MotorMount/Blank"], result.Nodes);
        // The same uses, now pointing at the new name.
        Assert.Equal(before.Edges.Count, after.Edges.Count);
        Assert.Contains(after.Edges, e => e.To == "MotorMount/Blank" && e.Via == "field:Profile");
        Assert.Contains(after.Edges, e => e.To == "MotorMount/Blank/Outer" && e.Via == "field:Target");
        Assert.Contains("Target: Blank.Outer", result.Text);
        Assert.Empty(after.Externals.Where(x => x.Name is "Stock" or "Blank"));
    }

    [Fact]
    public void RenameDeclaration_RenamesABodyAndTheModifyThatTargetsIt()
    {
        var result = Edit(Bracket, Op("renameDeclaration") with { Node = "WebBracket/Box:Plate", Name = "Base" });
        Assert.Contains("Box Base { Size", result.Text);
        Assert.Contains("Modify Base {", result.Text);
    }

    [Fact]
    public void RenameDeclaration_RefusesATakenNameAndABadName()
    {
        var source = "Model M {\n  Box A { Size: [1mm, 1mm, 1mm] }\n  Box B { Size: [1mm, 1mm, 1mm] }\n}\n";
        Assert.Equal("edit-name-taken", Assert.Throws<FirmamentLanguageEditException>(() =>
            Edit(source, Op("renameDeclaration") with { Node = "M/A", Name = "B" })).Code);
        Assert.Equal("edit-invalid-name", Assert.Throws<FirmamentLanguageEditException>(() =>
            Edit(source, Op("renameDeclaration") with { Node = "M/A", Name = "2nd" })).Code);
    }

    [Fact]
    public void MoveDeclaration_ReordersSiblingsAndKeepsTheirText()
    {
        var source = "Model M {\n  Box A { Size: [1mm, 1mm, 1mm] } // first\n  Box B { Size: [2mm, 2mm, 2mm] }\n  Box C { Size: [3mm, 3mm, 3mm] }\n}\n";
        var down = Edit(source, Op("moveDeclaration") with { Node = "M/A", After = "M/B" });
        // A trailing comment travels with its declaration.
        Assert.Equal("Model M {\n  Box B { Size: [2mm, 2mm, 2mm] }\n  Box A { Size: [1mm, 1mm, 1mm] } // first\n  Box C { Size: [3mm, 3mm, 3mm] }\n}\n", down.Text);
        var top = Edit(source, Op("moveDeclaration") with { Node = "M/C", First = true });
        Assert.StartsWith("Model M {\n  Box C { Size: [3mm, 3mm, 3mm] }\n  Box A", top.Text);
        // Moving `Box Base` past `Modify Base` changes the Modify's id (no longer a shared name): the anchor still holds.
        var renamed = Edit(Bracket, Op("moveDeclaration") with { Node = "WebBracket/Box:Plate", After = "WebBracket/Modify:Plate" });
        Assert.Contains("    }\n    Box Plate { Size: [50mm, 40mm, 8mm] }\n}", renamed.Text);
        Assert.Equal(["WebBracket/Box:Plate"], renamed.Nodes);
        Assert.Equal("edit-move-scope", Assert.Throws<FirmamentLanguageEditException>(() =>
            Edit(Bracket, Op("moveDeclaration") with { Node = "WebBracket/Box:Plate", After = "WebBracket/Modify:Plate/CenterMount" })).Code);
    }

    [Fact]
    public void Edits_ThatWouldNotParseOrTargetNothing_AreRefused()
    {
        Assert.Equal("edit-invalid-value", Assert.Throws<FirmamentLanguageEditException>(() =>
            Edit(Bracket, Op("setField") with { Node = "WebBracket/Box:Plate", Field = "Size", Value = "[1mm] }" })).Code);
        Assert.Equal("edit-invalid-value", Assert.Throws<FirmamentLanguageEditException>(() =>
            Edit(Bracket, Op("setField") with { Node = "WebBracket/Box:Plate", Field = "Size", Value = "1mm Extra: 2mm" })).Code);
        Assert.Equal("edit-node-not-found", Assert.Throws<FirmamentLanguageEditException>(() =>
            Edit(Bracket, Op("removeField") with { Node = "WebBracket/Gone", Field = "Size" })).Code);
        Assert.Equal("edit-field-not-found", Assert.Throws<FirmamentLanguageEditException>(() =>
            Edit(Bracket, Op("removeField") with { Node = "WebBracket/Box:Plate", Field = "Depth" })).Code);
        Assert.Equal("edit-unknown-operation", Assert.Throws<FirmamentLanguageEditException>(() =>
            Edit(Bracket, Op("explode") with { Node = "WebBracket" })).Code);
    }

    [Fact]
    public void Operations_ApplyInOrderAndReportOneReplacement()
    {
        var result = Edit(Bracket,
            Op("insertDeclaration") with { Parent = "WebBracket", Declaration = new("Box", Name: "Rib", Fields: [new("Size", "[1mm, 1mm, 1mm]")]) },
            Op("setField") with { Node = "WebBracket/Rib", Field = "Size", Value = "[2mm, 2mm, 2mm]" });
        Assert.Equal(["WebBracket/Rib", "WebBracket/Rib"], result.Nodes);
        Assert.Equal(result.Text, Bracket[..result.Edit.Start] + result.Edit.Replacement + Bracket[(result.Edit.Start + result.Edit.Length)..]);
        Assert.Contains("Box Rib { Size: [2mm, 2mm, 2mm] }", result.Text);
    }

    /// <summary>Over the canonical corpus: renaming any named declaration and renaming it back gives
    /// the original bytes, and rewriting any field to its own value changes nothing.</summary>
    [Fact]
    public void CanonicalCorpus_RenameRoundTripsAndSelfAssignmentAreIdentity()
    {
        var root = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical");
        var files = Directory.GetFiles(root, "*.firmament", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        var renamed = 0;
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            if (!FirmamentSyntaxParser.Parse(source).IsSuccess) continue;
            var outline = FirmamentLanguageOutlineService.Outline(source, "f", "1");
            // A name two same-kind siblings share cannot be renamed back onto: that refusal is the point.
            bool Shared(FirmamentOutlineNode n) => outline.Nodes.Count(m => m.ParentId == n.ParentId && m.Kind == n.Kind && m.Name == n.Name) > 1;
            foreach (var node in outline.Nodes.Where(n => n.Name is not null && !Shared(n)).Take(6))
            {
                FirmamentLanguageEdit there;
                try { there = Edit(source, Op("renameDeclaration") with { Node = node.Id, Name = node.Name + "Zq" }); }
                catch (FirmamentLanguageEditException e) when (e.Code is "edit-name-taken" or "edit-invalid-name") { continue; }
                var id = there.Nodes.Single();
                // Every use followed the declaration: same number of resolved uses, no new unresolved names.
                var renamedOutline = FirmamentLanguageOutlineService.Outline(there.Text, "f", "2");
                var retargets = node.Kind is "Modify" or "modify" or "Recognize" || node.Modifiers.Contains("Assert");
                Assert.True(retargets || outline.Edges.Count == renamedOutline.Edges.Count && outline.Externals.Count == renamedOutline.Externals.Count,
                    $"{file}: renaming {node.Id} changed what resolves ({outline.Edges.Count}/{outline.Externals.Count} -> {renamedOutline.Edges.Count}/{renamedOutline.Externals.Count})");
                var back = Edit(there.Text, Op("renameDeclaration") with { Node = id, Name = node.Name });
                Assert.True(source == back.Text, $"{file}: renaming {node.Id} did not round-trip");
                renamed++;
            }
            foreach (var node in outline.Nodes.Where(n => n.Fields.Count > 0).Take(4))
            foreach (var field in node.Fields.Where(f => node.Fields.Count(g => g.Name == f.Name) == 1).Take(3))
            {
                if (field.Text.Contains('\n')) continue;
                var same = Edit(source, Op("setField") with { Node = node.Id, Field = field.Name, Value = field.Text });
                Assert.True(source == same.Text, $"{file}: {node.Id}.{field.Name} self-assignment changed the source");
            }
        }
        Assert.True(renamed > 100, $"only {renamed} renames were exercised");
    }
}
