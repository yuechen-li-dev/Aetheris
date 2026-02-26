using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Brep.Features;

public sealed class BrepRevolveTests
{
    private static readonly ExtrudeFrame3D DefaultFrame = new(
        Point3D.Origin,
        Direction3D.Create(new Vector3D(0d, 0d, 1d)),
        Direction3D.Create(new Vector3D(1d, 0d, 0d)));

    private static readonly RevolveAxis3D DefaultAxis = new(
        Point3D.Origin,
        new Vector3D(0d, 0d, 1d));

    [Fact]
    public void Create_CylinderSegmentProfile_ProducesExpectedTopologyBindingsAndTraversal()
    {
        var result = BrepRevolve.Create(
            [
                new ProfilePoint2D(2d, 0d),
                new ProfilePoint2D(2d, 5d),
            ],
            DefaultFrame,
            DefaultAxis);

        Assert.True(result.IsSuccess);
        var body = result.Value;

        Assert.Single(body.Topology.Bodies);
        Assert.Single(body.Topology.Shells);
        Assert.Equal(3, body.Topology.Faces.Count());
        Assert.Equal(3, body.Topology.Loops.Count());
        Assert.Equal(6, body.Topology.Coedges.Count());
        Assert.Equal(3, body.Topology.Edges.Count());
        Assert.Equal(4, body.Topology.Vertices.Count());

        Assert.Contains(body.Topology.Faces, f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder);
        Assert.Equal(2, body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane));
        Assert.Contains(body.Topology.Edges, e => body.GetEdgeCurve(e.Id).Kind == CurveGeometryKind.Line3);
        Assert.Equal(2, body.Topology.Edges.Count(e => body.GetEdgeCurve(e.Id).Kind == CurveGeometryKind.Circle3));

        var seamEdge = body.Topology.Edges.Single(edge => body.GetEdgeCurve(edge.Id).Kind == CurveGeometryKind.Line3);
        Assert.Equal(2, body.Topology.Coedges.Count(coedge => coedge.EdgeId == seamEdge.Id));

        var bodyId = body.GetBodyIds().Single();
        foreach (var faceId in body.GetFaces(bodyId))
        {
            _ = body.GetEdges(faceId);
        }

        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);
    }

    [Fact]
    public void Create_FrustomSegmentProfile_UsesConeSideSurface()
    {
        var result = BrepRevolve.Create(
            [
                new ProfilePoint2D(1d, 0d),
                new ProfilePoint2D(3d, 4d),
            ],
            DefaultFrame,
            DefaultAxis);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value.Topology.Faces, f => result.Value.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cone);
    }

    [Fact]
    public void Create_InvalidAxisDirection_FailsWithInvalidArgument()
    {
        var result = BrepRevolve.Create(
            [new ProfilePoint2D(1d, 0d), new ProfilePoint2D(1d, 2d)],
            DefaultFrame,
            new RevolveAxis3D(Point3D.Origin, new Vector3D(0d, 0d, 0d)));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument && d.Message.Contains("axis direction", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Create_InvalidAngle_FailsWithInvalidArgument(double angle)
    {
        var result = BrepRevolve.Create(
            [new ProfilePoint2D(1d, 0d), new ProfilePoint2D(1d, 2d)],
            DefaultFrame,
            DefaultAxis,
            angle);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument && d.Message.Contains("angleRadians", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_PartialAngle_IsExplicitlyNotImplementedInM11()
    {
        var result = BrepRevolve.Create(
            [new ProfilePoint2D(1d, 0d), new ProfilePoint2D(1d, 2d)],
            DefaultFrame,
            DefaultAxis,
            double.Pi);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.NotImplemented && d.Message.Contains("full revolve only", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_UnsupportedProfileShape_ReturnsNotImplemented()
    {
        var result = BrepRevolve.Create(
            [
                new ProfilePoint2D(1d, 0d),
                new ProfilePoint2D(1d, 1d),
                new ProfilePoint2D(2d, 2d),
            ],
            DefaultFrame,
            DefaultAxis);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.NotImplemented && d.Message.Contains("two-point", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_ProfileAxisCrossing_IsRejectedForM11Subset()
    {
        var result = BrepRevolve.Create(
            [new ProfilePoint2D(0d, 0d), new ProfilePoint2D(1d, 2d)],
            DefaultFrame,
            DefaultAxis);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument && d.Message.Contains("axis-touching/crossing", StringComparison.Ordinal));
    }
}
