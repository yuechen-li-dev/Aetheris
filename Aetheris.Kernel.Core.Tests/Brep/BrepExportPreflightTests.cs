using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Core.Step242;
using Xunit;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class BrepExportPreflightTests
{
    private static readonly Direction3D Z = Direction3D.Create(new Vector3D(0, 0, 1));
    private static readonly Direction3D X = Direction3D.Create(new Vector3D(1, 0, 0));

    [Fact]
    public void ValidPeriodicCircleSeam_IsAccepted()
    {
        var body = ClosedCircleBody(SurfaceGeometry.FromCylinder(new CylinderSurface(Point3D.Origin, Z, 2, X)), new Circle3Curve(Point3D.Origin, Z, 2, X));
        Assert.True(BrepExportPreflight.Validate(body).IsValid);
    }

    [Theory]
    [InlineData("plane")]
    [InlineData("cylinder")]
    [InlineData("cone")]
    public void TrimOffAnalyticSupport_IsRejected(string family)
    {
        var body = family switch
        {
            "plane" => ClosedCircleBody(SurfaceGeometry.FromPlane(new PlaneSurface(Point3D.Origin, Z, X)), new Circle3Curve(new Point3D(0, 0, 1), Z, 2, X)),
            "cylinder" => ClosedCircleBody(SurfaceGeometry.FromCylinder(new CylinderSurface(Point3D.Origin, Z, 2, X)), new Circle3Curve(Point3D.Origin, Z, 3, X)),
            _ => ClosedCircleBody(SurfaceGeometry.FromCone(new ConeSurface(Point3D.Origin, Z, double.Pi / 4, X)), new Circle3Curve(new Point3D(0, 0, 2), Z, 3, X)),
        };

        var report = BrepExportPreflight.Validate(body);
        Assert.False(report.IsValid);
        Assert.Contains(report.Diagnostics, diagnostic =>
            diagnostic.Code == "brep-preflight-trim-off-surface" &&
            diagnostic.Classification == BrepExportPreflightFindingClassification.InvalidGeometry);
    }

    [Fact]
    public void EnforcedExport_FailsBeforeStepSerialization_WithStructuredPreflightEvidence()
    {
        var body = ClosedCircleBody(
            SurfaceGeometry.FromCone(new ConeSurface(Point3D.Origin, Z, double.Pi / 4, X)),
            new Circle3Curve(new Point3D(0, 0, 2), Z, 3, X));

        var export = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.False(export.IsSuccess);
        Assert.Contains(export.Diagnostics, diagnostic => diagnostic.Message.Contains("brep-preflight-trim-off-surface", StringComparison.Ordinal));
    }

    [Fact]
    public void CurveTrimEndpointsThatDisagreeWithTopologyVertices_AreRejected()
    {
        var circle = new Circle3Curve(Point3D.Origin, Z, 2, X);
        var body = ClosedCircleBody(
            SurfaceGeometry.FromCylinder(new CylinderSurface(Point3D.Origin, Z, 2, X)),
            circle,
            new Point3D(0, 2, 0));

        var report = BrepExportPreflight.Validate(body);

        Assert.False(report.IsValid);
        Assert.Contains(report.Diagnostics, diagnostic =>
            diagnostic.Code == "brep-preflight-edge-curve-endpoint-mismatch" &&
            diagnostic.Classification == BrepExportPreflightFindingClassification.InvalidTopology);
    }

    [Fact]
    public void DisconnectedLoop_IsClassifiedAsInvalidTopology()
    {
        var report = BrepExportPreflight.Validate(DisconnectedLoopBody());

        Assert.Contains(report.Diagnostics, diagnostic =>
            diagnostic.Code == "brep-preflight-coedge-disconnected" &&
            diagnostic.Classification == BrepExportPreflightFindingClassification.InvalidTopology);
    }

    private static BrepBody ClosedCircleBody(SurfaceGeometry surface, Circle3Curve circle, Point3D? topologyVertexPoint = null)
    {
        var topology = new TopologyModel();
        var vertex = new VertexId(1); var edge = new EdgeId(1); var coedge = new CoedgeId(1); var loop = new LoopId(1); var face = new FaceId(1); var shell = new ShellId(1);
        topology.AddVertex(new Vertex(vertex)); topology.AddEdge(new Edge(edge, vertex, vertex));
        topology.AddCoedge(new Coedge(coedge, edge, loop, coedge, coedge, false)); topology.AddLoop(new Loop(loop, [coedge]));
        topology.AddFace(new Face(face, [loop])); topology.AddShell(new Shell(shell, [face])); topology.AddBody(new Body(new BodyId(1), [shell]));
        var geometry = new BrepGeometryStore(); geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromCircle(circle)); geometry.AddSurface(new SurfaceGeometryId(1), surface);
        var bindings = new BrepBindingModel(); bindings.AddEdgeBinding(new EdgeGeometryBinding(edge, new CurveGeometryId(1), new ParameterInterval(0, 2 * double.Pi))); bindings.AddFaceBinding(new FaceGeometryBinding(face, new SurfaceGeometryId(1)));
        return new BrepBody(topology, geometry, bindings, new Dictionary<VertexId, Point3D> { [vertex] = topologyVertexPoint ?? circle.Evaluate(0) });
    }

    private static BrepBody DisconnectedLoopBody()
    {
        var topology = new TopologyModel();
        var vertices = new[] { new VertexId(1), new VertexId(2), new VertexId(3), new VertexId(4) };
        foreach (var vertex in vertices) topology.AddVertex(new Vertex(vertex));
        topology.AddEdge(new Edge(new EdgeId(1), vertices[0], vertices[1]));
        topology.AddEdge(new Edge(new EdgeId(2), vertices[2], vertices[3]));
        topology.AddCoedge(new Coedge(new CoedgeId(1), new EdgeId(1), new LoopId(1), new CoedgeId(2), new CoedgeId(2), false));
        topology.AddCoedge(new Coedge(new CoedgeId(2), new EdgeId(2), new LoopId(1), new CoedgeId(1), new CoedgeId(1), false));
        topology.AddLoop(new Loop(new LoopId(1), [new CoedgeId(1), new CoedgeId(2)]));
        topology.AddFace(new Face(new FaceId(1), [new LoopId(1)]));
        topology.AddShell(new Shell(new ShellId(1), [new FaceId(1)]));
        topology.AddBody(new Body(new BodyId(1), [new ShellId(1)]));
        var geometry = new BrepGeometryStore();
        geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromLine(new Line3Curve(Point3D.Origin, X)));
        geometry.AddCurve(new CurveGeometryId(2), CurveGeometry.FromLine(new Line3Curve(new Point3D(1, 1, 0), Direction3D.Create(new Vector3D(-1, 0, 0)))));
        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(Point3D.Origin, Z, X)));
        var bindings = new BrepBindingModel();
        bindings.AddEdgeBinding(new EdgeGeometryBinding(new EdgeId(1), new CurveGeometryId(1), new ParameterInterval(0, 1)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(new EdgeId(2), new CurveGeometryId(2), new ParameterInterval(0, 1)));
        bindings.AddFaceBinding(new FaceGeometryBinding(new FaceId(1), new SurfaceGeometryId(1)));
        return new BrepBody(topology, geometry, bindings);
    }
}
