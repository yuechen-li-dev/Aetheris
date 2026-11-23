using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.PlasticShell;

/// <summary>Nearest-hit rays in any direction against the part's display mesh, through a bounding-volume tree.</summary>
internal sealed class MoldMeshRays
{
    private readonly (Point3D A, Point3D B, Point3D C, int Face)[] triangles;
    private readonly Node root;

    private sealed record Node(Point3D Min, Point3D Max, Node? Left, Node? Right, int[]? Items);

    public MoldMeshRays(IReadOnlyList<DisplayFaceMeshPatch> patches)
    {
        triangles = patches.SelectMany(p => Enumerable.Range(0, p.TriangleIndices.Count / 3).Select(i =>
            (p.Positions[p.TriangleIndices[3 * i]], p.Positions[p.TriangleIndices[3 * i + 1]], p.Positions[p.TriangleIndices[3 * i + 2]], p.FaceId.Value))).ToArray();
        root = Build(Enumerable.Range(0, triangles.Length).ToArray(), 0);
    }

    private Node Build(int[] items, int depth)
    {
        var (min, max) = Bounds(items);
        if (items.Length <= 8 || depth > 40) return new(min, max, null, null, items);
        var extent = max - min;
        var axis = extent.X >= extent.Y && extent.X >= extent.Z ? 0 : extent.Y >= extent.Z ? 1 : 2;
        double Key(int i) { var t = triangles[i]; var c = axis == 0 ? t.A.X + t.B.X + t.C.X : axis == 1 ? t.A.Y + t.B.Y + t.C.Y : t.A.Z + t.B.Z + t.C.Z; return c; }
        var sorted = items.OrderBy(Key).ToArray();
        var half = sorted.Length / 2;
        return new(min, max, Build(sorted[..half], depth + 1), Build(sorted[half..], depth + 1), null);
    }

    private (Point3D, Point3D) Bounds(int[] items)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        foreach (var i in items)
            foreach (var p in new[] { triangles[i].A, triangles[i].B, triangles[i].C })
            { x0 = double.Min(x0, p.X); y0 = double.Min(y0, p.Y); z0 = double.Min(z0, p.Z); x1 = double.Max(x1, p.X); y1 = double.Max(y1, p.Y); z1 = double.Max(z1, p.Z); }
        return (new Point3D(x0, y0, z0), new Point3D(x1, y1, z1));
    }

    /// <summary>The nearest triangle the ray crosses beyond <paramref name="minimum"/>: its distance and face.</summary>
    public (double Distance, int Face)? Nearest(Point3D origin, Vector3D direction, double minimum)
    {
        (double, int)? best = null;
        var limit = double.PositiveInfinity;
        var stack = new Stack<Node>(); stack.Push(root);
        while (stack.TryPop(out var node))
        {
            if (!Slab(node, origin, direction, limit)) continue;
            if (node.Items is { } items)
            {
                foreach (var i in items)
                {
                    var (a, b, c, face) = triangles[i];
                    var e1 = b - a; var e2 = c - a;
                    var h = direction.Cross(e2);
                    var det = e1.Dot(h);
                    if (double.Abs(det) < 1e-14 * e1.Length * e2.Length) continue;
                    var f = 1d / det; var s = origin - a;
                    var u = f * s.Dot(h);
                    if (u < -1e-9 || u > 1 + 1e-9) continue;
                    var q = s.Cross(e1);
                    var v = f * direction.Dot(q);
                    if (v < -1e-9 || u + v > 1 + 1e-9) continue;
                    var t = f * e2.Dot(q);
                    if (t > minimum && t < limit) { limit = t; best = (t, face); }
                }
                continue;
            }
            stack.Push(node.Left!); stack.Push(node.Right!);
        }
        return best;
    }

    /// <summary>The nearest point of the mesh to <paramref name="point"/>, skipping some faces: its distance and face.</summary>
    public (double Distance, int Face)? Closest(Point3D point, Func<int, bool>? skip = null, double limit = double.PositiveInfinity)
    {
        (double, int)? best = null;
        var stack = new Stack<Node>(); stack.Push(root);
        while (stack.TryPop(out var node))
        {
            if (BoxDistance(node, point) >= limit) continue;
            if (node.Items is { } items)
            {
                foreach (var i in items)
                {
                    var t = triangles[i];
                    if (skip?.Invoke(t.Face) == true) continue;
                    var d = (ClosestOnTriangle(point, t.A, t.B, t.C) - point).Length;
                    if (d < limit) { limit = d; best = (d, t.Face); }
                }
                continue;
            }
            // Nearer child first, so the limit tightens early.
            var (near, far) = BoxDistance(node.Left!, point) <= BoxDistance(node.Right!, point) ? (node.Left!, node.Right!) : (node.Right!, node.Left!);
            stack.Push(far); stack.Push(near);
        }
        return best;
    }

    private static double BoxDistance(Node node, Point3D p)
    {
        var dx = double.Max(0, double.Max(node.Min.X - p.X, p.X - node.Max.X));
        var dy = double.Max(0, double.Max(node.Min.Y - p.Y, p.Y - node.Max.Y));
        var dz = double.Max(0, double.Max(node.Min.Z - p.Z, p.Z - node.Max.Z));
        return double.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>Closest point on a triangle (Ericson, Real-Time Collision Detection, 5.1.5).</summary>
    private static Point3D ClosestOnTriangle(Point3D p, Point3D a, Point3D b, Point3D c)
    {
        var ab = b - a; var ac = c - a; var ap = p - a;
        var d1 = ab.Dot(ap); var d2 = ac.Dot(ap);
        if (d1 <= 0 && d2 <= 0) return a;
        var bp = p - b; var d3 = ab.Dot(bp); var d4 = ac.Dot(bp);
        if (d3 >= 0 && d4 <= d3) return b;
        var vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
        var cp = p - c; var d5 = ab.Dot(cp); var d6 = ac.Dot(cp);
        if (d6 >= 0 && d5 <= d6) return c;
        var vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
        var va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
        var denom = 1 / (va + vb + vc);
        return a + ab * (vb * denom) + ac * (vc * denom);
    }

    private static bool Slab(Node node, Point3D o, Vector3D d, double limit)
    {
        double t0 = 0, t1 = limit;
        for (var axis = 0; axis < 3; axis++)
        {
            var (origin, dir, lo, hi) = axis switch { 0 => (o.X, d.X, node.Min.X, node.Max.X), 1 => (o.Y, d.Y, node.Min.Y, node.Max.Y), _ => (o.Z, d.Z, node.Min.Z, node.Max.Z) };
            if (double.Abs(dir) < 1e-300) { if (origin < lo - 1e-9 || origin > hi + 1e-9) return false; continue; }
            var a = (lo - 1e-9 - origin) / dir; var b = (hi + 1e-9 - origin) / dir;
            if (a > b) (a, b) = (b, a);
            t0 = double.Max(t0, a); t1 = double.Min(t1, b);
            if (t0 > t1) return false;
        }
        return true;
    }
}
