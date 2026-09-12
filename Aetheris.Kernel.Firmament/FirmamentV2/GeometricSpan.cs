using Aetheris.Kernel.Firmament.Materializer;
using System.Text.Json.Serialization;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// A named, non-owning bounded view of an existing geometric guide.  This is deliberately
/// closed over Firmament's current planar guide families; it is not a general generic value.
/// Geometry is evaluated from <see cref="ParentId"/> and the authored constraints each time.
/// </summary>
public sealed record GeometricSpanView(
    string SpanId,
    string SpanType,
    string ParentId,
    string ParentType,
    string Domain,
    string Orientation,
    string? From,
    string? To,
    string? BoundaryProfile,
    double? Length,
    string Provenance,
    [property: JsonIgnore] LineArcProfileCurve2D? Geometry = null,
    double? Area = null,
    double[]? Normal = null,
    string? LocalFrame = null,
    IReadOnlyList<string>? ConsumerReferences = null,
    string Validity = "Valid",
    [property: JsonIgnore] ResolvedProfile2D? Boundary = null,
    [property: JsonIgnore] ConstructionPlane? ParentPlane = null);

public sealed record GeometricSpanInspection(
    IReadOnlyList<GeometricSpanView> Spans,
    IReadOnlyList<string> Diagnostics);

/// <summary>Deterministic support-domain predicates over the existing Profile authority.</summary>
public static class PlanarSpanContainment
{
    // Points on the authored boundary are valid support points. Footprints require their
    // complete closed disk to remain inside or on the boundary within kernel tolerance.
    public const double Tolerance = 1e-7d;

    public static bool ContainsPoint(GeometricSpanView span, double x, double y)
    {
        if (span.Boundary is null) return false;
        return ProfileArrangementBuilder.PointInProfile(span.Boundary, (x, y))
            is ArrangementPointLocation.Inside or ArrangementPointLocation.OnBoundary;
    }

    public static bool ContainsCircle(GeometricSpanView span, double x, double y, double radius, out double boundaryMargin)
    {
        boundaryMargin = double.NaN;
        if (span.Boundary is null || !double.IsFinite(radius) || radius < 0d || !ContainsPoint(span, x, y)) return false;
        var distances = span.Boundary.Loops.SelectMany(loop => loop.Segments)
            .Select(segment => DistanceToCurve((x, y), segment.Geometry)).ToArray();
        if (distances.Length == 0 || distances.Any(distance => !double.IsFinite(distance))) return false;
        boundaryMargin = distances.Min() - radius;
        return boundaryMargin >= -Tolerance;
    }

    private static double DistanceToCurve((double X, double Y) point, LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => DistanceToSegment(point, line.Start, line.End),
        LineArcCircularArc2D arc => DistanceToArc(point, arc),
        LineArcFullCircle2D circle => Math.Abs(Distance(point, circle.Center) - circle.Radius),
        _ => double.NaN
    };

    private static double DistanceToSegment((double X, double Y) point, (double X, double Y) start, (double X, double Y) end)
    {
        var dx = end.X - start.X; var dy = end.Y - start.Y; var extent = dx * dx + dy * dy;
        if (extent <= Tolerance * Tolerance) return Distance(point, start);
        var t = Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / extent, 0d, 1d);
        return Distance(point, (start.X + t * dx, start.Y + t * dy));
    }

    private static double DistanceToArc((double X, double Y) point, LineArcCircularArc2D arc)
    {
        var angle = Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X);
        var delta = angle - arc.StartAngleRadians;
        if (arc.SweepAngleRadians >= 0d) while (delta < 0d) delta += 2d * Math.PI; else while (delta > 0d) delta -= 2d * Math.PI;
        if (delta / arc.SweepAngleRadians is >= -Tolerance and <= 1d + Tolerance)
            return Math.Abs(Distance(point, arc.Center) - arc.Radius);
        var a = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians));
        var b = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians));
        return Math.Min(Distance(point, a), Distance(point, b));
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
