using System.Buffers.Binary;
using System.Text.Json;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Scene;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FactorySceneTests
{
    private const string Rows = """
        Template<H: Length> Struct Block {
          RoundedRect2 outline { center: [0mm,0mm]; size: [100mm,100mm]; radius: 2mm; }
          Profile section { Loop Outer { outline |> TraceLoop } }
          Extrude body { profile: section; from: 0mm; to: H; }
        }
        Appearance signal { color: [.1,.2,.3]; metallic: 0; roughness: .4; emissive: [.2,.6,1]; }
        Linear Sites row { keys: [first, second]; start: [100mm,200mm,0mm]; step: [300mm,0mm,0mm]; }
        Mirrored Sites reflected From row { Across: YZ; }
        Scene Proof {
          units: m;
          Room hall { size: [2m + 500mm,3m,2m]; }
          Pattern originals over row {
            site => <Part item = Block<H:100mm>> appearance: signal;
              Placement { from: Origin; to: World; translateLocal: [site.X + .1m,site.Y,site.Z]; }
            </Part>
          }
          Pattern mirrors over reflected {
            site => <Part item = Block<H:100mm>> appearance: signal;
              Placement { from: Origin; to: World; translateLocal: [site.X,site.Y,site.Z]; }
            </Part>
          }
        }
        """;

    [Fact]
    public void SceneSiteRecipesRetainKeysShareGeometryAndExportEmission()
    {
        using var session = new FirmamentSceneSession();
        var first = session.Compile(Rows);
        Assert.True(first.IsSuccess,string.Join("\n",first.Diagnostics));
        var scene = first.Scene!;
        Assert.Equal(2500,scene.Source.Rooms.Single().SizeMm[0]);
        Assert.Equal(500,scene.Display.Occurrences.Single(o => o.Path == "Proof.originals.second.item").Transform[12]);
        Assert.Equal(-400,scene.Display.Occurrences.Single(o => o.Path == "Proof.mirrors.second.item").Transform[12]);
        Assert.Single(scene.Display.Occurrences.Where(o => o.Path.EndsWith(".item",StringComparison.Ordinal)).Select(o => o.DefinitionId).Distinct());
        Assert.Equal(2,scene.Source.Patterns.Single(p => p.Name == "mirrors").SiteRecipe!.Count);
        var bytes = SceneExport.Glb(scene);
        using var json = JsonDocument.Parse(bytes.AsMemory(20,BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12,4))));
        var material = json.RootElement.GetProperty("materials").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "signal");
        Assert.Equal(new[] {.2,.6,1},material.GetProperty("emissiveFactor").EnumerateArray().Select(v => v.GetDouble()));
        Assert.Contains("inputs:emissiveColor = (",SceneExport.Usd(scene));
        var changed = session.Compile(Rows.Replace("step: [300mm","step: [350mm",StringComparison.Ordinal));
        Assert.True(changed.IsSuccess,string.Join("\n",changed.Diagnostics));
        Assert.Equal(0,changed.Scene!.Performance.RebuiltEngineeringDefinitions);
        Assert.Equal(scene.Source.Occurrences.Select(o => o.Path),changed.Scene.Source.Occurrences.Select(o => o.Path));
        Assert.Equal(550,changed.Scene.Display.Occurrences.Single(o => o.Path == "Proof.originals.second.item").Transform[12]);
        Assert.Contains(FirmamentLanguageService.Complete("Appearance light { ","test","1",19).Fields,f => f.Name == "Emissive");
    }

    [Theory]
    [InlineData("[.2,.6,1]", "[.2,.6,1.1]")]
    [InlineData("[.2,.6,1]", "[.2,.6]")]
    [InlineData("[.2,.6,1]", "[-.2,.6,1]")]
    public void InvalidEmissionFailsAtAuthoringBoundary(string before,string after)
    {
        var result = SceneAuthoring.Parse(Rows.Replace(before,after,StringComparison.Ordinal));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,d => d.Code == "assembly-appearance-values-invalid");
    }

    [Theory]
    [InlineData("keys: [first, second]", "keys: [first, first]")]
    [InlineData("step: [300mm,0mm,0mm]", "step: [0mm,0mm,0mm]")]
    [InlineData("start: [100mm,200mm,0mm]", "start: [1m,200mm,0mm]")]
    public void SceneUsesSharedSiteRecipeRejectionBoundary(string before,string after)
    {
        var result = SceneAuthoring.Parse(Rows.Replace(before,after,StringComparison.Ordinal));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,d => d.Code == "scene-pattern-invalid");
    }

    [Theory]
    [InlineData("2m + 500mm", "2m + 3")]
    [InlineData("2m + 500mm", "2m * 3m")]
    [InlineData("2m + 500mm", "2m / 0")]
    public void SceneLengthExpressionsRemainDimensionChecked(string before,string after)
    {
        var result = SceneAuthoring.Parse(Rows.Replace(before,after,StringComparison.Ordinal));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,d => d.Code == "scene-length-unit-required");
    }

    [Fact]
    public void FactoryHasRequiredOperationalLayersAndRetainsDefinitionsAcrossLayoutEdits()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Scene/FactoryX0/factory.firmament");
        var source = File.ReadAllText(path);
        using var session = new FirmamentSceneSession();
        var first = session.Compile(source,path);
        Assert.True(first.IsSuccess,string.Join("\n",first.Diagnostics));
        var scene = first.Scene!;
        Assert.Equal(new[] {42000d,28000,7000},scene.Source.Rooms.Single().SizeMm);
        Assert.Equal(3,scene.Source.Occurrences.Count(o => o.Path.Contains(".productionLines.",StringComparison.Ordinal)));
        Assert.Equal(2,scene.Source.Occurrences.Count(o => o.Path.Contains(".robotCells.",StringComparison.Ordinal)));
        Assert.Equal(5,scene.Source.Cameras.Count);
        Assert.True(scene.Display.Occurrences.Count(o => o.DefinitionId is not null) > 1000);
        var changed = session.Compile(source.Replace("[32m, 3.5m, 4.5m]","[31.5m, 3.5m, 4.5m]",StringComparison.Ordinal)
            .Replace("step: [0mm, 7000mm, 0mm]","step: [0mm, 7100mm, 0mm]",StringComparison.Ordinal),path);
        Assert.True(changed.IsSuccess,string.Join("\n",changed.Diagnostics));
        Assert.Equal(0,changed.Scene!.Performance.RebuiltEngineeringDefinitions);
        Assert.Equal(scene.Performance.UniqueEngineeringDefinitions,changed.Scene.Performance.ReusedEngineeringDefinitions);
        foreach (var definition in scene.Display.Definitions)
        {
            var reused = changed.Scene.Display.Definitions.Single(d => d.Id == definition.Id);
            Assert.Equal(definition.Positions,reused.Positions);
            Assert.Equal(definition.Indices,reused.Indices);
        }
    }
}
