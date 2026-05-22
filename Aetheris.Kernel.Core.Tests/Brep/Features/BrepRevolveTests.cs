using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

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
    public void Create_PointedConeTop_ConstructsTruthfulTopologyAndExportsDeterministically()
    {
        var first = BrepRevolve.Create(
            [
                new ProfilePoint2D(2d, 0d),
                new ProfilePoint2D(0d, 5d),
            ],
            DefaultFrame,
            DefaultAxis);

        var second = BrepRevolve.Create(
            [
                new ProfilePoint2D(2d, 0d),
                new ProfilePoint2D(0d, 5d),
            ],
            DefaultFrame,
            DefaultAxis);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        AssertPointedConeTopology(
            first.Value,
            expectedCircularEdgeCount: 1,
            expectedPlanarFaceCount: 1,
            expectedBasePlaneNormalZ: -1d);

        var firstExport = Step242Exporter.ExportBody(first.Value);
        var secondExport = Step242Exporter.ExportBody(second.Value);

        Assert.True(firstExport.IsSuccess);
        Assert.True(secondExport.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(firstExport.Value));
        Assert.Equal(firstExport.Value, secondExport.Value);
        Assert.Contains("CONICAL_SURFACE", firstExport.Value, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(firstExport.Value, "CIRCLE("));
    }

    [Fact]
    public void Create_PointedConeBottom_ConstructsTruthfulTopologyAndExportsDeterministically()
    {
        var result = BrepRevolve.Create(
            [
                new ProfilePoint2D(0d, 0d),
                new ProfilePoint2D(2d, 5d),
            ],
            DefaultFrame,
            DefaultAxis);

        Assert.True(result.IsSuccess);

        AssertPointedConeTopology(
            result.Value,
            expectedCircularEdgeCount: 1,
            expectedPlanarFaceCount: 1,
            expectedBasePlaneNormalZ: 1d);

        var export = Step242Exporter.ExportBody(result.Value);

        Assert.True(export.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(export.Value));
        Assert.Contains("CONICAL_SURFACE", export.Value, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(export.Value, "CIRCLE("));
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
            [new ProfilePoint2D(-0.1d, 0d), new ProfilePoint2D(1d, 2d)],
            DefaultFrame,
            DefaultAxis);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument && d.Message.Contains("greater than or equal to zero", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_BothRadiiZero_IsRejected()
    {
        var result = BrepRevolve.Create(
            [new ProfilePoint2D(0d, 0d), new ProfilePoint2D(0d, 2d)],
            DefaultFrame,
            DefaultAxis);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument && d.Message.Contains("must not both be zero", StringComparison.Ordinal));
    }

    private static void AssertPointedConeTopology(
        BrepBody body,
        int expectedCircularEdgeCount,
        int expectedPlanarFaceCount,
        double expectedBasePlaneNormalZ)
    {
        Assert.Single(body.Topology.Bodies);
        Assert.Single(body.Topology.Shells);
        Assert.Equal(2, body.Topology.Faces.Count());
        Assert.Equal(2, body.Topology.Loops.Count());
        Assert.Equal(4, body.Topology.Coedges.Count());
        Assert.Equal(2, body.Topology.Edges.Count());
        Assert.Equal(3, body.Topology.Vertices.Count());

        Assert.Equal(1, body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cone));
        Assert.Equal(expectedPlanarFaceCount, body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane));
        Assert.Equal(1, body.Topology.Edges.Count(e => body.GetEdgeCurve(e.Id).Kind == CurveGeometryKind.Line3));
        Assert.Equal(expectedCircularEdgeCount, body.Topology.Edges.Count(e => body.GetEdgeCurve(e.Id).Kind == CurveGeometryKind.Circle3));

        var seamEdge = body.Topology.Edges.Single(edge => body.GetEdgeCurve(edge.Id).Kind == CurveGeometryKind.Line3);
        Assert.Equal(2, body.Topology.Coedges.Count(coedge => coedge.EdgeId == seamEdge.Id));

        var circularEdge = body.Topology.Edges.Single(edge => body.GetEdgeCurve(edge.Id).Kind == CurveGeometryKind.Circle3);
        Assert.Equal(2, body.Topology.Coedges.Count(coedge => coedge.EdgeId == circularEdge.Id));

        var coneFace = body.Topology.Faces.Single(face => body.GetFaceSurface(face.Id).Kind == SurfaceGeometryKind.Cone);
        Assert.Equal(3, body.GetCoedgeIds(body.GetLoopIds(coneFace.Id).Single()).Count);

        var planarFace = body.Topology.Faces.Single(face => body.GetFaceSurface(face.Id).Kind == SurfaceGeometryKind.Plane);
        Assert.Single(body.GetCoedgeIds(body.GetLoopIds(planarFace.Id).Single()));
        var plane = body.GetFaceSurface(planarFace.Id).Plane!.Value;
        Assert.Equal(expectedBasePlaneNormalZ, plane.Normal.ToVector().Z, 12);

        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
