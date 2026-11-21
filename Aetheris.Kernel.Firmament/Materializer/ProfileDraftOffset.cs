namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Uniform side draft of a profile extrusion. The section at local depth z is the profile region eroded by
/// (z − <see cref="NeutralLocalDepth"/>) · tan(<see cref="AngleRadians"/>): a positive angle narrows the solid
/// toward larger depth, a negative one widens it. Outer walls and hole walls tilt together, so every side face
/// releases along +local Z for a positive angle.
/// </summary>
public sealed record ProfileExtrusionDraft(double AngleRadians, double NeutralLocalDepth)
{
    public double ErosionAt(double localDepth) => (localDepth - NeutralLocalDepth) * Math.Tan(AngleRadians);
}

/// <summary>
/// Exact offset of a line/arc profile. Eroding the region moves each curve toward its material side, which is
/// the left of travel for a counterclockwise outer loop or a clockwise hole and the right otherwise (traced
/// guides do not promise a hole's winding): lines shift along their normal, arcs keep their centre and change
/// radius. A tangent junction moves along its shared
/// normal and stays tangent; a corner moves to the intersection of its two offset curves. Ellipses and cubic
/// Béziers have no exact offset in this representation and are refused.
/// </summary>
public static class ProfileDraftOffset
{
    private const double Tol = 1e-7;

    public static bool TryOffset(IReadOnlyList<LineArcProfileLoop2D> loops, double erosion, out IReadOnlyList<LineArcProfileLoop2D> offset, out string? error)
    {
        offset = loops; error = null;
        if (erosion == 0d) return true;
        var result = new List<LineArcProfileLoop2D>(loops.Count);
        foreach (var loop in loops)
        {
            if (!TryOffsetLoop(loop, erosion, out var moved, out error)) { offset = loops; return false; }
            result.Add(moved);
        }
        if (Collides(result, out error)) { offset = loops; return false; }
        offset = result;
        return true;
    }

    private static bool TryOffsetLoop(LineArcProfileLoop2D loop, double d, out LineArcProfileLoop2D moved, out string? error)
    {
        moved = loop; error = null;
        if (loop.Curves.Count == 1 && loop.Curves[0] is LineArcFullCircle2D circle)
        {
            // A lone circle's travel is implied by its role: an outer circle shrinks, a hole grows.
            var radius = loop.IsHole ? circle.Radius + d : circle.Radius - d;
            if (radius <= Tol) { error = $"draft-offset-collapse: the circle of radius {circle.Radius:0.###} mm closes at this depth"; return false; }
            moved = loop with { Curves = [circle with { Radius = radius }] };
            return true;
        }
        foreach (var curve in loop.Curves)
            if (curve is not (LineArcLineSegment2D or LineArcCircularArc2D))
            {
                error = $"draft-unsupported-curve: a drafted wall needs lines and circular arcs; {Describe(curve)} has no exact offset";
                return false;
            }

        // Offsets below are to the left of travel; a loop whose material lies on its right moves the other way.
        if (!MaterialOnLeft(loop)) d = -d;
        var count = loop.Curves.Count;
        var corners = new (double X, double Y)[count];
        var tangentAt = new bool[count];
        for (var i = 0; i < count; i++)
        {
            // Junction i joins the end of curve i to the start of curve i + 1.
            var a = loop.Curves[i]; var b = loop.Curves[(i + 1) % count];
            var p = End(a);
            var ta = TangentAtEnd(a); var tb = TangentAtStart(b);
            var tangent = Math.Abs(Cross(ta, tb)) <= 1e-9 && Dot(ta, tb) > 0d;
            tangentAt[i] = tangent;
            if (tangent) { var n = Left(ta); corners[i] = (p.X + n.X * d, p.Y + n.Y * d); continue; }
            if (!TryIntersect(Moved(a, d), Moved(b, d), p, out corners[i]))
            {
                error = $"draft-offset-corner: the corner at ({p.X:0.###}, {p.Y:0.###}) mm has no offset at this depth";
                return false;
            }
        }

        var curves = new LineArcProfileCurve2D[count];
        for (var i = 0; i < count; i++)
        {
            var start = corners[(i + count - 1) % count]; var end = corners[i];
            switch (loop.Curves[i])
            {
                case LineArcLineSegment2D line:
                {
                    var before = (X: line.End.X - line.Start.X, Y: line.End.Y - line.Start.Y);
                    var after = (X: end.X - start.X, Y: end.Y - start.Y);
                    if (Dot(before, after) <= 0d || Math.Sqrt(Dot(after, after)) <= Tol)
                    {
                        error = $"draft-offset-collapse: the line from ({line.Start.X:0.###}, {line.Start.Y:0.###}) mm vanishes at this depth";
                        return false;
                    }
                    curves[i] = new LineArcLineSegment2D(start, end);
                    break;
                }
                case LineArcCircularArc2D arc:
                {
                    var radius = arc.Radius - Math.Sign(arc.SweepAngleRadians) * d;
                    if (radius <= Tol) { error = $"draft-offset-collapse: the arc of radius {arc.Radius:0.###} mm closes at this depth"; return false; }
                    var startAngle = tangentAt[(i + count - 1) % count] ? arc.StartAngleRadians : Math.Atan2(start.Y - arc.Center.Y, start.X - arc.Center.X);
                    var endAngle = tangentAt[i] ? arc.StartAngleRadians + arc.SweepAngleRadians : Math.Atan2(end.Y - arc.Center.Y, end.X - arc.Center.X);
                    var sweep = tangentAt[(i + count - 1) % count] && tangentAt[i] ? arc.SweepAngleRadians : Unwrap(endAngle - startAngle, arc.SweepAngleRadians);
                    if (Math.Sign(sweep) != Math.Sign(arc.SweepAngleRadians) || Math.Abs(sweep) * radius <= Tol)
                    {
                        error = $"draft-offset-collapse: the arc of radius {arc.Radius:0.###} mm vanishes at this depth";
                        return false;
                    }
                    curves[i] = arc with { Radius = radius, StartAngleRadians = startAngle, SweepAngleRadians = sweep };
                    break;
                }
            }
        }
        moved = loop with { Curves = curves };
        return true;
    }

    /// <summary>Whether the region's material lies left of the loop's travel: a counterclockwise outer loop or a
    /// clockwise hole.</summary>
    public static bool MaterialOnLeft(LineArcProfileLoop2D loop)
        => loop.Curves.Sum(ResolvedProfile2DValidator.SignedAreaContribution) > 0d != loop.IsHole;

    /// <summary>A curve offset to its left, unbounded: a line as (point, direction), an arc as (centre, radius).</summary>
    private abstract record Carrier;
    private sealed record LineCarrier((double X, double Y) Point, (double X, double Y) Direction) : Carrier;
    private sealed record CircleCarrier((double X, double Y) Center, double Radius) : Carrier;

    private static Carrier Moved(LineArcProfileCurve2D curve, double d) => curve switch
    {
        LineArcLineSegment2D line => MovedLine(line, d),
        LineArcCircularArc2D arc => new CircleCarrier(arc.Center, arc.Radius - Math.Sign(arc.SweepAngleRadians) * d),
        _ => throw new InvalidOperationException("draft carrier")
    };

    private static LineCarrier MovedLine(LineArcLineSegment2D line, double d)
    {
        var t = Unit((line.End.X - line.Start.X, line.End.Y - line.Start.Y)); var n = Left(t);
        return new((line.Start.X + n.X * d, line.Start.Y + n.Y * d), t);
    }

    /// <summary>The intersection of two offset carriers nearest the original corner.</summary>
    private static bool TryIntersect(Carrier a, Carrier b, (double X, double Y) near, out (double X, double Y) point)
    {
        var candidates = new List<(double X, double Y)>();
        switch (a, b)
        {
            case (LineCarrier l, LineCarrier m):
            {
                var denominator = Cross(l.Direction, m.Direction);
                if (Math.Abs(denominator) <= 1e-12) break;
                var s = Cross((m.Point.X - l.Point.X, m.Point.Y - l.Point.Y), m.Direction) / denominator;
                candidates.Add((l.Point.X + l.Direction.X * s, l.Point.Y + l.Direction.Y * s));
                break;
            }
            case (LineCarrier l, CircleCarrier c): candidates.AddRange(LineCircle(l, c)); break;
            case (CircleCarrier c, LineCarrier l): candidates.AddRange(LineCircle(l, c)); break;
            case (CircleCarrier c, CircleCarrier e): candidates.AddRange(CircleCircle(c, e)); break;
        }
        point = default;
        if (candidates.Count == 0 || candidates.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))) return false;
        point = candidates.OrderBy(p => (p.X - near.X) * (p.X - near.X) + (p.Y - near.Y) * (p.Y - near.Y)).First();
        return true;
    }

    private static IEnumerable<(double X, double Y)> LineCircle(LineCarrier l, CircleCarrier c)
    {
        var f = (X: l.Point.X - c.Center.X, Y: l.Point.Y - c.Center.Y);
        var b = Dot(f, l.Direction); var q = Dot(f, f) - c.Radius * c.Radius;
        var discriminant = b * b - q;
        if (discriminant < 0d) yield break;
        var root = Math.Sqrt(discriminant);
        foreach (var s in new[] { -b - root, -b + root })
            yield return (l.Point.X + l.Direction.X * s, l.Point.Y + l.Direction.Y * s);
    }

    private static IEnumerable<(double X, double Y)> CircleCircle(CircleCarrier a, CircleCarrier b)
    {
        var dx = b.Center.X - a.Center.X; var dy = b.Center.Y - a.Center.Y; var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance <= 1e-12 || distance > a.Radius + b.Radius || distance < Math.Abs(a.Radius - b.Radius)) yield break;
        var along = (a.Radius * a.Radius - b.Radius * b.Radius + distance * distance) / (2 * distance);
        var h = Math.Sqrt(Math.Max(0d, a.Radius * a.Radius - along * along));
        var mx = a.Center.X + dx * along / distance; var my = a.Center.Y + dy * along / distance;
        yield return (mx + h * -dy / distance, my + h * dx / distance);
        yield return (mx - h * -dy / distance, my - h * dx / distance);
    }

    /// <summary>
    /// The offset loops must stay simple and apart: a narrow slot or a hole near the edge closes before its
    /// own curves do. Checked on a fine polyline of every curve (32 chords per arc), across all loops.
    /// </summary>
    private static bool Collides(IReadOnlyList<LineArcProfileLoop2D> loops, out string? error)
    {
        error = null;
        var chains = loops.Select(Polyline).ToArray();
        for (var a = 0; a < chains.Length; a++)
            for (var b = a; b < chains.Length; b++)
            {
                var p = chains[a]; var q = chains[b];
                for (var i = 0; i < p.Length; i++)
                    for (var j = a == b ? i + 1 : 0; j < q.Length; j++)
                    {
                        if (a == b && (j == i + 1 || (i == 0 && j == p.Length - 1))) continue;
                        if (!ProperlyCross(p[i].Start, p[i].End, q[j].Start, q[j].End)) continue;
                        error = a == b
                            ? "draft-offset-collapse: the profile closes on itself at this depth (a narrow neck or slot)"
                            : "draft-offset-collapse: two loops of the profile meet at this depth";
                        return true;
                    }
            }
        return false;
    }

    private static ((double X, double Y) Start, (double X, double Y) End)[] Polyline(LineArcProfileLoop2D loop)
    {
        var points = new List<(double X, double Y)>();
        foreach (var curve in loop.Curves)
            switch (curve)
            {
                case LineArcLineSegment2D line: points.Add(line.Start); break;
                case LineArcCircularArc2D arc:
                    for (var k = 0; k < 32; k++)
                    {
                        var angle = arc.StartAngleRadians + arc.SweepAngleRadians * k / 32d;
                        points.Add((arc.Center.X + arc.Radius * Math.Cos(angle), arc.Center.Y + arc.Radius * Math.Sin(angle)));
                    }
                    break;
                case LineArcFullCircle2D circle:
                    for (var k = 0; k < 64; k++)
                        points.Add((circle.Center.X + circle.Radius * Math.Cos(2 * Math.PI * k / 64d), circle.Center.Y + circle.Radius * Math.Sin(2 * Math.PI * k / 64d)));
                    break;
            }
        return points.Select((p, i) => (p, points[(i + 1) % points.Count])).ToArray();
    }

    private static bool ProperlyCross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
    {
        double O((double X, double Y) p, (double X, double Y) q, (double X, double Y) r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        return O(a, b, c) * O(a, b, d) < -1e-14 && O(c, d, a) * O(c, d, b) < -1e-14;
    }

    private static (double X, double Y) End(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => line.End,
        LineArcCircularArc2D arc => (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians)),
        _ => throw new InvalidOperationException("draft endpoint")
    };

    private static (double X, double Y) TangentAtStart(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => Unit((line.End.X - line.Start.X, line.End.Y - line.Start.Y)),
        LineArcCircularArc2D arc => ArcTangent(arc, arc.StartAngleRadians),
        _ => throw new InvalidOperationException("draft tangent")
    };

    private static (double X, double Y) TangentAtEnd(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => Unit((line.End.X - line.Start.X, line.End.Y - line.Start.Y)),
        LineArcCircularArc2D arc => ArcTangent(arc, arc.StartAngleRadians + arc.SweepAngleRadians),
        _ => throw new InvalidOperationException("draft tangent")
    };

    private static (double X, double Y) ArcTangent(LineArcCircularArc2D arc, double angle)
    {
        var s = Math.Sign(arc.SweepAngleRadians);
        return (-Math.Sin(angle) * s, Math.Cos(angle) * s);
    }

    private static double Unwrap(double sweep, double reference)
    {
        while (sweep - reference > Math.PI) sweep -= 2 * Math.PI;
        while (sweep - reference < -Math.PI) sweep += 2 * Math.PI;
        return sweep;
    }

    private static string Describe(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcFullEllipse2D => "an ellipse",
        LineArcCubicBezier2D => "a cubic Bézier",
        _ => curve.GetType().Name
    };

    private static (double X, double Y) Left((double X, double Y) t) => (-t.Y, t.X);
    private static (double X, double Y) Unit((double X, double Y) v) { var l = Math.Sqrt(v.X * v.X + v.Y * v.Y); return (v.X / l, v.Y / l); }
    private static double Dot((double X, double Y) a, (double X, double Y) b) => a.X * b.X + a.Y * b.Y;
    private static double Cross((double X, double Y) a, (double X, double Y) b) => a.X * b.Y - a.Y * b.X;
}
