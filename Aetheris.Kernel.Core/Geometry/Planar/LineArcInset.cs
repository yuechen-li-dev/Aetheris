namespace Aetheris.Kernel.Core.Geometry.Planar;

/// <summary>A planar span of a closed line/arc loop.</summary>
public abstract record PlanarSpan;
public sealed record PlanarLine((double X, double Y) Start, (double X, double Y) End) : PlanarSpan;
/// <summary>A circular arc; a positive sweep runs counter-clockwise.</summary>
public sealed record PlanarArc((double X, double Y) Center, double Radius, double StartAngle, double Sweep) : PlanarSpan;
public sealed record PlanarCircle((double X, double Y) Center, double Radius) : PlanarSpan;

/// <summary>
/// Exact inward offset of a closed line/arc loop: the one offset authority behind hollow section stacks and hollow
/// revolutions. Lines stay parallel lines; arcs stay concentric; a reflex corner rounds with the inset distance about
/// the corner, which keeps a wall constant; a convex arc no larger than the inset vanishes and its neighbours meet at
/// a sharp corner. A span that reverses, or neighbours that no longer meet, is refused. Crossings between spans that
/// are not neighbours are left to the caller, which owns the arrangement.
/// </summary>
public static class LineArcInset
{
    private const double Tol = 1e-9;

    private abstract record Carrier;
    private sealed record LineCarrier((double X, double Y) Point, (double X, double Y) Direction) : Carrier;
    private sealed record CircleCarrier((double X, double Y) Center, double Radius, bool CounterClockwise) : Carrier;

    private sealed class Element(Carrier carrier, (double X, double Y) start, (double X, double Y) end, double originalSweep)
    {
        public Carrier Carrier { get; } = carrier;
        public (double X, double Y) Start { get; set; } = start;
        public (double X, double Y) End { get; set; } = end;
        public double OriginalSweep { get; } = originalSweep;
    }

    /// <summary>Insets one closed loop; the material lies inside an outer loop and outside a hole loop.</summary>
    public static IReadOnlyList<PlanarSpan>? InsetLoop(IReadOnlyList<PlanarSpan> curves, double distance, bool outer,
        string context, List<string> diagnostics)
    {
        if (curves.Count == 1 && curves[0] is PlanarCircle circle)
        {
            var radius = outer ? circle.Radius - distance : circle.Radius + distance;
            if (radius <= Tol) { diagnostics.Add($"inset-wall-offset-collapse:{context}: a circle of radius {circle.Radius:R} closes at wall {distance:R}"); return null; }
            return [new PlanarCircle(circle.Center, radius)];
        }
        if (curves.Any(c => c is not (PlanarLine or PlanarArc)))
        {
            diagnostics.Add($"inset-offset-curve-unsupported:{context}: only lines and circular arcs have exact offsets of the same kind");
            return null;
        }
        // Material on the left: an outer loop runs counter-clockwise, a hole clockwise.
        var oriented = SignedArea(curves) > 0d == outer ? curves : Reverse(curves);
        var elements = new List<Element?>();
        foreach (var curve in oriented)
        {
            switch (curve)
            {
                case PlanarLine line:
                {
                    var d = Unit(Sub(line.End, line.Start));
                    var n = (-d.Y, d.X);
                    elements.Add(new Element(new LineCarrier(Add(line.Start, Mul(n, distance)), d),
                        Add(line.Start, Mul(n, distance)), Add(line.End, Mul(n, distance)), 0d));
                    break;
                }
                case PlanarArc arc:
                {
                    var ccw = arc.Sweep > 0d;
                    var radius = ccw ? arc.Radius - distance : arc.Radius + distance;
                    if (radius <= Tol) { elements.Add(null); break; }
                    var a0 = arc.StartAngle;
                    var a1 = a0 + arc.Sweep;
                    elements.Add(new Element(new CircleCarrier(arc.Center, radius, ccw),
                        Polar(arc.Center, radius, a0), Polar(arc.Center, radius, a1), arc.Sweep));
                    break;
                }
            }
        }
        var count = oriented.Count;
        var kept = Enumerable.Range(0, count).Where(i => elements[i] is not null).ToArray();
        if (kept.Length < 2) { diagnostics.Add($"inset-wall-offset-collapse:{context}: the loop closes at wall {distance:R}"); return null; }
        var result = new List<PlanarSpan>();
        var joins = new (double X, double Y)[kept.Length];
        var reflexArcs = new PlanarArc?[kept.Length];
        for (var k = 0; k < kept.Length; k++)
        {
            var i = kept[k];
            var j = kept[(k + 1) % kept.Length];
            var first = elements[i]!;
            var second = elements[j]!;
            var vanishedBetween = (j - i + count) % count != 1;
            var vertex = EndPoint(oriented[i]);
            if (!vanishedBetween)
            {
                var tout = Unit(EndTangent(oriented[i]));
                var tin = Unit(StartTangent(oriented[j]));
                var cross = tout.X * tin.Y - tout.Y * tin.X;
                var dot = tout.X * tin.X + tout.Y * tin.Y;
                if (double.Abs(cross) <= 1e-9 && dot > 0d)
                {
                    if (Distance(first.End, second.Start) > 1e-7) { diagnostics.Add($"inset-wall-offset-collapse:{context}: tangent curves part at the inset"); return null; }
                    joins[k] = first.End;
                    continue;
                }
                if (double.Abs(cross) <= 1e-9) { diagnostics.Add($"inset-wall-offset-collapse:{context}: the loop turns back on itself"); return null; }
                if (cross < 0d)
                {
                    // Reflex corner: the wall rounds about the corner, keeping its thickness.
                    var a0 = double.Atan2(first.End.Y - vertex.Y, first.End.X - vertex.X);
                    var a1 = double.Atan2(second.Start.Y - vertex.Y, second.Start.X - vertex.X);
                    var sweep = a1 - a0;
                    while (sweep >= 0d) sweep -= 2d * double.Pi;
                    while (sweep < -2d * double.Pi) sweep += 2d * double.Pi;
                    reflexArcs[k] = new PlanarArc(vertex, distance, a0, sweep);
                    joins[k] = (double.NaN, double.NaN);
                    continue;
                }
            }
            var expected = Mid(first.End, second.Start);
            if (!TryIntersect(first.Carrier, second.Carrier, expected, out var point))
            {
                diagnostics.Add($"inset-wall-offset-collapse:{context}: neighbouring walls no longer meet at the inset");
                return null;
            }
            joins[k] = point;
        }
        for (var k = 0; k < kept.Length; k++)
        {
            var element = elements[kept[k]]!;
            var previous = (k + kept.Length - 1) % kept.Length;
            var start = double.IsNaN(joins[previous].X) ? element.Start : joins[previous];
            var end = double.IsNaN(joins[k].X) ? element.End : joins[k];
            var curve = Trim(element, start, end);
            if (curve is null)
            {
                diagnostics.Add($"inset-wall-offset-collapse:{context}: curve {kept[k]} reverses at wall {distance:R}; the section is narrower than twice the wall there");
                return null;
            }
            result.Add(curve);
            if (reflexArcs[k] is { } arc) result.Add(arc);
        }
        return result;
    }

    private static PlanarSpan? Trim(Element element, (double X, double Y) start, (double X, double Y) end)
    {
        switch (element.Carrier)
        {
            case LineCarrier line:
                var along = (end.X - start.X) * line.Direction.X + (end.Y - start.Y) * line.Direction.Y;
                return along > 1e-9 ? new PlanarLine(start, end) : null;
            case CircleCarrier circle:
                var a0 = double.Atan2(start.Y - circle.Center.Y, start.X - circle.Center.X);
                var a1 = double.Atan2(end.Y - circle.Center.Y, end.X - circle.Center.X);
                var sweep = a1 - a0;
                // The trimmed span is the branch nearest the original one; a different sign means it reversed.
                var best = sweep;
                for (var k = -2; k <= 2; k++)
                    if (double.Abs(sweep + 2d * double.Pi * k - element.OriginalSweep) < double.Abs(best - element.OriginalSweep))
                        best = sweep + 2d * double.Pi * k;
                if (double.Sign(best) != double.Sign(element.OriginalSweep) || double.Abs(best) <= 1e-9) return null;
                return new PlanarArc(circle.Center, circle.Radius, a0, best);
            default:
                return null;
        }
    }

    private static bool TryIntersect(Carrier first, Carrier second, (double X, double Y) expected, out (double X, double Y) point)
    {
        var candidates = new List<(double X, double Y)>();
        switch (first, second)
        {
            case (LineCarrier a, LineCarrier b):
            {
                var den = a.Direction.X * b.Direction.Y - a.Direction.Y * b.Direction.X;
                if (double.Abs(den) > 1e-12)
                {
                    var w = Sub(b.Point, a.Point);
                    var s = (w.X * b.Direction.Y - w.Y * b.Direction.X) / den;
                    candidates.Add(Add(a.Point, Mul(a.Direction, s)));
                }
                break;
            }
            case (LineCarrier a, CircleCarrier c): candidates.AddRange(LineCircle(a, c)); break;
            case (CircleCarrier c, LineCarrier a): candidates.AddRange(LineCircle(a, c)); break;
            case (CircleCarrier a, CircleCarrier b): candidates.AddRange(CircleCircle(a, b)); break;
        }
        if (candidates.Count == 0) { point = default; return false; }
        point = candidates.MinBy(c => Distance(c, expected));
        return true;
    }

    private static IEnumerable<(double X, double Y)> LineCircle(LineCarrier line, CircleCarrier circle)
    {
        var w = Sub(line.Point, circle.Center);
        var b = w.X * line.Direction.X + w.Y * line.Direction.Y;
        var c = w.X * w.X + w.Y * w.Y - circle.Radius * circle.Radius;
        var disc = b * b - c;
        if (disc < -1e-12) yield break;
        var root = double.Sqrt(double.Max(0d, disc));
        yield return Add(line.Point, Mul(line.Direction, -b - root));
        yield return Add(line.Point, Mul(line.Direction, -b + root));
    }

    private static IEnumerable<(double X, double Y)> CircleCircle(CircleCarrier a, CircleCarrier b)
    {
        var d = Distance(a.Center, b.Center);
        if (d < 1e-12 || d > a.Radius + b.Radius + 1e-12 || d < double.Abs(a.Radius - b.Radius) - 1e-12) yield break;
        var x = (d * d + a.Radius * a.Radius - b.Radius * b.Radius) / (2d * d);
        var h = double.Sqrt(double.Max(0d, a.Radius * a.Radius - x * x));
        var u = Mul(Sub(b.Center, a.Center), 1d / d);
        var mid = Add(a.Center, Mul(u, x));
        yield return Add(mid, Mul((-u.Y, u.X), h));
        yield return Add(mid, Mul((u.Y, -u.X), h));
    }

    public static IReadOnlyList<PlanarSpan> Reverse(IReadOnlyList<PlanarSpan> curves) =>
        curves.Reverse().Select(c => c switch
        {
            PlanarLine line => (PlanarSpan)new PlanarLine(line.End, line.Start),
            PlanarArc arc => new PlanarArc(arc.Center, arc.Radius, arc.StartAngle + arc.Sweep, -arc.Sweep),
            _ => c
        }).ToArray();

    public static double SignedArea(IReadOnlyList<PlanarSpan> curves)
    {
        var sum = 0d;
        foreach (var curve in curves)
            sum += curve switch
            {
                PlanarLine line => line.Start.X * line.End.Y - line.End.X * line.Start.Y,
                PlanarArc arc => ArcDoubleArea(arc),
                PlanarCircle circle => 2d * double.Pi * circle.Radius * circle.Radius,
                _ => 0d
            };
        return sum / 2d;
    }

    private static double ArcDoubleArea(PlanarArc arc)
    {
        var a = arc.StartAngle; var b = a + arc.Sweep; var r = arc.Radius; var (x, y) = arc.Center;
        return x * r * (double.Sin(b) - double.Sin(a)) - y * r * (double.Cos(b) - double.Cos(a)) + r * r * (b - a);
    }

    public static (double X, double Y) StartPoint(PlanarSpan curve) => curve switch
    {
        PlanarLine line => line.Start,
        PlanarArc arc => Polar(arc.Center, arc.Radius, arc.StartAngle),
        PlanarCircle circle => (circle.Center.X + circle.Radius, circle.Center.Y),
        _ => throw new NotSupportedException()
    };
    public static (double X, double Y) EndPoint(PlanarSpan curve) => curve switch
    {
        PlanarLine line => line.End,
        PlanarArc arc => Polar(arc.Center, arc.Radius, arc.StartAngle + arc.Sweep),
        _ => throw new NotSupportedException()
    };
    private static (double X, double Y) StartTangent(PlanarSpan curve) => curve switch
    {
        PlanarLine line => Sub(line.End, line.Start),
        PlanarArc arc => ArcTangent(arc, arc.StartAngle),
        _ => throw new NotSupportedException()
    };
    private static (double X, double Y) EndTangent(PlanarSpan curve) => curve switch
    {
        PlanarLine line => Sub(line.End, line.Start),
        PlanarArc arc => ArcTangent(arc, arc.StartAngle + arc.Sweep),
        _ => throw new NotSupportedException()
    };
    private static (double X, double Y) ArcTangent(PlanarArc arc, double angle) =>
        Mul((-double.Sin(angle), double.Cos(angle)), double.Sign(arc.Sweep));

    private static (double X, double Y) Polar((double X, double Y) c, double r, double a) => (c.X + r * double.Cos(a), c.Y + r * double.Sin(a));
    private static (double X, double Y) Add((double X, double Y) a, (double X, double Y) b) => (a.X + b.X, a.Y + b.Y);
    private static (double X, double Y) Sub((double X, double Y) a, (double X, double Y) b) => (a.X - b.X, a.Y - b.Y);
    private static (double X, double Y) Mul((double X, double Y) a, double s) => (a.X * s, a.Y * s);
    private static (double X, double Y) Mid((double X, double Y) a, (double X, double Y) b) => ((a.X + b.X) / 2d, (a.Y + b.Y) / 2d);
    private static double Distance((double X, double Y) a, (double X, double Y) b) => double.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static (double X, double Y) Unit((double X, double Y) v) { var l = double.Sqrt(v.X * v.X + v.Y * v.Y); return (v.X / l, v.Y / l); }
}
