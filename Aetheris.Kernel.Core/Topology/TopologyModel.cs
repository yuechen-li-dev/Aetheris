namespace Aetheris.Kernel.Core.Topology;

public sealed class TopologyModel
{
    private readonly Dictionary<VertexId, Vertex> _vertices = [];
    private readonly Dictionary<EdgeId, Edge> _edges = [];
    private readonly Dictionary<CoedgeId, Coedge> _coedges = [];
    private readonly Dictionary<LoopId, Loop> _loops = [];
    private readonly Dictionary<FaceId, Face> _faces = [];
    private readonly Dictionary<ShellId, Shell> _shells = [];
    private readonly Dictionary<BodyId, Body> _bodies = [];

    public IEnumerable<Vertex> Vertices => _vertices.Values;
    public IEnumerable<Edge> Edges => _edges.Values;
    public IEnumerable<Coedge> Coedges => _coedges.Values;
    public IEnumerable<Loop> Loops => _loops.Values;
    public IEnumerable<Face> Faces => _faces.Values;
    public IEnumerable<Shell> Shells => _shells.Values;
    public IEnumerable<Body> Bodies => _bodies.Values;

    public void AddVertex(Vertex vertex) => _vertices.Add(vertex.Id, vertex);

    public void AddEdge(Edge edge) => _edges.Add(edge.Id, edge);

    public void AddCoedge(Coedge coedge) => _coedges.Add(coedge.Id, coedge);

    public void AddLoop(Loop loop) => _loops.Add(loop.Id, loop);

    public void AddFace(Face face) => _faces.Add(face.Id, face);

    public void AddShell(Shell shell) => _shells.Add(shell.Id, shell);

    public void AddBody(Body body) => _bodies.Add(body.Id, body);

    public bool TryGetVertex(VertexId id, out Vertex? vertex) => _vertices.TryGetValue(id, out vertex);

    public bool TryGetEdge(EdgeId id, out Edge? edge) => _edges.TryGetValue(id, out edge);

    public bool TryGetCoedge(CoedgeId id, out Coedge? coedge) => _coedges.TryGetValue(id, out coedge);

    public bool TryGetLoop(LoopId id, out Loop? loop) => _loops.TryGetValue(id, out loop);

    public bool TryGetFace(FaceId id, out Face? face) => _faces.TryGetValue(id, out face);

    public bool TryGetShell(ShellId id, out Shell? shell) => _shells.TryGetValue(id, out shell);

    public bool TryGetBody(BodyId id, out Body? body) => _bodies.TryGetValue(id, out body);

    public Vertex GetVertex(VertexId id) => _vertices[id];

    public Edge GetEdge(EdgeId id) => _edges[id];

    public Coedge GetCoedge(CoedgeId id) => _coedges[id];

    public Loop GetLoop(LoopId id) => _loops[id];

    public Face GetFace(FaceId id) => _faces[id];

    public Shell GetShell(ShellId id) => _shells[id];

    public Body GetBody(BodyId id) => _bodies[id];
}
