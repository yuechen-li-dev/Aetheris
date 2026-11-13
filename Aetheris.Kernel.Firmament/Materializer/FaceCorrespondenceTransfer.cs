using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Carries face correspondence across a construction that rebuilds a body around some of its edges (a fillet that
/// rounds where two operations meet). A face of the new body that lies on the surface of a face of the old one, and
/// overlaps it, is that face: it keeps the old face's owner, role and selector. Edges, loops and vertices are not
/// carried. Faces the construction made are named only when their caller can say what made them.
/// </summary>
public static class FaceCorrespondenceTransfer
{
    private const double Tolerance = 1e-6;

    /// <param name="reach">How far the construction can move a face's boundary (the largest fillet radius).</param>
    /// <param name="nameNew">Names a face that has no old counterpart, or returns null to leave it runtime-only.</param>
    public static SemanticTopologyCorrespondence Transfer(BrepBody from, SemanticTopologyCorrespondence correspondence, BrepBody to,
        double reach, Func<SurfaceGeometry, (string Selector, string Source)?>? nameNew = null)
    {
        var oldFaces = from.Topology.Faces.Select(f => (f.Id, Surface: Surface(from, f.Id), Box: Box(from, f.Id))).Where(f => f.Surface is not null).ToArray();
        var byFace = correspondence.Descendants.Where(d => d.Face.HasValue).ToLookup(d => d.Face!.Value);
        var descendants = new List<SemanticTopologyDescendant>();
        foreach (var face in to.Topology.Faces)
        {
            var surface = Surface(to, face.Id);
            if (surface is null) continue;
            var box = Box(to, face.Id);
            var matches = oldFaces.Where(old => SameSurface(old.Surface!, surface) && Overlap(old.Box, box, reach + Tolerance))
                .OrderByDescending(old => Extent(old.Box)).ThenBy(old => old.Id.Value).Select(old => old.Id).ToArray();
            // A face the construction merged from several old faces is owned by the largest of them; the others' names
            // stay on it as aliases.
            var carried = matches.SelectMany(id => byFace[id].OrderBy(d => d.IsTopologyAlias)).ToArray();
            var owner = true;
            foreach (var d in carried)
            {
                descendants.Add(d with
                {
                    StableId = $"{d.StableId}@{face.Id.Value}", Face = face.Id, Edge = null, Loop = null, Vertex = null,
                    IsTopologyAlias = d.IsTopologyAlias || !owner,
                });
                if (!d.IsTopologyAlias) owner = false;
            }
            if (carried.Length == 0 && nameNew?.Invoke(surface) is { } made)
                descendants.Add(new($"transfer:{made.Source}:face:{face.Id.Value}", "Face", SemanticTopologyRole.FilletSurface, made.Source, Face: face.Id,
                    ParentStableId: made.Source, FirmamentSelector: made.Selector, Addressability: SemanticTopologyAddressability.DerivedStable));
        }
        return correspondence with
        {
            Descendants = descendants,
            ProvenanceChain = correspondence.ProvenanceChain.Append("FaceCorrespondenceTransfer").ToArray(),
        };
    }

    /// <summary>The radius that defines a rounded surface: a cylinder's, a torus's tube, a sphere's.</summary>
    public static double? RoundingRadius(SurfaceGeometry surface) =>
        surface.Cylinder?.Radius ?? surface.Torus?.MinorRadius ?? surface.Sphere?.Radius;

    private static SurfaceGeometry? Surface(BrepBody body, FaceId face) => body.TryGetFaceSurfaceGeometry(face, out var surface) ? surface : null;

    private static bool SameSurface(SurfaceGeometry a, SurfaceGeometry b)
    {
        static bool OnLine(Point3D p, Point3D origin, Vector3D axis) { var d = p - origin; return (d - axis * d.Dot(axis)).Length < Tolerance; }
        static bool Parallel(Vector3D a, Vector3D b) => Math.Abs(Math.Abs(a.Dot(b)) - 1) < 1e-9;
        if (a.Plane is { } p && b.Plane is { } q)
            return p.Normal.ToVector().Dot(q.Normal.ToVector()) > 1 - 1e-9 && Math.Abs((q.Origin - p.Origin).Dot(p.Normal.ToVector())) < Tolerance;
        if (a.Cylinder is { } c && b.Cylinder is { } e)
            return Parallel(c.Axis.ToVector(), e.Axis.ToVector()) && Math.Abs(c.Radius - e.Radius) < Tolerance && OnLine(e.Origin, c.Origin, c.Axis.ToVector());
        if (a.Cone is { } k && b.Cone is { } m)
            return (k.Apex - m.Apex).Length < Tolerance && k.Axis.ToVector().Dot(m.Axis.ToVector()) > 1 - 1e-9 && Math.Abs(k.SemiAngleRadians - m.SemiAngleRadians) < 1e-9;
        if (a.Torus is { } t && b.Torus is { } u)
            return (t.Center - u.Center).Length < Tolerance && Parallel(t.Axis.ToVector(), u.Axis.ToVector())
                && Math.Abs(t.MajorRadius - u.MajorRadius) < Tolerance && Math.Abs(t.MinorRadius - u.MinorRadius) < Tolerance;
        if (a.Sphere is { } s && b.Sphere is { } w)
            return (s.Center - w.Center).Length < Tolerance && Math.Abs(s.Radius - w.Radius) < Tolerance;
        return false;
    }

    private static (Point3D Min, Point3D Max) Box(BrepBody body, FaceId face)
    {
        var points = body.Topology.GetFace(face).LoopIds.SelectMany(id => body.Topology.GetLoop(id).CoedgeIds)
            .Select(id => body.Topology.GetEdge(body.Topology.GetCoedge(id).EdgeId))
            .SelectMany(e => new[] { e.StartVertexId, e.EndVertexId }).Distinct()
            .Select(v => body.TryGetVertexPoint(v, out var p) ? p : (Point3D?)null).OfType<Point3D>().ToArray();
        if (points.Length == 0) return (new(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity), new(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity));
        return (new(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z)), new(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z)));
    }

    /// <summary>The area of a box's two largest sides' rectangle: a size measure that ranks a face among faces on one surface.</summary>
    private static double Extent((Point3D Min, Point3D Max) box)
    {
        var sides = new[] { box.Max.X - box.Min.X, box.Max.Y - box.Min.Y, box.Max.Z - box.Min.Z }.Order().ToArray();
        return double.IsFinite(sides[2]) ? sides[1] * sides[2] : 0;
    }

    private static bool Overlap((Point3D Min, Point3D Max) a, (Point3D Min, Point3D Max) b, double margin) =>
        a.Min.X - margin <= b.Max.X && b.Min.X <= a.Max.X + margin
        && a.Min.Y - margin <= b.Max.Y && b.Min.Y <= a.Max.Y + margin
        && a.Min.Z - margin <= b.Max.Z && b.Min.Z <= a.Max.Z + margin;
}
