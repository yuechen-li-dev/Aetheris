using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

public sealed record PlanarMeshFillResult(IReadOnlyList<Point3D> Points, IReadOnlyList<int> Indices);

/// <summary>Bounded point-distribution refinement and Lawson flips on an XY triangulation.
/// Existing boundary segments and vertex identities remain unchanged.</summary>
public static class PlanarMeshQuality
{
    /// <summary>Populate a constrained XY triangulation with a bounded triangular lattice,
    /// then legalize its interior edges. Existing vertices and boundary segments are retained.
    /// Clearance avoids packing new interior points beside short authored boundary features;
    /// this is size/point-distribution control, not a guaranteed minimum-angle mesher.</summary>
    public static PlanarMeshFillResult FillInterior(IReadOnlyList<Point3D> sourcePoints,
        IReadOnlyList<int> sourceIndices, double spacing, int maximumVertices = 8192)
    {
        ArgumentNullException.ThrowIfNull(sourceIndices);
        var grid = FillGrid(sourcePoints, spacing, maximumVertices);
        var points = sourcePoints.ToList();
        var indices = Improve(sourcePoints, sourceIndices).ToList();
        if (indices.Count == 0)
        {
            throw new ArgumentException("Planar fill requires a nonempty constant-Z triangulation.");
        }
        var counts = new Dictionary<(int, int), int>();
        for (int face = 0; face < indices.Count; face += 3)
        {
            for (int edge = 0; edge < 3; edge++)
            {
                int a = indices[face + edge];
                int b = indices[face + (edge + 1) % 3];
                var key = (System.Math.Min(a, b), System.Math.Max(a, b));
                counts.TryGetValue(key, out int count);
                counts[key] = count + 1;
            }
        }
        if (counts.Any(entry => entry.Value > 2))
        {
            throw new ArgumentException("Planar fill requires manifold triangle edges.");
        }
        var boundary = counts.Where(entry => entry.Value == 1).Select(entry => entry.Key).ToArray();
        double clearanceSquared = spacing * spacing * .16;
        for (int row = 0; row < grid.Rows; row++)
        {
            double y = grid.MinimumY + (row + .5) * grid.RowSpacing;
            double offset = row % 2 == 0 ? .5 : 1;
            for (int column = 0; column < grid.Columns; column++)
            {
                var point = new Point3D(grid.MinimumX + (column + offset) * spacing, y, points[0].Z);
                if (boundary.Any(edge => SegmentDistanceSquared(point, points[edge.Item1], points[edge.Item2]) < clearanceSquared)
                    || points.Any(existing => DistanceSquared(point, existing) < clearanceSquared))
                {
                    continue;
                }
                var containing = new List<int>();
                for (int face = 0; face < indices.Count; face += 3)
                {
                    if (Cross(points[indices[face]], points[indices[face + 1]], point) >= -1e-8
                        && Cross(points[indices[face + 1]], points[indices[face + 2]], point) >= -1e-8
                        && Cross(points[indices[face + 2]], points[indices[face]], point) >= -1e-8)
                    {
                        containing.Add(face);
                    }
                }
                if (containing.Count == 0)
                {
                    continue;
                }
                if (points.Count == maximumVertices)
                {
                    throw new InvalidOperationException("planar-mesh-fill-budget: Interior fill exceeds its vertex budget.");
                }
                int inserted = points.Count;
                points.Add(point);
                foreach (int face in containing.OrderDescending())
                {
                    int[] triangle = indices.Skip(face).Take(3).ToArray();
                    indices.RemoveRange(face, 3);
                    for (int edge = 0; edge < 3; edge++)
                    {
                        int a = triangle[edge];
                        int b = triangle[(edge + 1) % 3];
                        if (Cross(points[a], points[b], point) > 1e-8)
                        {
                            indices.AddRange([a, b, inserted]);
                        }
                    }
                }
            }
        }
        return new(points, Improve(points, indices));

        static double Cross(Point3D a, Point3D b, Point3D c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }
        static double DistanceSquared(Point3D a, Point3D b)
        {
            double x = a.X - b.X;
            double y = a.Y - b.Y;
            return x * x + y * y;
        }
        static double SegmentDistanceSquared(Point3D point, Point3D a, Point3D b)
        {
            double x = b.X - a.X;
            double y = b.Y - a.Y;
            double t = System.Math.Clamp(((point.X - a.X) * x + (point.Y - a.Y) * y) / (x * x + y * y), 0, 1);
            return DistanceSquared(point, new(a.X + x * t, a.Y + y * t, a.Z));
        }
    }

    /// <summary>Check fill admission before a caller pays for seed triangulation.
    /// The same checks run in FillInterior; this does not validate polygon topology.</summary>
    public static void ValidateFillBudget(IReadOnlyList<Point3D> points, double spacing, int maximumVertices = 8192)
    {
        _ = FillGrid(points, spacing, maximumVertices);
    }

    private static (double MinimumX, double MinimumY, double Columns, double Rows, double RowSpacing)
        FillGrid(IReadOnlyList<Point3D> points, double spacing, int maximumVertices)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (!double.IsFinite(spacing) || spacing <= 0 || maximumVertices is < 3 or > 65536
            || points.Count > maximumVertices)
        {
            throw new ArgumentException("Invalid planar fill spacing or vertex budget.");
        }
        if (points.Count < 3 || points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)
            || !double.IsFinite(point.Z) || System.Math.Abs(point.Z - points[0].Z) > 1e-9))
        {
            throw new ArgumentException("Planar fill requires finite constant-Z points.");
        }
        double minimumX = points.Min(point => point.X);
        double minimumY = points.Min(point => point.Y);
        double rowSpacing = spacing * System.Math.Sqrt(3) * .5;
        double columns = System.Math.Ceiling((points.Max(point => point.X) - minimumX) / spacing);
        double rows = System.Math.Ceiling((points.Max(point => point.Y) - minimumY) / rowSpacing);
        if (!double.IsFinite(columns * rows) || columns * rows > maximumVertices * 16d)
        {
            throw new InvalidOperationException("planar-mesh-fill-budget: Candidate grid exceeds its bounded work budget.");
        }
        return (minimumX, minimumY, columns, rows, rowSpacing);
    }

    public static IReadOnlyList<int> Improve(IReadOnlyList<Point3D> points, IReadOnlyList<int> source)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(source);
        if (source.Count % 3 != 0 || source.Any(index => index < 0 || index >= points.Count)
            || points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z)))
        {
            throw new ArgumentException("Expected valid indexed XY triangles.");
        }
        var indices = source.ToArray();
        for (int face = 0; face < indices.Length; face += 3)
        {
            if (Cross(indices[face], indices[face + 1], indices[face + 2]) <= 0)
            {
                throw new ArgumentException("Expected nondegenerate counterclockwise XY triangles.");
            }
        }
        for (int pass = 0; pass < 128; pass++)
        {
            var edges = new Dictionary<(int, int), List<(int Face, int Opposite)>>();
            for (int face = 0; face < indices.Length; face += 3)
            {
                for (int edge = 0; edge < 3; edge++)
                {
                    int a = indices[face + edge];
                    int b = indices[face + (edge + 1) % 3];
                    var key = (System.Math.Min(a, b), System.Math.Max(a, b));
                    if (!edges.TryGetValue(key, out var neighbors))
                    {
                        edges.Add(key, neighbors = []);
                    }
                    neighbors.Add((face, indices[face + (edge + 2) % 3]));
                }
            }
            var changed = new HashSet<int>();
            foreach (var (edge, neighbors) in edges.OrderBy(entry => entry.Key))
            {
                if (neighbors.Count != 2 || neighbors.Any(neighbor => changed.Contains(neighbor.Face)))
                {
                    continue;
                }
                int a = edge.Item1;
                int b = edge.Item2;
                int c = neighbors[0].Opposite;
                int d = neighbors[1].Opposite;
                if (c == d || Cross(a, b, c) * Cross(a, b, d) >= 0 || Cross(c, d, a) * Cross(c, d, b) >= 0)
                {
                    continue;
                }
                double cotangents = Cotangent(a, c, b) + Cotangent(a, d, b);
                if (cotangents >= -1e-8)
                {
                    continue;
                }
                Set(neighbors[0].Face, c, d, a);
                Set(neighbors[1].Face, d, c, b);
                changed.Add(neighbors[0].Face);
                changed.Add(neighbors[1].Face);
            }
            if (changed.Count == 0)
            {
                return indices;
            }
        }
        throw new InvalidOperationException("planar-mesh-quality-budget: Delaunay flips exceeded 128 passes.");

        double Cross(int a, int b, int c)
        {
            return (points[b].X - points[a].X) * (points[c].Y - points[a].Y)
                - (points[b].Y - points[a].Y) * (points[c].X - points[a].X);
        }
        double Cotangent(int a, int center, int b)
        {
            double ax = points[a].X - points[center].X;
            double ay = points[a].Y - points[center].Y;
            double bx = points[b].X - points[center].X;
            double by = points[b].Y - points[center].Y;
            return (ax * bx + ay * by) / System.Math.Abs(ax * by - ay * bx);
        }
        void Set(int face, int a, int b, int c)
        {
            indices[face] = a;
            if (Cross(a, b, c) > 0)
            {
                indices[face + 1] = b;
                indices[face + 2] = c;
            }
            else
            {
                indices[face + 1] = c;
                indices[face + 2] = b;
            }
        }
    }
}
