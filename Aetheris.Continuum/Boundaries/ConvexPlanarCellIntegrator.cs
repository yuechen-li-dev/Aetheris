using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Continuum.Boundaries;

/// <summary>Exact deterministic clipping for a convex planar BRep shell against one Cartesian cell.</summary>
internal static class ConvexPlanarCellIntegrator
{
    private sealed record Polygon(IReadOnlyList<Point3D> Vertices, FaceId? BoundaryFace);

    public static LocalBoundaryIntegration Integrate(BoundingBox3D cell, WholeShellBoundaryQuery shell,
        IReadOnlyDictionary<FaceId, MaterialSideEvidence> materialSides,
        IReadOnlyList<WholeShellBoundaryCandidate> localCandidates)
    {
        var polygons = CellPolygons(cell).ToList();
        // A Cut cell is clipped only by support patches that intersect that cell.  Clipping by every
        // planar face in the shell incorrectly treats a non-convex whole part as one global half-space
        // intersection (most visibly for grid-aligned prism faces in M4B).
        foreach (var face in localCandidates.OrderBy(f => f.FaceId.Value))
        {
            var evidence = materialSides[face.FaceId];
            if (evidence.MaterialSideNormal is not Vector3D inward)
                throw new InvalidOperationException($"Planar face {face.FaceId.Value} has no CIR-resolved material side.");
            polygons = Clip(polygons, evidence.BoundaryPoint, inward, face.FaceId);
            if (polygons.Count == 0) return new(0d, 0d, new Dictionary<string, double>(), "exact-convex-planar-clipping", 0);
        }

        var all = polygons.SelectMany(p => p.Vertices).ToArray();
        var center = new Point3D(all.Average(p => p.X), all.Average(p => p.Y), all.Average(p => p.Z));
        var volume = polygons.Sum(p => FanMeasure(p.Vertices, center, volume: true));
        var areas = polygons.Where(p => p.BoundaryFace.HasValue)
            .GroupBy(p => p.BoundaryFace!.Value.Value.ToString())
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(p => FanMeasure(p.Vertices, center, volume: false)), StringComparer.Ordinal);
        var cellVolume = (cell.Max.X-cell.Min.X)*(cell.Max.Y-cell.Min.Y)*(cell.Max.Z-cell.Min.Z);
        return new(double.Clamp(volume / cellVolume, 0d, 1d), areas.Values.Sum(), areas, "exact-convex-planar-clipping", 0);
    }

    private static List<Polygon> Clip(IReadOnlyList<Polygon> source, Point3D origin, Vector3D inward, FaceId sourceFace)
    {
        const double tolerance = 1e-11d;
        var result = new List<Polygon>(); var intersections = new List<Point3D>();
        foreach (var polygon in source)
        {
            var output = new List<Point3D>();
            for (var i = 0; i < polygon.Vertices.Count; i++)
            {
                var a = polygon.Vertices[i]; var b = polygon.Vertices[(i + 1) % polygon.Vertices.Count];
                var da = (a-origin).Dot(inward); var db = (b-origin).Dot(inward);
                var aInside = da >= -tolerance; var bInside = db >= -tolerance;
                if (aInside) AddUnique(output, a, tolerance);
                if (aInside != bInside)
                {
                    var t = da / (da-db); var point = a + ((b-a)*t);
                    AddUnique(output, point, tolerance); AddUnique(intersections, point, tolerance);
                }
            }
            RemoveClosingDuplicate(output, tolerance);
            if (output.Count >= 3) result.Add(new(output, polygon.BoundaryFace));
        }
        if (intersections.Count >= 3)
        {
            var ordered = OrderCap(intersections, -inward);
            if (ordered.Count >= 3) result.Add(new(ordered, sourceFace));
        }
        return result;
    }

    private static IReadOnlyList<Point3D> OrderCap(IReadOnlyList<Point3D> points, Vector3D normal)
    {
        var center = new Point3D(points.Average(p=>p.X),points.Average(p=>p.Y),points.Average(p=>p.Z));
        var seed = double.Abs(normal.X) < .8d ? new Vector3D(1,0,0) : new Vector3D(0,1,0);
        var u = seed - normal * seed.Dot(normal); u.TryNormalize(out u); var v = normal.Cross(u); v.TryNormalize(out v);
        return points.OrderBy(p => double.Atan2((p-center).Dot(v),(p-center).Dot(u))).ToArray();
    }

    private static double FanMeasure(IReadOnlyList<Point3D> vertices, Point3D center, bool volume)
    {
        if (vertices.Count < 3) return 0d; var total=0d; var a=vertices[0];
        for(var i=1;i<vertices.Count-1;i++)
        {
            var cross=(vertices[i]-a).Cross(vertices[i+1]-a);
            total += volume ? double.Abs((a-center).Dot(cross))/6d : cross.Length*.5d;
        }
        return total;
    }

    private static IEnumerable<Polygon> CellPolygons(BoundingBox3D b)
    {
        var p000=new Point3D(b.Min.X,b.Min.Y,b.Min.Z); var p100=new Point3D(b.Max.X,b.Min.Y,b.Min.Z);
        var p010=new Point3D(b.Min.X,b.Max.Y,b.Min.Z); var p110=new Point3D(b.Max.X,b.Max.Y,b.Min.Z);
        var p001=new Point3D(b.Min.X,b.Min.Y,b.Max.Z); var p101=new Point3D(b.Max.X,b.Min.Y,b.Max.Z);
        var p011=new Point3D(b.Min.X,b.Max.Y,b.Max.Z); var p111=new Point3D(b.Max.X,b.Max.Y,b.Max.Z);
        yield return new([p000,p001,p011,p010],null); yield return new([p100,p110,p111,p101],null);
        yield return new([p000,p100,p101,p001],null); yield return new([p010,p011,p111,p110],null);
        yield return new([p000,p010,p110,p100],null); yield return new([p001,p101,p111,p011],null);
    }

    private static void AddUnique(List<Point3D> points, Point3D value, double tolerance)
    { if (!points.Any(p => (p-value).Length <= tolerance)) points.Add(value); }
    private static void RemoveClosingDuplicate(List<Point3D> points,double tolerance)
    { if(points.Count>1 && (points[0]-points[^1]).Length<=tolerance) points.RemoveAt(points.Count-1); }
}
