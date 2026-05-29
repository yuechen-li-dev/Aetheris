using Aetheris.Kernel.Core.Brep.Prismatic;

namespace Aetheris.Kernel.Core.Tests;

public sealed class PrismaticTopEdgeChamferPrototypeTests
{
    [Theory]
    [InlineData(10, 8, 6, 1, "[-5,-4,0]..[5,4,6]")]
    [InlineData(10, 8, 6, 2, "[-5,-4,0]..[5,4,6]")]
    public void ValidTopPositiveXChamfer_UsesPrismaticEmitterAndMatchesControlledTopology(double width, double depth, double height, double chamferDistance, string bounds)
    {
        var result = PrismaticTopEdgeChamferPrototype.Emit(new(width, depth, height, chamferDistance, ExportStep: true));

        Assert.True(result.Succeeded);
        Assert.Equal(PrismaticTopEdgeChamferStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
        Assert.Equal("prismatic-top-edge-chamfer-ready-for-controlled-route-evaluation", result.Recommendation);
        Assert.Contains("edge-prismatic-v2-prismatic-emitter-invoked", result.Diagnostics);
        Assert.Contains("edge-prismatic-v2-body-created", result.Diagnostics);
        Assert.Equal(12, result.Topology.VertexCount);
        Assert.Equal(20, result.Topology.EdgeCount);
        Assert.Equal(10, result.Topology.FaceCount);
        Assert.Equal(10, result.Topology.PlanarFaceCount);
        Assert.Equal(0, result.Topology.CylindricalFaceCount);
        Assert.Equal(4, result.Topology.LowerPrismSideFaceCount);
        Assert.Equal(4, result.Topology.TransitionFaceCount);
        Assert.Equal(1, result.Topology.ChamferTransitionFaceCount);
        Assert.Equal(10, result.Topology.LoopCount);
        Assert.Equal(40, result.Topology.CoedgeCount);
        Assert.Equal(bounds, result.Topology.Bounds);
        Assert.Contains("edge-prismatic-v2-topology-validated", result.Diagnostics);
        Assert.True(result.Step.Exported);
        Assert.Empty(result.Step.MissingRequiredMarkers);
        Assert.Empty(result.Step.UnexpectedPresentMarkers);
        Assert.Contains("ISO-10303-21", result.Step.PresentMarkers);
        Assert.Contains("MANIFOLD_SOLID_BREP", result.Step.PresentMarkers);
        Assert.Contains("ADVANCED_FACE", result.Step.PresentMarkers);
        Assert.Contains("PLANE", result.Step.PresentMarkers);
        Assert.Contains("CYLINDRICAL_SURFACE", result.Step.AbsentMarkers);
        Assert.Contains("BREP_WITH_VOIDS", result.Step.AbsentMarkers);
        Assert.Contains("edge-prismatic-v2-step-smoke-succeeded", result.Diagnostics);
        Assert.Contains("edge-prismatic-v2-no-air-edge-sweep-used", result.Diagnostics);
        Assert.Contains("edge-prismatic-v2-no-brep-bounded-chamfer-used", result.Diagnostics);
        Assert.Contains("edge-prismatic-v2-no-topology-graft-used", result.Diagnostics);
        Assert.Contains("edge-prismatic-v2-no-3d-boolean-used", result.Diagnostics);
        Assert.Contains("edge-prismatic-v2-no-production-route-replacement", result.Diagnostics);
    }

    [Fact]
    public void CanonicalSectionStack_IsExplicitIdentityCompatibleTopPositiveXInset()
    {
        var sections = PrismaticTopEdgeChamferPrototype.CreateSectionStack(new(10, 8, 6, 1));

        Assert.Equal([0d, 5d, 6d], sections.Select(s => s.Z));
        Assert.Equal([(-5d, -4d), (5d, -4d), (5d, 4d), (-5d, 4d)], sections[0].OuterLoop);
        Assert.Equal(sections[0].OuterLoop, sections[1].OuterLoop);
        Assert.Equal([(-5d, -4d), (4d, -4d), (4d, 4d), (-5d, 4d)], sections[2].OuterLoop);
        Assert.Equal([0, 1, 2, 3], PrismaticCorrespondenceMap.Identity(4).VertexMap);
    }

    [Theory]
    [InlineData(0, 8, 6, 1, "edge-prismatic-v2-invalid-dimensions-rejected")]
    [InlineData(-10, 8, 6, 1, "edge-prismatic-v2-invalid-dimensions-rejected")]
    [InlineData(10, 0, 6, 1, "edge-prismatic-v2-invalid-dimensions-rejected")]
    [InlineData(10, 8, 0, 1, "edge-prismatic-v2-invalid-dimensions-rejected")]
    [InlineData(double.NaN, 8, 6, 1, "edge-prismatic-v2-invalid-dimensions-rejected")]
    [InlineData(10, double.PositiveInfinity, 6, 1, "edge-prismatic-v2-invalid-dimensions-rejected")]
    [InlineData(10, 8, 6, 0, "edge-prismatic-v2-invalid-chamfer-distance-rejected")]
    [InlineData(10, 8, 6, -1, "edge-prismatic-v2-invalid-chamfer-distance-rejected")]
    [InlineData(10, 8, 6, double.NaN, "edge-prismatic-v2-invalid-chamfer-distance-rejected")]
    [InlineData(10, 8, 6, double.PositiveInfinity, "edge-prismatic-v2-invalid-chamfer-distance-rejected")]
    [InlineData(10, 8, 6, 5, "edge-prismatic-v2-invalid-chamfer-distance-rejected")]
    public void InvalidDimensionsAndChamferDistances_RejectBeforePrismaticEmitter(double width, double depth, double height, double chamferDistance, string diagnostic)
    {
        var result = PrismaticTopEdgeChamferPrototype.Emit(new(width, depth, height, chamferDistance, ExportStep: true));

        Assert.False(result.Succeeded);
        Assert.Equal(PrismaticTopEdgeChamferStatus.Rejected, result.Status);
        Assert.Null(result.Body);
        Assert.False(result.Topology.BodyProduced);
        Assert.False(result.Step.Exported);
        Assert.Contains(diagnostic, result.Diagnostics);
        Assert.DoesNotContain("edge-prismatic-v2-prismatic-emitter-invoked", result.Diagnostics);
        Assert.Equal("prismatic-top-edge-chamfer-invalid-rejected", result.Recommendation);
    }

    [Fact]
    public void UnsupportedSelection_RejectsBeforePrismaticEmitter()
    {
        var result = PrismaticTopEdgeChamferPrototype.Emit(new(10, 8, 6, 1, PrismaticTopEdgeChamferSelection.TopNegativeXSide, ExportStep: true));

        Assert.False(result.Succeeded);
        Assert.Equal(PrismaticTopEdgeChamferStatus.Rejected, result.Status);
        Assert.Contains("edge-prismatic-v2-unsupported-selection-rejected", result.Diagnostics);
        Assert.DoesNotContain("edge-prismatic-v2-prismatic-emitter-invoked", result.Diagnostics);
        Assert.Equal("prismatic-top-edge-chamfer-invalid-rejected", result.Recommendation);
    }
}
