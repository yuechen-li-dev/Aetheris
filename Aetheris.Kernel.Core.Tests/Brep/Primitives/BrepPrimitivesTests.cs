using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;

namespace Aetheris.Kernel.Core.Tests.Brep.Primitives;

public sealed class BrepPrimitivesTests
{
    [Fact]
    public void CreateBox_ProducesExpectedTopologyAndBindings()
    {
        var result = BrepPrimitives.CreateBox(4d, 6d, 8d);

        Assert.True(result.IsSuccess);
        var body = result.Value;

        Assert.Single(body.Topology.Bodies);
        Assert.Single(body.Topology.Shells);
        Assert.Equal(6, body.Topology.Faces.Count());
        Assert.Equal(6, body.Topology.Loops.Count());
        Assert.Equal(24, body.Topology.Coedges.Count());
        Assert.Equal(12, body.Topology.Edges.Count());
        Assert.Equal(8, body.Topology.Vertices.Count());

        Assert.All(body.Topology.Faces, face =>
        {
            var surface = body.GetFaceSurface(face.Id);
            Assert.Equal(SurfaceGeometryKind.Plane, surface.Kind);
        });

        Assert.All(body.Topology.Edges, edge =>
        {
            var curve = body.GetEdgeCurve(edge.Id);
            Assert.Equal(CurveGeometryKind.Line3, curve.Kind);
        });

        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);
    }

    [Theory]
    [InlineData(0d, 1d, 1d)]
    [InlineData(1d, 0d, 1d)]
    [InlineData(1d, 1d, 0d)]
    [InlineData(-1d, 1d, 1d)]
    [InlineData(1d, -1d, 1d)]
    [InlineData(1d, 1d, -1d)]
    [InlineData(double.NaN, 1d, 1d)]
    [InlineData(1d, double.PositiveInfinity, 1d)]
    public void CreateBox_InvalidDimensions_Fails(double width, double height, double depth)
    {
        var result = BrepPrimitives.CreateBox(width, height, depth);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument);
    }

    [Fact]
    public void CreateCylinder_ProducesExpectedSurfaceAndEdgeTypes_WithSeamStrategy()
    {
        var result = BrepPrimitives.CreateCylinder(2d, 10d);

        Assert.True(result.IsSuccess);
        var body = result.Value;

        Assert.Single(body.Topology.Bodies);
        Assert.Single(body.Topology.Shells);
        Assert.Equal(3, body.Topology.Faces.Count());
        Assert.Equal(3, body.Topology.Edges.Count());

        var surfaceKinds = body.Topology.Faces
            .Select(f => body.GetFaceSurface(f.Id).Kind)
            .OrderBy(kind => kind)
            .ToArray();

        Assert.Equal(
            [SurfaceGeometryKind.Plane, SurfaceGeometryKind.Plane, SurfaceGeometryKind.Cylinder],
            surfaceKinds);

        var curveKinds = body.Topology.Edges
            .Select(e => body.GetEdgeCurve(e.Id).Kind)
            .ToArray();

        Assert.Equal(2, curveKinds.Count(kind => kind == CurveGeometryKind.Circle3));
        Assert.Equal(1, curveKinds.Count(kind => kind == CurveGeometryKind.Line3));

        // The side face loop intentionally references the seam edge twice (forward and reversed).
        var sideFaceId = body.Topology.Faces.First().Id;
        var sideEdges = body.GetEdges(sideFaceId);
        Assert.Equal(3, sideEdges.Count);

        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);
    }

    [Theory]
    [InlineData(0d, 1d)]
    [InlineData(1d, 0d)]
    [InlineData(-1d, 1d)]
    [InlineData(1d, -1d)]
    [InlineData(double.NaN, 1d)]
    [InlineData(1d, double.NegativeInfinity)]
    public void CreateCylinder_InvalidInputs_Fails(double radius, double height)
    {
        var result = BrepPrimitives.CreateCylinder(radius, height);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument);
    }

    [Fact]
    public void CreateSphere_ProducesSingleClosedFaceWithSphereBinding()
    {
        var result = BrepPrimitives.CreateSphere(5d);

        Assert.True(result.IsSuccess);
        var body = result.Value;

        Assert.Single(body.Topology.Bodies);
        Assert.Single(body.Topology.Shells);
        Assert.Single(body.Topology.Faces);
        Assert.Empty(body.Topology.Loops);
        Assert.Empty(body.Topology.Edges);
        Assert.Empty(body.Topology.Vertices);

        var sphereFace = body.Topology.Faces.Single();
        Assert.Equal(SurfaceGeometryKind.Sphere, body.GetFaceSurface(sphereFace.Id).Kind);

        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);

        var bodyId = body.GetBodyIds().Single();
        var faceIds = body.GetFaces(bodyId);
        Assert.Single(faceIds);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void CreateSphere_InvalidRadius_Fails(double radius)
    {
        var result = BrepPrimitives.CreateSphere(radius);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument);
    }
}
