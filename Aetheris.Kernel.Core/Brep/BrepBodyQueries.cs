using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

/// <summary>
/// Read-only traversal and binding lookup helpers for <see cref="BrepBody"/>.
/// </summary>
public static class BrepBodyQueries
{
    public static IEnumerable<BodyId> GetBodyIds(this BrepBody body)
    {
        foreach (var topologyBody in body.Topology.Bodies)
        {
            yield return topologyBody.Id;
        }
    }

    public static bool TryGetShellIds(this BrepBody body, BodyId bodyId, out IReadOnlyList<ShellId>? shellIds)
    {
        shellIds = null;
        if (!body.Topology.TryGetBody(bodyId, out var topologyBody) || topologyBody is null)
        {
            return false;
        }

        shellIds = topologyBody.ShellIds;
        return true;
    }

    public static IReadOnlyList<ShellId> GetShellIds(this BrepBody body, BodyId bodyId)
    {
        return body.TryGetShellIds(bodyId, out var shellIds)
            ? shellIds!
            : throw CreateMissingTopologyException("body", bodyId);
    }

    public static bool TryGetFaceIds(this BrepBody body, ShellId shellId, out IReadOnlyList<FaceId>? faceIds)
    {
        faceIds = null;
        if (!body.Topology.TryGetShell(shellId, out var shell) || shell is null)
        {
            return false;
        }

        faceIds = shell.FaceIds;
        return true;
    }

    public static IReadOnlyList<FaceId> GetFaceIds(this BrepBody body, ShellId shellId)
    {
        return body.TryGetFaceIds(shellId, out var faceIds)
            ? faceIds!
            : throw CreateMissingTopologyException("shell", shellId);
    }

    public static bool TryGetLoopIds(this BrepBody body, FaceId faceId, out IReadOnlyList<LoopId>? loopIds)
    {
        loopIds = null;
        if (!body.Topology.TryGetFace(faceId, out var face) || face is null)
        {
            return false;
        }

        loopIds = face.LoopIds;
        return true;
    }

    public static IReadOnlyList<LoopId> GetLoopIds(this BrepBody body, FaceId faceId)
    {
        return body.TryGetLoopIds(faceId, out var loopIds)
            ? loopIds!
            : throw CreateMissingTopologyException("face", faceId);
    }

    public static bool TryGetCoedgeIds(this BrepBody body, LoopId loopId, out IReadOnlyList<CoedgeId>? coedgeIds)
    {
        coedgeIds = null;
        if (!body.Topology.TryGetLoop(loopId, out var loop) || loop is null)
        {
            return false;
        }

        coedgeIds = loop.CoedgeIds;
        return true;
    }

    public static IReadOnlyList<CoedgeId> GetCoedgeIds(this BrepBody body, LoopId loopId)
    {
        return body.TryGetCoedgeIds(loopId, out var coedgeIds)
            ? coedgeIds!
            : throw CreateMissingTopologyException("loop", loopId);
    }

    public static bool TryGetCoedgeEdgeId(this BrepBody body, CoedgeId coedgeId, out EdgeId edgeId)
    {
        edgeId = default;
        if (!body.Topology.TryGetCoedge(coedgeId, out var coedge) || coedge is null)
        {
            return false;
        }

        edgeId = coedge.EdgeId;
        return true;
    }

    public static EdgeId GetCoedgeEdgeId(this BrepBody body, CoedgeId coedgeId)
    {
        return body.TryGetCoedgeEdgeId(coedgeId, out var edgeId)
            ? edgeId
            : throw CreateMissingTopologyException("coedge", coedgeId);
    }

    public static bool TryGetEdgeVertices(this BrepBody body, EdgeId edgeId, out VertexId startVertexId, out VertexId endVertexId)
    {
        startVertexId = default;
        endVertexId = default;

        if (!body.Topology.TryGetEdge(edgeId, out var edge) || edge is null)
        {
            return false;
        }

        startVertexId = edge.StartVertexId;
        endVertexId = edge.EndVertexId;
        return true;
    }

    public static (VertexId StartVertexId, VertexId EndVertexId) GetEdgeVertices(this BrepBody body, EdgeId edgeId)
    {
        return body.TryGetEdgeVertices(edgeId, out var startVertexId, out var endVertexId)
            ? (startVertexId, endVertexId)
            : throw CreateMissingTopologyException("edge", edgeId);
    }

    public static bool TryGetEdgeCurve(this BrepBody body, EdgeId edgeId, out CurveGeometry? curve)
    {
        return body.TryGetEdgeCurveGeometry(edgeId, out curve);
    }

    public static CurveGeometry GetEdgeCurve(this BrepBody body, EdgeId edgeId)
    {
        return body.TryGetEdgeCurve(edgeId, out var curve)
            ? curve!
            : throw CreateMissingBindingOrGeometryException("edge curve", edgeId);
    }

    public static bool TryGetFaceSurface(this BrepBody body, FaceId faceId, out SurfaceGeometry? surface)
    {
        return body.TryGetFaceSurfaceGeometry(faceId, out surface);
    }

    public static SurfaceGeometry GetFaceSurface(this BrepBody body, FaceId faceId)
    {
        return body.TryGetFaceSurface(faceId, out var surface)
            ? surface!
            : throw CreateMissingBindingOrGeometryException("face surface", faceId);
    }

    public static IReadOnlyList<FaceId> GetFaces(this BrepBody body, BodyId bodyId)
    {
        var shellIds = body.GetShellIds(bodyId);
        var faceIds = new List<FaceId>();

        foreach (var shellId in shellIds)
        {
            var shellFaceIds = body.GetFaceIds(shellId);
            faceIds.AddRange(shellFaceIds);
        }

        return faceIds;
    }

    /// <summary>
    /// Returns unique edge IDs referenced by the face loops, preserving first-seen order.
    /// </summary>
    public static IReadOnlyList<EdgeId> GetEdges(this BrepBody body, FaceId faceId)
    {
        var loopIds = body.GetLoopIds(faceId);
        var edgeIds = new List<EdgeId>();
        var seen = new HashSet<EdgeId>();

        foreach (var loopId in loopIds)
        {
            var coedgeIds = body.GetCoedgeIds(loopId);
            foreach (var coedgeId in coedgeIds)
            {
                var edgeId = body.GetCoedgeEdgeId(coedgeId);
                if (seen.Add(edgeId))
                {
                    edgeIds.Add(edgeId);
                }
            }
        }

        return edgeIds;
    }

    public static IReadOnlyList<VertexId> GetVertices(this BrepBody body, EdgeId edgeId)
    {
        var vertices = body.GetEdgeVertices(edgeId);
        return [vertices.StartVertexId, vertices.EndVertexId];
    }

    private static KeyNotFoundException CreateMissingTopologyException(string entityName, object id)
    {
        return new KeyNotFoundException($"B-rep topology {entityName} '{id}' was not found.");
    }

    private static KeyNotFoundException CreateMissingBindingOrGeometryException(string bindingName, object id)
    {
        return new KeyNotFoundException($"B-rep {bindingName} for '{id}' was not found or references missing geometry.");
    }
}
