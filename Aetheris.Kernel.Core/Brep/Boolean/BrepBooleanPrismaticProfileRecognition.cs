using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public readonly record struct RecognizedPrismaticProfile(
    AxisAlignedBoxExtents Bounds,
    IReadOnlyList<(double X, double Y)> Footprint);

internal static class BrepBooleanPrismaticProfileRecognition
{
    public static bool TryRecognize(BrepBody body, ToleranceContext tolerance, out RecognizedPrismaticProfile profile, out string reason)
    {
        profile = default;
        reason = "uninitialized";

        var points = new List<Point3D>();
        foreach (var vertex in body.Topology.Vertices)
        {
            if (!body.TryGetVertexPoint(vertex.Id, out var point))
            {
                points.Clear();
                break;
            }

            points.Add(point);
        }

        if (points.Count == 0)
        {
            foreach (var edge in body.Topology.Edges)
            {
                if (!body.TryGetEdgeCurveGeometry(edge.Id, out var curve) || curve?.Kind != Geometry.CurveGeometryKind.Line3)
                {
                    continue;
                }

                if (!body.Bindings.TryGetEdgeBinding(edge.Id, out var edgeBinding))
                {
                    continue;
                }

                var interval = edgeBinding.TrimInterval ?? new ParameterInterval(0d, 1d);
                var start = curve.Line3!.Value.Evaluate(interval.Start);
                var end = curve.Line3.Value.Evaluate(interval.End);
                points.Add(start);
                points.Add(end);
            }
        }

        if (points.Count == 0)
        {
            reason = "prismatic profile recognition requires vertex points or line-edge bindings.";
            return false;
        }

        if (points.Count < 6)
        {
            reason = "prismatic profile recognition requires at least six vertices.";
            return false;
        }

        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);
        var minZ = points.Min(p => p.Z);
        var maxZ = points.Max(p => p.Z);
        if ((maxZ - minZ) <= tolerance.Linear)
        {
            reason = "prismatic profile recognition requires positive Z span.";
            return false;
        }

        var bottom = points
            .Where(p => ToleranceMath.AlmostEqual(p.Z, minZ, tolerance))
            .Select(p => (p.X, p.Y))
            .ToArray();
        var top = points
            .Where(p => ToleranceMath.AlmostEqual(p.Z, maxZ, tolerance))
            .Select(p => (p.X, p.Y))
            .ToArray();

        if (bottom.Length < 3 || top.Length < 3)
        {
            reason = "prismatic profile recognition requires planar top and bottom loops with at least three vertices.";
            return false;
        }

        var bottomUnique = UniqueXY(bottom, tolerance);
        var topUnique = UniqueXY(top, tolerance);
        if (bottomUnique.Count < 3 || topUnique.Count < 3 || bottomUnique.Count != topUnique.Count)
        {
            reason = "prismatic profile recognition requires matching top/bottom XY loop cardinality.";
            return false;
        }

        foreach (var xy in bottomUnique)
        {
            if (!ContainsXY(topUnique, xy, tolerance))
            {
                reason = "prismatic profile recognition requires matching top/bottom XY vertices.";
                return false;
            }
        }

        profile = new RecognizedPrismaticProfile(
            new AxisAlignedBoxExtents(minX, maxX, minY, maxY, minZ, maxZ),
            OrderLoop(bottomUnique));
        reason = string.Empty;
        return true;
    }

    private static List<(double X, double Y)> UniqueXY(IEnumerable<(double X, double Y)> points, ToleranceContext tolerance)
    {
        var unique = new List<(double X, double Y)>();
        foreach (var point in points)
        {
            if (!ContainsXY(unique, point, tolerance))
            {
                unique.Add(point);
            }
        }

        return unique;
    }

    private static bool ContainsXY(IEnumerable<(double X, double Y)> points, (double X, double Y) candidate, ToleranceContext tolerance)
        => points.Any(existing =>
            ToleranceMath.AlmostEqual(existing.X, candidate.X, tolerance)
            && ToleranceMath.AlmostEqual(existing.Y, candidate.Y, tolerance));

    private static IReadOnlyList<(double X, double Y)> OrderLoop(IReadOnlyList<(double X, double Y)> points)
    {
        var centroidX = points.Average(p => p.X);
        var centroidY = points.Average(p => p.Y);
        return points
            .OrderBy(point => double.Atan2(point.Y - centroidY, point.X - centroidX))
            .ToArray();
    }
}
