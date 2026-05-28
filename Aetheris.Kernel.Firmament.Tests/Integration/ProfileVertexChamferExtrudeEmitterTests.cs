using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

public class ProfileVertexChamferExtrudeEmitterTests
{
    [Theory]
    [InlineData("canonical rectangle", 10d, 8d, 6d, 1d)]
    [InlineData("larger valid chamfer", 10d, 8d, 6d, 2d)]
    [InlineData("non-square rectangle", 12d, 5d, 7d, 1d)]
    public void RectangleCasesEmitExpectedProfileAuthoredChamferTopology(string name, double width, double depth, double height, double chamferDistance)
    {
        var result = ProfileVertexChamferExtrudeEmitter.TryEmit(
            ProfileVertexChamferExtrudeRequest.Rectangle(width, depth, height, chamferDistance));

        Assert.Equal(ProfileVertexChamferExtrudeStatus.Succeeded, result.Status);
        Assert.Equal("profile-chamfer-emitter-ready-for-controlled-route-evaluation", result.Recommendation);
        Assert.NotNull(result.Body);
        Assert.Equal(5, result.ChamferedProfile.Count);
        Assert.Equal(5, result.Topology.ProfileVertexCount);
        Assert.Equal(10, result.Topology.VertexCount);
        Assert.Equal(15, result.Topology.EdgeCount);
        Assert.Equal(7, result.Topology.FaceCount);
        Assert.Equal(2, result.Topology.CapFaceCount);
        Assert.Equal(5, result.Topology.SideFaceCount);
        Assert.Equal(1, result.Topology.ChamferFaceCount);
        Assert.Equal(7, result.Topology.PlanarFaceCount);
        Assert.Equal(0, result.Topology.CylindricalFaceCount);
        Assert.True(result.Step.Exported, name);
        Assert.Empty(result.Step.MissingRequiredMarkers);
        Assert.Empty(result.Step.UnexpectedPresentMarkers);
        Assert.Contains("edge-profile-v1-step-smoke-succeeded", result.Diagnostics);
        Assert.Contains("edge-profile-v1-no-air-edge-sweep-used", result.Diagnostics);
        Assert.Contains("edge-profile-v1-no-brep-bounded-chamfer-used", result.Diagnostics);
        Assert.Contains("edge-profile-v1-no-topology-graft-used", result.Diagnostics);
        Assert.Contains("edge-profile-v1-no-3d-boolean-used", result.Diagnostics);

        var step = Step242Exporter.ExportBody(result.Body!);
        Assert.True(step.IsSuccess, name);
        Assert.Contains("ISO-10303-21", step.Value, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", step.Value, StringComparison.Ordinal);
        Assert.Contains("ADVANCED_FACE", step.Value, StringComparison.Ordinal);
        Assert.Contains("PLANE", step.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("CYLINDRICAL_SURFACE", step.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", step.Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zero chamfer", 10d, 8d, 6d, 0d, "edge-profile-v1-invalid-chamfer-distance-rejected")]
    [InlineData("negative chamfer", 10d, 8d, 6d, -1d, "edge-profile-v1-invalid-chamfer-distance-rejected")]
    [InlineData("too large chamfer", 10d, 8d, 6d, 8d, "edge-profile-v1-chamfer-distance-too-large-rejected")]
    [InlineData("zero width", 0d, 8d, 6d, 1d, "edge-profile-v1-invalid-dimensions-rejected")]
    [InlineData("negative depth", 10d, -8d, 6d, 1d, "edge-profile-v1-invalid-dimensions-rejected")]
    [InlineData("zero height", 10d, 8d, 0d, 1d, "edge-profile-v1-invalid-dimensions-rejected")]
    [InlineData("non-finite width", double.PositiveInfinity, 8d, 6d, 1d, "edge-profile-v1-invalid-dimensions-rejected")]
    public void RectangleInvalidCasesRejectDeterministically(string name, double width, double depth, double height, double chamferDistance, string expectedDiagnostic)
    {
        Assert.False(string.IsNullOrWhiteSpace(name));
        var result = ProfileVertexChamferExtrudeEmitter.TryEmit(
            ProfileVertexChamferExtrudeRequest.Rectangle(width, depth, height, chamferDistance));

        Assert.Equal(ProfileVertexChamferExtrudeStatus.Rejected, result.Status);
        Assert.Null(result.Body);
        Assert.Contains(expectedDiagnostic, result.Diagnostics);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.StartsWith("edge-profile-v1-request-rejected:", StringComparison.Ordinal));
        Assert.Equal("profile-chamfer-emitter-invalid-rejected", result.Recommendation);
        Assert.DoesNotContain("edge-profile-v1-profile-extrude-attempted", result.Diagnostics);
    }

    [Fact]
    public void GenericConvexPolygonCaseIsEvaluationOnlyAndEmitsExpectedCounts()
    {
        var request = new ProfileVertexChamferExtrudeRequest(
            [
                new(0d, 0d),
                new(4d, 0d),
                new(5d, 2d),
                new(2d, 4d),
                new(-1d, 2d),
            ],
            SelectedVertexIndex: 2,
            ChamferDistance: 0.75d,
            ExtrusionHeight: 3d);

        var result = ProfileVertexChamferExtrudeEmitter.TryEmit(request);

        Assert.Equal(ProfileVertexChamferExtrudeStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
        Assert.Equal(6, result.Topology.ProfileVertexCount);
        Assert.Equal(12, result.Topology.VertexCount);
        Assert.Equal(18, result.Topology.EdgeCount);
        Assert.Equal(8, result.Topology.FaceCount);
        Assert.Equal(6, result.Topology.SideFaceCount);
        Assert.Equal(1, result.Topology.ChamferFaceCount);
        Assert.Equal(8, result.Topology.PlanarFaceCount);
        Assert.Equal(0, result.Topology.CylindricalFaceCount);
        Assert.Contains("edge-profile-v1-no-3d-boolean-used", result.Diagnostics);
    }

    [Fact]
    public void GenericConcaveSelectedVertexRejects()
    {
        var request = new ProfileVertexChamferExtrudeRequest(
            [
                new(0d, 0d),
                new(4d, 0d),
                new(2d, 1d),
                new(4d, 4d),
                new(0d, 4d),
            ],
            SelectedVertexIndex: 2,
            ChamferDistance: 0.5d,
            ExtrusionHeight: 3d);

        var result = ProfileVertexChamferExtrudeEmitter.TryEmit(request);

        Assert.Equal(ProfileVertexChamferExtrudeStatus.Rejected, result.Status);
        Assert.Null(result.Body);
        Assert.Contains("edge-profile-v1-selected-vertex-not-convex-rejected", result.Diagnostics);
    }

    [Fact]
    public void GenericAdjacentEdgeTooShortRejects()
    {
        var request = new ProfileVertexChamferExtrudeRequest(
            [
                new(0d, 0d),
                new(0.25d, 0d),
                new(1d, 1d),
                new(0d, 2d),
            ],
            SelectedVertexIndex: 1,
            ChamferDistance: 0.5d,
            ExtrusionHeight: 3d);

        var result = ProfileVertexChamferExtrudeEmitter.TryEmit(request);

        Assert.Equal(ProfileVertexChamferExtrudeStatus.Rejected, result.Status);
        Assert.Null(result.Body);
        Assert.Contains("edge-profile-v1-adjacent-edge-too-short-rejected", result.Diagnostics);
    }
}
