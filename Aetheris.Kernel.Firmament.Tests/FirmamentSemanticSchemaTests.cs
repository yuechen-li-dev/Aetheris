using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentSemanticSchemaTests
{
    [Fact]
    public void GeneratedRegistry_IsStableOptInAndCoversFirstConstructs()
    {
        Assert.Equal("firmament-semantic-schema/1", FirmamentSemanticSchemas.Version);
        string[] expectedNames =
        [
            "Appearance", "FrameTransform", "Placement", "Pmi", "Note", "Provenance",
            "Box", "Circle2", "Compose", "Base", "Concept", "Points", "Door", "Drape", "EdgeFinish", "Ellipsoid", "Extrude", "Fabric", "FigureFit", "Fitting", "Garment",
            "Helix", "Hole", "Interface<Fixed>", "Interface<Prismatic>", "Interface<Revolute>",
            "Interface<Stitch>", "Inventory", "Joint", "Loft", "Panel", "Perforation", "Material", "Linear", "Series", "Polyhedron", "Pose", "PoseTransition",
            "Profile", "Rect2", "Room", "RoundedRect2", "Scene", "Camera", "Section", "Slot2", "Thread", "Window", "Follow", "WireRoute",
        ];
        Assert.Equal(expectedNames, FirmamentSemanticSchemas.All.Select(schema => schema.Name));
        var hole = Assert.IsType<FirmamentConstructSchema>(FirmamentSemanticSchemas.Get("Hole"));
        var diameter = Assert.Single(hole.Fields, f => f.Name == "Diameter");
        Assert.Equal("Hole.Diameter", diameter.Id.Value);
        Assert.Equal(FirmamentSchemaValueKind.Length, diameter.Kind);
        Assert.Equal(FirmamentUnitKind.Length, diameter.Unit);
        Assert.True(diameter.Required);
        var wall = Assert.Single(hole.Outputs);
        Assert.Equal("Hole.Wall", wall.Id.Value);
        Assert.Equal("HoleWallFace", wall.SourceRole);
        Assert.True(wall.SourceAddressable);
    }

    [Fact]
    public void ExistingLanguageHelp_ConsumesGeneratedHelixAndLoftFields()
    {
        var helix = FirmamentSemanticSchemas.Get("Helix")!;
        Assert.Equal("WireForm", helix.Context);
        Assert.Equal("AxisCoil", helix.CompatibilityAlias);
        Assert.Equal(helix.Fields.Select(f => f.Name), FirmamentSchemaAuthoringFields.For("Helix").Select(f => f.Name));
        Assert.Equal(helix.Fields.Select(f => f.Name), LoftOrHelixCompletion("Model M { WireForm Spring { Helix Winding {\n ", "Helix").Fields.Select(f => f.Name));

        var loft = FirmamentSemanticSchemas.Get("Loft")!;
        Assert.Contains("Loft Body", loft.Entry);
        Assert.Equal(loft.Fields.Select(f => f.Name), LoftAuthoringParser.SolidFields.Select(f => f.Name));
        Assert.Equal(loft.Fields.Select(f => f.Name), LoftOrHelixCompletion("Model M { Loft Body {\n ", "Loft").Fields.Select(f => f.Name));
    }

    [Fact]
    public void GraphAuthoringPresetsComeFromSchemaAndBuildOnTheirDocumentedHost()
    {
        var hole = FirmamentSemanticSchemas.Get("Hole")!;
        foreach (var on in hole.Fields.Single(f => f.Name == "On").Choices)
        foreach (var end in hole.Fields.Single(f => f.Name == "End").Choices)
        {
            var source = $"Model Preset {{ units: mm Box Body {{ size: [40mm, 30mm, 20mm] }} Modify Body {{ Hole<Shaft> H {{ on: {on} center: Point2(0mm, 0mm) diameter: 6mm end: {end} }} }} }}";
            var build = FirmamentBuildAndExport.CompileSource(source);
            Assert.True(build.IsSuccess, $"{on}/{end}: {string.Join(';', build.Diagnostics.Select(d => d.Message))}");
        }
        var finish = FirmamentSemanticSchemas.Get("EdgeFinish")!;
        var completionSource = "Model M { Box B { size: [40mm,30mm,10mm] } Modify B { EdgeFinish F { ";
        var completion = FirmamentLanguageService.Complete(completionSource, "graph.firmament", "1", completionSource.Length);
        Assert.Equal("EdgeFinish", completion.Context);
        Assert.Equal(finish.Fields.Select(f => f.Name), completion.Fields.Select(f => f.Name));
        foreach (var kind in new[] { "Chamfer", "Fillet" })
        {
            var source = $"Model Preset {{ units: mm Box Body {{ size: [40mm, 30mm, 10mm] }} Modify Body {{ EdgeFinish F {{ face: +Z target: Boundary kind: {kind} {(kind == "Chamfer" ? "distance" : "radius")}: 1mm }} }} }}";
            Assert.True(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        }
    }

    [Fact]
    public void ProfileWorkflowEntries_BuildAsWritten()
    {
        // Agents and completion copy these entries verbatim; each must compile in its documented host.
        string Entry(string name) => FirmamentSemanticSchemas.Get(name)!.Entry!;
        foreach (var shape in new[] { "Rect2", "RoundedRect2", "Circle2", "Slot2" })
        {
            var guide = Entry(shape);
            var profile = guide[(guide.IndexOf(' ') + 1)..guide.IndexOf('{')].Trim();
            var source = $"Model M {{ units: mm {guide} Profile P {{ Loop Outer {{ {profile} |> TraceLoop }} }} Extrude Body {{ profile: P; from: 0mm; to: 5mm }} }}";
            Assert.True(FirmamentBuildAndExport.CompileSource(source).IsSuccess, shape);
        }
        var plate = $"Model M {{ units: mm {Entry("Profile")} {Entry("Extrude")} }}";
        Assert.True(FirmamentBuildAndExport.CompileSource(plate).IsSuccess, plate);
        var composed = $"Model M {{ units: mm {Entry("Profile")} {Entry("Compose")} Modify Plate {{ Hole<Shaft> H {{ on: +Z center: Point2(19mm, 14mm) diameter: 5.5mm end: ThroughAll }} }} }}";
        Assert.True(FirmamentBuildAndExport.CompileSource(composed).IsSuccess, composed);
    }

    [Fact]
    public void ModifyFeaturesOnAPlainExtrude_RejectInsteadOfVanishing()
    {
        var source = "Model M { units: mm Profile P = Rect2 { center: [0mm, 0mm]; size: [50mm, 40mm] } Extrude Plate { profile: P; from: 0mm; to: 8mm } "
            + "Modify Plate { Hole<Shaft> H { on: +Z center: Point2(0mm, 0mm) diameter: 6mm end: ThroughAll } } }";
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(build.IsSuccess);
        var diagnostic = Assert.Single(build.Diagnostics, d => d.Message.Contains("Compose Plate", StringComparison.Ordinal));
        Assert.Contains("Hole H would not be cut", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConceptAndBox_KeepTheirSemanticBoundaries()
    {
        Assert.Empty(FirmamentSemanticSchemas.Get("Concept")!.Fields);
        var box = FirmamentSemanticSchemas.Get("Box")!;
        Assert.Contains(box.Fields, f => f.Name == "Size" && f.Kind == FirmamentSchemaValueKind.Vector && f.Unit == FirmamentUnitKind.Length);
        Assert.Contains(box.Fields, f => f.Name == "Bounds" && f.Kind == FirmamentSchemaValueKind.Box3);
        Assert.All(box.Fields, f => Assert.False(f.Required)); // Size or Bounds is required, not either field alone.
    }

    [Theory]
    [InlineData("Helix", "WireForm Spring")]
    [InlineData("Loft", "Construction Plane LowerFrame")]
    public void BareConstructSearchReturnsGeneratedLegalEntry(string name, string requiredContext)
    {
        var source = $"Model Draft {{ Units: mm\n {name}";
        var completion = FirmamentLanguageService.Complete(source, "draft.firmament", "1", source.Length);
        Assert.Equal("ConstructEntry", completion.Context);
        var entry = Assert.Single(completion.Entries!);
        Assert.Equal(name, entry.Name);
        Assert.Contains(requiredContext, entry.Source);
        Assert.Equal(FirmamentSourceSpelling.Prefer(FirmamentSemanticSchemas.Get(name)!.Entry!), entry.Source);
    }

    [Theory]
    [InlineData("Helix")]
    [InlineData("Loft")]
    [InlineData("WireRoute")]
    public void GeneratedEntryIsBuildable(string name)
    {
        var source = FirmamentSemanticSchemas.Get(name)!.Entry!;
        foreach (var spelling in new[] { source, FirmamentSourceSpelling.Prefer(source) })
        {
            var result = FirmamentBuildAndExport.CompileSource(spelling);
            Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        }
    }

    private static FirmamentLanguageCompletion LoftOrHelixCompletion(string source, string expected)
    {
        var result = FirmamentLanguageService.Complete(source, "draft.firmament", "1", source.Length);
        Assert.Equal(expected, result.Context);
        return result;
    }
}
