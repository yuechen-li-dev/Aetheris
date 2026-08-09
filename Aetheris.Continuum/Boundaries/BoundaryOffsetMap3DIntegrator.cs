using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Boundaries;

/// <summary>
/// Deterministic fixed-cell integration of a local graph. Subsamples refine geometry inside a Cut cell;
/// they never subdivide or alter the owning continuum lattice.
/// </summary>
public static class BoundaryOffsetMap3DIntegrator
{
    private readonly record struct Point2(double U, double V);

    public static BoundaryMapCellEstimate Integrate(IBoundaryOffsetMap map, BoundingBox3D bounds,
        int volumeSamplesPerAxis = 10, int areaSubdivisionsPerMapInterval = 6)
    {
        if (volumeSamplesPerAxis < 2) throw new ArgumentOutOfRangeException(nameof(volumeSamplesPerAxis));
        var occupiedVolume = IntegrateProjectedFootprint(map, bounds, volumeSamplesPerAxis);

        var area = 0d;
        var moment = Vector3D.Zero;
        var normalIntegral = Vector3D.Zero;
        // A fixed local quadrature density keeps integration cost independent of map-node count.
        var nu = int.Max(12, areaSubdivisionsPerMapInterval * 4);
        var nv = int.Max(12, areaSubdivisionsPerMapInterval * 4);
        for (var j = 0; j < nv; j++)
        for (var i = 0; i < nu; i++)
        {
            var u0 = Lerp(map.Domain.MinimumU, map.Domain.MaximumU, i / (double)nu);
            var u1 = Lerp(map.Domain.MinimumU, map.Domain.MaximumU, (i + 1d) / nu);
            var v0 = Lerp(map.Domain.MinimumV, map.Domain.MaximumV, j / (double)nv);
            var v1 = Lerp(map.Domain.MinimumV, map.Domain.MaximumV, (j + 1d) / nv);
            var a = map.Evaluate(u0, v0);
            var b = map.Evaluate(u1, v0);
            var c = map.Evaluate(u0, v1);
            var d = map.Evaluate(u1, v1);
            AddTriangle(a, b, d, bounds, ref area, ref moment, ref normalIntegral);
            AddTriangle(a, d, c, bounds, ref area, ref moment, ref normalIntegral);
        }

        var cellVolume = (bounds.Max.X - bounds.Min.X) * (bounds.Max.Y - bounds.Min.Y) * (bounds.Max.Z - bounds.Min.Z);
        return new BoundaryMapCellEstimate(double.Clamp(occupiedVolume / cellVolume, 0d, 1d), area, moment, normalIntegral);
    }

    private static double IntegrateProjectedFootprint(IBoundaryOffsetMap map, BoundingBox3D bounds, int subdivisions)
    {
        var projected = BoxCorners(bounds).Select(point =>
        {
            var delta = point - map.LocalFrame.Origin;
            return new Point2(delta.Dot(map.LocalFrame.TangentU), delta.Dot(map.LocalFrame.TangentV));
        });
        var hull = ConvexHull(projected);
        if (hull.Count < 3) return 0d;
        var volume = 0d;
        for (var h = 1; h < hull.Count - 1; h++)
        {
            var a = hull[0]; var b = hull[h]; var c = hull[h + 1];
            var triangleArea = double.Abs(Cross(a, b, c)) * 0.5d;
            var microArea = triangleArea / (subdivisions * subdivisions);
            for (var j = 0; j < subdivisions; j++)
            for (var i = 0; i < subdivisions - j; i++)
            {
                var p00 = Barycentric(a, b, c, i / (double)subdivisions, j / (double)subdivisions);
                var p10 = Barycentric(a, b, c, (i + 1d) / subdivisions, j / (double)subdivisions);
                var p01 = Barycentric(a, b, c, i / (double)subdivisions, (j + 1d) / subdivisions);
                volume += Thickness(map, bounds, Centroid(p00, p10, p01)) * microArea;
                if (i + j < subdivisions - 1)
                {
                    var p11 = Barycentric(a, b, c, (i + 1d) / subdivisions, (j + 1d) / subdivisions);
                    volume += Thickness(map, bounds, Centroid(p10, p11, p01)) * microArea;
                }
            }
        }
        return volume;
    }

    private static double Thickness(IBoundaryOffsetMap map, BoundingBox3D bounds, Point2 uv)
    {
        var lineOrigin = map.LocalFrame.Origin + (map.LocalFrame.TangentU * uv.U) + (map.LocalFrame.TangentV * uv.V);
        if (!ClipLineToBox(lineOrigin, map.LocalFrame.Normal, bounds, out var minimumW, out var maximumW)) return 0d;
        return double.Max(0d, maximumW - double.Max(minimumW, map.Evaluate(uv.U, uv.V).Offset));
    }

    private static IReadOnlyList<Point2> ConvexHull(IEnumerable<Point2> points)
    {
        var values = points.Distinct().OrderBy(p => p.U).ThenBy(p => p.V).ToArray();
        if (values.Length <= 2) return values;
        var lower = new List<Point2>();
        foreach (var p in values)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 1e-15d) lower.RemoveAt(lower.Count - 1);
            lower.Add(p);
        }
        var upper = new List<Point2>();
        for (var i = values.Length - 1; i >= 0; i--)
        {
            var p = values[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 1e-15d) upper.RemoveAt(upper.Count - 1);
            upper.Add(p);
        }
        lower.RemoveAt(lower.Count - 1); upper.RemoveAt(upper.Count - 1); lower.AddRange(upper);
        return lower;
    }

    private static Point2 Barycentric(Point2 a, Point2 b, Point2 c, double s, double t) =>
        new(a.U + (s * (b.U - a.U)) + (t * (c.U - a.U)), a.V + (s * (b.V - a.V)) + (t * (c.V - a.V)));
    private static Point2 Centroid(Point2 a, Point2 b, Point2 c) => new((a.U + b.U + c.U) / 3d, (a.V + b.V + c.V) / 3d);
    private static double Cross(Point2 a, Point2 b, Point2 c) => ((b.U - a.U) * (c.V - a.V)) - ((b.V - a.V) * (c.U - a.U));
    private static Point3D[] BoxCorners(BoundingBox3D b) =>
    [
        new(b.Min.X,b.Min.Y,b.Min.Z), new(b.Max.X,b.Min.Y,b.Min.Z), new(b.Min.X,b.Max.Y,b.Min.Z), new(b.Max.X,b.Max.Y,b.Min.Z),
        new(b.Min.X,b.Min.Y,b.Max.Z), new(b.Max.X,b.Min.Y,b.Max.Z), new(b.Min.X,b.Max.Y,b.Max.Z), new(b.Max.X,b.Max.Y,b.Max.Z),
    ];

    private static bool ClipLineToBox(Point3D origin, Vector3D direction, BoundingBox3D bounds, out double minimum, out double maximum)
    {
        minimum = double.NegativeInfinity;
        maximum = double.PositiveInfinity;
        if (!ClipAxis(origin.X, direction.X, bounds.Min.X, bounds.Max.X, ref minimum, ref maximum)
            || !ClipAxis(origin.Y, direction.Y, bounds.Min.Y, bounds.Max.Y, ref minimum, ref maximum)
            || !ClipAxis(origin.Z, direction.Z, bounds.Min.Z, bounds.Max.Z, ref minimum, ref maximum)) return false;
        return maximum >= minimum;
    }

    private static bool ClipAxis(double origin, double direction, double low, double high, ref double minimum, ref double maximum)
    {
        if (double.Abs(direction) <= 1e-14d) return origin >= low && origin <= high;
        var a = (low - origin) / direction;
        var b = (high - origin) / direction;
        if (a > b) (a, b) = (b, a);
        minimum = double.Max(minimum, a);
        maximum = double.Min(maximum, b);
        return maximum >= minimum;
    }

    private static void AddTriangle(BoundaryMapEvaluation a, BoundaryMapEvaluation b, BoundaryMapEvaluation c,
        BoundingBox3D bounds, ref double area, ref Vector3D moment, ref Vector3D normalIntegral)
    {
        var centroid = new Point3D((a.Position.X + b.Position.X + c.Position.X) / 3d,
            (a.Position.Y + b.Position.Y + c.Position.Y) / 3d,
            (a.Position.Z + b.Position.Z + c.Position.Z) / 3d);
        if (!Contains(bounds, centroid)) return;
        var cross = (b.Position - a.Position).Cross(c.Position - a.Position);
        var triangleArea = cross.Length * 0.5d;
        if (triangleArea <= 0d) return;
        area += triangleArea;
        moment += new Vector3D(centroid.X, centroid.Y, centroid.Z) * triangleArea;
        var normal = a.Normal + b.Normal + c.Normal;
        if (normal.TryNormalize(out normal)) normalIntegral += normal * triangleArea;
    }

    private static bool Contains(BoundingBox3D b, Point3D p) =>
        p.X >= b.Min.X - 1e-12d && p.X <= b.Max.X + 1e-12d
        && p.Y >= b.Min.Y - 1e-12d && p.Y <= b.Max.Y + 1e-12d
        && p.Z >= b.Min.Z - 1e-12d && p.Z <= b.Max.Z + 1e-12d;
    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);
}
