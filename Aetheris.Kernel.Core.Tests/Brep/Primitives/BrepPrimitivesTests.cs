using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

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
    public void CreateTorus_ProducesExpectedTopologyAndBindings_WithCircularSelfLoopSeams()
    {
        var result = BrepPrimitives.CreateTorus(5d, 2d);

        Assert.True(result.IsSuccess);
        var body = result.Value;

        Assert.Single(body.Topology.Bodies);
        Assert.Single(body.Topology.Shells);
        Assert.Single(body.Topology.Faces);
        Assert.Single(body.Topology.Loops);
        Assert.Equal(4, body.Topology.Coedges.Count());
        Assert.Equal(2, body.Topology.Edges.Count());
        Assert.Single(body.Topology.Vertices);

        var torusFace = Assert.Single(body.Topology.Faces);
        Assert.Equal(SurfaceGeometryKind.Torus, body.GetFaceSurface(torusFace.Id).Kind);

        var loopId = Assert.Single(body.GetLoopIds(torusFace.Id));
        var coedges = body.GetCoedgeIds(loopId)
            .Select(body.Topology.GetCoedge)
            .ToArray();

        Assert.Equal(4, coedges.Length);
        Assert.All(coedges, coedge => Assert.Equal(CurveGeometryKind.Circle3, body.GetEdgeCurve(coedge.EdgeId).Kind));
        Assert.Equal(2, coedges.Select(coedge => coedge.EdgeId).Distinct().Count());
        Assert.All(body.Topology.Edges, edge => Assert.Equal(edge.StartVertexId, edge.EndVertexId));
        Assert.Equal(4, coedges.Count(coedge => body.Topology.GetEdge(coedge.EdgeId).StartVertexId == body.Topology.GetEdge(coedge.EdgeId).EndVertexId));

        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);
    }

    [Theory]
    [InlineData(0d, 1d)]
    [InlineData(1d, 0d)]
    [InlineData(-1d, 1d)]
    [InlineData(1d, -1d)]
    [InlineData(1d, 1d)]
    [InlineData(1d, 2d)]
    [InlineData(double.NaN, 1d)]
    [InlineData(2d, double.PositiveInfinity)]
    public void CreateTorus_InvalidInputs_Fails(double majorRadius, double minorRadius)
    {
        var result = BrepPrimitives.CreateTorus(majorRadius, minorRadius);

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

    [Fact]
    public void CreateTriangularPrism_ProducesPlanarPolyhedralBody()
    {
        var result = BrepPrimitives.CreateTriangularPrism(8d, 6d, 10d);

        Assert.True(result.IsSuccess);
        var body = result.Value;
        Assert.Equal(5, body.Topology.Faces.Count());
        Assert.All(body.Topology.Faces, face =>
        {
            var surface = body.GetFaceSurface(face.Id);
            Assert.Equal(SurfaceGeometryKind.Plane, surface.Kind);
        });
    }

    [Fact]
    public void CreateTriangularPrism_UsesCenteredIsoscelesProfileContract()
    {
        var result = BrepPrimitives.CreateTriangularPrism(8d, 6d, 10d);

        Assert.True(result.IsSuccess);
        var step = Step242Exporter.ExportBody(result.Value);
        Assert.True(step.IsSuccess);
        var imported = Step242Importer.ImportBody(step.Value);
        Assert.True(imported.IsSuccess);
        var body = imported.Value;

        var vertices = body.Topology.Vertices
            .Select(vertex =>
            {
                Assert.True(body.TryGetVertexPoint(vertex.Id, out var point));
                return point;
            })
            .OrderBy(point => point.Z)
            .ThenBy(point => point.Y)
            .ThenBy(point => point.X)
            .ToArray();

        Assert.Equal(6, vertices.Length);
        Assert.Equal(-4d, vertices.Min(p => p.X), 8);
        Assert.Equal(4d, vertices.Max(p => p.X), 8);
        Assert.Equal(-3d, vertices.Min(p => p.Y), 8);
        Assert.Equal(3d, vertices.Max(p => p.Y), 8);
        Assert.Equal(-5d, vertices.Min(p => p.Z), 8);
        Assert.Equal(5d, vertices.Max(p => p.Z), 8);

        var expected = new (double X, double Y, double Z)[]
        {
            (-4d, -3d, -5d),
            (4d, -3d, -5d),
            (0d, 3d, -5d),
            (-4d, -3d, 5d),
            (4d, -3d, 5d),
            (0d, 3d, 5d),
        };

        Assert.Equal(expected.Length, vertices.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].X, vertices[index].X, 8);
            Assert.Equal(expected[index].Y, vertices[index].Y, 8);
            Assert.Equal(expected[index].Z, vertices[index].Z, 8);
        }
    }

    [Fact]
    public void CreateHexagonalPrism_ProducesPlanarPolyhedralBody()
    {
        var result = BrepPrimitives.CreateHexagonalPrism(10d, 12d);

        Assert.True(result.IsSuccess);
        var body = result.Value;
        Assert.Equal(8, body.Topology.Faces.Count());
        Assert.All(body.Topology.Faces, face =>
        {
            var surface = body.GetFaceSurface(face.Id);
            Assert.Equal(SurfaceGeometryKind.Plane, surface.Kind);
        });
    }

    [Fact]
    public void CreateStraightSlot_ProducesPlanarPolyhedralBody()
    {
        var result = BrepPrimitives.CreateStraightSlot(20d, 8d, 6d);

        Assert.True(result.IsSuccess);
        var body = result.Value;
        Assert.True(body.Topology.Faces.Count() >= 10);
        Assert.All(body.Topology.Faces, face =>
        {
            var surface = body.GetFaceSurface(face.Id);
            Assert.Equal(SurfaceGeometryKind.Plane, surface.Kind);
        });
    }
}
