using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

/// <summary>Bounded Lawson flips on a counterclockwise XY triangulation. Boundary
/// edges and vertex identities remain unchanged. This does not insert or move geometry.</summary>
public static class PlanarMeshQuality
{
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
