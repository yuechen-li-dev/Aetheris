using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

/// <summary>Every palette item is inserted the way a visual editor inserts it and must build.</summary>
public sealed class FirmamentLanguagePaletteTests
{
    private const string Host = "Model PaletteHost {\n    Units: mm\n    Box Plate { Size: [60mm, 40mm, 10mm] }\n}\n";

    public static TheoryData<string> Items()
    {
        var data = new TheoryData<string>();
        foreach (var item in FirmamentLanguagePaletteService.Palette().Items) data.Add(item.Id);
        return data;
    }

    [Theory]
    [MemberData(nameof(Items))]
    public void Item_InsertedIntoAHost_Builds(string itemId)
    {
        var item = FirmamentLanguagePaletteService.Palette().Items.Single(i => i.Id == itemId);
        var operation = item.Placement switch
        {
            "model" => new FirmamentLanguageEditOperation("insertDeclaration") { Parent = "PaletteHost", Source = item.Source, Name = item.NameStem + "1" },
            "feature" => new FirmamentLanguageEditOperation("insertFeature") { Node = "PaletteHost/Plate", Source = item.Source, Name = item.NameStem + "1" },
            _ => throw new InvalidOperationException(item.Placement)
        };
        var host = item.RequiresEmptyModel ? "Model PaletteHost {\n    Units: mm\n}\n" : Host;
        var edit = FirmamentLanguageEditService.Apply(host, "host.firmament", "1", [operation]);
        Assert.Single(edit.Nodes);
        Assert.EndsWith("/" + item.NameStem + "1", edit.Nodes[0], StringComparison.Ordinal);

        var build = FirmamentBuildAndExport.CompileSource(edit.Text);
        Assert.True(build.IsSuccess, $"{item.Id} did not build:\n{edit.Text}\n{string.Join("\n", build.Diagnostics.Select(d => d.Message))}");
        if (item.RequiresEmptyModel)
        {
            var occupied = FirmamentLanguageEditService.Apply(Host, "host.firmament", "1", [operation]);
            var rejected = FirmamentBuildAndExport.CompileSource(occupied.Text);
            Assert.False(rejected.IsSuccess);
            Assert.Contains(rejected.Diagnostics, d => d.Message.Contains("multiple-material-bodies-unsupported", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("hole", "chamfer")]
    [InlineData("counterbore", "fillet")]
    public void FeaturesStackedOnOneBody_Build(string first, string second)
    {
        var items = FirmamentLanguagePaletteService.Palette().Items;
        var operations = new[] { first, second }.Select(id => items.Single(i => i.Id == id))
            .Select(item => new FirmamentLanguageEditOperation("insertFeature") { Node = "PaletteHost/Plate", Source = item.Source, Name = item.NameStem })
            .Select((op, index) => index == 0 ? op : op with { Node = "PaletteHost/Box:Plate" }).ToArray();
        var edit = FirmamentLanguageEditService.Apply(Host, "host.firmament", "1", operations);
        var build = FirmamentBuildAndExport.CompileSource(edit.Text);
        Assert.True(build.IsSuccess, $"{first} + {second} did not build:\n{edit.Text}\n{string.Join("\n", build.Diagnostics.Select(d => d.Message))}");
    }

    [Theory]
    [InlineData("chamfer")]
    [InlineData("fillet")]
    public void CountersinkWithAnEdgeFinish_BuildsThroughThePalette(string finish)
    {
        var items = FirmamentLanguagePaletteService.Palette().Items;
        var edit = FirmamentLanguageEditService.Apply(Host, "host.firmament", "1",
        [
            new("insertFeature") { Node = "PaletteHost/Plate", Source = items.Single(i => i.Id == "countersink").Source },
            new("insertFeature") { Node = "PaletteHost/Box:Plate", Source = items.Single(i => i.Id == finish).Source },
        ]);
        var build = FirmamentBuildAndExport.CompileSource(edit.Text);
        Assert.True(build.IsSuccess, string.Join(';', build.Diagnostics.Select(d => d.Message)));
        Assert.Equal(1, Assert.Single(build.Value.Features!).ConicalFaces);
    }

    [Fact]
    public void Items_HaveUniqueIdsAndOneDeclarationNamedByTheirStem()
    {
        var items = FirmamentLanguagePaletteService.Palette().Items;
        Assert.Equal(items.Count, items.Select(i => i.Id).Distinct().Count());
        Assert.All(items, item =>
        {
            Assert.Contains(item.Placement, new[] { "model", "feature" });
            var outline = FirmamentLanguageOutlineService.Outline(item.Source, "item", "1");
            Assert.Equal(item.NameStem, Assert.Single(outline.Nodes).Name);
        });
    }

    [Fact]
    public void InsertFeature_ReusesTheBodysModifyOrOpensOneAfterTheBody()
    {
        var hole = FirmamentLanguagePaletteService.Palette().Items.Single(i => i.Id == "hole");
        var first = FirmamentLanguageEditService.Apply(Host, "h", "1",
            [new("insertFeature") { Node = "PaletteHost/Plate", Source = hole.Source, Name = "A" }]);
        Assert.Equal("Model PaletteHost {\n    Units: mm\n    Box Plate { Size: [60mm, 40mm, 10mm] }\n    Modify Plate {\n        "
            + hole.Source.Replace("Hole<Shaft> Hole", "Hole<Shaft> A", StringComparison.Ordinal) + "\n    }\n}\n", first.Text);
        Assert.Equal(["PaletteHost/Modify:Plate/A"], first.Nodes);

        var second = FirmamentLanguageEditService.Apply(first.Text, "h", "2",
            [new("insertFeature") { Node = "PaletteHost/Box:Plate", Source = hole.Source, Name = "B" }]);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(second.Text, "Modify Plate"));
        Assert.Contains("End: ThroughAll }\n        Hole<Shaft> B {", second.Text);
        Assert.Equal(["PaletteHost/Modify:Plate/B"], second.Nodes);
    }

    [Fact]
    public void InsertDeclaration_FromMultilineSourceKeepsItsShapeAtTheNewIndent()
    {
        var source = "Box Rib {\n  Size: [1mm, 2mm, 3mm]\n}";
        var edit = FirmamentLanguageEditService.Apply(Host, "h", "1",
            [new("insertDeclaration") { Parent = "PaletteHost", Source = source, Name = "Web" }]);
        Assert.Contains("    Box Plate { Size: [60mm, 40mm, 10mm] }\n    Box Web {\n      Size: [1mm, 2mm, 3mm]\n    }\n}\n", edit.Text);
        Assert.Equal("edit-invalid-declaration", Assert.Throws<FirmamentLanguageEditException>(() =>
            FirmamentLanguageEditService.Apply(Host, "h", "1", [new("insertDeclaration") { Parent = "PaletteHost", Source = "Box A {} Box B {}" }])).Code);
    }
}
