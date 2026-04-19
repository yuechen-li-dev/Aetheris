using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

internal static class BrepBooleanPrismaticFootprintContainment
{
    public static bool TryComputeContainmentMargin(
        (double X, double Y) point,
        IReadOnlyList<(double X, double Y)> footprint,
        ToleranceContext tolerance,
        out double minimumEdgeDistance)
    {
        minimumEdgeDistance = 0d;
        if (footprint.Count < 3 || !IsPointInsidePolygon(point, footprint, tolerance))
        {
            return false;
        }

        minimumEdgeDistance = ComputeMinimumDistanceToPolygonEdges(point, footprint);
        return true;
    }

    private static bool IsPointInsidePolygon(
        (double X, double Y) point,
        IReadOnlyList<(double X, double Y)> polygon,
        ToleranceContext tolerance)
    {
        var winding = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            if (DistancePointToSegment(point, a, b) <= tolerance.Linear)
            {
                return true;
            }

            var intersects = ((a.Y > point.Y) != (b.Y > point.Y))
                && (point.X < (((b.X - a.X) * (point.Y - a.Y)) / (b.Y - a.Y + double.Epsilon)) + a.X);
            if (intersects)
            {
                winding = !winding;
            }
        }

        return winding;
    }

    private static double ComputeMinimumDistanceToPolygonEdges((double X, double Y) point, IReadOnlyList<(double X, double Y)> polygon)
    {
        var minDistance = double.PositiveInfinity;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            var distance = DistancePointToSegment(point, a, b);
            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }

        return minDistance;
    }

    private static double DistancePointToSegment((double X, double Y) point, (double X, double Y) a, (double X, double Y) b)
    {
        var abX = b.X - a.X;
        var abY = b.Y - a.Y;
        var abLengthSquared = (abX * abX) + (abY * abY);
        if (abLengthSquared <= double.Epsilon)
        {
            var dx = point.X - a.X;
            var dy = point.Y - a.Y;
            return System.Math.Sqrt((dx * dx) + (dy * dy));
        }

        var t = (((point.X - a.X) * abX) + ((point.Y - a.Y) * abY)) / abLengthSquared;
        t = System.Math.Max(0d, System.Math.Min(1d, t));
        var closestX = a.X + (t * abX);
        var closestY = a.Y + (t * abY);
        var ddx = point.X - closestX;
        var ddy = point.Y - closestY;
        return System.Math.Sqrt((ddx * ddx) + (ddy * ddy));
    }
}
