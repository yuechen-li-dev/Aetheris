using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class BrepBodyQueriesTests
{
    [Fact]
    public void Queries_EnumerateHierarchyAndResolveBindings_ForBoxFixture()
    {
        var body = BoxBrepFixture.BuildUnitBoxBrep();

        var bodyIds = body.GetBodyIds().ToArray();
        Assert.Single(bodyIds);

        var shellIds = body.GetShellIds(bodyIds[0]);
        Assert.Single(shellIds);

        var faceIds = body.GetFaceIds(shellIds[0]);
        Assert.Equal(6, faceIds.Count);

        var loopIds = body.GetLoopIds(faceIds[0]);
        Assert.Single(loopIds);

        var coedgeIds = body.GetCoedgeIds(loopIds[0]);
        Assert.Equal(4, coedgeIds.Count);

        var edgeId = body.GetCoedgeEdgeId(coedgeIds[0]);
        var (startVertexId, endVertexId) = body.GetEdgeVertices(edgeId);
        Assert.True(startVertexId.IsValid);
        Assert.True(endVertexId.IsValid);

        var edgeCurve = body.GetEdgeCurve(edgeId);
        Assert.Equal(CurveGeometryKind.Line3, edgeCurve.Kind);

        var faceSurface = body.GetFaceSurface(faceIds[0]);
        Assert.Equal(SurfaceGeometryKind.Plane, faceSurface.Kind);
    }

    [Fact]
    public void ConvenienceQueries_ReturnExpectedCounts_ForBoxFixture()
    {
        var body = BoxBrepFixture.BuildUnitBoxBrep();
        var bodyId = body.GetBodyIds().Single();
        var faceId = body.GetFaces(bodyId).First();

        var faces = body.GetFaces(bodyId);
        Assert.Equal(6, faces.Count);

        var edges = body.GetEdges(faceId);
        Assert.Equal(4, edges.Count);

        var vertices = body.GetVertices(edges[0]);
        Assert.Equal(2, vertices.Count);
    }

    [Fact]
    public void GetEdges_DeduplicatesRepeatedEdges_PreservingFirstSeenOrder()
    {
        var topology = new TopologyModel();
        topology.AddVertex(new Vertex(new VertexId(1)));
        topology.AddVertex(new Vertex(new VertexId(2)));

        var edgeId = new EdgeId(1);
        topology.AddEdge(new Edge(edgeId, new VertexId(1), new VertexId(2)));

        var coedge1 = new CoedgeId(1);
        var coedge2 = new CoedgeId(2);
        var loopId = new LoopId(1);
        var faceId = new FaceId(1);
        var shellId = new ShellId(1);
        var bodyId = new BodyId(1);

        topology.AddCoedge(new Coedge(coedge1, edgeId, loopId, coedge2, coedge2, false));
        topology.AddCoedge(new Coedge(coedge2, edgeId, loopId, coedge1, coedge1, true));
        topology.AddLoop(new Loop(loopId, [coedge1, coedge2]));
        topology.AddFace(new Face(faceId, [loopId]));
        topology.AddShell(new Shell(shellId, [faceId]));
        topology.AddBody(new Body(bodyId, [shellId]));

        var body = new BrepBody(topology, new BrepGeometryStore(), new BrepBindingModel());

        var edges = body.GetEdges(faceId);

        Assert.Single(edges);
        Assert.Equal(edgeId, edges[0]);
    }

    [Fact]
    public void TryMethods_ReturnFalseWithoutThrowing_ForMissingReferences()
    {
        var body = new BrepBody(new TopologyModel(), new BrepGeometryStore(), new BrepBindingModel());

        Assert.False(body.TryGetShellIds(new BodyId(99), out var shellIds));
        Assert.Null(shellIds);

        Assert.False(body.TryGetEdgeCurve(new EdgeId(10), out var curve));
        Assert.Null(curve);

        Assert.False(body.TryGetFaceSurface(new FaceId(11), out var surface));
        Assert.Null(surface);
    }

    [Fact]
    public void GetMethods_ThrowClearException_ForMissingReferences()
    {
        var topology = new TopologyModel();
        var faceId = new FaceId(1);
        topology.AddFace(new Face(faceId, [new LoopId(999)]));

        var body = new BrepBody(topology, new BrepGeometryStore(), new BrepBindingModel());

        var missingBody = Assert.Throws<KeyNotFoundException>(() => body.GetShellIds(new BodyId(404)));
        Assert.Contains("topology body", missingBody.Message);

        var missingLoop = Assert.Throws<KeyNotFoundException>(() => body.GetEdges(faceId));
        Assert.Contains("topology loop", missingLoop.Message);

        var missingCurve = Assert.Throws<KeyNotFoundException>(() => body.GetEdgeCurve(new EdgeId(777)));
        Assert.Contains("edge curve", missingCurve.Message);
    }
}
