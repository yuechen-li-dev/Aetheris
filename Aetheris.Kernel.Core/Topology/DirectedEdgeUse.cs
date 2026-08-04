namespace Aetheris.Kernel.Core.Topology;

/// <summary>
/// The sole interpretation of a coedge's directed topology use.  An edge owns
/// its start/end vertices; a coedge reverses that ordered pair exactly once.
/// Curve parameter sense and face sense are deliberately not inputs here.
/// </summary>
public readonly record struct DirectedEdgeUse(VertexId StartVertexId, VertexId EndVertexId)
{
    public static DirectedEdgeUse Resolve(Edge edge, bool isReversed) => isReversed
        ? new DirectedEdgeUse(edge.EndVertexId, edge.StartVertexId)
        : new DirectedEdgeUse(edge.StartVertexId, edge.EndVertexId);

    public static DirectedEdgeUse Resolve(Edge edge, Coedge coedge) => Resolve(edge, coedge.IsReversed);
}
