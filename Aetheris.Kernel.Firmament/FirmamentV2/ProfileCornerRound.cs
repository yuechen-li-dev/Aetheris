using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// Rounds the junction where one traced line or circular arc ends and the next begins: a tangent arc of the given
/// radius replaces the corner, and both neighbours are trimmed back to their tangent points. The arc lies on the
/// side the path turns towards, so a convex corner is rounded off and a concave one is filled in.
/// </summary>
internal static class ProfileCornerRound
{
    private const double Tolerance = 1e-9;

    internal sealed record Result(LineArcProfileCurve2D Previous, LineArcCircularArc2D Arc, LineArcProfileCurve2D Next);

    internal static Result? TryRound(LineArcProfileCurve2D previous, LineArcProfileCurve2D next, double radius, out string? failure)
    {
        failure = null;
        if (!(radius > 0) || !double.IsFinite(radius)) { failure = "radius must be a positive length"; return null; }
        if (previous is not (LineArcLineSegment2D or LineArcCircularArc2D) || next is not (LineArcLineSegment2D or LineArcCircularArc2D))
        { failure = "only lines and circular arcs can be rounded"; return null; }

        var junction = End(previous);
        if (Distance(junction, Start(next)) > 1e-7) { failure = "the two stages do not meet"; return null; }
        var tangentIn = EndTangent(previous);
        var tangentOut = StartTangent(next);
        var turn = Cross(tangentIn, tangentOut);
        if (Math.Abs(turn) <= 1e-9 && Dot(tangentIn, tangentOut) > 0) { failure = "the stages already meet tangentially; there is no corner to round"; return null; }
        if (Math.Abs(turn) <= 1e-9) { failure = "the path reverses here; a round needs a corner"; return null; }
        var side = turn > 0 ? 1d : -1d; // +1: the path turns left, so the arc's centre is on the left of both stages.

        var centre = Centre(previous, next, radius, side, junction);
        if (centre is null) { failure = "no circle of this radius touches both stages near the corner"; return null; }
        var a = Foot(previous, centre.Value);
        var b = Foot(next, centre.Value);
        var trimmedPrevious = TrimEnd(previous, a);
        var trimmedNext = TrimStart(next, b);
        if (trimmedPrevious is null || trimmedNext is null)
        { failure = "the radius is too large: a tangent point falls outside a neighbouring stage"; return null; }

        var start = Math.Atan2(a.Y - centre.Value.Y, a.X - centre.Value.X);
        var end = Math.Atan2(b.Y - centre.Value.Y, b.X - centre.Value.X);
        var sweep = end - start;
        if (side > 0) { while (sweep <= 0) sweep += 2 * Math.PI; while (sweep > 2 * Math.PI) sweep -= 2 * Math.PI; }
        else { while (sweep >= 0) sweep -= 2 * Math.PI; while (sweep < -2 * Math.PI) sweep += 2 * Math.PI; }
        return new(trimmedPrevious, new LineArcCircularArc2D(centre.Value, radius, start, sweep), trimmedNext);
    }

    // The centre is r from both stages on the turning side: intersect each stage's offset (a parallel line or a
    // concentric circle) and keep the intersection nearest the corner.
    private static (double X, double Y)? Centre(LineArcProfileCurve2D previous, LineArcProfileCurve2D next, double r, double side, (double X, double Y) junction)
    {
        var candidates = Intersect(Offset(previous, r, side), Offset(next, r, side));
        return candidates.Count == 0 ? null : candidates.OrderBy(c => Distance(c, junction)).First();
    }

    private abstract record Carrier;
    private sealed record LineCarrier((double X, double Y) Point, (double X, double Y) Direction) : Carrier;
    private sealed record CircleCarrier((double X, double Y) Centre, double Radius) : Carrier;

    private static Carrier Offset(LineArcProfileCurve2D curve, double r, double side)
    {
        if (curve is LineArcLineSegment2D line)
        {
            var d = Normalize((line.End.X - line.Start.X, line.End.Y - line.Start.Y));
            var left = (X: -d.Y, Y: d.X);
            return new LineCarrier((line.Start.X + left.X * r * side, line.Start.Y + left.Y * r * side), d);
        }

        var arc = (LineArcCircularArc2D)curve;
        // Travelling counter-clockwise the centre is on the left; the offset towards the turning side shrinks
        // the circle when that side faces the centre and grows it otherwise.
        var towardsCentre = arc.SweepAngleRadians > 0 ? side > 0 : side < 0;
        return new CircleCarrier(arc.Center, towardsCentre ? arc.Radius - r : arc.Radius + r);
    }

    private static List<(double X, double Y)> Intersect(Carrier a, Carrier b)
    {
        switch (a, b)
        {
            case (LineCarrier p, LineCarrier q):
            {
                var denominator = Cross(p.Direction, q.Direction);
                if (Math.Abs(denominator) <= 1e-12) return [];
                var t = Cross((q.Point.X - p.Point.X, q.Point.Y - p.Point.Y), q.Direction) / denominator;
                return [(p.Point.X + p.Direction.X * t, p.Point.Y + p.Direction.Y * t)];
            }
            case (LineCarrier l, CircleCarrier c): return LineCircle(l, c);
            case (CircleCarrier c, LineCarrier l): return LineCircle(l, c);
            case (CircleCarrier p, CircleCarrier q):
            {
                if (p.Radius <= 0 || q.Radius <= 0) return [];
                var dx = q.Centre.X - p.Centre.X; var dy = q.Centre.Y - p.Centre.Y; var d = Math.Sqrt(dx * dx + dy * dy);
                if (d <= 1e-12 || d > p.Radius + q.Radius + 1e-12 || d < Math.Abs(p.Radius - q.Radius) - 1e-12) return [];
                var along = (p.Radius * p.Radius - q.Radius * q.Radius + d * d) / (2 * d);
                var h = Math.Sqrt(Math.Max(0, p.Radius * p.Radius - along * along));
                var mid = (X: p.Centre.X + dx * along / d, Y: p.Centre.Y + dy * along / d);
                return [(mid.X - dy * h / d, mid.Y + dx * h / d), (mid.X + dy * h / d, mid.Y - dx * h / d)];
            }
        }

        return [];
    }

    private static List<(double X, double Y)> LineCircle(LineCarrier line, CircleCarrier circle)
    {
        if (circle.Radius <= 0) return [];
        var f = (X: line.Point.X - circle.Centre.X, Y: line.Point.Y - circle.Centre.Y);
        var b = Dot(f, line.Direction);
        var c = Dot(f, f) - circle.Radius * circle.Radius;
        var discriminant = b * b - c;
        if (discriminant < -1e-12) return [];
        var root = Math.Sqrt(Math.Max(0, discriminant));
        return [(line.Point.X + line.Direction.X * (-b - root), line.Point.Y + line.Direction.Y * (-b - root)),
                (line.Point.X + line.Direction.X * (-b + root), line.Point.Y + line.Direction.Y * (-b + root))];
    }

    private static (double X, double Y) Foot(LineArcProfileCurve2D curve, (double X, double Y) point)
    {
        if (curve is LineArcLineSegment2D line)
        {
            var d = Normalize((line.End.X - line.Start.X, line.End.Y - line.Start.Y));
            var t = Dot((point.X - line.Start.X, point.Y - line.Start.Y), d);
            return (line.Start.X + d.X * t, line.Start.Y + d.Y * t);
        }

        var arc = (LineArcCircularArc2D)curve;
        var v = Normalize((point.X - arc.Center.X, point.Y - arc.Center.Y));
        return (arc.Center.X + v.X * arc.Radius, arc.Center.Y + v.Y * arc.Radius);
    }

    // Keeps the part of the curve from its start to the tangent point; null when the point is not on the curve.
    private static LineArcProfileCurve2D? TrimEnd(LineArcProfileCurve2D curve, (double X, double Y) point)
    {
        if (curve is LineArcLineSegment2D line)
        {
            var length = Distance(line.Start, line.End);
            var t = Dot((point.X - line.Start.X, point.Y - line.Start.Y), Normalize((line.End.X - line.Start.X, line.End.Y - line.Start.Y)));
            return t > Tolerance && t <= length + Tolerance ? new LineArcLineSegment2D(line.Start, point) : null;
        }

        var arc = (LineArcCircularArc2D)curve;
        var swept = SweptTo(arc, point);
        return swept is { } s && Math.Abs(s) > Tolerance ? arc with { SweepAngleRadians = s } : null;
    }

    // Keeps the part of the curve from the tangent point to its end; null when the point is not on the curve.
    private static LineArcProfileCurve2D? TrimStart(LineArcProfileCurve2D curve, (double X, double Y) point)
    {
        if (curve is LineArcLineSegment2D line)
        {
            var length = Distance(line.Start, line.End);
            var t = Dot((point.X - line.Start.X, point.Y - line.Start.Y), Normalize((line.End.X - line.Start.X, line.End.Y - line.Start.Y)));
            return t >= -Tolerance && t < length - Tolerance ? new LineArcLineSegment2D(point, line.End) : null;
        }

        var arc = (LineArcCircularArc2D)curve;
        var swept = SweptTo(arc, point);
        if (swept is not { } s || Math.Abs(arc.SweepAngleRadians - s) <= Tolerance) return null;
        return new LineArcCircularArc2D(arc.Center, arc.Radius, arc.StartAngleRadians + s, arc.SweepAngleRadians - s);
    }

    // Signed sweep from the arc's start to the point, within the arc; null when the point is outside it.
    private static double? SweptTo(LineArcCircularArc2D arc, (double X, double Y) point)
    {
        var angle = Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X) - arc.StartAngleRadians;
        var direction = Math.Sign(arc.SweepAngleRadians);
        while (angle * direction < -Tolerance) angle += 2 * Math.PI * direction;
        while (angle * direction > 2 * Math.PI) angle -= 2 * Math.PI * direction;
        return angle * direction <= Math.Abs(arc.SweepAngleRadians) + Tolerance ? angle : null;
    }

    private static (double X, double Y) StartTangent(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => Normalize((line.End.X - line.Start.X, line.End.Y - line.Start.Y)),
        LineArcCircularArc2D arc => ArcTangent(arc, arc.StartAngleRadians),
        _ => throw new InvalidOperationException()
    };

    private static (double X, double Y) EndTangent(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => Normalize((line.End.X - line.Start.X, line.End.Y - line.Start.Y)),
        LineArcCircularArc2D arc => ArcTangent(arc, arc.StartAngleRadians + arc.SweepAngleRadians),
        _ => throw new InvalidOperationException()
    };

    private static (double X, double Y) ArcTangent(LineArcCircularArc2D arc, double angle)
    {
        var s = Math.Sign(arc.SweepAngleRadians);
        return (-Math.Sin(angle) * s, Math.Cos(angle) * s);
    }

    private static (double X, double Y) Start(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => line.Start,
        LineArcCircularArc2D arc => (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)),
        _ => throw new InvalidOperationException()
    };

    private static (double X, double Y) End(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => line.End,
        LineArcCircularArc2D arc => (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians)),
        _ => throw new InvalidOperationException()
    };

    private static double Cross((double X, double Y) a, (double X, double Y) b) => a.X * b.Y - a.Y * b.X;
    private static double Dot((double X, double Y) a, (double X, double Y) b) => a.X * b.X + a.Y * b.Y;
    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static (double X, double Y) Normalize((double X, double Y) v) { var l = Math.Sqrt(v.X * v.X + v.Y * v.Y); return (v.X / l, v.Y / l); }
}
