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

/// <summary>
/// Topology-only loop that owns ordered coedges.
/// </summary>
public sealed record Loop(LoopId Id, IReadOnlyList<CoedgeId> CoedgeIds);

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
