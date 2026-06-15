using Aetheris.Kernel.Core.Air;

namespace Aetheris.Kernel.Core.Tests.Air;

public sealed class AirWrapperTests
{
    [Fact]
    public void AirPrismaticSectionTransitionWrapper_ProducesSplitPreservingSummary()
    {
        var summary = AirPrismaticSectionTransitionWrapper.LowerCanonicalRectangleInset();
        Assert.True(summary.Succeeded);
        Assert.Equal(AirNodeKind.PrismaticSectionTransition, summary.NodeKind);
        Assert.Equal(AirRouteKind.PrismaticSectionTransitionEmitter, summary.RouteKind);
        Assert.Equal(12, summary.TopologySummary.VertexCount);
        Assert.Equal(20, summary.TopologySummary.EdgeCount);
        Assert.Equal(10, summary.TopologySummary.FaceCount);
        Assert.Equal(10, summary.TopologySummary.PlanarFaceCount);
        Assert.Equal(0, summary.TopologySummary.CylindricalFaceCount);
        Assert.Equal(10, summary.TopologySummary.LoopCount);
        Assert.Equal(40, summary.TopologySummary.CoedgeCount);
        Assert.Equal(4, summary.TopologySummary.TransitionFaceCount);
        Assert.Equal("[-5,-4,0]..[5,4,6]", summary.TopologySummary.Bounds);
        Assert.True(summary.StepSmokeSummary.WasChecked);
        Assert.True(summary.StepSmokeSummary.Succeeded);
        Assert.Contains(summary.Diagnostics, d => d.Code == "air-x1-prismatic-split-preserving");
        Assert.Contains(summary.Diagnostics, d => d.Code == "air-x1-no-production-route-replacement");
        Assert.Contains(summary.Guarantees, g => g == "no coplanar merge");
    }

    [Fact]
    public void AirTopFaceLoopChamferWrapper_PreservesClassBMetadata()
    {
        var summary = AirTopFaceLoopChamferWrapper.LowerCanonicalTopFaceLoopChamfer();
        Assert.True(summary.Succeeded);
        Assert.Equal(AirNodeKind.TopFaceLoopChamfer, summary.NodeKind);
        Assert.Equal(AirRouteKind.TopFaceLoopChamferPrismatic, summary.RouteKind);
        Assert.Equal(AirSelectionClass.FaceBoundaryLoop, summary.Provenance.SelectionClass);
        Assert.Equal(AirRuleKind.UniformChamfer, summary.Provenance.RuleKind);
        Assert.Equal(12, summary.TopologySummary.VertexCount);
        Assert.Equal(20, summary.TopologySummary.EdgeCount);
        Assert.Equal(10, summary.TopologySummary.FaceCount);
        Assert.Equal(10, summary.TopologySummary.PlanarFaceCount);
        Assert.Equal(0, summary.TopologySummary.CylindricalFaceCount);
        Assert.Equal(2, summary.TopologySummary.CapFaceCount);
        Assert.Equal(4, summary.TopologySummary.SideFaceCount);
        Assert.Equal(4, summary.TopologySummary.TransitionFaceCount);
        Assert.Equal(4, summary.TopologySummary.ChamferFaceCount);
        Assert.Equal(10, summary.TopologySummary.LoopCount);
        Assert.Equal(40, summary.TopologySummary.CoedgeCount);
        Assert.Equal("[-5,-4,0]..[5,4,6]", summary.TopologySummary.Bounds);
        foreach (var code in new[] { "air-x1-class-b-face-boundary-loop", "air-x1-not-four-independent-single-edge-chamfers", "air-x1-no-air-edge-sweep-used", "air-x1-no-brep-bounded-chamfer-used", "air-x1-no-topology-graft-used", "air-x1-no-3d-boolean-used", "air-x1-no-coplanar-merge-used", "air-x1-no-production-route-replacement" })
            Assert.Contains(summary.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void AirWrappers_AreDeterministic()
    {
        AssertStable(AirPrismaticSectionTransitionWrapper.LowerCanonicalRectangleInset(), AirPrismaticSectionTransitionWrapper.LowerCanonicalRectangleInset());
        AssertStable(AirTopFaceLoopChamferWrapper.LowerCanonicalTopFaceLoopChamfer(), AirTopFaceLoopChamferWrapper.LowerCanonicalTopFaceLoopChamfer());
    }

    private static void AssertStable(AirLoweringSummary a, AirLoweringSummary b)
    {
        Assert.Equal(a.NodeKind, b.NodeKind);
        Assert.Equal(a.RouteKind, b.RouteKind);
        Assert.Equal(a.TopologySummary, b.TopologySummary);
        Assert.Equal(a.Diagnostics.Select(d => d.Code), b.Diagnostics.Select(d => d.Code));
        Assert.Equal(a.Guarantees, b.Guarantees);
        Assert.Equal(a.Recommendation, b.Recommendation);
    }
}
