using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

internal enum PlanarDisplayLoopRole
{
    Outer,
    Hole,
    Island,
    Degenerate,
    UnsupportedNested,
    Unknown
}

internal enum PlanarDisplayLoopOrientation
{
    Clockwise,
    CounterClockwise,
    Degenerate
}

internal readonly record struct PlanarDisplayPoint2D(double X, double Y);
internal readonly record struct PlanarDisplayBoundingBox(double MinX, double MinY, double MaxX, double MaxY);

internal sealed record PlanarLoop2D(
    int LoopId,
    int SourceFaceId,
    IReadOnlyList<PlanarDisplayPoint2D> Points2D,
    IReadOnlyList<Point3D> Points3D,
    double SignedArea,
    PlanarDisplayLoopOrientation Orientation,
    PlanarDisplayBoundingBox BoundingBox,
    PlanarDisplayLoopRole RoleCandidate,
    IReadOnlyList<string> Diagnostics);

internal sealed record PlanarLoopClassification(
    PlanarLoop2D Loop,
    PlanarDisplayLoopRole Role,
    IReadOnlyList<string> Reasons,
    int ContainmentDepth);

internal sealed class PlanarDisplayLoopClassifier
{
    private const double Epsilon = 1e-9d;

    public IReadOnlyList<PlanarLoopClassification> Classify(int sourceFaceId, IReadOnlyList<(int LoopId, IReadOnlyList<Point3D> Points)> loops)
    {
        var descriptors = loops
            .OrderBy(loop => loop.LoopId)
            .Select(loop => Describe(sourceFaceId, loop.LoopId, loop.Points))
            .ToArray();

        if (descriptors.Length == 0)
        {
            return Array.Empty<PlanarLoopClassification>();
        }

        var largestAreaLoopId = descriptors
            .Where(loop => loop.RoleCandidate != PlanarDisplayLoopRole.Degenerate)
            .OrderByDescending(loop => double.Abs(loop.SignedArea))
            .ThenBy(loop => loop.LoopId)
            .Select(loop => (int?)loop.LoopId)
            .FirstOrDefault();

        return descriptors.Select(loop =>
        {
            var reasons = new List<string>();
            if (loop.RoleCandidate == PlanarDisplayLoopRole.Degenerate)
            {
                reasons.AddRange(loop.Diagnostics);
                return new PlanarLoopClassification(loop, PlanarDisplayLoopRole.Degenerate, reasons, 0);
            }

            var depth = descriptors.Count(other =>
                other.LoopId != loop.LoopId
                && other.RoleCandidate != PlanarDisplayLoopRole.Degenerate
                && System.Math.Abs(other.SignedArea) > System.Math.Abs(loop.SignedArea) + Epsilon
                && ContainsPoint(other.Points2D, loop.Points2D[0]));

            if (largestAreaLoopId == loop.LoopId)
            {
                reasons.Add("largest-absolute-area");
                reasons.Add("containment-depth-even");
                return new PlanarLoopClassification(loop, PlanarDisplayLoopRole.Outer, reasons, depth);
            }

            if (depth == 1)
            {
                reasons.Add("containment-depth-odd");
                if (loop.Orientation == PlanarDisplayLoopOrientation.Clockwise)
                {
                    reasons.Add("orientation-matches-hole");
                }

                return new PlanarLoopClassification(loop, PlanarDisplayLoopRole.Hole, reasons, depth);
            }

            if (depth > 1)
            {
                reasons.Add(depth % 2 == 0 ? "containment-depth-even" : "containment-depth-odd");
                reasons.Add("unsupported-nesting");
                return new PlanarLoopClassification(loop, depth % 2 == 0 ? PlanarDisplayLoopRole.Island : PlanarDisplayLoopRole.UnsupportedNested, reasons, depth);
            }

            reasons.Add("outside-primary-loop");
            return new PlanarLoopClassification(loop, PlanarDisplayLoopRole.Unknown, reasons, depth);
        }).ToArray();
    }

    private static PlanarLoop2D Describe(int sourceFaceId, int loopId, IReadOnlyList<Point3D> points)
    {
        var diagnostics = new List<string>();
        if (points.Count < 3)
        {
            diagnostics.Add("degenerate-area");
            return new PlanarLoop2D(loopId, sourceFaceId, Array.Empty<PlanarDisplayPoint2D>(), points, 0d, PlanarDisplayLoopOrientation.Degenerate, default, PlanarDisplayLoopRole.Degenerate, diagnostics);
        }

        var points2D = Project(points);
        var area = SignedArea(points2D);
        for (var i = 0; i < points2D.Count; i++)
        {
            var next = points2D[(i + 1) % points2D.Count];
            var dx = points2D[i].X - next.X;
            var dy = points2D[i].Y - next.Y;
            if ((dx * dx) + (dy * dy) <= Epsilon * Epsilon)
            {
                diagnostics.Add("duplicate-point-collapse");
                break;
            }
        }

        if (System.Math.Abs(area) <= Epsilon)
        {
            diagnostics.Add("degenerate-area");
        }

        if (HasSelfIntersection(points2D))
        {
            diagnostics.Add("self-intersection");
        }

        var role = diagnostics.Count == 0 ? PlanarDisplayLoopRole.Unknown : PlanarDisplayLoopRole.Degenerate;
        var orientation = area > Epsilon ? PlanarDisplayLoopOrientation.CounterClockwise : area < -Epsilon ? PlanarDisplayLoopOrientation.Clockwise : PlanarDisplayLoopOrientation.Degenerate;
        return new PlanarLoop2D(loopId, sourceFaceId, points2D, points, area, orientation, Bounds(points2D), role, diagnostics);
    }

    private static PlanarDisplayBoundingBox Bounds(IReadOnlyList<PlanarDisplayPoint2D> points) => new(points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));

    private static IReadOnlyList<PlanarDisplayPoint2D> Project(IReadOnlyList<Point3D> points)
    {
        var normal = NewellNormal(points);
        var ax = System.Math.Abs(normal.X);
        var ay = System.Math.Abs(normal.Y);
        var az = System.Math.Abs(normal.Z);
        return points.Select(point =>
            az >= ax && az >= ay ? new PlanarDisplayPoint2D(point.X, point.Y) :
            ay >= ax ? new PlanarDisplayPoint2D(point.X, point.Z) :
            new PlanarDisplayPoint2D(point.Y, point.Z)).ToArray();
    }

    private static Point3D NewellNormal(IReadOnlyList<Point3D> points)
    {
        var x = 0d; var y = 0d; var z = 0d;
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            x += (current.Y - next.Y) * (current.Z + next.Z);
            y += (current.Z - next.Z) * (current.X + next.X);
            z += (current.X - next.X) * (current.Y + next.Y);
        }
        return new Point3D(x, y, z);
    }

    private static double SignedArea(IReadOnlyList<PlanarDisplayPoint2D> points)
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

    private static bool ContainsPoint(IReadOnlyList<PlanarDisplayPoint2D> polygon, PlanarDisplayPoint2D point)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];
            if (((current.Y > point.Y) != (next.Y > point.Y))
                && point.X < (((next.X - current.X) * (point.Y - current.Y)) / (next.Y - current.Y + double.Epsilon)) + current.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool HasSelfIntersection(IReadOnlyList<PlanarDisplayPoint2D> points)
    {
        for (var i = 0; i < points.Count; i++)
        for (var j = i + 1; j < points.Count; j++)
        {
            if (i == j || (i + 1) % points.Count == j || i == (j + 1) % points.Count)
            {
                continue;
            }

            if (Intersects(points[i], points[(i + 1) % points.Count], points[j], points[(j + 1) % points.Count]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Intersects(PlanarDisplayPoint2D a, PlanarDisplayPoint2D b, PlanarDisplayPoint2D c, PlanarDisplayPoint2D d)
    {
        static double O(PlanarDisplayPoint2D p, PlanarDisplayPoint2D q, PlanarDisplayPoint2D r) => ((q.X - p.X) * (r.Y - p.Y)) - ((q.Y - p.Y) * (r.X - p.X));
        var o1 = O(a, b, c); var o2 = O(a, b, d); var o3 = O(c, d, a); var o4 = O(c, d, b);
        return o1 * o2 < -Epsilon && o3 * o4 < -Epsilon;
    }
}
