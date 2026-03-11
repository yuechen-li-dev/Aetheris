using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

internal enum PlanarPolygonTriangulationFailure
{
    Degenerate,
    NonSimple,
    TriangulationFailed
}

internal static class PlanarPolygonTriangulator
{
    private const double Epsilon = 1e-9d;

    public static bool TryTriangulate(
        IReadOnlyList<Point3D> polygonPoints,
        Vector3D planeNormal,
        Vector3D? planeUAxis,
        Vector3D? planeVAxis,
        out IReadOnlyList<int> indices,
        out PlanarPolygonTriangulationFailure? failure)
    {
        indices = Array.Empty<int>();
        failure = null;

        if (polygonPoints.Count < 3)
        {
            failure = PlanarPolygonTriangulationFailure.Degenerate;
            return false;
        }

        if (!planeNormal.TryNormalize(out var normal))
        {
            failure = PlanarPolygonTriangulationFailure.Degenerate;
            return false;
        }

        var projected = ProjectToPlane(polygonPoints, normal, planeUAxis, planeVAxis);
        if (!ValidatePolygon(projected))
        {
            failure = PlanarPolygonTriangulationFailure.Degenerate;
            return false;
        }

        if (HasSelfIntersection(projected))
        {
            failure = PlanarPolygonTriangulationFailure.NonSimple;
            return false;
        }

        var winding = SignedArea(projected);
        if (double.Abs(winding) <= Epsilon)
        {
            failure = PlanarPolygonTriangulationFailure.Degenerate;
            return false;
        }

        var working = Enumerable.Range(0, polygonPoints.Count).ToList();
        var result = new List<int>((polygonPoints.Count - 2) * 3);
        var isCounterClockwise = winding > 0d;

        while (working.Count > 3)
        {
            var earFound = false;
            for (var i = 0; i < working.Count; i++)
            {
                var prevIndex = working[(i + working.Count - 1) % working.Count];
                var currentIndex = working[i];
                var nextIndex = working[(i + 1) % working.Count];

                if (!IsConvex(projected[prevIndex], projected[currentIndex], projected[nextIndex], isCounterClockwise))
                {
                    continue;
                }

                var containsVertex = false;
                for (var j = 0; j < working.Count; j++)
                {
                    var candidate = working[j];
                    if (candidate == prevIndex || candidate == currentIndex || candidate == nextIndex)
                    {
                        continue;
                    }

                    if (PointInTriangle(projected[candidate], projected[prevIndex], projected[currentIndex], projected[nextIndex]))
                    {
                        containsVertex = true;
                        break;
                    }
                }

                if (containsVertex)
                {
                    continue;
                }

                result.Add(prevIndex);
                result.Add(currentIndex);
                result.Add(nextIndex);
                working.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound)
            {
                failure = PlanarPolygonTriangulationFailure.TriangulationFailed;
                return false;
            }
        }

        result.Add(working[0]);
        result.Add(working[1]);
        result.Add(working[2]);

        var expectedNormal = normal;
        if (result.Count >= 3)
        {
            var firstNormal = (polygonPoints[result[1]] - polygonPoints[result[0]])
                .Cross(polygonPoints[result[2]] - polygonPoints[result[0]]);
            if (firstNormal.Dot(expectedNormal) < 0d)
            {
                for (var i = 0; i < result.Count; i += 3)
                {
                    (result[i + 1], result[i + 2]) = (result[i + 2], result[i + 1]);
                }
            }
        }

        indices = result;
        return true;
    }

    private static List<Point2> ProjectToPlane(
        IReadOnlyList<Point3D> polygonPoints,
        Vector3D normal,
        Vector3D? planeUAxis,
        Vector3D? planeVAxis)
    {
        if (planeUAxis is { } providedU && planeVAxis is { } providedV
            && providedU.TryNormalize(out var normalizedU)
            && providedV.TryNormalize(out var normalizedV))
        {
            return ProjectToAxes(polygonPoints, normalizedU, normalizedV);
        }

        var referenceAxis = double.Abs(normal.Z) < 0.9d
            ? new Vector3D(0d, 0d, 1d)
            : new Vector3D(1d, 0d, 0d);

        var uAxis = referenceAxis.Cross(normal);
        if (!uAxis.TryNormalize(out uAxis))
        {
            referenceAxis = new Vector3D(0d, 1d, 0d);
            uAxis = referenceAxis.Cross(normal);
            uAxis.TryNormalize(out uAxis);
        }

        var vAxis = normal.Cross(uAxis);
        vAxis.TryNormalize(out vAxis);
        return ProjectToAxes(polygonPoints, uAxis, vAxis);
    }

    private static List<Point2> ProjectToAxes(IReadOnlyList<Point3D> polygonPoints, Vector3D uAxis, Vector3D vAxis)
    {
        var origin = polygonPoints[0];
        return polygonPoints
            .Select(point =>
            {
                var offset = point - origin;
                return new Point2(offset.Dot(uAxis), offset.Dot(vAxis));
            })
            .ToList();
    }

    private static bool ValidatePolygon(IReadOnlyList<Point2> points)
    {
        for (var i = 0; i < points.Count; i++)
        {
            var next = points[(i + 1) % points.Count];
            if (DistanceSquared(points[i], next) <= Epsilon * Epsilon)
            {
                return false;
            }
        }

        var uniqueCount = points.Distinct().Count();
        return uniqueCount >= 3;
    }

    private static bool HasSelfIntersection(IReadOnlyList<Point2> points)
    {
        for (var i = 0; i < points.Count; i++)
        {
            var a0 = points[i];
            var a1 = points[(i + 1) % points.Count];
            for (var j = i + 1; j < points.Count; j++)
            {
                var b0 = points[j];
                var b1 = points[(j + 1) % points.Count];
                if (i == j || (i + 1) % points.Count == j || i == (j + 1) % points.Count)
                {
                    continue;
                }

                if (SegmentsIntersect(a0, a1, b0, b1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(Point2 a0, Point2 a1, Point2 b0, Point2 b1)
    {
        var o1 = Orientation(a0, a1, b0);
        var o2 = Orientation(a0, a1, b1);
        var o3 = Orientation(b0, b1, a0);
        var o4 = Orientation(b0, b1, a1);

        if ((o1 * o2 < -Epsilon) && (o3 * o4 < -Epsilon))
        {
            return true;
        }

        if (double.Abs(o1) <= Epsilon && OnSegment(a0, b0, a1)) return true;
        if (double.Abs(o2) <= Epsilon && OnSegment(a0, b1, a1)) return true;
        if (double.Abs(o3) <= Epsilon && OnSegment(b0, a0, b1)) return true;
        if (double.Abs(o4) <= Epsilon && OnSegment(b0, a1, b1)) return true;
        return false;
    }

    private static bool IsConvex(Point2 a, Point2 b, Point2 c, bool isCounterClockwise)
    {
        var cross = Orientation(a, b, c);
        return isCounterClockwise ? cross > Epsilon : cross < -Epsilon;
    }

    private static bool PointInTriangle(Point2 p, Point2 a, Point2 b, Point2 c)
    {
        var o1 = Orientation(a, b, p);
        var o2 = Orientation(b, c, p);
        var o3 = Orientation(c, a, p);

        var hasNeg = o1 < -Epsilon || o2 < -Epsilon || o3 < -Epsilon;
        var hasPos = o1 > Epsilon || o2 > Epsilon || o3 > Epsilon;
        return !(hasNeg && hasPos);
    }

    private static double SignedArea(IReadOnlyList<Point2> points)
    {
        var area = 0d;
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            area += (current.X * next.Y) - (next.X * current.Y);
        }

        return area * 0.5d;
    }

    private static double Orientation(Point2 a, Point2 b, Point2 c)
        => ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

    private static bool OnSegment(Point2 a, Point2 p, Point2 b)
        => p.X >= double.Min(a.X, b.X) - Epsilon
           && p.X <= double.Max(a.X, b.X) + Epsilon
           && p.Y >= double.Min(a.Y, b.Y) - Epsilon
           && p.Y <= double.Max(a.Y, b.Y) + Epsilon;

    private static double DistanceSquared(Point2 a, Point2 b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    private readonly record struct Point2(double X, double Y);
}
