namespace Aetheris.Kernel.Core.Topology;

/// <summary>
/// Topology-only vertex node. Geometry binding is intentionally deferred in M04.
/// </summary>
public sealed record Vertex(VertexId Id);

/// <summary>
/// Topology-only edge that references its endpoint vertices.
/// </summary>
public sealed record Edge(EdgeId Id, VertexId StartVertexId, VertexId EndVertexId);

/// <summary>
/// Topology-only directed edge use inside a loop.
/// </summary>
/// <remarks>
/// M04 keeps coedge structure minimal: edge use, owning loop, and local loop links.
/// </remarks>
public sealed record Coedge(
    CoedgeId Id,
    EdgeId EdgeId,
    LoopId LoopId,
    CoedgeId NextCoedgeId,
    CoedgeId PrevCoedgeId,
    bool IsReversed);

public enum LoopKind { Edge, Vertex }

/// <summary>
/// Topology-only boundary loop. An edge loop owns ordered coedges; a vertex loop
/// owns one explicit vertex at a collapsed support boundary.
/// </summary>
public sealed record Loop(
    LoopId Id,
    IReadOnlyList<CoedgeId> CoedgeIds,
    VertexId? VertexLoopVertexId = null)
{
    public LoopKind Kind => VertexLoopVertexId is null ? LoopKind.Edge : LoopKind.Vertex;

    public static Loop VertexLoop(LoopId id, VertexId vertexId) => new(id, [], vertexId);
}

/// <summary>
/// Topology-only face that owns loops.
/// </summary>
public sealed record Face(FaceId Id, IReadOnlyList<LoopId> LoopIds);

/// <summary>
/// Topology-only shell that owns faces.
/// </summary>
public sealed record Shell(ShellId Id, IReadOnlyList<FaceId> FaceIds);

/// <summary>
/// Topology-only body that owns shells.
/// </summary>
public sealed record Body(BodyId Id, IReadOnlyList<ShellId> ShellIds);
