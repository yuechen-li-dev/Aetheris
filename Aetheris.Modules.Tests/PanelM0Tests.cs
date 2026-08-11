using Aetheris.Kernel.Core.Math;
using Aetheris.Semantics;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Modules.Tests;

public sealed class PanelM0Tests
{
    [Fact]
    public void RectangularParametricPanelHasOrderedSemanticEdgesCornersAndStableIdentity()
    {
        var source=MathematicalSurfaces.HyperbolicParaboloid("saddle",20,15,6);
        var first=PanelFactory.FromParametric(source);var second=PanelFactory.FromParametric(source);
        Assert.True(first.IsSuccess,Evidence(first));Assert.True(second.IsSuccess,Evidence(second));
        var panel=first.Panel!;
        Assert.Equal("panel:saddle",panel.StableId);
        Assert.Equal(["South","East","North","West"],panel.BoundaryEdges.Select(edge=>edge.Name));
        Assert.Equal([0,1,2,3],panel.BoundaryEdges.Select(edge=>edge.BoundaryOrder));
        Assert.Equal(["NE","NW","SE","SW"],panel.Corners.Keys.Order());
        Assert.All(panel.BoundaryEdges,edge=>
        {
            Assert.True(edge.SemanticValue.Capabilities.Supports<CurveCapability>());
            Assert.True(edge.SemanticValue.Capabilities.Supports<BoundaryEdgeCapability>());
            Assert.True(edge.SemanticValue.TryBinding<ExactCurveBinding>(out _));
        });
        Assert.Equal(panel.BoundaryEdges.Select(edge=>edge.StableId),second.Panel!.BoundaryEdges.Select(edge=>edge.StableId));
        Assert.Equal(panel.Corners.Values.Select(corner=>corner.StableId).Order(),second.Panel.Corners.Values.Select(corner=>corner.StableId).Order());
        Assert.True(PanelConcept.Validate(panel).Satisfies);
    }

    [Fact]
    public void TypedRuledCanopyTemplateProducesPanelNotNakedSurface()
    {
        var result=RuledCanopyPanelTemplate.Create("template-canopy",60,30,8,1,"Aluminum");
        Assert.True(result.IsSuccess,Evidence(result));Assert.Equal("panel:template-canopy",result.Panel!.StableId);Assert.Equal(4,result.Panel.BoundaryEdges.Count);
    }

    [Fact]
    public void RuledPanelRetainsConstructionDevelopabilityAndOrientation()
    {
        var a=new RuledBoundary.Line("a",new(-5,0,0),new(5,0,0));var b=new RuledBoundary.Line("b",new(-5,5,2),new(5,5,2));
        var source=new RuledSurfaceIr("roof",RuledConstructionKind.RuledSurface,a,b,new("a","fixture","south"),new("b","fixture","north"));
        var result=PanelFactory.FromRuled(source,new(PanelNormalOrientation.ReversedSupportNormal,PanelMaterialSide.Back),1.2,"Aluminum");
        Assert.True(result.IsSuccess,Evidence(result));var panel=result.Panel!;
        Assert.Equal(SurfaceConstructionKind.RuledSurface,panel.SurfaceConstruction.Kind);
        Assert.Equal(DevelopabilityKind.Developable,panel.Developability.Kind);
        Assert.False(panel.Orientation.SameSense);Assert.Equal(1.2,panel.Thickness);Assert.Equal("Aluminum",panel.Material);
        Assert.Equal(["West","North","East","South"],panel.BoundaryEdges.Select(edge=>edge.Name));
        Assert.Empty(PanelManufacturability.RequireDevelopable(panel));
    }

    [Fact]
    public void BoundaryAndSectionPanelsUseHonestFourEdgeDomains()
    {
        var s=Line("s",0,0);var n=Line("n",5,1);var w=new RuledBoundary.Line("w",s.Start,n.Start);var e=new RuledBoundary.Line("e",s.End,n.End);
        var boundary=new BoundaryPatchIr("patch",s,n,w,e,[new("s","fixture","South"),new("n","fixture","North"),new("w","fixture","West"),new("e","fixture","East")]);
        var boundaryPanel=PanelFactory.FromBoundaryPatch(boundary);Assert.True(boundaryPanel.IsSuccess,Evidence(boundaryPanel));Assert.Equal(4,boundaryPanel.Panel!.BoundaryEdges.Count);
        var sections=new RuledBoundary[]{Line("s0",0,0),Line("s1",3,2),Line("s2",7,0)};
        var section=new SectionSurfaceIr("fairing",sections,sections.Select((item,index)=>new BoundaryProvenance(item.StableId,"fixture",$"section-{index}")).ToArray());
        var sectionPanel=PanelFactory.FromSectionSurface(section);Assert.True(sectionPanel.IsSuccess,Evidence(sectionPanel));Assert.Equal(SurfaceConstructionKind.SectionSurface,sectionPanel.Panel!.SurfaceConstruction.Kind);
    }

    [Fact]
    public void ExactG0NetworkReportsMatedFreeDuplicateAndG1Evidence()
    {
        var showcase=PanelShowcases.DevelopableFoldedCanopy();Assert.True(showcase.Network.IsSuccess,Evidence(showcase.Network));
        Assert.Equal(4,showcase.Panels.Count);Assert.Equal(3,showcase.Network.Mates.Count);Assert.Equal(10,showcase.Network.FreeEdges.Count);
        Assert.All(showcase.Panels,panel=>Assert.Equal(DevelopabilityKind.Developable,panel.Developability.Kind));
        Assert.All(showcase.Network.Mates,mate=>{Assert.Equal(0,mate.G0Residual,9);Assert.Equal("valid",mate.Status);});
        var duplicate=PanelNetworkValidator.Validate(showcase.Panels,[showcase.Mates[0],showcase.Mates[0] with{StableId="duplicate"}]);
        Assert.Contains(duplicate.Diagnostics,item=>item.Code=="panel-mate-edge-already-mated");
        var g1=PanelNetworkValidator.Validate(showcase.Panels,[showcase.Mates[0] with{Continuity=PanelContinuity.TangentG1}]);
        Assert.Contains(g1.Diagnostics,item=>item.Code=="panel-mate-g1-unsupported");
    }

    [Fact]
    public void GalleryEntriesAreFirstClassPanels()
    {
        var gallery=SurfacingGallery.Build();Assert.Equal(6,gallery.Count);
        Assert.All(gallery,entry=>{Assert.Equal("panel:"+entry.StableId,entry.Panel.StableId);Assert.Equal(4,entry.Panel.BoundaryEdges.Count);});
    }

    private static RuledBoundary.Line Line(string id,double y,double z)=>new(id,new(-5,y,z),new(5,y,z));
    private static string Evidence(PanelResult result)=>string.Join(Environment.NewLine,result.Diagnostics.Select(item=>$"{item.Code}: {item.Message}"));
    private static string Evidence(PanelNetworkReport result)=>string.Join(Environment.NewLine,result.Diagnostics.Select(item=>$"{item.Code}: {item.Message}"));
}
