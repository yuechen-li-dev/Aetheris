using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class BezierSectionStackTests
{
    [Fact]
    public void CodexEngravesOnThreadedHexBoltAndRoundTripsStep()
    {
        var source=File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","fixtures","Thread","hexbolt-threaded.firmament")));
        var mark=ThreadedHexBoltMakerMark.Build(source,"CODEX",2.5,.2);
        Assert.Equal(2,mark.Text.CounterCount);
        Assert.Equal(3,mark.Section.Slabs.Last().MaterialRegions.Count);
        var mass=BrepMassProperties.Evaluate(mark.Body);
        Assert.True(mass.IsEnclosed,string.Join("; ",mass.Diagnostics));
        Assert.True(mass.IsOrientationConsistent,string.Join("; ",mass.Diagnostics));
        var imported=Step242Importer.ImportBody(mark.StepText);
        Assert.True(imported.IsSuccess,string.Join("; ",imported.Diagnostics.Select(x=>x.Message)));
        var importedMass=BrepMassProperties.Evaluate(imported.Value!);
        Assert.True(importedMass.IsEnclosed,string.Join("; ",importedMass.Diagnostics));
        Assert.True(importedMass.IsOrientationConsistent,string.Join("; ",importedMass.Diagnostics));
        if (Environment.GetEnvironmentVariable("AETHERIS_MAKER_MARK_ARTIFACT_DIR") is { Length: > 0 } artifactDirectory)
        {
            Directory.CreateDirectory(artifactDirectory);
            File.WriteAllText(Path.Combine(artifactDirectory,"codex-threaded-hexbolt.step"),mark.StepText);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenericCubicSectionBossAndPocketRemainExact(bool pocket)
    {
        var mark = CubicRegion();
        Assert.True(ResolvedProfile2DValidator.Validate(mark).IsValid);
        Qualify(mark,pocket);
    }

    [Fact]
    public void CounterPocketReconnectsThroughUnderlyingPlate()
    {
        var mark=Assert.Single(PlanarTextProfiles.Build("O",3).Regions);
        var plate=Rectangle();
        var feature=new PrismaticProfileCompositionFeature("CounterPocket","XY","+Z",
            new PrismaticProfilePlacement("P",0d,0d,0d,"XY","+Z","+X",false),
            [new("Plate",PrismaticProfileIntent.Base,plate.Name,0d,1d,"Stock","test"),
             new("Mark",PrismaticProfileIntent.Remove,mark.Name,.8d,1d,"Pocket","test")],
            [0d,.8d,1d],"test");
        var parsed=new PrismaticProfileCompositionParseResult(feature,
            new Dictionary<string,ResolvedProfile2D> { [plate.Name]=plate,[mark.Name]=mark },[]);
        var stack=PrismaticSectionStackCompiler.Normalize(parsed,out var diagnostics);
        Assert.NotNull(stack);
        Assert.Empty(diagnostics);
        Assert.Equal(2,stack.Slabs.Single(x=>x.From==.8d).MaterialRegions.Count);
        var emitted=PrismaticSectionStackEmitter.Emit(stack);
        Assert.NotNull(emitted.Body);
        Assert.DoesNotContain(emitted.Diagnostics,x=>x.Contains("non-manifold",StringComparison.Ordinal));
        var mass=BrepMassProperties.Evaluate(emitted.Body!);
        Assert.True(mass.IsEnclosed,string.Join("; ",mass.Diagnostics));
        Assert.True(mass.IsOrientationConsistent,string.Join("; ",mass.Diagnostics));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurvedProfileFlowsThroughOrdinarySectionStackAndStep(bool pocket)
    {
        var mark = Assert.Single(PlanarTextProfiles.Build(pocket ? "C" : "O", 3).Regions);
        Qualify(mark,pocket);
    }

    [Theory]
    [InlineData("D", true)]
    [InlineData("OO", true)]
    [InlineData("CODEX", true)]
    [InlineData("AETHERIS", false)]
    public void MultipleTextRegionsShareOneConnectedPlate(string word, bool pocket)
    {
        var text=PlanarTextProfiles.Build(word,2.5,PlanarTextAlignment.Center);
        Assert.True(text.Succeeded,string.Join("; ",text.Diagnostics));
        var plate=Rectangle(30d,12d);
        var operations=new List<PrismaticProfileOperation> { new("Plate",PrismaticProfileIntent.Base,plate.Name,0d,1d,"Stock","test") };
        operations.AddRange(text.Regions.Select((region,index)=>new PrismaticProfileOperation($"Mark{index}",
            pocket ? PrismaticProfileIntent.Remove : PrismaticProfileIntent.Add,region.Name,
            pocket ? .8d : 1d,pocket ? 1d : 1.2d,pocket ? "Pocket" : "Boss","test")));
        var feature=new PrismaticProfileCompositionFeature(word,"XY","+Z",
            new PrismaticProfilePlacement("P",0d,0d,0d,"XY","+Z","+X",false),operations,
            pocket ? [0d,.8d,1d] : [0d,1d,1.2d],"test");
        var profiles=text.Regions.Append(plate).ToDictionary(x=>x.Name);
        var stack=PrismaticSectionStackCompiler.Normalize(new(feature,profiles,[]),out var diagnostics);
        Assert.True(stack is not null,string.Join("; ",diagnostics));
        Assert.Empty(diagnostics);
        Assert.Equal(pocket ? text.CounterCount+1 : text.Regions.Count,stack.Slabs.Last().MaterialRegions.Count);
        if (word == "CODEX")
            Assert.Contains(stack.Slabs.Last().Arrangement!.AtomicFragments, fragment =>
                fragment.StableId.Contains("Glyph[4].Region[0].Outer.Outer.Segment[5]",StringComparison.Ordinal)
                && fragment.FromParameter == 0d && fragment.ToParameter > .999d);
        var emitted=PrismaticSectionStackEmitter.Emit(stack);
        Assert.NotNull(emitted.Body);
        var mass=BrepMassProperties.Evaluate(emitted.Body!);
        Assert.True(mass.IsEnclosed,string.Join("; ",mass.Diagnostics));
        Assert.True(mass.IsOrientationConsistent,string.Join("; ",mass.Diagnostics));
        var step=Step242Exporter.ExportBody(emitted.Body!);
        Assert.True(step.IsSuccess,string.Join("; ",step.Diagnostics.Select(x=>x.Message)));
        var imported=Step242Importer.ImportBody(step.Value!);
        Assert.True(imported.IsSuccess,string.Join("; ",imported.Diagnostics.Select(x=>x.Message)));
        var importedMass=BrepMassProperties.Evaluate(imported.Value!);
        Assert.True(importedMass.IsEnclosed,string.Join("; ",importedMass.Diagnostics));
        Assert.True(importedMass.IsOrientationConsistent,string.Join("; ",importedMass.Diagnostics));
    }

    private static void Qualify(ResolvedProfile2D mark,bool pocket)
    {
        var plate = Rectangle();
        var operations = new[]
        {
            new PrismaticProfileOperation("Plate",PrismaticProfileIntent.Base,plate.Name,0d,1d,"Stock","test"),
            new PrismaticProfileOperation("Mark",pocket ? PrismaticProfileIntent.Remove : PrismaticProfileIntent.Add,
                mark.Name,pocket ? .8d : 1d,pocket ? 1d : 1.2d,pocket ? "Pocket" : "Boss","test")
        };
        var feature = new PrismaticProfileCompositionFeature("BezierPlate","XY","+Z",
            new PrismaticProfilePlacement("P",0d,0d,0d,"XY","+Z","+X",false),operations,
            pocket ? [0d,.8d,1d] : [0d,1d,1.2d],"test");
        var parsed = new PrismaticProfileCompositionParseResult(feature,
            new Dictionary<string,ResolvedProfile2D> { [plate.Name]=plate,[mark.Name]=mark },[]);
        var stack = PrismaticSectionStackCompiler.Normalize(parsed,out var diagnostics);
        Assert.True(stack is not null,string.Join("; ",diagnostics));
        Assert.Empty(diagnostics);
        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        Assert.NotNull(emitted.Body);
        var body = emitted.Body!;
        Assert.Contains(body.Geometry.Surfaces, x => x.Value.Kind==SurfaceGeometryKind.LinearExtrusion);
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed,string.Join("; ",mass.Diagnostics));
        Assert.True(mass.IsOrientationConsistent,string.Join("; ",mass.Diagnostics));
        var step=Step242Exporter.ExportBody(body);
        Assert.True(step.IsSuccess,string.Join("; ",step.Diagnostics.Select(x=>x.Message)));
        var imported=Step242Importer.ImportBody(step.Value!);
        Assert.True(imported.IsSuccess,string.Join("; ",imported.Diagnostics.Select(x=>x.Message)));
        Assert.Contains(imported.Value!.Geometry.Surfaces,x=>x.Value.Kind==SurfaceGeometryKind.LinearExtrusion);
        var originalCurves=body.Geometry.Surfaces.Select(entry=>entry.Value)
            .Where(x=>x.Kind==SurfaceGeometryKind.LinearExtrusion)
            .Select(x=>x.LinearExtrusion!.Value.Directrix.BSpline3!.Value)
            .OrderBy(x=>x.ControlPoints[0].X).ThenBy(x=>x.ControlPoints[0].Y).ToArray();
        var importedCurves=imported.Value.Geometry.Surfaces.Select(entry=>entry.Value)
            .Where(x=>x.Kind==SurfaceGeometryKind.LinearExtrusion)
            .Select(x=>x.LinearExtrusion!.Value.Directrix.BSpline3!.Value)
            .OrderBy(x=>x.ControlPoints[0].X).ThenBy(x=>x.ControlPoints[0].Y).ToArray();
        Assert.Equal(originalCurves.Length,importedCurves.Length);
        for (var i=0;i<originalCurves.Length;i++)
        for (var j=0;j<4;j++)
        {
            Assert.Equal(originalCurves[i].ControlPoints[j].X,importedCurves[i].ControlPoints[j].X,7);
            Assert.Equal(originalCurves[i].ControlPoints[j].Y,importedCurves[i].ControlPoints[j].Y,7);
            Assert.Equal(originalCurves[i].ControlPoints[j].Z,importedCurves[i].ControlPoints[j].Z,7);
        }
    }

    private static ResolvedProfile2D CubicRegion()
    {
        LineArcProfileCurve2D[] geometry =
        [
            new LineArcLineSegment2D((-1d,-1d),(1d,-1d)),
            new LineArcCubicBezier2D((1d,-1d),(2d,-.5d),(2d,.5d),(1d,1d)),
            new LineArcLineSegment2D((1d,1d),(-1d,1d)),
            new LineArcLineSegment2D((-1d,1d),(-1d,-1d))
        ];
        return new("CubicRegion","XY",[new ResolvedProfileLoop2D("Outer",true,
            geometry.Select((curve,i)=>new ResolvedProfileSegment2D($"Edge{i}",curve,
                new ProfileSegmentProvenance($"cubic:{i}","cubic","test","test","XY"))).ToArray())]);
    }

    private static ResolvedProfile2D Rectangle(double width=10d,double height=10d)
    {
        var points=new[] {(-width/2,-height/2),(width/2,-height/2),(width/2,height/2),(-width/2,height/2)};
        return new("Plate","XY",[new ResolvedProfileLoop2D("Outer",true,
            Enumerable.Range(0,4).Select(i=>new ResolvedProfileSegment2D($"Edge{i}",
                new LineArcLineSegment2D(points[i],points[(i+1)%4]),
                new ProfileSegmentProvenance($"plate:{i}","plate","test","test","XY"))).ToArray())]);
    }
}
