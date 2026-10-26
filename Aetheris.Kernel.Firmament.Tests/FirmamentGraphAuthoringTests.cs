using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentGraphAuthoringTests
{
    private static string Fixture(string name) => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/GraphAuthoring/" + name + ".firmament")).ReplaceLineEndings("\n");
    private static string Plate => Fixture("plate");
    private static string Profile => Fixture("profile");
    private static FirmamentLanguageEdit Edit(string source, params FirmamentLanguageEditOperation[] operations) =>
        FirmamentLanguageEditService.Apply(source, "authoring.firmament", "1", operations);
    private static void Builds(string source)
    {
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, source + "\n" + string.Join('\n', built.Diagnostics.Select(d => d.Message)));
    }

    [Fact]
    public void ReplaceBaseKeepsItsNameReferencesAndSurroundingSource()
    {
        var result = Edit(Plate, new FirmamentLanguageEditOperation("replaceDeclaration") { Node = "Part/Box:Stock", Source = "Box Other { size: [90mm,60mm,8mm] }" });
        Assert.Contains("Box Stock", result.Text);
        Assert.Contains("Modify Stock {", result.Text);
        Assert.EndsWith("}\n", result.Text);
        Builds(result.Text);
    }

    [Fact]
    public void PickableRetainedCapsKeepExactGeometryForSingleAndMultipleHoles()
    {
        var multipleSource = Edit(Plate, new FirmamentLanguageEditOperation("insertFeature") { Node = "Part/Box:Stock",
            Source = "Hole<Shaft> Second { on: +Z center: Point2(20mm,0mm) diameter: 6mm end: ThroughAll }" }).Text;
        foreach (var source in new[] { Plate, multipleSource })
        {
            var built = FirmamentBuildAndExport.CompileSource(source);
            Assert.True(built.IsSuccess, string.Join('\n', built.Diagnostics.Select(d => d.Message)));
            var caps = built.Value.RuntimeCorrespondence!.Descendants.Where(d => d.Role == SemanticTopologyRole.BoxFace).ToArray();
            Assert.Equal(2, caps.Length);
            var top = Assert.Single(caps, d => d.FirmamentSelector == "face(+Z)");
            var body = built.Value.RuntimeBody!;
            var binding = Assert.Single(body.Bindings.FaceBindings, b => b.FaceId == top.Face);
            Assert.Equal(1, body.Geometry.GetSurface(binding.SurfaceGeometryId).Plane!.Value.Normal.Z, 10);
            var selected = FirmamentBuildAndExport.CompileSource(source.Replace("on: +Z", "on: face(+Z)", StringComparison.Ordinal));
            Assert.True(selected.IsSuccess, string.Join('\n', selected.Diagnostics.Select(d => d.Message)));
            Assert.Equal(built.Value.StepText[built.Value.StepText.IndexOf("DATA;", StringComparison.Ordinal)..], selected.Value.StepText[selected.Value.StepText.IndexOf("DATA;", StringComparison.Ordinal)..]);
        }
    }

    [Fact]
    public void GridPreservesSeedIdentityAndBuildsSixActualHoles()
    {
        var result = Edit(Plate, new FirmamentLanguageEditOperation("patternFeature") { Node = "Part/Modify:Stock/Seed", Name = "Mounts", Grid = new(3, 2, 20, 20) });
        Assert.Contains("Hole<Shaft> Seed { on: +Z center: Point2(-20mm, -10mm) diameter: 4mm end: ThroughAll }", result.Text);
        Assert.Contains("r1c2 =>", result.Text);
        Assert.DoesNotContain("r0c0 =>", result.Text);
        var built = FirmamentBuildAndExport.CompileSource(result.Text);
        Assert.True(built.IsSuccess, result.Text + "\n" + string.Join('\n', built.Diagnostics.Select(d => d.Message)));
        Assert.Equal(6, built.Value.Features!.Count);
    }

    [Theory]
    [InlineData(0, 1, 20, 0)]
    [InlineData(1024, 2, 20, 20)]
    [InlineData(2, 1, 0, 0)]
    public void InvalidGridDoesNotWriteSource(int cols, int rows, double x, double y) =>
        Assert.Equal("edit-grid-invalid", Assert.Throws<FirmamentLanguageEditException>(() => Edit(Plate,
            new FirmamentLanguageEditOperation("patternFeature") { Node = "Part/Modify:Stock/Seed", Grid = new(cols, rows, x, y) })).Code);

    [Fact]
    public void ProfileExtrudesThenPocketsThroughExistingComposeOwner()
    {
        var extruded = Edit(Profile, new FirmamentLanguageEditOperation("extrudeProfile") { Node = "Part/Section", Value = "8mm" });
        Builds(extruded.Text);
        var extra = Edit(extruded.Text,
            new FirmamentLanguageEditOperation("insertDeclaration") { Parent = "Part", Source = "Circle2 Recess { center: [0mm,0mm] radius: 5mm }" },
            new FirmamentLanguageEditOperation("insertDeclaration") { Parent = "Part", Source = "Profile RecessProfile { Loop Outer { Recess |> TraceLoop } }" });
        var pocket = Edit(extra.Text, new FirmamentLanguageEditOperation("pocketProfile") { Node = "Part/RecessProfile", Parent = "Part/Body", Value = "2mm", Name = "Counterbore" });
        Assert.Contains("Compose Body", pocket.Text);
        Assert.Contains("Base Stock", pocket.Text);
        Builds(pocket.Text);
    }

    [Fact]
    public void ExtrusionEndEditPreservesTheAuthoredStartAndParameterReference()
    {
        var original = Edit(Profile, new FirmamentLanguageEditOperation("insertDeclaration") { Parent = "Part", Source = "Static End: Length = 10mm;" },
            new FirmamentLanguageEditOperation("insertDeclaration") { Parent = "Part", Source = "Extrude Body { profile: Section; from: 2mm; to: End; }" });
        var unchanged = Edit(original.Text, new FirmamentLanguageEditOperation("extrudeProfile") { Node = "Part/Section", Value = "End" });
        Assert.Contains("from: 2mm", unchanged.Text);
        Assert.Contains("to: End", unchanged.Text);
        var extended = Edit(unchanged.Text, new FirmamentLanguageEditOperation("extrudeProfile") { Node = "Part/Section", Value = "14mm" });
        Assert.Contains("from: 2mm", extended.Text);
        Assert.Contains("Static End: Length = 10mm", extended.Text);
        Builds(unchanged.Text); Builds(extended.Text);
    }

    [Fact]
    public void BoxPocketUsesTheQualifiedProfileCompositionWithoutLosingStockSize()
    {
        var result = Edit("Model Part { units: mm Box Stock { size: [80mm,60mm,8mm] } }", new FirmamentLanguageEditOperation("pocketProfile")
        {
            Parent = "Part/Stock", Name = "Recess", Value = "2mm",
            Declaration = new("Circle2", Fields: [new("center", "[0mm,0mm]"), new("diameter", "10mm")])
        });
        Assert.Contains("size: [80mm, 60mm]", result.Text);
        Assert.Contains("to: 8mm", result.Text);
        Builds(result.Text);
        var built = FirmamentBuildAndExport.CompileSource(result.Text).Value;
        var correspondence = built.RuntimeCorrespondence!;
        var map = new GeometrySourceMap(correspondence);
        var walls = correspondence.Descendants.Where(d => d.Face.HasValue && !d.IsTopologyAlias && d.GeometryPreview == "Pocket").ToArray();
        Assert.NotEmpty(walls);
        foreach (var wall in walls)
        {
            Assert.True(map.TryGetByBrepFace(wall.Face!.Value, out var picked));
            Assert.Equal(wall, picked);
            var origin = Assert.Single(correspondence.Descendants, d => d.IsTopologyAlias && d.Face == wall.Face);
            Assert.StartsWith("profile:", origin.SourceStableId);
            Assert.Contains(origin, map.GetEntitiesForSemanticKey(origin.StableId));
            Assert.Throws<InvalidOperationException>(() => new GeometrySourceMap(correspondence with { Descendants = correspondence.Descendants.Where(d => d != wall).ToArray() }));
        }
    }

    [Fact]
    public void ExtrudeDoesNotSilentlyReplaceAnotherBase() =>
        Assert.Equal("edit-body-occupied", Assert.Throws<FirmamentLanguageEditException>(() => Edit(
            Profile.Replace("units: mm", "units: mm Box Stock { size: [80mm,60mm,8mm] }"),
            new FirmamentLanguageEditOperation("extrudeProfile") { Node = "Part/Section", Value = "8mm" })).Code);

    [Fact]
    public void PlacementUsesReadableFrameFieldsAndReplacesOnlyTheOldPlacement()
    {
        var source = Fixture("placement");
        var result = Edit(source, new FirmamentLanguageEditOperation("setPlacement") { Node = "Pair/Pair/B", Placement = new([20, 5, 0], "Z", 90) });
        Assert.DoesNotContain("Placement Old", result.Text);
        Assert.Contains("translateLocal: [20mm, 5mm, 0mm]", result.Text);
        var built = new AssemblyM1Pipeline().Compile(result.Text, "placement.firmament");
        Assert.True(built.IsSuccess, string.Join('\n', built.Diagnostics));
        var moved = built.Ir!.Instances.Single(i => i.Path.ToString() == "Pair.B").ResolvedTransform!.Matrix;
        Assert.Equal(20, moved[12], 8); Assert.Equal(5, moved[13], 8);
        var controls = FirmamentLanguageOutlineService.Outline(result.Text, "placement", "1").Nodes.Single(n => n.Kind == "Placement").PlacementControls!;
        Assert.Null(controls.ReadOnlyReason); Assert.Equal(90, controls.AngleDegrees); Assert.Equal([20d, 5, 0], controls.TranslationMm);
    }

    [Theory]
    [InlineData("Placement { from: Origin; to: Frame.Top; }")]
    [InlineData("Placement { from: Origin; to: Parent; translateLocal: [Width,0mm,0mm]; }")]
    [InlineData("Placement Old = [0,1,0,0,-1,0,0,0,0,0,1,0,20,0,0,1];")]
    public void AdvancedPlacementControlsDoNotFreezeOrDiscardAuthoredIntent(string placement)
    {
        var outline = FirmamentLanguageOutlineService.Outline(placement, "placement", "1");
        Assert.NotNull(Assert.Single(outline.Nodes).PlacementControls!.ReadOnlyReason);
    }

    [Fact]
    public void StarterPublishedFramesMateThroughTheExistingAssemblySolver()
    {
        var source = Fixture("assembly-starter");
        var before = new AssemblyM1Pipeline().Compile(source, "starter.firmament");
        Assert.True(before.IsSuccess, string.Join('\n', before.Diagnostics));
        var node = FirmamentLanguageOutlineService.Outline(source, "starter", "1").Nodes.Single(n => n.Kind == "Placement");
        var edited = Edit(source, new FirmamentLanguageEditOperation("removeDeclaration") { Node = node.Id },
            new FirmamentLanguageEditOperation("insertDeclaration") { Parent = null, First = true, Source = "Interface<Fixed> Rigid { }" },
            new FirmamentLanguageEditOperation("insertDeclaration") { Parent = "WebBlockPair", Source = "Mate Stack: Rigid { A: WebBlockPair.Fixed.Top; B: WebBlockPair.Moving.Bottom; }" });
        var after = new AssemblyM1Pipeline().Compile(edited.Text, "starter.firmament");
        Assert.True(after.IsSuccess, string.Join('\n', after.Diagnostics));
        var transform = after.Ir!.Instances.Single(i => i.Path.ToString() == "WebBlockPair.Moving").ResolvedTransform!.Matrix;
        Assert.Equal(0, transform[12], 8); Assert.Equal(10, transform[14], 8);
        Assert.Equal(before.Geometry!.Artifact.Definitions.Select(d => d.StepSha256).Order(), after.Geometry!.Artifact.Definitions.Select(d => d.StepSha256).Order());
    }
}
