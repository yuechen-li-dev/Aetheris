using Aetheris.Kernel.Firmament.Materializer;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalMaterialAreaAuthorityTests
{
    [Fact]
    public void AddProfileDelta_IncreasesAreaFromFinalExactRegion()
    {
        var result=Compile("profiledelta-add-area.firmament");
        var wall=Assert.Single(result.Part!.Regions,x=>x.StableId=="Wall");
        Assert.NotNull(wall.ExactContour);
        Assert.Equal(3590d,PlanarContourKernel.Area(wall.ExactContour!),8);
        Assert.Equal(3590d,wall.ApproximateArea,8);
        Assert.NotNull(result.FlatPattern!.ExactBlankContour);
        Assert.Equal(PlanarContourKernel.Area(result.FlatPattern.ExactBlankContour!),result.FlatPattern.MaterialArea!.Value,10);
        Assert.True(result.FlatPattern.MaterialArea<result.FlatPattern.BoundingArea);
    }

    [Fact]
    public void RemoveProfileDelta_DecreasesAreaFromFinalExactRegion()
    {
        var result=Compile("profiledelta-remove-area.firmament");
        var wall=Assert.Single(result.Part!.Regions,x=>x.StableId=="Wall");
        Assert.NotNull(wall.ExactContour);
        Assert.Equal(3410d,PlanarContourKernel.Area(wall.ExactContour!),8);
        Assert.Equal(3410d,wall.ApproximateArea,8);
        Assert.Equal(PlanarContourKernel.Area(result.FlatPattern!.ExactBlankContour!),result.FlatPattern.MaterialArea!.Value,10);
    }

    [Fact]
    public void InnerCut_IsSubtractedByExactContourIntegration()
    {
        var outer=PlanarContourKernel.FromPolygon("outer","XY",[(0,0),(20,0),(20,10),(0,10)],"test");
        var inner=PlanarContourKernel.FromPolygon("inner","XY",[(5,2),(9,2),(9,5),(5,5)],"test").OuterLoop with { IsOuter=false,Segments=PlanarContourKernel.FromPolygon("inner-reverse","XY",[(5,5),(9,5),(9,2),(5,2)],"test").OuterLoop.Segments };
        var contour=outer with { InnerLoops=[inner] };
        Assert.Equal(188d,PlanarContourKernel.Area(contour),10);
    }

    [Fact]
    public void AddProfileDeltaAndHole_ComposeInOneFinalMaterialRegion()
    {
        var tab=Compile("profiledelta-add-area.firmament");
        var tabAndHole=Compile("profiledelta-add-hole-area.firmament");
        Assert.Equal(tab.FlatPattern!.MaterialArea!.Value-Math.PI*4d*4d,tabAndHole.FlatPattern!.MaterialArea!.Value,8);
        Assert.Single(tabAndHole.FlatPattern.CutLoops);
        Assert.Single(tabAndHole.FlatPattern.ExactBlankContour!.InnerLoops);
    }

    private static SheetMetalAuthoringResult Compile(string fixture)
    {
        var path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../fixtures/Canonical/SheetMetal",fixture));
        var result=SheetMetalFirmament.Compile(File.ReadAllText(path));
        Assert.True(result.IsSuccess,string.Join("; ",result.Diagnostics.Select(x=>$"{x.Code}:{x.Message}")));
        return result;
    }
}
