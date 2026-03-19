using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Firmament.ParsedModel;
using Aetheris.Kernel.Firmament.Validation;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class FirmamentSelectorResolver
{
    public static bool TryResolve(
        string selectorTarget,
        IReadOnlyDictionary<string, BrepBody> featureBodies,
        FirmamentSelectorResultKind resultKind,
        out FirmamentSelectorResolution resolution)
    {
        resolution = null!;

        var separatorIndex = selectorTarget.IndexOf('.', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= selectorTarget.Length - 1)
        {
            return false;
        }

        var featureId = selectorTarget[..separatorIndex];
        var port = selectorTarget[(separatorIndex + 1)..];

        if (!featureBodies.TryGetValue(featureId, out var body))
        {
            return false;
        }

        var count = TryResolveCylinderCount(body, port, resultKind, out var cylinderCount)
            ? cylinderCount
            : TryResolveConeCount(body, port, resultKind, out var coneCount)
                ? coneCount
            : resultKind switch
            {
                FirmamentSelectorResultKind.Face or FirmamentSelectorResultKind.FaceSet => body.Topology.Faces.Count(),
                FirmamentSelectorResultKind.EdgeSet => body.Topology.Edges.Count(),
                FirmamentSelectorResultKind.VertexSet => body.Topology.Vertices.Count(),
                _ => 0
            };

        resolution = new FirmamentSelectorResolution(featureId, port, resultKind, count);
        return true;
    }

    private static bool TryResolveConeCount(BrepBody body, string port, FirmamentSelectorResultKind resultKind, out int count)
    {
        count = 0;

        if (!LooksLikeCone(body))
        {
            return false;
        }

        count = port switch
        {
            "top_face" => CountFaces(body, surface => surface.Kind == SurfaceGeometryKind.Plane && surface.Plane!.Value.Normal.ToVector().Z > 0.5d),
            "bottom_face" => CountFaces(body, surface => surface.Kind == SurfaceGeometryKind.Plane && surface.Plane!.Value.Normal.ToVector().Z < -0.5d),
            "side_face" => CountFaces(body, surface => surface.Kind == SurfaceGeometryKind.Cone),
            "circular_edges" => CountEdges(body, curve => curve.Kind == CurveGeometryKind.Circle3),
            "edges" when resultKind == FirmamentSelectorResultKind.EdgeSet => body.Topology.Edges.Count(),
            "vertices" when resultKind == FirmamentSelectorResultKind.VertexSet => body.Topology.Vertices.Count(),
            _ => 0
        };

        return true;
    }

    private static bool TryResolveCylinderCount(BrepBody body, string port, FirmamentSelectorResultKind resultKind, out int count)
    {
        count = 0;

        if (!LooksLikeCylinder(body))
        {
            return false;
        }

        count = port switch
        {
            "top_face" => CountFaces(body, surface => surface.Kind == SurfaceGeometryKind.Plane && surface.Plane!.Value.Normal.ToVector().Z > 0.5d),
            "bottom_face" => CountFaces(body, surface => surface.Kind == SurfaceGeometryKind.Plane && surface.Plane!.Value.Normal.ToVector().Z < -0.5d),
            "side_face" => CountFaces(body, surface => surface.Kind == SurfaceGeometryKind.Cylinder),
            "circular_edges" => CountEdges(body, curve => curve.Kind == CurveGeometryKind.Circle3),
            "edges" when resultKind == FirmamentSelectorResultKind.EdgeSet => body.Topology.Edges.Count(),
            "vertices" when resultKind == FirmamentSelectorResultKind.VertexSet => body.Topology.Vertices.Count(),
            _ => 0
        };

        return true;
    }

    private static bool LooksLikeCylinder(BrepBody body)
    {
        if (body.Topology.Faces.Count() != 3 || body.Topology.Edges.Count() != 3)
        {
            return false;
        }

        var cylindricalFaces = 0;
        var planarFaces = 0;

        foreach (var face in body.Topology.Faces)
        {
            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null)
            {
                return false;
            }

            switch (surface.Kind)
            {
                case SurfaceGeometryKind.Cylinder:
                    cylindricalFaces++;
                    break;
                case SurfaceGeometryKind.Plane:
                    planarFaces++;
                    break;
                default:
                    return false;
            }
        }

        return cylindricalFaces == 1 && planarFaces == 2;
    }

    private static bool LooksLikeCone(BrepBody body)
    {
        var faceCount = body.Topology.Faces.Count();
        var edgeCount = body.Topology.Edges.Count();
        if ((faceCount != 3 || edgeCount != 3) && (faceCount != 2 || edgeCount != 2))
        {
            return false;
        }

        var conicalFaces = 0;
        var planarFaces = 0;

        foreach (var face in body.Topology.Faces)
        {
            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null)
            {
                return false;
            }

            switch (surface.Kind)
            {
                case SurfaceGeometryKind.Cone:
                    conicalFaces++;
                    break;
                case SurfaceGeometryKind.Plane:
                    planarFaces++;
                    break;
                default:
                    return false;
            }
        }

        return conicalFaces == 1 && (planarFaces == 1 || planarFaces == 2);
    }

    private static int CountFaces(BrepBody body, Func<SurfaceGeometry, bool> predicate)
    {
        var count = 0;
        foreach (var face in body.Topology.Faces)
        {
            if (body.TryGetFaceSurfaceGeometry(face.Id, out var surface) && surface is not null && predicate(surface))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountEdges(BrepBody body, Func<CurveGeometry, bool> predicate)
    {
        var count = 0;
        foreach (var edge in body.Topology.Edges)
        {
            if (body.TryGetEdgeCurveGeometry(edge.Id, out var curve) && curve is not null && predicate(curve))
            {
                count++;
            }
        }

        return count;
    }
}

internal sealed record FirmamentSelectorResolution(
    string FeatureId,
    string Port,
    FirmamentSelectorResultKind ResultKind,
    int Count);
