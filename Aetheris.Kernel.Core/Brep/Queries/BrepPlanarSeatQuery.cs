using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Brep.Tessellation;

namespace Aetheris.Kernel.Core.Brep.Queries;

/// <summary>Find admitted planar material faces containing an authored seating point.
/// Plane coincidence is analytic. Curved trim inclusion uses the existing admitted
/// boundary mesh and is approximate; no bounding-box fallback proves contact.</summary>
public sealed class BrepPlanarSeatQuery(BrepBody body)
{
    private DisplayTessellationResult? mesh;
    public IReadOnlyList<FaceId> FindFaces(Point3D point, Vector3D normal)
    {
        if (!normal.TryNormalize(out normal)) return [];
        var result = new List<FaceId>();
        foreach (var face in body.Topology.Faces)
        {
            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var geometry) || geometry?.Plane is not { } plane) continue;
            var n = new Vector3D(plane.Normal.X, plane.Normal.Y, plane.Normal.Z);
            if (n.Cross(normal).Length > 1e-7 || System.Math.Abs((point - plane.Origin).Dot(n)) > 1e-7) continue;
            var edges = face.LoopIds.SelectMany(id => body.Topology.GetLoop(id).CoedgeIds)
                .Select(id => body.Topology.GetCoedge(id).EdgeId).ToArray();
            if (edges.All(id => body.TryGetEdgeCurveGeometry(id, out var c) && c?.Kind == CurveGeometryKind.Line3))
            {
                if (AnalyticPlanarFaceDomain.TryCreate(body, face.Id, plane, out var domain) && domain.Contains(point, ToleranceContext.Default)) result.Add(face.Id);
                continue;
            }
            if (mesh is null)
            {
                var options = new DisplayTessellationOptions(double.Pi / 32, .05, 6, 128);
                if (!RectangularSplineDisplayTessellator.TryTessellate(body, options, out mesh!))
                {
                    var built = body.Topology.Faces.All(f => body.TryGetFaceSurfaceGeometry(f.Id, out var s) && s?.Kind is SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Torus)
                        ? BrepDisplayTessellator.TessellateSurfaceMeshIr(body, options) : BrepDisplayTessellator.TessellateBounded(body, options);
                    if (!built.IsSuccess) continue;
                    mesh = built.Value;
                }
            }
            var patch = mesh.FacePatches.FirstOrDefault(p => p.FaceId == face.Id);
            if (patch is null) continue;
            for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
            {
                var a = patch.Positions[patch.TriangleIndices[i]];
                var b = patch.Positions[patch.TriangleIndices[i + 1]];
                var c = patch.Positions[patch.TriangleIndices[i + 2]];
                var ab = b - a; var ac = c - a; var ap = point - a;
                var d00 = ab.Dot(ab); var d01 = ab.Dot(ac); var d11 = ac.Dot(ac);
                var determinant = d00*d11 - d01*d01;
                if (determinant <= 1e-20) continue;
                var u = (d11*ap.Dot(ab) - d01*ap.Dot(ac))/determinant;
                var v = (d00*ap.Dot(ac) - d01*ap.Dot(ab))/determinant;
                if (u >= -1e-9 && v >= -1e-9 && u + v <= 1 + 1e-9) { result.Add(face.Id); break; }
            }
        }
        return result;
    }
}
