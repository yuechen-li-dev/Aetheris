using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class BrepBindingValidatorTests
{
    [Fact]
    public void Validator_DetectsMissingReferencedCurveGeometry()
    {
        var topology = new TopologyModel();
        topology.AddVertex(new Vertex(new VertexId(1)));
        topology.AddVertex(new Vertex(new VertexId(2)));
        topology.AddEdge(new Edge(new EdgeId(1), new VertexId(1), new VertexId(2)));

        var bindings = new BrepBindingModel();
        bindings.AddEdgeBinding(new EdgeGeometryBinding(new EdgeId(1), new CurveGeometryId(99)));

        var result = BrepBindingValidator.Validate(new BrepBody(topology, new BrepGeometryStore(), bindings), requireAllEdgeAndFaceBindings: false);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("missing curve geometry 99"));
    }

    [Fact]
    public void Validator_DetectsMissingReferencedSurfaceGeometry()
    {
        var topology = new TopologyModel();
        topology.AddFace(new Face(new FaceId(1), []));

        var bindings = new BrepBindingModel();
        bindings.AddFaceBinding(new FaceGeometryBinding(new FaceId(1), new SurfaceGeometryId(77)));

        var result = BrepBindingValidator.Validate(new BrepBody(topology, new BrepGeometryStore(), bindings), requireAllEdgeAndFaceBindings: false);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("missing surface geometry 77"));
    }

    [Fact]
    public void Validator_DetectsBindingsToNonexistentTopology()
    {
        var geometry = new BrepGeometryStore();
        geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromLine(new Line3Curve(Point3D.Origin, Direction3D.Create(new Vector3D(1d, 0d, 0d)))));
        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(
            Point3D.Origin,
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            Direction3D.Create(new Vector3D(1d, 0d, 0d)))));

        var bindings = new BrepBindingModel();
        bindings.AddEdgeBinding(new EdgeGeometryBinding(new EdgeId(10), new CurveGeometryId(1)));
        bindings.AddFaceBinding(new FaceGeometryBinding(new FaceId(20), new SurfaceGeometryId(1)));

        var result = BrepBindingValidator.Validate(new BrepBody(new TopologyModel(), geometry, bindings), requireAllEdgeAndFaceBindings: false);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("missing edge 10"));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("missing face 20"));
    }

    [Fact]
    public void Validator_ReturnsSuccessForValidCombinedModel()
    {
        var topology = new TopologyModel();
        topology.AddVertex(new Vertex(new VertexId(1)));
        topology.AddVertex(new Vertex(new VertexId(2)));
        topology.AddEdge(new Edge(new EdgeId(1), new VertexId(1), new VertexId(2)));
        topology.AddFace(new Face(new FaceId(1), []));

        var geometry = new BrepGeometryStore();
        geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromLine(new Line3Curve(Point3D.Origin, Direction3D.Create(new Vector3D(1d, 0d, 0d)))));
        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(
            Point3D.Origin,
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            Direction3D.Create(new Vector3D(1d, 0d, 0d)))));

        var bindings = new BrepBindingModel();
        bindings.AddEdgeBinding(new EdgeGeometryBinding(new EdgeId(1), new CurveGeometryId(1), new ParameterInterval(0d, 1d)));
        bindings.AddFaceBinding(new FaceGeometryBinding(new FaceId(1), new SurfaceGeometryId(1)));

        var result = BrepBindingValidator.Validate(new BrepBody(topology, geometry, bindings));

        Assert.True(result.IsSuccess);
    }
}
