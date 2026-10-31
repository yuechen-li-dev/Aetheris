using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Firmament.ParsedModel;
using Aetheris.Kernel.Firmament.Validation;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class FirmamentSelectorResolver
{
    /// <summary>
    /// PMI meaning of an axis face such as <c>face(-X)</c>: every planar face lying in the body's
    /// extreme support plane in that direction, the surface a datum simulator would contact.
    /// Construction may split one physical face into several coplanar patches; they are returned
    /// together. Empty when the extreme in that direction is not planar.
    /// </summary>
    internal static IReadOnlyList<FaceId> ResolveSupportPlaneFaces(BrepBody body, string selector)
    {
        var port = selector[(selector.LastIndexOf('.') + 1)..];
        Vector3D? axis = port switch { "top_face" => new(0, 0, 1), "bottom_face" => new(0, 0, -1),
            "plus_x_face" => new(1, 0, 0), "minus_x_face" => new(-1, 0, 0), "plus_y_face" => new(0, 1, 0), "minus_y_face" => new(0, -1, 0), _ => null };
        if (axis is not { } a) return [];
        var planes = body.Topology.Faces.Select(face => (face.Id, Plane: body.TryGetFaceSurfaceGeometry(face.Id, out var surface) ? surface?.Plane : null))
            .Where(f => f.Plane is { } plane && plane.Normal.ToVector().Dot(a) > 1 - 1e-8)
            .Select(f => (f.Id, Offset: (f.Plane!.Value.Origin - Point3D.Origin).Dot(a))).ToArray();
        if (planes.Length == 0) return [];
        var extreme = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? (p - Point3D.Origin).Dot(a) : double.NegativeInfinity)
            .Concat(body.Topology.Edges.Select(e => body.TryGetEdgeCurveGeometry(e.Id, out var curve) && curve?.Circle3 is { } circle
                ? (circle.Center - Point3D.Origin).Dot(a) + circle.Radius * Math.Sqrt(Math.Max(0, 1 - Math.Pow(circle.Normal.ToVector().Dot(a), 2)))
                : double.NegativeInfinity))
            .DefaultIfEmpty(double.NegativeInfinity).Max();
        var support = planes.Max(p => p.Offset);
        if (support < extreme - 1e-7) return [];
        return planes.Where(p => Math.Abs(p.Offset - support) < 1e-7).Select(p => p.Id).ToArray();
    }

    /// <summary>Resolve an existing axis-face port only when it denotes exactly one planar face.
    /// A stepped body with multiple same-facing planes remains ambiguous.</summary>
    internal static IReadOnlyList<FaceId> ResolveUniquePlanarFace(BrepBody body, string selector)
    {
        var port = selector[(selector.LastIndexOf('.') + 1)..];
        Vector3D? axis = port switch { "top_face" => new(0, 0, 1), "bottom_face" => new(0, 0, -1),
            "plus_x_face" => new(1, 0, 0), "minus_x_face" => new(-1, 0, 0), "plus_y_face" => new(0, 1, 0), "minus_y_face" => new(0, -1, 0), _ => null };
        if (axis is null) return [];
        var faces = body.Topology.Faces.Where(face => body.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface?.Plane is { } plane && plane.Normal.ToVector().Dot(axis.Value) > 1 - 1e-8).Select(face => face.Id).ToArray();
        return faces.Length == 1 ? faces : [];
    }

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
                : TryResolveTorusCount(body, port, resultKind, out var torusCount)
                    ? torusCount
                    : TryResolveBoxLikeCount(body, port, resultKind, out var boxLikeCount)
                        ? boxLikeCount
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

    private static bool TryResolveBoxLikeCount(BrepBody body, string port, FirmamentSelectorResultKind resultKind, out int count)
    {
        count = port switch
        {
            "top_face" => CountFaces(body, surface => surface.Kind == SurfaceGeometryKind.Plane && surface.Plane!.Value.Normal.ToVector().Z > 0.5d),
            "bottom_face" => CountFaces(body, surface => surface.Kind == SurfaceGeometryKind.Plane && surface.Plane!.Value.Normal.ToVector().Z < -0.5d),
            "edges" when resultKind == FirmamentSelectorResultKind.EdgeSet => body.Topology.Edges.Count(),
            "vertices" when resultKind == FirmamentSelectorResultKind.VertexSet => body.Topology.Vertices.Count(),
            _ => -1
        };

        return count >= 0;
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

    private static bool TryResolveTorusCount(BrepBody body, string port, FirmamentSelectorResultKind resultKind, out int count)
    {
        count = 0;

        if (!LooksLikeTorus(body))
        {
            return false;
        }

        count = port switch
        {
            "surface" => CountFaces(body, surface => surface.Kind == SurfaceGeometryKind.Torus),
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

    private static bool LooksLikeTorus(BrepBody body)
    {
        if (body.Topology.Faces.Count() != 1 || body.Topology.Edges.Count() != 2 || body.Topology.Vertices.Count() != 1)
        {
            return false;
        }

        var face = body.Topology.Faces.Single();
        return body.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface is not null
            && surface.Kind == SurfaceGeometryKind.Torus;
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
