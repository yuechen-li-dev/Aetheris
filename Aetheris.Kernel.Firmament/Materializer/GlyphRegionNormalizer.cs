namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>A bounded non-zero-winding arrangement of one TrueType glyph's line and Bézier contours.</summary>
internal static class GlyphRegionNormalizer
{
    private const double GeometryTolerance = 1e-7;
    private const double VertexTolerance = 1e-6;
    private const double SideOffset = 1e-5;

    internal sealed record Region(IReadOnlyList<LineArcProfileCurve2D> Outer,
        IReadOnlyList<IReadOnlyList<LineArcProfileCurve2D>> Holes);
    internal sealed record Result(IReadOnlyList<Region> Regions, int Intersections,
        IReadOnlyList<string> Diagnostics);
    private sealed record Source(int Contour, int Segment, LineArcProfileCurve2D Curve);
    private sealed record Piece(double From, double To, (double X, double Y) Start, (double X, double Y) End);
    private sealed record Boundary(string Id, LineArcProfileCurve2D Curve);
    private sealed record BoundaryLoop(IReadOnlyList<LineArcProfileCurve2D> Curves, double Area);

    internal static bool Contains(ResolvedProfile2D profile, (double X, double Y) point) =>
        Winding(profile.Loops.SelectMany((loop, index) => loop.Segments.Select((segment, number) =>
            new Source(index, number, segment.Geometry))).ToArray(), point) != 0;

    internal static Result Normalize(IReadOnlyList<IReadOnlyList<LineArcProfileCurve2D>> contours)
    {
        var diagnostics = new List<string>();
        var source = contours.SelectMany((contour, index) => contour.Select((curve, segment) =>
            new Source(index, segment, curve))).ToArray();
        if (source.Any(s => s.Curve is not (LineArcLineSegment2D or LineArcCubicBezier2D)))
            return new([], 0, ["glyph-region-unsupported-curve"]);
        var pieces = source.Select(s => Flatten(s.Curve)).ToArray();
        var splits = source.Select(_ => new List<double> { 0, 1 }).ToArray();
        var intersectionKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < source.Length; i++)
        for (var j = i + 1; j < source.Length; j++)
        {
            if (!BoxesOverlap(source[i].Curve, source[j].Curve)) continue;
            foreach (var a in pieces[i]) foreach (var b in pieces[j])
            {
                if (!SegmentIntersection(a.Start, a.End, b.Start, b.End, out var u, out var v)) continue;
                var ta = a.From + (a.To - a.From) * u;
                var tb = b.From + (b.To - b.From) * v;
                if (!Refine(source[i].Curve, source[j].Curve, ref ta, ref tb)) continue;
                if (ta < -1e-8 || ta > 1 + 1e-8 || tb < -1e-8 || tb > 1 + 1e-8) continue;
                splits[i].Add(Math.Clamp(ta, 0, 1));
                splits[j].Add(Math.Clamp(tb, 0, 1));
                if (ta > 1e-8 && ta < 1 - 1e-8 && tb > 1e-8 && tb < 1 - 1e-8)
                    intersectionKeys.Add($"{i}:{j}:{Math.Round(ta / 1e-7)}:{Math.Round(tb / 1e-7)}");
            }
        }

        var boundary = new List<Boundary>();
        for (var i = 0; i < source.Length; i++)
        {
            var parameters = splits[i].Order().Aggregate(new List<double>(), (list, value) =>
            {
                if (list.Count == 0 || value - list[^1] > 1e-8) list.Add(value);
                return list;
            });
            for (var j = 0; j < parameters.Count - 1; j++)
            {
                var from = parameters[j]; var to = parameters[j + 1];
                if (to - from <= 1e-8) continue;
                var curve = Subcurve(source[i].Curve, from, to);
                if (Distance(At(curve, 0), At(curve, 1)) < GeometryTolerance) continue;
                var mid = At(curve, .5); var tangent = Tangent(curve, .5);
                var length = Math.Sqrt(tangent.X * tangent.X + tangent.Y * tangent.Y);
                if (length < GeometryTolerance) { diagnostics.Add("glyph-region-degenerate-tangent"); continue; }
                var left = (mid.X - tangent.Y * SideOffset / length, mid.Y + tangent.X * SideOffset / length);
                var right = (mid.X + tangent.Y * SideOffset / length, mid.Y - tangent.X * SideOffset / length);
                var leftFilled = Winding(source, left) != 0;
                var rightFilled = Winding(source, right) != 0;
                if (leftFilled == rightFilled) continue;
                boundary.Add(new($"{i}:{j}", leftFilled ? curve : Reverse(curve)));
            }
        }
        var distinct = boundary.GroupBy(b => GeometryKey(b.Curve), StringComparer.Ordinal)
            .Select(group => group.OrderBy(item => item.Id, StringComparer.Ordinal).First())
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var incoming = distinct.GroupBy(b => Key(At(b.Curve, 1))).ToDictionary(g => g.Key, g => g.Count());
        var outgoing = distinct.GroupBy(b => Key(At(b.Curve, 0))).ToDictionary(g => g.Key, g => g.ToArray());
        foreach (var vertex in incoming.Keys.Concat(outgoing.Keys).Distinct())
            if (incoming.GetValueOrDefault(vertex) != 1 || outgoing.GetValueOrDefault(vertex)?.Length != 1)
                diagnostics.Add($"glyph-region-ambiguous-vertex:{vertex}");
        if (diagnostics.Count > 0) return new([], intersectionKeys.Count, diagnostics.Distinct().ToArray());

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var loops = new List<BoundaryLoop>();
        foreach (var seed in distinct)
        {
            if (!visited.Add(seed.Id)) continue;
            var firstKey = Key(At(seed.Curve, 0));
            var current = seed;
            var curves = new List<LineArcProfileCurve2D> { seed.Curve };
            while (Key(At(current.Curve, 1)) != firstKey)
            {
                var nextKey = Key(At(current.Curve, 1));
                if (!outgoing.TryGetValue(nextKey, out var options) || options.Length != 1 ||
                    !visited.Add(options[0].Id))
                {
                    diagnostics.Add($"glyph-region-open-boundary:{nextKey}"); break;
                }
                current = options[0]; curves.Add(current.Curve);
            }
            if (diagnostics.Count > 0) break;
            var area = curves.Sum(ResolvedProfile2DValidator.SignedAreaContribution);
            if (Math.Abs(area) < GeometryTolerance * GeometryTolerance)
                diagnostics.Add("glyph-region-zero-area-loop");
            else loops.Add(new(curves, area));
        }
        if (diagnostics.Count > 0) return new([], intersectionKeys.Count, diagnostics.Distinct().ToArray());
        var outers = loops.Where(l => l.Area > 0).ToArray();
        var holes = loops.Where(l => l.Area < 0).ToArray();
        var regions = new List<Region>();
        foreach (var outer in outers)
        {
            var nested = holes.Where(h => Winding(outer.Curves.Select(c => new Source(0, 0, c)).ToArray(),
                At(h.Curves[0], 0)) != 0).ToArray();
            regions.Add(new(outer.Curves, nested.Select(h => h.Curves).ToArray()));
        }
        foreach (var hole in holes)
            if (!regions.Any(r => r.Holes.Any(h => ReferenceEquals(h, hole.Curves))))
                diagnostics.Add("glyph-region-uncontained-counter");
        return diagnostics.Count == 0
            ? new(regions, intersectionKeys.Count, [])
            : new([], intersectionKeys.Count, diagnostics.Distinct().ToArray());
    }

    private static IReadOnlyList<Piece> Flatten(LineArcProfileCurve2D curve)
    {
        if (curve is LineArcLineSegment2D line) return [new(0, 1, line.Start, line.End)];
        var result = new List<Piece>();
        void Visit(LineArcCubicBezier2D cubic, double from, double to, int depth)
        {
            var chord = Distance(cubic.Start, cubic.End);
            var flat = chord < GeometryTolerance
                ? Math.Max(Distance(cubic.Control1, cubic.Start), Distance(cubic.Control2, cubic.Start))
                : Math.Max(PointLineDistance(cubic.Control1, cubic.Start, cubic.End),
                    PointLineDistance(cubic.Control2, cubic.Start, cubic.End));
            if (flat <= GeometryTolerance / 4 || depth >= 18)
            {
                result.Add(new(from, to, cubic.Start, cubic.End)); return;
            }
            var (left, right) = Divide(cubic, .5);
            var mid = (from + to) / 2;
            Visit(left, from, mid, depth + 1); Visit(right, mid, to, depth + 1);
        }
        Visit((LineArcCubicBezier2D)curve, 0, 1, 0);
        return result;
    }

    private static bool BoxesOverlap(LineArcProfileCurve2D a, LineArcProfileCurve2D b)
    {
        static (double MinX, double MaxX, double MinY, double MaxY) Bounds(LineArcProfileCurve2D c)
        {
            var points = c is LineArcLineSegment2D l ? new[] { l.Start, l.End } :
                c is LineArcCubicBezier2D q ? new[] { q.Start, q.Control1, q.Control2, q.End } : [];
            return (points.Min(p => p.X), points.Max(p => p.X), points.Min(p => p.Y), points.Max(p => p.Y));
        }
        var x = Bounds(a); var y = Bounds(b);
        return x.MinX <= y.MaxX + GeometryTolerance && y.MinX <= x.MaxX + GeometryTolerance &&
               x.MinY <= y.MaxY + GeometryTolerance && y.MinY <= x.MaxY + GeometryTolerance;
    }

    private static bool SegmentIntersection((double X, double Y) a, (double X, double Y) b,
        (double X, double Y) c, (double X, double Y) d, out double u, out double v)
    {
        var r = (b.X - a.X, b.Y - a.Y); var s = (d.X - c.X, d.Y - c.Y);
        var denominator = Cross(r, s);
        if (Math.Abs(denominator) < 1e-14) { u = v = 0; return false; }
        var delta = (c.X - a.X, c.Y - a.Y);
        u = Cross(delta, s) / denominator; v = Cross(delta, r) / denominator;
        return u >= -1e-7 && u <= 1 + 1e-7 && v >= -1e-7 && v <= 1 + 1e-7;
    }

    private static bool Refine(LineArcProfileCurve2D a, LineArcProfileCurve2D b, ref double ta, ref double tb)
    {
        for (var i = 0; i < 12; i++)
        {
            var pa = At(a, ta); var pb = At(b, tb);
            var da = Tangent(a, ta); var db = Tangent(b, tb);
            var det = Cross(da, db);
            if (Math.Abs(det) < 1e-14) break;
            var delta = (pb.X - pa.X, pb.Y - pa.Y);
            ta += Cross(delta, db) / det;
            tb += Cross(delta, da) / det;
            if (ta < -.01 || ta > 1.01 || tb < -.01 || tb > 1.01) return false;
            if (Distance(At(a, ta), At(b, tb)) < 1e-10) break;
        }
        return Distance(At(a, ta), At(b, tb)) < GeometryTolerance / 4;
    }

    private static int Winding(IReadOnlyList<Source> source, (double X, double Y) point)
    {
        var count = 0;
        foreach (var item in source)
        {
            if (item.Curve is LineArcLineSegment2D line)
            {
                if ((line.Start.Y <= point.Y && point.Y < line.End.Y) ||
                    (line.End.Y <= point.Y && point.Y < line.Start.Y))
                {
                    var x = line.Start.X + (point.Y - line.Start.Y) *
                        (line.End.X - line.Start.X) / (line.End.Y - line.Start.Y);
                    if (x > point.X) count += Math.Sign(line.End.Y - line.Start.Y);
                }
                continue;
            }
            var cubic = (LineArcCubicBezier2D)item.Curve;
            var a = -cubic.Start.Y + 3 * cubic.Control1.Y - 3 * cubic.Control2.Y + cubic.End.Y;
            var b = 3 * cubic.Start.Y - 6 * cubic.Control1.Y + 3 * cubic.Control2.Y;
            var c = -3 * cubic.Start.Y + 3 * cubic.Control1.Y;
            var intervals = new List<double> { 0, 1 };
            if (Math.Abs(a) < 1e-14)
            {
                if (Math.Abs(b) > 1e-14) intervals.Add(-c / (2 * b));
            }
            else
            {
                var discriminant = b * b - 3 * a * c;
                if (discriminant >= 0)
                {
                    var root = Math.Sqrt(discriminant);
                    intervals.Add((-b - root) / (3 * a)); intervals.Add((-b + root) / (3 * a));
                }
            }
            var ordered = intervals.Where(t => t >= 0 && t <= 1).Distinct().Order().ToArray();
            for (var k = 0; k < ordered.Length - 1; k++)
            {
                var lo = ordered[k]; var hi = ordered[k + 1];
                var y0 = At(cubic, lo).Y; var y1 = At(cubic, hi).Y;
                if (Math.Abs(y1 - y0) < 1e-14 ||
                    point.Y < Math.Min(y0, y1) || point.Y >= Math.Max(y0, y1)) continue;
                for (var iteration = 0; iteration < 48; iteration++)
                {
                    var mid = (lo + hi) / 2;
                    if ((At(cubic, mid).Y < point.Y) == (y1 > y0)) lo = mid;
                    else hi = mid;
                }
                if (At(cubic, (lo + hi) / 2).X > point.X) count += Math.Sign(y1 - y0);
            }
        }
        return count;
    }

    private static LineArcProfileCurve2D Subcurve(LineArcProfileCurve2D curve, double from, double to)
    {
        if (curve is LineArcLineSegment2D) return new LineArcLineSegment2D(At(curve, from), At(curve, to));
        var cubic = (LineArcCubicBezier2D)curve;
        if (from > 0) cubic = Divide(cubic, from).Right;
        if (to < 1) cubic = Divide(cubic, (to - from) / (1 - from)).Left;
        return cubic;
    }

    private static (LineArcCubicBezier2D Left, LineArcCubicBezier2D Right) Divide(LineArcCubicBezier2D c, double t)
    {
        var a = Mix(c.Start, c.Control1, t); var b = Mix(c.Control1, c.Control2, t);
        var d = Mix(c.Control2, c.End, t); var e = Mix(a, b, t); var f = Mix(b, d, t);
        var p = Mix(e, f, t);
        return (new(c.Start, a, e, p), new(p, f, d, c.End));
    }

    private static LineArcProfileCurve2D Reverse(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D l => new LineArcLineSegment2D(l.End, l.Start),
        LineArcCubicBezier2D c => new LineArcCubicBezier2D(c.End, c.Control2, c.Control1, c.Start),
        _ => throw new NotSupportedException()
    };
    private static (double X, double Y) At(LineArcProfileCurve2D curve, double t) => curve switch
    {
        LineArcLineSegment2D l => Mix(l.Start, l.End, t),
        LineArcCubicBezier2D c => Cubic(c, t),
        _ => throw new NotSupportedException()
    };
    private static (double X, double Y) Cubic(LineArcCubicBezier2D c, double t)
    {
        var u = 1 - t;
        return (u*u*u*c.Start.X + 3*u*u*t*c.Control1.X + 3*u*t*t*c.Control2.X + t*t*t*c.End.X,
            u*u*u*c.Start.Y + 3*u*u*t*c.Control1.Y + 3*u*t*t*c.Control2.Y + t*t*t*c.End.Y);
    }
    private static (double X, double Y) Tangent(LineArcProfileCurve2D curve, double t) => curve switch
    {
        LineArcLineSegment2D l => (l.End.X - l.Start.X, l.End.Y - l.Start.Y),
        LineArcCubicBezier2D c => (3*((1-t)*(1-t)*(c.Control1.X-c.Start.X)+2*(1-t)*t*(c.Control2.X-c.Control1.X)+t*t*(c.End.X-c.Control2.X)),
            3*((1-t)*(1-t)*(c.Control1.Y-c.Start.Y)+2*(1-t)*t*(c.Control2.Y-c.Control1.Y)+t*t*(c.End.Y-c.Control2.Y))),
        _ => throw new NotSupportedException()
    };
    private static (double X, double Y) Mix((double X, double Y) a, (double X, double Y) b, double t)
        => (a.X + (b.X-a.X)*t, a.Y + (b.Y-a.Y)*t);
    private static double Cross((double X, double Y) a, (double X, double Y) b) => a.X*b.Y-a.Y*b.X;
    private static double Distance((double X, double Y) a, (double X, double Y) b)
        => Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
    private static double PointLineDistance((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
        => Math.Abs(Cross((b.X-a.X,b.Y-a.Y),(p.X-a.X,p.Y-a.Y))) / Distance(a,b);
    private static string Key((double X, double Y) p) => $"{Math.Round(p.X/VertexTolerance)},{Math.Round(p.Y/VertexTolerance)}";
    private static string GeometryKey(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D l => $"L:{Key(l.Start)}:{Key(l.End)}",
        LineArcCubicBezier2D c => $"C:{Key(c.Start)}:{Key(c.Control1)}:{Key(c.Control2)}:{Key(c.End)}",
        _ => throw new NotSupportedException()
    };
}
