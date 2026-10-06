using System.Text.Json;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.Scene;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class DisplayProjectionTests
{
    private static string PathFor(string relative) => FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/"+relative);
    [Theory]
    [InlineData("Basics/box.firmament")]
    [InlineData("Basics/cylinder.firmament")]
    [InlineData("DisplayProjection/sphere.firmament")]
    [InlineData("DisplayProjection/cone.firmament")]
    [InlineData("DisplayProjection/torus.firmament")]
    public void V2RetainsQualifiedEvaluableCirBesideBrepAndSerializesOnlySafeMetadata(string fixture)
    {
        var source = File.ReadAllText(PathFor(fixture));
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
        Assert.NotNull(built.Value.RuntimeBody);
        var cir = Assert.IsType<FirmamentCirRetention>(built.Value.Cir);
        Assert.Equal("cir-qualified", cir.Qualification);
        Assert.NotNull(cir.RuntimeRoot);
        Assert.Equal(cir.DefinitionId, FirmamentBuildAndExport.CompileSource(source).Value.Cir!.DefinitionId);
        Assert.Contains("function Field", cir.FieldSource);
        Assert.DoesNotContain("RuntimeRoot", JsonSerializer.Serialize(cir));
        Assert.DoesNotContain("RuntimeBody", JsonSerializer.Serialize(built.Value));
        var outside = new Point3D(cir.MaximumMm![0]+100, cir.MaximumMm[1]+100, cir.MaximumMm[2]+100);
        Assert.True(cir.RuntimeRoot!.Evaluate(outside)>0);
        var shader = CirShaderArtifactProvider.Resolve(cir);
        Assert.Equal("shader-artifact-bound", shader.Status);
        Assert.NotNull(shader.Artifact);
        Assert.Equal(cir.StructuralIdentity, shader.Artifact.SourceIdentity);
        Assert.Contains("@fragment", shader.Artifact.Wgsl);
        Assert.Equal(32, Assert.Single(shader.Artifact.Bindings).ByteSize);
        Assert.True(CirShaderArtifactProvider.Resolve(cir).CacheHit);
        var roundtrip = JsonSerializer.Deserialize<DisplayShaderArtifact>(JsonSerializer.Serialize(shader.Artifact))!;
        Assert.Equal(shader.Artifact.ShaderId, roundtrip.ShaderId);
        Assert.Equal(shader.Artifact.Wgsl, roundtrip.Wgsl);
    }

    [Fact]
    public void ExistingBoundedCsgMirrorRetainsItsEvaluableRootWithoutSerializingAst()
    {
        var compilation = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("fixtures/Compatibility/LegacyV1/Examples/w2_cylinder_root_blind_bore_semantic.firmament"));
        Assert.True(compilation.Compilation.IsSuccess);
        var mirror = compilation.Compilation.Value.PrimitiveExecutionResult!.NativeGeometryState.CirMirror;
        Assert.NotNull(mirror.RuntimeRoot);
        Assert.DoesNotContain("RuntimeRoot", JsonSerializer.Serialize(mirror));
        var retained = FirmamentCirRetention.FromRoot(mirror.RuntimeRoot, "compatibility-csg-test");
        Assert.Equal("cir-qualified",retained.Qualification);
        Assert.Equal("shader-artifact-bound",CirShaderArtifactProvider.Resolve(retained).Status);
        var normal = FirmamentBuildAndExport.CompileSource(FirmamentCorpusHarness.ReadFixtureText("fixtures/Compatibility/LegacyV1/Examples/w2_cylinder_root_blind_bore_semantic.firmament"));
        Assert.False(normal.IsSuccess);
        Assert.Contains(normal.Diagnostics,d=>d.Message.StartsWith("firmament-v2-source-required",StringComparison.Ordinal));
    }

    [Fact]
    public void HouseProjectsTheSameOwnedEnvironmentMaterialsAndCamerasFromSnapshot()
    {
        var root = PathFor("Scene/WarmModernHouse/house.firmament");
        var documents = Directory.GetFiles(Path.GetDirectoryName(root)!, "*.firmament")
            .ToDictionary(p => Path.GetFileName(p), File.ReadAllText);
        using var session = new FirmamentSceneSession();
        var result = session.CompileProject(new("house.firmament", documents!));
        Assert.True(result.IsSuccess, string.Join("\n",result.Diagnostics));
        var compiled = result.Scene!;
        var display = DisplayProjection.Project(compiled);
        Assert.Equal(94,display.Definitions.Count);
        Assert.Equal(254,display.Occurrences.Count);
        Assert.Equal(3,display.Cameras.Count);
        Assert.Equal(compiled.MinimumMm, display.MinimumMm);
        Assert.Equal(compiled.Appearances.Count, display.Occurrences.Count(o=>o.Material is not null));
        Assert.Contains(display.Occurrences,o=>o.Kind=="WindowFinish" && o.Material?.Opacity<1);
        Assert.Contains(display.Occurrences,o=>o.Kind=="EnvironmentPanel");
        Assert.Throws<ArgumentException>(()=>DisplayProjection.Project(compiled,["main.notABoundary"]));
        var repeated = session.CompileProject(new("house.firmament", documents!));
        Assert.Equal(0,repeated.Scene!.Performance.RebuiltEngineeringDefinitions);
        Assert.Equal(display.Occurrences.Select(o=>o.Id), repeated.Scene.Display.Occurrences.Select(o=>o.Id));
        var changedDocuments = new Dictionary<string,string>(documents!)
        { ["house.firmament"] = documents["house.firmament"].Replace("[.55, .35, .18]", "[.25, .35, .18]")
            .Replace("[2500mm, 1450mm]", "[2600mm, 1450mm]")
            .Replace("[8.1m, .65m, 1.65m]", "[8.2m, .65m, 1.65m]") };
        var edited = session.CompileProject(new("house.firmament",changedDocuments));
        Assert.True(edited.IsSuccess, string.Join("\n",edited.Diagnostics));
        var next = DisplayProjection.Project(edited.Scene!);
        Assert.Equal(0,edited.Scene!.Performance.RebuiltEngineeringDefinitions);
        Assert.Equal(display.Definitions.Select(d=>d.GeometryRevision),next.Definitions.Select(d=>d.GeometryRevision));
        Assert.NotEqual(display.Occurrences.Single(o=>o.Path=="WarmModernHouse.main.floor.panel0").Material!.BaseColor[0],
            next.Occurrences.Single(o=>o.Path=="WarmModernHouse.main.floor.panel0").Material!.BaseColor[0]);
        Assert.NotEqual(display.Occurrences.Single(o=>o.Path=="WarmModernHouse.living").Transform[12],next.Occurrences.Single(o=>o.Path=="WarmModernHouse.living").Transform[12]);
        Assert.NotEqual(display.Cameras.Single(c=>c.Name=="Hero").Transform[12], next.Cameras.Single(c=>c.Name=="Hero").Transform[12]);
    }

    [Fact]
    public void MissingSnapshotDependencyNeverFallsBackToDisk()
    {
        var source = File.ReadAllText(PathFor("Scene/WarmModernHouse/house.firmament"));
        using var session = new FirmamentSceneSession();
        var result = session.CompileProject(new("house.firmament", new Dictionary<string,string>{{"house.firmament",source}}));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,d=>d.Code=="scene-assembly-file-missing");
    }

    [Fact]
    public void ShaderIdentityFollowsGeometryAndRejectsCorruptTransport()
    {
        var source = File.ReadAllText(PathFor("Basics/cylinder.firmament"));
        var first = FirmamentBuildAndExport.CompileSource(source).Value.Cir!;
        var changed = FirmamentBuildAndExport.CompileSource(source.Replace("12mm", "14mm")).Value.Cir!;
        Assert.NotEqual(CirShaderArtifactProvider.Resolve(first).Artifact!.ShaderId, CirShaderArtifactProvider.Resolve(changed).Artifact!.ShaderId);
        Assert.Equal(CirShaderArtifactProvider.Resolve(first).Artifact!.ShaderId, CirShaderArtifactProvider.Resolve(first with { DefinitionId = "another-definition" }).Artifact!.ShaderId);
        var failure = CirShaderArtifactProvider.Resolve(first with { FieldSource = "corrupt" });
        Assert.Null(failure.Artifact);
        Assert.Equal("shader-artifact-generation-failed", failure.Status);
    }

    [Fact]
    public void NormalAssemblySharesBoundArtifactAcrossOccurrencesAndKeepsMeshFallback()
    {
        var compilation = new AssemblyM1Pipeline().CompileFile(PathFor("DisplayProjection/mixed.firmament"));
        Assert.True(compilation.IsSuccess, string.Join("\n",compilation.Diagnostics));
        var display = AssemblyDisplayMeshExporter.Export(compilation);
        Assert.Contains(display.Definitions, d=>d.Shader?.Artifact is not null);
        Assert.Contains(display.Definitions, d=>d.DisplayPath=="mesh");
        var field = Assert.Single(display.Definitions, d=>d.Shader?.Artifact is not null);
        Assert.Equal(2,display.Occurrences.Count(o=>o.DefinitionId==field.Id));
        Assert.NotEmpty(field.Edges);
        Assert.All(field.Ranges!,range=>Assert.Null(range.SemanticEntityId));
        Assert.Contains(display.Occurrences,o=>o.Material?.Metallic==.7);
    }

    [Fact]
    public void SceneMaterialAndPlacementEditsReuseAutomaticShaderArtifact()
    {
        var root = PathFor("DisplayProjection/scene.firmament");
        var assembly = File.ReadAllText(PathFor("DisplayProjection/mixed.firmament"));
        var documents = new Dictionary<string,string> { ["scene.firmament"]=File.ReadAllText(root), ["mixed.firmament"]=assembly };
        using var session = new FirmamentSceneSession();
        var first = session.CompileProject(new("scene.firmament",documents));
        Assert.True(first.IsSuccess,string.Join("\n",first.Diagnostics));
        var display = DisplayProjection.Project(first.Scene!);
        var shader = Assert.Single(display.Definitions,d=>d.Shader?.Artifact is not null).Shader!.Artifact!;
        documents["mixed.firmament"] = assembly.Replace("[.55,.18,.06]","[.1,.5,.2]");
        documents["scene.firmament"] = documents["scene.firmament"].Replace("[0mm,0mm,0mm]","[10mm,0mm,0mm]");
        var edited = session.CompileProject(new("scene.firmament",documents));
        Assert.True(edited.IsSuccess,string.Join("\n",edited.Diagnostics));
        var next = DisplayProjection.Project(edited.Scene!);
        var retained = Assert.Single(next.Definitions,d=>d.Shader?.Artifact is not null).Shader!;
        Assert.Equal(shader.ShaderId,retained.Artifact!.ShaderId);
        Assert.True(retained.CacheHit);
        Assert.Contains(next.Occurrences,o=>o.Material?.BaseColor[0]==.1);
        Assert.NotEqual(display.Occurrences.Last().Transform[12],next.Occurrences.Last().Transform[12]);
    }
}
