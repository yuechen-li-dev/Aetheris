using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class BrepBindingModelAndBodyTests
{
    [Fact]
    public void BrepBody_EdgeAndFaceBindingLookupResolvesGeometry()
    {
        var topology = new TopologyModel();
        var edgeId = new EdgeId(1);
        var faceId = new FaceId(1);

        topology.AddVertex(new Vertex(new VertexId(1)));
        topology.AddVertex(new Vertex(new VertexId(2)));
        topology.AddEdge(new Edge(edgeId, new VertexId(1), new VertexId(2)));
        topology.AddFace(new Face(faceId, []));

        var geometry = new BrepGeometryStore();
        geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromLine(new Line3Curve(Point3D.Origin, Direction3D.Create(new Vector3D(1d, 0d, 0d)))));
        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(
            Point3D.Origin,
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            Direction3D.Create(new Vector3D(1d, 0d, 0d)))));

        var bindings = new BrepBindingModel();
        bindings.AddEdgeBinding(new EdgeGeometryBinding(edgeId, new CurveGeometryId(1), new ParameterInterval(0d, 1d)));
        bindings.AddFaceBinding(new FaceGeometryBinding(faceId, new SurfaceGeometryId(1)));

        var body = new BrepBody(topology, geometry, bindings);

        Assert.True(body.TryGetEdgeCurveGeometry(edgeId, out var edgeCurve));
        Assert.Equal(CurveGeometryKind.Line3, edgeCurve!.Kind);

        Assert.True(body.TryGetFaceSurfaceGeometry(faceId, out var faceSurface));
        Assert.Equal(SurfaceGeometryKind.Plane, faceSurface!.Kind);
    }

    [Fact]
    public void BrepBody_MissingBindingLookupReturnsFalse()
    {
        var body = new BrepBody(new TopologyModel(), new BrepGeometryStore(), new BrepBindingModel());

        Assert.False(body.TryGetEdgeCurveGeometry(new EdgeId(42), out var curve));
        Assert.Null(curve);

        Assert.False(body.TryGetFaceSurfaceGeometry(new FaceId(42), out var surface));
        Assert.Null(surface);
    }
}
