using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Brep.Features;

public sealed class BrepExtrudeTests
{
    [Fact]
    public void Create_RectangleProfile_ProducesExpectedTopologyAndBindings()
    {
        var profile = PolylineProfile2D.Rectangle(4d, 2d);
        var frame = new ExtrudeFrame3D(
            Point3D.Origin,
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        var result = BrepExtrude.Create(profile, frame, 3d);

        Assert.True(result.IsSuccess);
        var body = result.Value;

        Assert.Single(body.Topology.Bodies);
        Assert.Single(body.Topology.Shells);
        Assert.Equal(6, body.Topology.Faces.Count());
        Assert.Equal(6, body.Topology.Loops.Count());
        Assert.Equal(24, body.Topology.Coedges.Count());
        Assert.Equal(12, body.Topology.Edges.Count());
        Assert.Equal(8, body.Topology.Vertices.Count());

        Assert.All(body.Topology.Faces, face => Assert.Equal(SurfaceGeometryKind.Plane, body.GetFaceSurface(face.Id).Kind));
        Assert.All(body.Topology.Edges, edge => Assert.Equal(CurveGeometryKind.Line3, body.GetEdgeCurve(edge.Id).Kind));

        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);

        var bodyId = body.GetBodyIds().Single();
        Assert.Equal(6, body.GetFaces(bodyId).Count);
        foreach (var faceId in body.GetFaces(bodyId))
        {
            _ = body.GetEdges(faceId);
        }
    }

    [Fact]
    public void Create_DepthDirection_OffsetsTopCapAlongPositiveNormal()
    {
        var profile = PolylineProfile2D.Rectangle(2d, 2d);
        var frame = new ExtrudeFrame3D(
            new Point3D(10d, -3d, 5d),
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        var result = BrepExtrude.Create(profile, frame, 7d);

        Assert.True(result.IsSuccess);
        var topFace = result.Value.Topology.Faces.Single(face => face.Id.Value == 2);
        var topSurface = result.Value.GetFaceSurface(topFace.Id).Plane;
        Assert.NotNull(topSurface);
        Assert.Equal(new Point3D(10d, -3d, 12d), topSurface.Value.Origin);
        Assert.Equal(frame.Normal, topSurface.Value.Normal);
    }

    [Fact]
    public void Create_AcceptsClockwiseAndCounterClockwiseWinding()
    {
        var ccw = PolylineProfile2D.Create(
        [
            new ProfilePoint2D(0d, 0d),
            new ProfilePoint2D(3d, 0d),
            new ProfilePoint2D(3d, 1d),
            new ProfilePoint2D(0d, 1d),
        ]);
        var cw = PolylineProfile2D.Create(
        [
            new ProfilePoint2D(0d, 1d),
            new ProfilePoint2D(3d, 1d),
            new ProfilePoint2D(3d, 0d),
            new ProfilePoint2D(0d, 0d),
        ]);

        var frame = new ExtrudeFrame3D(
            Point3D.Origin,
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        var ccwResult = BrepExtrude.Create(ccw.Value, frame, 1d);
        var cwResult = BrepExtrude.Create(cw.Value, frame, 1d);

        Assert.True(ccwResult.IsSuccess);
        Assert.True(cwResult.IsSuccess);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Create_InvalidDepth_FailsWithDiagnostics(double depth)
    {
        var profile = PolylineProfile2D.Rectangle(2d, 2d);
        var frame = new ExtrudeFrame3D(
            Point3D.Origin,
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        var result = BrepExtrude.Create(profile, frame, depth);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument && d.Message.Contains("depth", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_BowTieProfile_FailsValidation_CurrentBehavior()
    {
        var bowTie = PolylineProfile2D.Create(
        [
            new ProfilePoint2D(0d, 0d),
            new ProfilePoint2D(2d, 2d),
            new ProfilePoint2D(0d, 2d),
            new ProfilePoint2D(2d, 0d),
        ]);

        Assert.False(bowTie.IsSuccess);
        Assert.Contains(bowTie.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument);
    }
}
