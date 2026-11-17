using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CompositionLanguageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConcreteWireFormsUseTheirOwningValidator(bool standalone)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/WireForm/paperclip.firmament");
        if (standalone)
        {
            var syntax = FirmamentV2.Syntax.FirmamentSyntaxParser.Parse(source);
            var wire = Assert.Single(FirmamentV2.Syntax.FirmamentSyntaxTraversal.Declarations(syntax.Document),
                d => d.Type.Name.Text == "WireForm");
            source = "schema WireForm\n\n" + source.Substring(wire.Span.Start, wire.Span.Length);
        }

        Assert.Empty(FirmamentLanguageAnalysisService.Analyze(source, "paperclip.firmament", "1").Diagnostics);
        var project = new FirmamentProjectSnapshot("paperclip.firmament", new Dictionary<string, string> { ["paperclip.firmament"] = source });
        Assert.Empty(FirmamentLanguageAnalysisService.Analyze(project, "paperclip.firmament", "1").Diagnostics);

        var invalid = source.ReplaceRequired("Diameter: 1mm", "Diameter: 0mm");
        Assert.Contains(FirmamentLanguageAnalysisService.Analyze(invalid, "paperclip.firmament", "2").Diagnostics,
            d => d.Severity == "error" && d.Message.Contains("wireform-diameter-invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void DeclarationOnlyModulesStillRequireProjectContext()
    {
        var result = FirmamentLanguageAnalysisService.Analyze("Material Steel { Density: 7850 }", "materials.firmament", "1");
        Assert.Contains(result.Diagnostics, d => d.Code == "firmament-language-project-context-required");
    }

    [Theory]
    [InlineData("WireRoute Lead {\n Min", "WireRoute", "MinimumBendRadius")]
    [InlineData("WireRoute Lead { Path { Follow Groove {\n Cur", "Follow", "Curve")]
    [InlineData("Section Station { Frame: Plane { Origin: [0mm,0mm,0mm]; }\n Pro", "Section", "Profile")]
    [InlineData("Concept Struct Board { Points<Point2> Frets { Series {\n Pos", "Series", "Position")]
    [InlineData("Appearance Paint {\n Rou", "Appearance", "Roughness")]
    [InlineData("Material Steel {\n App", "Material", "Appearance")]
    [InlineData("Placement {\n Fro", "Placement", "From")]
    public void CompositionDraftsOfferImplementedOwnerFields(string source, string context, string field)
    {
        var completion = FirmamentLanguageService.Complete(source, "draft.firmament", "1", source.Length);
        Assert.Equal(context, completion.Context);
        Assert.Contains(completion.Fields, f => f.Name == field);
    }

    [Fact]
    public void SnapshotNavigationFindsProfileRecipeAndMaterialInOtherFiles()
    {
        var root = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm");
        var files = Directory.GetFiles(Path.GetDirectoryName(root)!, "*.firmament").Append(root)
            .Select(p => new KeyValuePair<string, string>(Path.GetFileName(p), File.ReadAllText(p)));
        var snapshot = new FirmamentProjectSnapshot("guitar.firmasm", files);
        var neck = snapshot.Documents["Neck.firmament"];
        var profile = FirmamentLanguageAnalysisService.Definition(snapshot, "Neck.firmament", neck.IndexOf("NeckBack<", StringComparison.Ordinal));
        Assert.Equal("neck-profile.firmament", profile!.Document);
        var knob = snapshot.Documents["knob.firmament"];
        var material = FirmamentLanguageAnalysisService.Definition(snapshot, "knob.firmament", knob.IndexOf("Brass;", StringComparison.Ordinal));
        Assert.Equal("materials.firmament", material!.Document);
        var analysis = FirmamentLanguageAnalysisService.Analyze(snapshot, "Neck.firmament", "1");
        Assert.DoesNotContain(analysis.Diagnostics, d => d.Severity == "error");
    }

    [Fact]
    public void RootClassificationIgnoresCommentsStringsAndSubassemblyDefinitions()
    {
        Assert.False(FirmamentLanguageAnalysisService.HasAssemblyRoot("// <Assembly Fake>\nSubassembly Nested { <Assembly Nested> </Assembly> }"));
        Assert.False(FirmamentLanguageAnalysisService.HasAssemblyRoot("Static Text: String = \"<Assembly Fake>\";"));
        Assert.True(FirmamentLanguageAnalysisService.HasAssemblyRoot("Subassembly Nested { <Assembly Nested> </Assembly> }\n<Assembly Actual> </Assembly>"));
        Assert.True(FirmamentLanguageAnalysisService.HasAssemblyRoot("Assembly Product { <Assembly Product> </Assembly> }"));
        Assert.False(FirmamentLanguageAnalysisService.HasAssemblyRoot("/* <Assembly Fake> */ Model X { }"));
    }

    [Fact]
    public void NestedSectionFieldsAreNotOfferedAgainAndConcreteRoutesUseWireParser()
    {
        const string draft = "Section S { Frame: Plane { Origin: [0mm,0mm,0mm]; }\n ";
        var completion = FirmamentLanguageService.Complete(draft, "draft.firmament", "1", draft.Length);
        Assert.DoesNotContain(completion.Fields, f => f.Name == "Frame");
        Assert.Contains(completion.Fields, f => f.Name == "Profile");
        var route = FirmamentCorpusHarness.CanonicalMutationSource(FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/WireForm/line-guide.firmament"));
        Assert.Empty(FirmamentLanguageAnalysisService.Analyze(route, "guide.firmament", "1").Diagnostics);
        var invalid = FirmamentLanguageAnalysisService.Analyze(route.Replace("Distance: 60mm", "Distance: 100mm"), "guide.firmament", "2");
        Assert.Contains(invalid.Diagnostics, d => d.Message.Contains("guide-interval-invalid"));
    }
}
