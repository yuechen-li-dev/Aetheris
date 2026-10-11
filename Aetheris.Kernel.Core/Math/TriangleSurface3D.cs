using System.Numerics;

namespace Aetheris.Kernel.Core.Math;

public readonly record struct TriangleProximity3D(Vector3 Point, Vector3 Barycentric);

/// <summary>Shared closest-surface query. Degenerate triangles reduce to their longest edge.</summary>
public static class TriangleSurface3D
{
    public static TriangleProximity3D ClosestPoint(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        float aa = Vector3.Dot(ab, ab);
        float bb = Vector3.Dot(ab, ac);
        float cc = Vector3.Dot(ac, ac);
        float determinant = aa * cc - bb * bb;
        if (determinant <= 1e-8f * aa * cc || aa * cc < 1e-24f)
        {
            float bc = Vector3.DistanceSquared(b, c);
            if (aa >= cc && aa >= bc)
            {
                return Segment(point, a, b, Vector3.UnitX, Vector3.UnitY);
            }
            if (cc >= bc)
            {
                return Segment(point, a, c, Vector3.UnitX, Vector3.UnitZ);
            }
            return Segment(point, b, c, Vector3.UnitY, Vector3.UnitZ);
        }
        Vector3 closest = ClosestOnTriangle(point, a, b, c);
        Vector3 delta = closest - a;
        float x = Vector3.Dot(delta, ab);
        float y = Vector3.Dot(delta, ac);
        float v = (cc * x - bb * y) / determinant;
        float w = (aa * y - bb * x) / determinant;
        return new(closest, new(1 - v - w, v, w));
    }

    private static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = p - a;
        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return a;
        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return b;
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return c;
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
        float va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 >= d3 && d5 >= d6) return b + (c - b) * ((d4 - d3) / (d4 - d3 + d5 - d6));
        float inverse = 1 / (va + vb + vc);
        return a + ab * (vb * inverse) + ac * (vc * inverse);
    }

    private static TriangleProximity3D Segment(Vector3 point, Vector3 start, Vector3 end,
        Vector3 startWeight, Vector3 endWeight)
    {
        Vector3 edge = end - start;
        float length = edge.LengthSquared();
        float t = length > 1e-20f ? System.Math.Clamp(Vector3.Dot(point - start, edge) / length, 0, 1) : 0;
        return new(start + edge * t, startWeight * (1 - t) + endWeight * t);
    }
}
