using System.Buffers.Binary;
using System.Text.Json;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Scene;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SceneAuthoringTests
{
    private static string PathFor(string name) => FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Scene/"+name+".firmament");
    private static string Source(string name) => File.ReadAllText(PathFor(name));
    private static CompiledScene Compile(string name)
    { using var session=new FirmamentSceneSession(); var r=session.CompileFile(PathFor(name)); Assert.True(r.IsSuccess,string.Join("\n",r.Diagnostics)); return r.Scene!; }

    [Fact]
    public void RoomMetresNormalizeToMmAndPublishOwnedStableBoundaries()
    {
        var s=Compile("room"); var room=Assert.Single(s.Source.Rooms);
        Assert.Equal(new[] {10000d,8000,4000},room.SizeMm);
        Assert.Equal(6,s.Boundaries.Count); Assert.Contains(s.Boundaries,b => b.Path == "hall.floor");
        var south=Assert.Single(s.Boundaries,b => b.Path == "hall.southWall");
        Assert.Equal(new[] {1000d,2000,0},south.Frame.Skip(12).Take(3)); Assert.Equal("loading",Assert.Single(south.Openings));
        Assert.Equal(new[] {900d,1900,-100},s.MinimumMm); Assert.Equal(new[] {11100d,10100,4100},s.MaximumMm);
    }

    [Fact]
    public void ActualWallDisplayVolumeExcludesDoorAndWindowApertures()
    {
        var s=Compile("room"); var defs=s.Display.Definitions.ToDictionary(d => d.Id);
        double Volume(string wall) => s.Display.Occurrences.Where(o => o.Path.StartsWith("RoomProof.hall."+wall+".panel",StringComparison.Ordinal)).Sum(o =>
        {
            var p=defs[o.DefinitionId!].Positions;
            return Enumerable.Range(0,3).Select(a => Enumerable.Range(0,p.Length/3).Max(i => p[i*3+a])-Enumerable.Range(0,p.Length/3).Min(i => p[i*3+a])).Aggregate(1d,(a,b) => a*b);
        });
        Assert.Equal((10000d*4000-2000*3000)*100,Volume("southWall"));
        Assert.Equal((8000d*4000-2000*1000)*100,Volume("eastWall"));
        var opening=Assert.Single(s.Source.Openings,o => o.Kind == "Window"); Assert.Equal(1500,opening.SillMm);
        Assert.Contains(s.Nodes,n => n.Path == "RoomProof.hall.eastWall.glazing" && n.Kind == "Window");
    }

    [Theory]
    [InlineData("[10m, 8m, 4m]","[10000mm, 8000mm, 4000mm]")]
    [InlineData("units: m","units: mm")]
    public void MmCompatibilityAndExplicitSuffixesPreserveGeometry(string before,string after)
    {
        using var session=new FirmamentSceneSession(); var baseline=session.Compile(Source("room")); var changed=session.Compile(Source("room").Replace(before,after,StringComparison.Ordinal));
        Assert.True(changed.IsSuccess); Assert.Equal(baseline.Scene!.MinimumMm,changed.Scene!.MinimumMm); Assert.Equal(baseline.Scene.MaximumMm,changed.Scene.MaximumMm);
    }

    [Theory]
    [InlineData("[10m, 8m, 4m]","[10, 8m, 4m]","scene-length-unit-required")]
    [InlineData("width: 2m; height: 3m","width: 11m; height: 3m","scene-opening-outside-wall")]
    [InlineData("hall.southWall","hall.floor","scene-opening-boundary-invalid")]
    [InlineData("thickness: 100mm","thickness: 0mm","scene-room-size-invalid")]
    [InlineData("fov: 45deg","fov: 180deg","scene-camera-invalid")]
    [InlineData("size: [10m, 8m, 4m]","size: [10m, 8m, 4m]; Size: [10m,8m,4m]","scene-duplicate-field")]
    public void InvalidSceneIntentFailsWithSpecificDiagnostics(string before,string after,string code)
    {
        var r=SceneAuthoring.Parse(Source("room").Replace(before,after,StringComparison.Ordinal)); Assert.False(r.IsSuccess); Assert.Contains(r.Diagnostics,d => d.Code == code);
    }

    [Fact]
    public void PatternKeysSourceSpansAndSharedGeometrySurviveCameraPlacementAndCountEdits()
    {
        using var session=new FirmamentSceneSession(); var source=Source("parts-and-pattern");
        var first=session.Compile(source,PathFor("parts-and-pattern")); Assert.True(first.IsSuccess,string.Join("\n",first.Diagnostics));
        Assert.Single(first.Scene!.Display.Definitions); Assert.Equal(2,first.Scene.Source.Occurrences.Count);
        var bodies=first.Scene.Display.Occurrences.Where(o => o.DefinitionId is not null).ToArray(); Assert.Equal(bodies[0].DefinitionId,bodies[1].DefinitionId);
        Assert.Equal(1000,bodies[0].Transform[12]); Assert.Equal(2000,bodies[1].Transform[12]);
        foreach(var o in first.Scene.Source.Occurrences) { Assert.NotNull(o.PatternKey); Assert.StartsWith("Pattern packages",source.Substring(o.Span.Start,o.Span.Length)); }
        var edited=source.Replace("[3m, -2m, 1m]","[4m, -2m, 1m]",StringComparison.Ordinal).Replace("x: 2m","x: 2.5m",StringComparison.Ordinal);
        var second=session.Compile(edited,PathFor("parts-and-pattern")); Assert.True(second.IsSuccess);
        Assert.Equal(0,second.Scene!.Performance.RebuiltEngineeringDefinitions); Assert.Equal(1,second.Scene.Performance.ReusedEngineeringDefinitions);
        Assert.Equal(bodies.Select(o => o.Id),second.Scene.Display.Occurrences.Where(o => o.DefinitionId is not null).Select(o => o.Id));
        var third=session.Compile(edited.Replace("second => Site", "third => Site { x: 3m; }\n second => Site",StringComparison.Ordinal),PathFor("parts-and-pattern"));
        Assert.True(third.IsSuccess); Assert.Equal(3,third.Scene!.Source.Occurrences.Count); Assert.Equal(0,third.Scene.Performance.RebuiltEngineeringDefinitions);
    }

    [Fact]
    public void SceneEditorProjectsSchemaHoverDiagnosticsAndCommentSafePreferredFormatting()
    {
        var source=Source("room"); var entries=FirmamentLanguageService.Complete("", "scene","r",0).Entries!;
        foreach(var kind in new[] {"Scene","Room","Door","Window","Camera"}) Assert.Contains(entries,e => e.Name == kind);
        var prefix="Scene Factory { units: m; Room hall { "; var completion=FirmamentLanguageService.Complete(prefix,"scene","r",prefix.Length);
        Assert.Equal("Room",completion.Context); Assert.Contains(completion.Fields,f => f.Name == "Size");
        var scenePrefix="Scene Factory { units: m;\n  Ro";
        Assert.Contains(FirmamentLanguageService.Complete(scenePrefix,"scene","r",scenePrefix.Length).Entries!,e => e.Name == "Room");
        Assert.NotNull(FirmamentLanguageAnalysisService.Hover(source,"scene","r",source.IndexOf("Room hall",StringComparison.Ordinal)));
        Assert.DoesNotContain(FirmamentLanguageAnalysisService.Analyze(source,"scene","r").Diagnostics,d => d.Severity == "error");
        var pascal=source.Replace("units:","Units:",StringComparison.Ordinal).Replace("size:","Size:",StringComparison.Ordinal);
        var formatted=FirmamentLanguageAnalysisService.Format(pascal,"scene","r").Text;
        Assert.Equal(source,formatted); Assert.Equal(formatted,FirmamentLanguageAnalysisService.Format(formatted,"scene","r").Text);
        Assert.Contains("// A semantic room",formatted); Assert.Equal("RoomProof",SceneAuthoring.Parse(formatted).Source!.Name);
    }

    [Theory]
    [InlineData("Scene test { units: m; <Part unfinished = }")]
    [InlineData("Scene test { units: m; <Part unfinished = ></Part> }")]
    public void UnfinishedOccurrenceDefinitionsReturnSyntaxDiagnostics(string source)
    {
        var parsed=SceneAuthoring.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics,d => d.Code == "scene-syntax-invalid");
    }

    [Fact]
    public void SceneUsdAndGlbPreserveHierarchyCamerasAndInstancing()
    {
        var s=Compile("parts-and-pattern"); var usd=SceneExport.Usd(s);
        Assert.Contains("defaultPrim = \"Scene\"",usd); Assert.Contains("instanceable = true",usd); Assert.Contains("def Camera",usd);
        Assert.DoesNotContain("PhysicsRigidBodyAPI",usd); Assert.DoesNotContain("PhysicsFixedJoint",usd);
        var bytes=SceneExport.Glb(s); Assert.Equal(bytes,SceneExport.Glb(s));
        var length=BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12,4)); using var json=JsonDocument.Parse(bytes.AsMemory(20,length));
        Assert.Single(json.RootElement.GetProperty("meshes").EnumerateArray()); Assert.Single(json.RootElement.GetProperty("cameras").EnumerateArray());
        var bodyNodes=json.RootElement.GetProperty("nodes").EnumerateArray().Where(n => n.TryGetProperty("mesh",out _)).ToArray();
        Assert.Equal(bodyNodes[0].GetProperty("mesh").GetInt32(),bodyNodes[1].GetProperty("mesh").GetInt32());
    }

    [Fact]
    public void PascalCompatibilityAndPreferredFieldsHaveByteIdenticalPresentationArtifacts()
    {
        using var session=new FirmamentSceneSession(); var source=Source("room");
        var preferred=session.Compile(source); var pascal=session.Compile(FirmamentSourceSpelling.Normalize(source));
        Assert.True(preferred.IsSuccess); Assert.True(pascal.IsSuccess);
        Assert.Equal(SceneExport.Usd(preferred.Scene!),SceneExport.Usd(pascal.Scene!));
        Assert.Equal(SceneExport.Glb(preferred.Scene!),SceneExport.Glb(pascal.Scene!));
    }

    [Fact]
    public void WarehousePlacesThreeExistingRobotsWithoutSolvingMechanicalRelationshipsAtSceneRoot()
    {
        var s=Compile("warehouse"); Assert.Equal(3,s.Source.Occurrences.Count(o => o.Kind == "Assembly")); Assert.Equal(5,s.Source.Occurrences.Count(o => o.Path.Contains(".packages.",StringComparison.Ordinal)));
        var robot=s.Display.Occurrences.Single(o => o.Path == "WarehouseX0.robots.sorting.robot"); Assert.Equal(2500,robot.Transform[12]); Assert.Equal(2000,robot.Transform[13]);
        Assert.Single(s.Source.Cameras); Assert.All(s.Nodes,n => Assert.Equal("SceneFrame",n.PlacementAuthority));
        var shared=s.Display.Occurrences.Where(o => o.Path.EndsWith(".robot.Base",StringComparison.Ordinal)).Select(o => o.DefinitionId).Distinct().ToArray(); Assert.Single(shared);
    }

    [Fact]
    public void WarehouseCameraAndPlacementEditsReuseEveryEngineeringDefinition()
    {
        using var session=new FirmamentSceneSession(); var path=PathFor("warehouse"); var source=Source("warehouse");
        var first=session.Compile(source,path); Assert.True(first.IsSuccess,string.Join("\n",first.Diagnostics));
        var changed=source.Replace("position: [5.6m","position: [5.4m",StringComparison.Ordinal)
            .Replace("x: 2500mm","x: 2600mm",StringComparison.Ordinal);
        var second=session.Compile(changed,path); Assert.True(second.IsSuccess,string.Join("\n",second.Diagnostics));
        Assert.Equal(0,second.Scene!.Performance.RebuiltEngineeringDefinitions);
        Assert.Equal(first.Scene!.Performance.UniqueEngineeringDefinitions,second.Scene.Performance.ReusedEngineeringDefinitions);
        foreach(var d in first.Scene.Display.Definitions)
        {
            var reused=second.Scene.Display.Definitions.Single(v => v.Id == d.Id);
            Assert.Equal(d.Positions,reused.Positions); Assert.Equal(d.Indices,reused.Indices);
        }
        Assert.Equal(2600,second.Scene.Display.Occurrences.Single(o => o.Path == "WarehouseX0.robots.sorting.robot").Transform[12]);
        Assert.Equal(2500,first.Scene.Display.Occurrences.Single(o => o.Path == "WarehouseX0.robots.sorting.robot").Transform[12]);
    }
}
