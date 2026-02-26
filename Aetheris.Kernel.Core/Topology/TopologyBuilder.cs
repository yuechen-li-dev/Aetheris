namespace Aetheris.Kernel.Core.Topology;

/// <summary>
/// Minimal helper for constructing topology graphs in tests and simple setup code.
/// </summary>
public sealed class TopologyBuilder
{
    private int _nextVertexId = 1;
    private int _nextEdgeId = 1;
    private int _nextCoedgeId = 1;
    private int _nextLoopId = 1;
    private int _nextFaceId = 1;
    private int _nextShellId = 1;
    private int _nextBodyId = 1;

    public TopologyModel Model { get; } = new();

    public VertexId AddVertex()
    {
        var id = new VertexId(_nextVertexId++);
        Model.AddVertex(new Vertex(id));
        return id;
    }

    public EdgeId AddEdge(VertexId startVertexId, VertexId endVertexId)
    {
        var id = new EdgeId(_nextEdgeId++);
        Model.AddEdge(new Edge(id, startVertexId, endVertexId));
        return id;
    }

    public CoedgeId AddCoedge(EdgeId edgeId, LoopId loopId, CoedgeId nextCoedgeId, CoedgeId prevCoedgeId, bool isReversed)
    {
        var id = new CoedgeId(_nextCoedgeId++);
        Model.AddCoedge(new Coedge(id, edgeId, loopId, nextCoedgeId, prevCoedgeId, isReversed));
        return id;
    }

    public LoopId AddLoop(IReadOnlyList<CoedgeId> coedgeIds)
    {
        var id = new LoopId(_nextLoopId++);
        Model.AddLoop(new Loop(id, coedgeIds));
        return id;
    }

    public LoopId AllocateLoopId() => new(_nextLoopId++);

    public void AddLoop(Loop loop) => Model.AddLoop(loop);

    public FaceId AddFace(IReadOnlyList<LoopId> loopIds)
    {
        var id = new FaceId(_nextFaceId++);
        Model.AddFace(new Face(id, loopIds));
        return id;
    }

    public ShellId AddShell(IReadOnlyList<FaceId> faceIds)
    {
        var id = new ShellId(_nextShellId++);
        Model.AddShell(new Shell(id, faceIds));
        return id;
    }

    public BodyId AddBody(IReadOnlyList<ShellId> shellIds)
    {
        var id = new BodyId(_nextBodyId++);
        Model.AddBody(new Body(id, shellIds));
        return id;
    }

    public CoedgeId AllocateCoedgeId() => new(_nextCoedgeId++);

    public void AddCoedge(Coedge coedge) => Model.AddCoedge(coedge);
}
