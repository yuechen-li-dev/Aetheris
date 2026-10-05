using System.Buffers.Binary;
using System.Text.Json;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Scene;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ArchvizSceneTests
{
    private const string Finishes = """
        Appearance wall { color: [.8,.8,.8]; metallic: 0; roughness: .8; }
        Appearance floor { color: [.5,.3,.1]; metallic: 0; roughness: .5; }
        Appearance glass { color: [.8,.9,.9]; metallic: 0; roughness: .1; opacity: .2; }
        Scene FinishProof {
          units: m;
          Room room { size: [4m,3m,2.8m]; appearance: wall; floorAppearance: floor; ceilingAppearance: wall; }
          Window opening { on: room.WALL; along: .5m; width: 2m; height: 2m; sill: .3m;
            glazingAppearance: glass; frameAppearance: floor; frameWidth: 50mm; glassThickness: 6mm; }
        }
        """;
    private static string HousePath => FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Scene/WarmModernHouse/house.firmament");

    [Theory]
    [InlineData("southWall", -50d, 1)]
    [InlineData("northWall", 3050d, 1)]
    [InlineData("westWall", -50d, 0)]
    [InlineData("eastWall", 4050d, 0)]
    public void WindowFrameAndGlazingFollowTheirOwnedApertureAndWallSkin(string wall,double normalCentre,int axis)
    {
        using var session = new FirmamentSceneSession();
        var result = session.Compile(Finishes.Replace("WALL",wall,StringComparison.Ordinal));
        Assert.True(result.IsSuccess,string.Join("\n",result.Diagnostics));
        var scene = result.Scene!;
        Assert.Equal(5,scene.Nodes.Count(n => n.Kind == "WindowFinish"));
        var pane = scene.Display.Occurrences.Single(o => o.Path.EndsWith(".pane",StringComparison.Ordinal));
        var definition = scene.Display.Definitions.Single(d => d.Id == pane.DefinitionId);
        Assert.Equal(new[] {1900d,1900,6},Enumerable.Range(0,3).Select(a => Enumerable.Range(0,definition.Positions.Length/3)
            .Max(i => definition.Positions[3*i+a])-Enumerable.Range(0,definition.Positions.Length/3).Min(i => definition.Positions[3*i+a])));
        var centre = Transform3D.FromRowMajor(pane.Transform).Apply(new Point3D(950,950,3));
        Assert.Equal(normalCentre,axis == 0 ? centre.X : centre.Y);
        Assert.Equal(.2,scene.Appearances[pane.Id].Preview.Opacity);
        var floor = scene.Display.Occurrences.Single(o => o.Path.EndsWith(".floor.panel0",StringComparison.Ordinal));
        Assert.Equal("floor",scene.Appearances[floor.Id].Appearance);
        Assert.Contains("inputs:opacity = 0.2",SceneExport.Usd(scene));
        var bytes = SceneExport.Glb(scene);
        using var json = JsonDocument.Parse(bytes.AsMemory(20,BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12,4))));
        var material = json.RootElement.GetProperty("materials").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "glass");
        Assert.Equal("BLEND",material.GetProperty("alphaMode").GetString());
        Assert.Equal(.2,material.GetProperty("pbrMetallicRoughness").GetProperty("baseColorFactor")[3].GetDouble());
    }

    [Theory]
    [InlineData("frameWidth: 50mm", "frameWidth: 1m", "scene-window-finish-invalid")]
    [InlineData("glassThickness: 6mm", "glassThickness: 101mm", "scene-window-finish-invalid")]
    [InlineData("glazingAppearance: glass", "glazingAppearance: missing", "scene-appearance-unknown")]
    [InlineData("floorAppearance: floor", "floorAppearance: missing", "scene-appearance-unknown")]
    [InlineData("opacity: .2", "opacity: 1.1", "assembly-appearance-values-invalid")]
    [InlineData("frameAppearance: floor;", "", "scene-window-finish-invalid")]
    public void InvalidNewIntentHasActionableSourceDiagnostics(string before,string after,string code)
    {
        var result = SceneAuthoring.Parse(Finishes.Replace("WALL","southWall",StringComparison.Ordinal).Replace(before,after,StringComparison.Ordinal));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,d => d.Code == code);
    }

    [Fact]
    public void HouseGuidesShareFurnitureAndPresentationEditsReuseEveryEngineeringDefinition()
    {
        using var session = new FirmamentSceneSession();
        var source = File.ReadAllText(HousePath);
        var first = session.Compile(source,HousePath);
        Assert.True(first.IsSuccess,string.Join("\n",first.Diagnostics));
        var scene = first.Scene!;
        Assert.Equal(2,scene.Source.Rooms.Count);
        Assert.Equal(3,scene.Source.Cameras.Count);
        Assert.Equal(8,scene.Source.LayoutFrames.Count);
        Assert.Equal(4,scene.Display.Occurrences.Count(o => o.Path.Contains(".chairs.",StringComparison.Ordinal) && o.Path.EndsWith(".chair.seat",StringComparison.Ordinal)));
        Assert.Single(scene.Display.Occurrences.Where(o => o.Path.Contains(".chairs.",StringComparison.Ordinal) && o.Path.EndsWith(".chair.seat",StringComparison.Ordinal)).Select(o => o.DefinitionId).Distinct());
        Assert.Equal(6,scene.Display.Occurrences.Count(o => o.Path.Contains(".cabinetry.",StringComparison.Ordinal) && o.Path.EndsWith(".cabinet.door",StringComparison.Ordinal)));
        Assert.Single(scene.Display.Occurrences.Where(o => o.Path.Contains(".cabinetry.",StringComparison.Ordinal) && o.Path.EndsWith(".cabinet.door",StringComparison.Ordinal)).Select(o => o.DefinitionId).Distinct());
        foreach (var edit in new[] {
            source.Replace("[8.1m, .65m, 1.65m]","[8m, .65m, 1.65m]",StringComparison.Ordinal),
            source.Replace("[2500mm, 1450mm]","[2600mm, 1450mm]",StringComparison.Ordinal),
            source.Replace("color: [.55, .35, .18]","color: [.58, .38, .2]",StringComparison.Ordinal),
            source.Replace("size: [2.4m, 2.2m, 3.1m]","size: [2.5m, 2.2m, 3.1m]",StringComparison.Ordinal) })
        {
            var changed = session.Compile(edit,HousePath);
            Assert.True(changed.IsSuccess,string.Join("\n",changed.Diagnostics));
            Assert.Equal(0,changed.Scene!.Performance.RebuiltEngineeringDefinitions);
            Assert.Equal(scene.Performance.UniqueEngineeringDefinitions,changed.Scene.Performance.ReusedEngineeringDefinitions);
            foreach (var definition in scene.Display.Definitions.Where(d => !d.Identity.StartsWith("scene-box:",StringComparison.Ordinal)))
            {
                var reused = changed.Scene.Display.Definitions.Single(d => d.Id == definition.Id);
                Assert.Equal(definition.Positions,reused.Positions);
                Assert.Equal(definition.Indices,reused.Indices);
            }
        }
        var formatted = FirmamentLanguageAnalysisService.Format(source,HousePath,"1").Text;
        Assert.Equal(source,formatted);
        Assert.DoesNotContain(FirmamentLanguageAnalysisService.Analyze(source,HousePath,"1").Diagnostics,d => d.Severity == "error");
        foreach (var field in new[] {"FloorAppearance","CeilingAppearance","GlazingAppearance","FrameAppearance","FrameWidth","GlassThickness","Opacity"})
        {
            var owner = field switch { "Opacity" => "Appearance", "FloorAppearance" or "CeilingAppearance" => "Room", _ => "Window" };
            Assert.Contains(FirmamentLanguageService.Complete(owner+" test { ",HousePath,"1",owner.Length+8).Fields,f => f.Name == field);
            var label = char.ToLowerInvariant(field[0])+field[1..];
            var hoverSource = source.Contains(label,StringComparison.Ordinal) ? source : Finishes.Replace("WALL","southWall",StringComparison.Ordinal);
            var offset = hoverSource.IndexOf(label,StringComparison.Ordinal);
            Assert.True(offset >= 0,field+" requires a real hover specimen.");
            Assert.NotNull(FirmamentLanguageAnalysisService.Hover(hoverSource,HousePath,"1",offset));
        }
    }

    [Fact]
    public void CutawayIsAnExplicitExportProjectionWithoutMutationOrRecompilation()
    {
        using var session = new FirmamentSceneSession();
        var result = session.Compile(Finishes.Replace("WALL","southWall",StringComparison.Ordinal));
        Assert.True(result.IsSuccess);
        var scene = result.Scene!;
        var original = SceneExport.Glb(scene);
        var cutaway = SceneExport.Glb(scene,["room.ceiling","room.southWall"]);
        using var json = JsonDocument.Parse(cutaway.AsMemory(20,BinaryPrimitives.ReadInt32LittleEndian(cutaway.AsSpan(12,4))));
        foreach (var node in json.RootElement.GetProperty("nodes").EnumerateArray()
            .Where(n => n.GetProperty("name").GetString()!.StartsWith("FinishProof.room.ceiling.",StringComparison.Ordinal)
                || n.GetProperty("name").GetString()!.StartsWith("FinishProof.room.southWall.",StringComparison.Ordinal)))
            Assert.False(node.TryGetProperty("mesh",out _));
        Assert.NotEqual(SceneExport.Usd(scene),SceneExport.Usd(scene,["room.ceiling"]));
        Assert.Equal(original,SceneExport.Glb(scene));
        Assert.Throws<ArgumentException>(() => SceneExport.Glb(scene,["missing.ceiling"]));
    }

    [Fact]
    public void ConceptGuidesDeriveFromTranslatedRoomAndNeverMaterialize()
    {
        const string source = """
            Template<H: Length> Struct Block {
              RoundedRect2 outline { center: [0mm,0mm]; size: [100mm,100mm]; radius: 10mm; }
              Profile section { Loop Outer { outline |> TraceLoop } }
              Extrude body { profile: section; from: 0mm; to: H; }
            }
            Concept Struct Layout {
              Plane floor { from: room.floor; }
              DatumFrame seat { on: floor; at: [200mm,300mm]; x: [0,1]; }
            }
            Scene Guides {
              units: m;
              Room room { size: [2m,2m,2m]; at: [1m,2m,0mm]; }
              <Part block = Block<H:200mm>> Placement { from: Origin; to: Layout.seat; } </Part>
            }
            """;
        using var session = new FirmamentSceneSession();
        var result = session.Compile(source);
        Assert.True(result.IsSuccess,string.Join("\n",result.Diagnostics));
        var block = result.Scene!.Display.Occurrences.Single(o => o.Path == "Guides.block");
        Assert.Equal(new[] {1200d,2300,0},block.Transform.Skip(12).Take(3));
        Assert.Equal(1,block.Transform[1],8);
        Assert.DoesNotContain(result.Scene.Nodes,n => n.Path.Contains("Layout",StringComparison.Ordinal));
        var unknown = SceneAuthoring.Parse(source.Replace("from: room.floor","from: missing.floor",StringComparison.Ordinal));
        Assert.False(unknown.IsSuccess);
        Assert.Contains(unknown.Diagnostics,d => d.Code == "scene-frame-unresolved");
        var cycle = SceneAuthoring.Parse(source.Replace("from: room.floor","from: Layout.seat",StringComparison.Ordinal));
        Assert.False(cycle.IsSuccess);
        Assert.Contains(cycle.Diagnostics,d => d.Code == "assembly-datum-dependency-cycle");
    }
}
