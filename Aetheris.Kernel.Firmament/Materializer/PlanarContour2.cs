namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Exact planar material contour shared by resolved Profiles and domain lowerers.
/// Topology is ordered and authored; this type is intentionally not a sketch solver
/// or an arbitrary boolean API.
/// </summary>
public sealed record PlanarContourSegment2(
    string StableId,
    LineArcProfileCurve2D Geometry,
    ProfileSegmentProvenance Provenance);

public sealed record PlanarContourLoop2(
    string StableId,
    bool IsOuter,
    IReadOnlyList<PlanarContourSegment2> Segments);

public sealed record PlanarContour2(
    string StableId,
    string PlaneFrame,
    PlanarContourLoop2 OuterLoop,
    IReadOnlyList<PlanarContourLoop2> InnerLoops,
    string Provenance)
{
    public IReadOnlyList<PlanarContourLoop2> Loops => [OuterLoop, .. InnerLoops];
}

public enum PlanarContourDiagnosticSeverity { Information, Warning, Error }
public sealed record PlanarContourDiagnostic(string Code, PlanarContourDiagnosticSeverity Severity, string Message, string? SubjectId = null);
public sealed record PlanarContourValidation(bool IsValid, IReadOnlyList<PlanarContourDiagnostic> Diagnostics);
public enum PlanarOffsetSide { Left, Right }
public sealed record PlanarContourOperationResult(PlanarContour2? Contour, IReadOnlyList<PlanarContourDiagnostic> Diagnostics)
{
    public bool Succeeded => Contour is not null && Diagnostics.All(x => x.Severity != PlanarContourDiagnosticSeverity.Error);
}

/// <summary>Bounded exact line/arc contour operations under known-topology authority.</summary>
public static class PlanarContourKernel
{
    private const double Tol = 1e-7;

    public static PlanarContour2 FromResolvedProfile(ResolvedProfile2D profile, string? provenance = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var outer = profile.Loops.Single(x => x.IsOuter);
        PlanarContourLoop2 Convert(ResolvedProfileLoop2D loop) => new(loop.Name, loop.IsOuter,
            loop.Segments.Select(x => new PlanarContourSegment2(x.Provenance.StableId, x.Geometry, x.Provenance)).ToArray());
        return new(profile.Name, profile.PlaneFrame, Convert(outer), profile.Loops.Where(x => !x.IsOuter).Select(Convert).ToArray(), provenance ?? $"ResolvedProfile2D:{profile.Name}");
    }

    public static ResolvedProfile2D ToResolvedProfile(PlanarContour2 contour, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(contour);
        ResolvedProfileLoop2D Convert(PlanarContourLoop2 loop) => new(loop.StableId, loop.IsOuter,
            loop.Segments.Select(x => new ResolvedProfileSegment2D(x.StableId, x.Geometry, x.Provenance)).ToArray());
        return new(name ?? contour.StableId, contour.PlaneFrame, contour.Loops.Select(Convert).ToArray());
    }

    public static PlanarContourValidation Validate(PlanarContour2 contour)
    {
        ArgumentNullException.ThrowIfNull(contour);
        var diagnostics = new List<PlanarContourDiagnostic>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var loop in contour.Loops)
        {
            if (loop.Segments.Count == 0)
            {
                diagnostics.Add(new("planar-contour-empty-loop", PlanarContourDiagnosticSeverity.Error, $"Loop '{loop.StableId}' has no boundary segments.", loop.StableId));
                continue;
            }
            if (loop.Segments.Count == 1 && loop.Segments[0].Geometry is LineArcFullCircle2D circle)
            {
                if (!Finite(circle.Center) || !double.IsFinite(circle.Radius) || circle.Radius <= Tol)
                    diagnostics.Add(new("planar-contour-invalid-circle", PlanarContourDiagnosticSeverity.Error, $"Loop '{loop.StableId}' has an invalid full circle.", loop.StableId));
                continue;
            }
            if (loop.Segments.Count < 2)
                diagnostics.Add(new("planar-contour-open-loop", PlanarContourDiagnosticSeverity.Error, $"Loop '{loop.StableId}' is not a closed bounded chain.", loop.StableId));
            for (var i = 0; i < loop.Segments.Count; i++)
            {
                var segment = loop.Segments[i];
                if (!ids.Add(segment.StableId)) diagnostics.Add(new("planar-contour-duplicate-id", PlanarContourDiagnosticSeverity.Error, $"Segment stable ID '{segment.StableId}' is duplicated.", segment.StableId));
                if (!TryEnds(segment.Geometry, out var start, out var end) || !Finite(start) || !Finite(end))
                {
                    diagnostics.Add(new("planar-contour-unsupported-curve", PlanarContourDiagnosticSeverity.Error, $"Segment '{segment.StableId}' is not a finite bounded line or circular arc.", segment.StableId));
                    continue;
                }
                var negligible = segment.Geometry is LineArcCircularArc2D boundedArc
                    ? boundedArc.Radius <= Tol || Math.Abs(boundedArc.SweepAngleRadians) * boundedArc.Radius <= Tol
                    : Distance(start, end) <= Tol;
                if (negligible)
                    diagnostics.Add(new("planar-contour-zero-length", PlanarContourDiagnosticSeverity.Error, $"Segment '{segment.StableId}' has negligible length.", segment.StableId));
                var next = loop.Segments[(i + 1) % loop.Segments.Count];
                if (TryEnds(next.Geometry, out var nextStart, out _) && Distance(end, nextStart) > Tol)
                    diagnostics.Add(new("planar-contour-endpoint-mismatch", PlanarContourDiagnosticSeverity.Error, $"Endpoint authority mismatch '{segment.StableId}' -> '{next.StableId}' ({Distance(end, nextStart):G6}).", segment.StableId));
            }
            for (var i = 0; i < loop.Segments.Count; i++) for (var j = i + 1; j < loop.Segments.Count; j++)
            {
                if (Adjacent(i, j, loop.Segments.Count)) continue;
                var hit = ProfileArrangementBuilder.IntersectBounded(loop.Segments[i].Geometry, loop.Segments[j].Geometry);
                if (hit.HasBoundedOverlap || hit.Intersections.Count > 0)
                {
                    var detail=hit.Intersections.Count==0?"positive bounded coincident overlap":string.Join(", ",hit.Intersections.Select(x=>$"({x.Point.X:G6},{x.Point.Y:G6}) t={x.FirstParameter:G4}/{x.SecondParameter:G4}"));
                    diagnostics.Add(new("planar-contour-self-intersection", PlanarContourDiagnosticSeverity.Error, $"Non-adjacent segments '{loop.Segments[i].StableId}' and '{loop.Segments[j].StableId}' intersect: {detail}.", loop.StableId));
                }
            }
            var area = SignedArea(loop);
            if (Math.Abs(area) <= Tol) diagnostics.Add(new("planar-contour-zero-area", PlanarContourDiagnosticSeverity.Error, $"Loop '{loop.StableId}' has negligible signed area.", loop.StableId));
            else if (loop.IsOuter && area < 0d || !loop.IsOuter && area > 0d)
                diagnostics.Add(new("planar-contour-winding", PlanarContourDiagnosticSeverity.Error, $"Loop '{loop.StableId}' winding must be {(loop.IsOuter ? "counter-clockwise" : "clockwise")}.", loop.StableId));
        }
        foreach (var inner in contour.InnerLoops)
        {
            var probe = PointAt(inner.Segments[0].Geometry, 0.5d);
            if (!PointInLoop(contour.OuterLoop, probe))
                diagnostics.Add(new("planar-contour-inner-outside", PlanarContourDiagnosticSeverity.Error, $"Inner loop '{inner.StableId}' is not inside outer loop '{contour.OuterLoop.StableId}'.", inner.StableId));
        }
        return new(!diagnostics.Any(x => x.Severity == PlanarContourDiagnosticSeverity.Error), diagnostics);
    }

    /// <summary>
    /// Exact signed-loop integration of the final material contour. Outer material
    /// is positive and inner loops are voids; lines, circular arcs, and full circles
    /// are integrated analytically.
    /// </summary>
    public static double Area(PlanarContour2 contour)
    {
        ArgumentNullException.ThrowIfNull(contour);
        return Math.Abs(SignedArea(contour.OuterLoop))
            - contour.InnerLoops.Sum(loop => Math.Abs(SignedArea(loop)));
    }

    /// <summary>
    /// Offsets a known closed line/arc chain and resolves each neighboring support
    /// intersection as a miter. Collapsed arcs and ambiguous/self-intersecting results
    /// are rejected with typed diagnostics; no repair is applied.
    /// </summary>
    public static PlanarContourOperationResult Offset(PlanarContour2 contour, double distance, PlanarOffsetSide side)
    {
        ArgumentNullException.ThrowIfNull(contour);
        if (!double.IsFinite(distance) || distance <= Tol)
            return Fail("planar-offset-invalid-distance", "Offset distance must be finite and positive.", contour.StableId);
        if (contour.InnerLoops.Count > 0)
            return Fail("planar-offset-inner-loops-deferred", "One-call offset of a contour with inner loops is outside the bounded offset contract; offset each loop with an explicit side.", contour.StableId);
        var source = contour.OuterLoop.Segments;
        if (source.Count < 2 || source.Any(x => x.Geometry is not LineArcLineSegment2D and not LineArcCircularArc2D))
            return Fail("planar-offset-unsupported-curve", "Offset supports closed line/circular-arc chains.", contour.StableId);
        var signed = side == PlanarOffsetSide.Left ? distance : -distance;
        var supports = new List<LineArcProfileCurve2D>();
        foreach (var segment in source)
        {
            switch (segment.Geometry)
            {
                case LineArcLineSegment2D line:
                    var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y; var length = Math.Sqrt(dx * dx + dy * dy);
                    if (length <= Tol) return Fail("planar-offset-collapsed-segment", $"Segment '{segment.StableId}' is too short to offset.", segment.StableId);
                    var n = (X: -dy / length * signed, Y: dx / length * signed);
                    supports.Add(new LineArcLineSegment2D((line.Start.X + n.X, line.Start.Y + n.Y), (line.End.X + n.X, line.End.Y + n.Y)));
                    break;
                case LineArcCircularArc2D arc:
                    var radius = arc.Radius - Math.Sign(arc.SweepAngleRadians) * signed;
                    if (radius <= Tol) return Fail("planar-offset-collapsed-arc", $"Offset collapses arc '{segment.StableId}' (result radius {radius:G6}).", segment.StableId);
                    supports.Add(arc with { Radius = radius });
                    break;
            }
        }
        var junctions = new (double X, double Y)[supports.Count];
        for (var i = 0; i < supports.Count; i++)
        {
            var previous = (i + supports.Count - 1) % supports.Count;
            var authoredVertex = TryEnds(source[i].Geometry, out var currentStart, out _) ? currentStart : default;
            var candidates = IntersectSupports(supports[previous], supports[i]);
            if (candidates.Count == 0) return Fail("planar-offset-disconnected-corner", $"Offset supports at corner '{source[i].StableId}' do not intersect.", source[i].StableId);
            junctions[i] = candidates.OrderBy(x => Distance(x, authoredVertex)).First();
        }
        var resultSegments = new List<PlanarContourSegment2>();
        for (var i = 0; i < supports.Count; i++)
        {
            var geometry = WithEndpoints(supports[i], junctions[i], junctions[(i + 1) % supports.Count]);
            if (geometry is null) return Fail("planar-offset-impossible-trim", $"Offset segment '{source[i].StableId}' could not be trimmed to its resolved corner intersections.", source[i].StableId);
            resultSegments.Add(source[i] with { StableId = $"{source[i].StableId}.offset", Geometry = geometry, Provenance = source[i].Provenance with { StableId = $"{source[i].Provenance.StableId}.offset", Derivation = $"offset:{side}:{distance:R}" } });
        }
        var result = contour with { StableId = $"{contour.StableId}.offset", OuterLoop = contour.OuterLoop with { StableId = $"{contour.OuterLoop.StableId}.offset", Segments = resultSegments }, Provenance = $"{contour.Provenance}; offset {side} {distance:R}" };
        var validation = Validate(result);
        return validation.IsValid ? new(result, validation.Diagnostics) : new(null, validation.Diagnostics.Prepend(new("planar-offset-invalid-result", PlanarContourDiagnosticSeverity.Error, "Offset result violates closed-contour topology; no repair was applied.", contour.StableId)).ToArray());
    }

    public static PlanarContour2 FromPolygon(string stableId, string frame, IReadOnlyList<(double X, double Y)> points, string provenance)
    {
        ArgumentNullException.ThrowIfNull(points);
        var clean = points.Aggregate(new List<(double X, double Y)>(), (list, point) => { if (list.Count == 0 || Distance(list[^1], point) > Tol) list.Add(point); return list; });
        if (clean.Count > 1 && Distance(clean[0], clean[^1]) <= Tol) clean.RemoveAt(clean.Count - 1);
        if (PolygonArea(clean) < 0d) clean.Reverse();
        var segments = clean.Select((point, index) => new PlanarContourSegment2($"{stableId}.edge{index:D3}", new LineArcLineSegment2D(point, clean[(index + 1) % clean.Count]), new($"{stableId}.edge{index:D3}", stableId, stableId, provenance, frame))).ToArray();
        return new(stableId, frame, new($"{stableId}.outer", true, segments), [], provenance);
    }

    private static PlanarContourOperationResult Fail(string code, string message, string subject) => new(null, [new(code, PlanarContourDiagnosticSeverity.Error, message, subject)]);
    private static bool Adjacent(int i, int j, int count) => j == i + 1 || i == 0 && j == count - 1;
    private static bool Finite((double X, double Y) p) => double.IsFinite(p.X) && double.IsFinite(p.Y);
    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static double PolygonArea(IReadOnlyList<(double X, double Y)> points) { var sum = 0d; for (var i = 0; i < points.Count; i++) { var q = points[(i + 1) % points.Count]; sum += points[i].X * q.Y - q.X * points[i].Y; } return sum / 2d; }
    private static double SignedArea(PlanarContourLoop2 loop) => loop.Segments.Sum(x => x.Geometry switch
    {
        LineArcLineSegment2D line => line.Start.X * line.End.Y - line.End.X * line.Start.Y,
        LineArcCircularArc2D arc => ArcArea(arc),
        LineArcFullCircle2D circle => 2d * Math.PI * circle.Radius * circle.Radius,
        _ => 0d
    }) / 2d;
    private static double ArcArea(LineArcCircularArc2D arc) { var a = arc.StartAngleRadians; var b = a + arc.SweepAngleRadians; return arc.Center.X * arc.Radius * (Math.Sin(b) - Math.Sin(a)) - arc.Center.Y * arc.Radius * (Math.Cos(b) - Math.Cos(a)) + arc.Radius * arc.Radius * (b - a); }
    private static bool TryEnds(LineArcProfileCurve2D curve, out (double X, double Y) start, out (double X, double Y) end)
    {
        switch (curve)
        {
            case LineArcLineSegment2D line: start = line.Start; end = line.End; return true;
            case LineArcCircularArc2D arc: start = PointAt(arc, 0d); end = PointAt(arc, 1d); return true;
            default: start = end = default; return false;
        }
    }
    private static (double X, double Y) PointAt(LineArcProfileCurve2D curve, double t) => curve switch
    {
        LineArcLineSegment2D line => (line.Start.X + (line.End.X - line.Start.X) * t, line.Start.Y + (line.End.Y - line.Start.Y) * t),
        LineArcCircularArc2D arc => (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians * t), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians * t)),
        LineArcFullCircle2D circle => (circle.Center.X + circle.Radius * Math.Cos(2d * Math.PI * t), circle.Center.Y + circle.Radius * Math.Sin(2d * Math.PI * t)),
        _ => throw new NotSupportedException()
    };
    private static bool PointInLoop(PlanarContourLoop2 loop, (double X, double Y) point)
    {
        var samples = loop.Segments.SelectMany(x => x.Geometry switch
        {
            LineArcLineSegment2D line => new[] { line.Start },
            LineArcCircularArc2D arc => Enumerable.Range(0, Math.Max(2, (int)Math.Ceiling(Math.Abs(arc.SweepAngleRadians) / (Math.PI / 24d)))).Select(i => PointAt(arc, (double)i / Math.Max(2, (int)Math.Ceiling(Math.Abs(arc.SweepAngleRadians) / (Math.PI / 24d))))),
            LineArcFullCircle2D circle => Enumerable.Range(0, 96).Select(i => PointAt(circle, i / 96d)),
            _ => []
        }).ToArray();
        var inside = false; for (var i = 0; i < samples.Length; i++) { var a = samples[i]; var b = samples[(i + 1) % samples.Length]; if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside; } return inside;
    }
    private static IReadOnlyList<(double X, double Y)> IntersectSupports(LineArcProfileCurve2D a, LineArcProfileCurve2D b)
    {
        if (a is LineArcLineSegment2D la && b is LineArcLineSegment2D lb)
        {
            var r = (X: la.End.X - la.Start.X, Y: la.End.Y - la.Start.Y); var s = (X: lb.End.X - lb.Start.X, Y: lb.End.Y - lb.Start.Y); var cross = r.X * s.Y - r.Y * s.X;
            if (Math.Abs(cross) <= Tol) return [];
            var d = (X: lb.Start.X - la.Start.X, Y: lb.Start.Y - la.Start.Y); var t = (d.X * s.Y - d.Y * s.X) / cross; return [(la.Start.X + t * r.X, la.Start.Y + t * r.Y)];
        }
        if (a is LineArcLineSegment2D lineA && b is LineArcCircularArc2D circleB) return LineCircleSupport(lineA, circleB);
        if (a is LineArcCircularArc2D circleA && b is LineArcLineSegment2D lineB) return LineCircleSupport(lineB, circleA);
        if (a is LineArcCircularArc2D ca && b is LineArcCircularArc2D cb)
        {
            var dx = cb.Center.X - ca.Center.X; var dy = cb.Center.Y - ca.Center.Y; var d = Math.Sqrt(dx * dx + dy * dy);
            if (d <= Tol || d > ca.Radius + cb.Radius + Tol || d < Math.Abs(ca.Radius - cb.Radius) - Tol) return [];
            var x = (ca.Radius * ca.Radius - cb.Radius * cb.Radius + d * d) / (2d * d); var h = Math.Sqrt(Math.Max(0d, ca.Radius * ca.Radius - x * x)); var px = ca.Center.X + x * dx / d; var py = ca.Center.Y + x * dy / d;
            return h <= Tol ? [(px, py)] : [(px - dy * h / d, py + dx * h / d), (px + dy * h / d, py - dx * h / d)];
        }
        return [];
    }
    private static IReadOnlyList<(double X, double Y)> LineCircleSupport(LineArcLineSegment2D line, LineArcCircularArc2D circle)
    {
        var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y; var fx = line.Start.X - circle.Center.X; var fy = line.Start.Y - circle.Center.Y; var aa = dx * dx + dy * dy; var bb = 2d * (fx * dx + fy * dy); var cc = fx * fx + fy * fy - circle.Radius * circle.Radius; var disc = bb * bb - 4d * aa * cc;
        if (disc < -Tol || aa <= Tol) return []; var root = Math.Sqrt(Math.Max(0d, disc)); var ts = root <= Tol ? new[] { -bb / (2d * aa) } : new[] { (-bb - root) / (2d * aa), (-bb + root) / (2d * aa) }; return ts.Select(t => (line.Start.X + t * dx, line.Start.Y + t * dy)).ToArray();
    }
    private static LineArcProfileCurve2D? WithEndpoints(LineArcProfileCurve2D support, (double X, double Y) start, (double X, double Y) end)
    {
        if (Distance(start, end) <= Tol) return null;
        if (support is LineArcLineSegment2D) return new LineArcLineSegment2D(start, end);
        if (support is LineArcCircularArc2D arc)
        {
            if (Math.Abs(Distance(start, arc.Center) - arc.Radius) > Tol * 10 || Math.Abs(Distance(end, arc.Center) - arc.Radius) > Tol * 10) return null;
            var a = Math.Atan2(start.Y - arc.Center.Y, start.X - arc.Center.X); var b = Math.Atan2(end.Y - arc.Center.Y, end.X - arc.Center.X); var sweep = b - a;
            if (arc.SweepAngleRadians > 0) while (sweep <= 0d) sweep += 2d * Math.PI; else while (sweep >= 0d) sweep -= 2d * Math.PI;
            if (Math.Abs(sweep) >= 2d * Math.PI - Tol) return null;
            return arc with { StartAngleRadians = a, SweepAngleRadians = sweep };
        }
        return null;
    }
}
