using System.Diagnostics;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// A bounded analytic arrangement of resolved line and circular-arc profile curves.
/// It is construction IR: source curves retain their profile/operation provenance and
/// result loops contain only boundary fragments that separate material from void.
/// </summary>
public sealed record ArrangementSourceCurve2D(
    string StableId, string Operation, string Profile, string Loop, string Segment,
    LineArcProfileCurve2D Geometry, ProfileSegmentProvenance Provenance);
public sealed record ArrangementVertex2D(string StableId, (double X, double Y) Position);
public sealed record ArrangementFragment2D(
    string StableId, ArrangementSourceCurve2D Source, double FromParameter, double ToParameter,
    LineArcProfileCurve2D Geometry, bool MaterialOnLeft, bool Retained);
public sealed record ArrangementLoop2D(string StableId, bool IsOuter, IReadOnlyList<ArrangementFragment2D> Fragments, double SignedArea, double Perimeter);
public sealed record ProfileArrangement2D(
    string Frame, IReadOnlyList<ArrangementSourceCurve2D> SourceCurves,
    IReadOnlyList<ArrangementVertex2D> IntersectionVertices, IReadOnlyList<ArrangementFragment2D> AtomicFragments,
    IReadOnlyList<ArrangementLoop2D> ResultLoops, int CoincidentFragmentCount,
    IReadOnlyList<string> Diagnostics, TimeSpan IntersectionTime, TimeSpan SplitTime,
    TimeSpan ClassificationTime, TimeSpan ReconstructionTime)
{
    public int RetainedBoundaryFragmentCount => AtomicFragments.Count(x => x.Retained);
}

public sealed record ProfileArrangementResult(ProfileArrangement2D Arrangement, PrismaticSectionRegion? Region);

public static class ProfileArrangementBuilder
{
    private const double Tol = 1e-7;
    private const double SideSample = 1e-5;

    public static ProfileArrangementResult Compose(
        string frame,
        IReadOnlyList<PrismaticProfileOperation> active,
        IReadOnlyDictionary<string, ResolvedProfile2D> profiles,
        string context)
    {
        var sources = active.SelectMany(operation => profiles[operation.ProfileReference].Loops.SelectMany(loop => loop.Segments.Select(segment =>
            new ArrangementSourceCurve2D(
                $"compose:{operation.Name}.{operation.ProfileReference}.{loop.Name}.{segment.Name}", operation.Name,
                operation.ProfileReference, loop.Name, segment.Name, segment.Geometry, segment.Provenance)))).ToArray();
        var positive = active.Where(x => x.Intent is PrismaticProfileIntent.Base or PrismaticProfileIntent.Add)
            .Select(x => profiles[x.ProfileReference]).ToArray();
        var negative = active.Where(x => x.Intent == PrismaticProfileIntent.Remove)
            .Select(x => profiles[x.ProfileReference]).ToArray();
        return Build(frame, sources, p => positive.Any(profile => PointInProfile(profile, p) == ArrangementPointLocation.Inside)
            && !negative.Any(profile => PointInProfile(profile, p) == ArrangementPointLocation.Inside), context);
    }

    /// <summary>Exact region subtraction used only to derive horizontal section transitions.</summary>
    public static ProfileArrangementResult Difference(string frame, PrismaticSectionRegion? left, PrismaticSectionRegion? right, string context)
    {
        if (left is null) return Empty(frame, context);
        var sourceRegions = new[] { (Name: "left", Region: left), (Name: "right", Region: right) }
            .Where(x => x.Region is not null).ToArray();
        var sources = sourceRegions.SelectMany(item => RegionSources(item.Name, item.Region!)).ToArray();
        return Build(frame, sources, p => InRegion(left, p) && !InRegion(right, p), context);
    }

    public static ArrangementPointLocation PointInProfile(ResolvedProfile2D profile, (double X, double Y) point)
    {
        var loop = profile.Loops.SingleOrDefault();
        if (loop is null) return ArrangementPointLocation.Outside;
        return PointInLoop(loop.Segments.Select(x => x.Geometry).ToArray(), point);
    }

    private static ProfileArrangementResult Empty(string frame, string context) =>
        new(new(frame, [], [], [], [], 0, [], TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), null);

    private static IEnumerable<ArrangementSourceCurve2D> RegionSources(string name, PrismaticSectionRegion region)
    {
        foreach (var item in new[] { (region.Outer, "Outer") }.Concat(region.Holes.Select(x => (x, "Inner"))))
            foreach (var segment in item.Item1.Loops[0].Segments)
                yield return new ArrangementSourceCurve2D($"transition:{name}.{item.Item2}.{segment.Name}", name, item.Item1.Name, item.Item2,
                    segment.Name, segment.Geometry, segment.Provenance);
    }

    private static bool InRegion(PrismaticSectionRegion? region, (double X, double Y) point)
    {
        if (region is null || PointInProfile(region.Outer, point) != ArrangementPointLocation.Inside) return false;
        return !region.Holes.Any(hole => PointInProfile(hole, point) == ArrangementPointLocation.Inside);
    }

    private static ProfileArrangementResult Build(string frame, IReadOnlyList<ArrangementSourceCurve2D> sources, Func<(double X, double Y), bool> material, string context)
    {
        var diagnostics = new List<string>();
        var intersectionClock = Stopwatch.StartNew();
        var parameters = sources.ToDictionary(x => x.StableId, _ => new List<double> { 0d, 1d }, StringComparer.Ordinal);
        var vertices = new List<(double X, double Y)>();
        for (var i = 0; i < sources.Count; i++)
        for (var j = i + 1; j < sources.Count; j++)
        {
            var intersections = Intersections(sources[i].Geometry, sources[j].Geometry, out var coincident);
            if (coincident)
            {
                // Collinear/co-circular support needs endpoint transfer before side
                // classification; otherwise an internal shared interval can leave a
                // mismatched fragment at a transition boundary.
                foreach (var point in new[] { Ends(sources[i].Geometry).Start, Ends(sources[i].Geometry).End })
                    if (OnCurve(sources[j].Geometry, point, out var parameter)) { parameters[sources[j].StableId].Add(parameter); vertices.Add(point); }
                foreach (var point in new[] { Ends(sources[j].Geometry).Start, Ends(sources[j].Geometry).End })
                    if (OnCurve(sources[i].Geometry, point, out var parameter)) { parameters[sources[i].StableId].Add(parameter); vertices.Add(point); }
                continue;
            }
            foreach (var hit in intersections)
            {
                if (!OnCurve(sources[i].Geometry, hit, out var a) || !OnCurve(sources[j].Geometry, hit, out var b)) continue;
                parameters[sources[i].StableId].Add(a); parameters[sources[j].StableId].Add(b); vertices.Add(hit);
            }
        }
        intersectionClock.Stop();

        var splitClock = Stopwatch.StartNew();
        var fragments = new List<ArrangementFragment2D>();
        foreach (var source in sources.OrderBy(x => x.StableId, StringComparer.Ordinal))
        {
            var ordered = parameters[source.StableId].OrderBy(x => x).Aggregate(new List<double>(), (list, value) =>
            { if (list.Count == 0 || Math.Abs(list[^1] - value) > Tol) list.Add(Math.Clamp(value, 0d, 1d)); return list; });
            for (var i = 0; i < ordered.Count - 1; i++)
            {
                if (ordered[i + 1] - ordered[i] <= Tol) continue;
                var geometry = Trim(source.Geometry, ordered[i], ordered[i + 1]);
                fragments.Add(new ArrangementFragment2D($"{source.StableId}.part{i}", source, ordered[i], ordered[i + 1], geometry, false, false));
            }
        }
        splitClock.Stop();

        var classificationClock = Stopwatch.StartNew();
        var oriented = new List<ArrangementFragment2D>();
        foreach (var fragment in fragments)
        {
            var midpoint = At(fragment.Geometry, .5d); var tangent = Tangent(fragment.Geometry, .5d);
            var length = Math.Sqrt(tangent.X * tangent.X + tangent.Y * tangent.Y);
            if (length <= Tol) { diagnostics.Add($"arrangement-rejected:degenerate-fragment:{fragment.StableId}"); continue; }
            var offset = SideSample;
            var left = (midpoint.X - tangent.Y / length * offset, midpoint.Y + tangent.X / length * offset);
            var right = (midpoint.X + tangent.Y / length * offset, midpoint.Y - tangent.X / length * offset);
            var leftMaterial = material(left); var rightMaterial = material(right);
            if (leftMaterial == rightMaterial) continue;
            var curve = leftMaterial ? fragment.Geometry : Reverse(fragment.Geometry);
            oriented.Add(fragment with { Geometry = curve, MaterialOnLeft = true, Retained = true });
        }
        // Multiple source curves can normalize to the same material-oriented support interval.  Keep the lowest semantic id.
        var retained = oriented.GroupBy(x => GeometryKey(x.Geometry), StringComparer.Ordinal).Select(x => x.OrderBy(y => y.StableId, StringComparer.Ordinal).First()).ToArray();
        var coincidentCount = oriented.Count - retained.Length;
        classificationClock.Stop();

        var reconstructionClock = Stopwatch.StartNew();
        var loops = Reconstruct(retained, diagnostics, context);
        reconstructionClock.Stop();
        var allFragments = fragments.Select(fragment => retained.FirstOrDefault(x => x.StableId == fragment.StableId) ?? fragment).ToArray();
        var arrangement = new ProfileArrangement2D(frame, sources, vertices.DistinctBy(VertexKey).OrderBy(VertexKey).Select((p, i) => new ArrangementVertex2D($"vertex:{i}", p)).ToArray(),
            allFragments, loops, coincidentCount, diagnostics.Distinct().ToArray(), intersectionClock.Elapsed, splitClock.Elapsed, classificationClock.Elapsed, reconstructionClock.Elapsed);
        if (diagnostics.Any(x => x.StartsWith("arrangement-rejected", StringComparison.Ordinal))) return new(arrangement, null);
        var region = ToRegion(loops, diagnostics, context);
        return new(arrangement with { Diagnostics = diagnostics.Distinct().ToArray() }, region);
    }

    private static IReadOnlyList<ArrangementLoop2D> Reconstruct(IReadOnlyList<ArrangementFragment2D> fragments, List<string> diagnostics, string context)
    {
        if (fragments.Count == 0) return [];
        var starts = fragments.GroupBy(x => VertexKey(Ends(x.Geometry).Start), StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.StableId, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal); var loops = new List<ArrangementLoop2D>();
        foreach (var seed in fragments.OrderBy(x => x.StableId, StringComparer.Ordinal))
        {
            if (!used.Add(seed.StableId)) continue;
            var current = seed; var chain = new List<ArrangementFragment2D> { seed }; var first = VertexKey(Ends(seed.Geometry).Start);
            for (var guard = 0; guard <= fragments.Count; guard++)
            {
                var at = VertexKey(Ends(current.Geometry).End);
                if (at == first) break;
                if (!starts.TryGetValue(at, out var candidates)) { diagnostics.Add($"arrangement-rejected:dangling-edge:{current.StableId}:context={context}"); break; }
                var next = candidates.Where(x => !used.Contains(x.StableId)).OrderBy(x => Turn(current.Geometry, x.Geometry)).ThenBy(x => x.StableId, StringComparer.Ordinal).FirstOrDefault();
                if (next is null) { diagnostics.Add($"arrangement-rejected:unresolved-angular-order:{current.StableId}:context={context}"); break; }
                used.Add(next.StableId); chain.Add(next); current = next;
            }
            if (VertexKey(Ends(current.Geometry).End) != first) continue;
            var area = chain.Sum(x => SignedAreaContribution(x.Geometry)) / 2d;
            if (Math.Abs(area) <= Tol) { diagnostics.Add($"arrangement-rejected:zero-width-loop:{seed.StableId}:context={context}"); continue; }
            loops.Add(new ArrangementLoop2D($"arrangement-loop:{loops.Count}", area > 0d, chain, area, chain.Sum(x => Length(x.Geometry))));
        }
        if (used.Count != fragments.Count) diagnostics.Add($"arrangement-rejected:unwalked-boundary-fragment:context={context}");
        return loops;
    }

    private static PrismaticSectionRegion? ToRegion(IReadOnlyList<ArrangementLoop2D> loops, List<string> diagnostics, string context)
    {
        if (loops.Count == 0) return null;
        var outers = loops.Where(x => x.IsOuter).OrderByDescending(x => x.SignedArea).ToArray();
        if (outers.Length != 1) { diagnostics.Add($"arrangement-rejected:disconnected-or-invalid-material:outer-loops={outers.Length}:context={context}"); return null; }
        var outer = ToProfile(outers[0], "Outer");
        var holes = loops.Where(x => !x.IsOuter).Select((loop, index) => ToProfile(loop, $"Inner{index}")).ToArray();
        foreach (var hole in holes)
            if (PointInProfile(outer, Ends(hole.Loops[0].Segments[0].Geometry).Start) != ArrangementPointLocation.Inside)
            { diagnostics.Add($"arrangement-rejected:inner-loop-not-nested:context={context}"); return null; }
        return new PrismaticSectionRegion(outer, holes, loops.SelectMany(x => x.Fragments).Select(x => x.Source.StableId).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static ResolvedProfile2D ToProfile(ArrangementLoop2D loop, string role)
    {
        // The section-stack emitter's explicit inner-loop contract stores source loops CCW
        // and applies the material-facing reversal while lowering FACE_BOUND.  Arrangement
        // traversal itself is material-left (thus clockwise for a void), so reverse here.
        var fragments = role == "Outer" ? loop.Fragments : loop.Fragments.Reverse().Select(x => x with { Geometry = Reverse(x.Geometry) }).ToArray();
        return new(
        $"{loop.StableId}.{role}", "XY", [new ResolvedProfileLoop2D(role, role == "Outer", fragments.Select((x, i) =>
            new ResolvedProfileSegment2D($"{x.Source.Segment}.part{i}", x.Geometry,
                x.Source.Provenance with { StableId = $"{x.Source.Provenance.StableId}.arrangement.part{i}", Derivation = $"arrangement:{x.Source.StableId}:{x.FromParameter:R}..{x.ToParameter:R}" })).ToArray())]);
    }

    private static double Turn(LineArcProfileCurve2D incoming, LineArcProfileCurve2D outgoing)
    {
        var a = Tangent(incoming, 1d); var b = Tangent(outgoing, 0d);
        var reverse = Math.Atan2(-a.Y, -a.X); var candidate = Math.Atan2(b.Y, b.X);
        var delta = reverse - candidate; while (delta < 0d) delta += 2d * Math.PI; while (delta >= 2d * Math.PI) delta -= 2d * Math.PI;
        return delta;
    }

    private static IReadOnlyList<(double X, double Y)> Intersections(LineArcProfileCurve2D a, LineArcProfileCurve2D b, out bool coincident)
    {
        coincident = false;
        return (a, b) switch
        {
            (LineArcLineSegment2D x, LineArcLineSegment2D y) => LineLine(x, y, ref coincident),
            (LineArcLineSegment2D x, LineArcCircularArc2D y) => LineCircle(x, y),
            (LineArcCircularArc2D x, LineArcLineSegment2D y) => LineCircle(y, x),
            (LineArcCircularArc2D x, LineArcCircularArc2D y) => CircleCircle(x, y, ref coincident),
            _ => []
        };
    }

    private static IReadOnlyList<(double X, double Y)> LineLine(LineArcLineSegment2D a, LineArcLineSegment2D b, ref bool coincident)
    {
        var r = (X: a.End.X - a.Start.X, Y: a.End.Y - a.Start.Y); var s = (X: b.End.X - b.Start.X, Y: b.End.Y - b.Start.Y);
        var cross = Cross(r, s); var delta = (X: b.Start.X - a.Start.X, Y: b.Start.Y - a.Start.Y);
        if (Math.Abs(cross) <= Tol) { coincident = Math.Abs(Cross(delta, r)) <= Tol; return []; }
        var t = Cross(delta, s) / cross; var u = Cross(delta, r) / cross;
        return t >= -Tol && t <= 1d + Tol && u >= -Tol && u <= 1d + Tol ? [(a.Start.X + t * r.X, a.Start.Y + t * r.Y)] : [];
    }

    private static IReadOnlyList<(double X, double Y)> LineCircle(LineArcLineSegment2D line, LineArcCircularArc2D arc)
    {
        var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y; var fx = line.Start.X - arc.Center.X; var fy = line.Start.Y - arc.Center.Y;
        var aa = dx * dx + dy * dy; var bb = 2d * (fx * dx + fy * dy); var cc = fx * fx + fy * fy - arc.Radius * arc.Radius; var discriminant = bb * bb - 4d * aa * cc;
        if (discriminant < -Tol) return []; if (Math.Abs(discriminant) <= Tol) discriminant = 0d;
        var root = Math.Sqrt(discriminant); double[] values = root <= Tol ? [(-bb) / (2d * aa)] : [(-bb - root) / (2d * aa), (-bb + root) / (2d * aa)];
        return values.Where(t => t >= -Tol && t <= 1d + Tol).Select(t => (line.Start.X + dx * t, line.Start.Y + dy * t)).Where(p => OnCurve(arc, p, out _)).ToArray();
    }

    private static IReadOnlyList<(double X, double Y)> CircleCircle(LineArcCircularArc2D a, LineArcCircularArc2D b, ref bool coincident)
    {
        var dx = b.Center.X - a.Center.X; var dy = b.Center.Y - a.Center.Y; var d = Math.Sqrt(dx * dx + dy * dy);
        if (d <= Tol) { coincident = Math.Abs(a.Radius - b.Radius) <= Tol; return []; }
        if (d > a.Radius + b.Radius + Tol || d < Math.Abs(a.Radius - b.Radius) - Tol) return [];
        var x = (a.Radius * a.Radius - b.Radius * b.Radius + d * d) / (2d * d); var h2 = a.Radius * a.Radius - x * x;
        if (h2 < -Tol) return []; var h = Math.Sqrt(Math.Max(0d, h2)); var px = a.Center.X + x * dx / d; var py = a.Center.Y + x * dy / d;
        (double X, double Y)[] candidates = h <= Tol ? [(px, py)] : [(px + -dy * h / d, py + dx * h / d), (px - -dy * h / d, py - dx * h / d)];
        return candidates.Where(p => OnCurve(a, p, out _) && OnCurve(b, p, out _)).ToArray();
    }

    private static bool OnCurve(LineArcProfileCurve2D curve, (double X, double Y) p, out double parameter)
    {
        switch (curve)
        {
            case LineArcLineSegment2D line:
                var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y; var length2 = dx * dx + dy * dy;
                parameter = ((p.X - line.Start.X) * dx + (p.Y - line.Start.Y) * dy) / length2;
                var projection = (line.Start.X + parameter * dx, line.Start.Y + parameter * dy);
                return parameter >= -Tol && parameter <= 1d + Tol && Distance(projection, p) <= Tol;
            case LineArcCircularArc2D arc:
                if (Math.Abs(Distance(arc.Center, p) - arc.Radius) > Tol) { parameter = 0; return false; }
                var angle = Math.Atan2(p.Y - arc.Center.Y, p.X - arc.Center.X); parameter = ArcParameter(arc, angle);
                return parameter >= -Tol && parameter <= 1d + Tol;
            default: parameter = 0; return false;
        }
    }

    private static double ArcParameter(LineArcCircularArc2D arc, double angle)
    {
        var delta = angle - arc.StartAngleRadians;
        if (arc.SweepAngleRadians >= 0d) while (delta < 0d) delta += 2d * Math.PI;
        else while (delta > 0d) delta -= 2d * Math.PI;
        return delta / arc.SweepAngleRadians;
    }
    private static LineArcProfileCurve2D Trim(LineArcProfileCurve2D curve, double from, double to) => curve switch
    {
        LineArcLineSegment2D line => new LineArcLineSegment2D(At(line, from), At(line, to)),
        LineArcCircularArc2D arc => new LineArcCircularArc2D(arc.Center, arc.Radius, arc.StartAngleRadians + arc.SweepAngleRadians * from, arc.SweepAngleRadians * (to - from)),
        _ => throw new NotSupportedException("Profile arrangements require bounded line or circular-arc source curves.")
    };
    private static LineArcProfileCurve2D Reverse(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => new LineArcLineSegment2D(line.End, line.Start),
        LineArcCircularArc2D arc => new LineArcCircularArc2D(arc.Center, arc.Radius, arc.StartAngleRadians + arc.SweepAngleRadians, -arc.SweepAngleRadians),
        _ => throw new NotSupportedException()
    };
    private static (double X, double Y) At(LineArcProfileCurve2D curve, double parameter) => curve switch
    {
        LineArcLineSegment2D line => (line.Start.X + (line.End.X - line.Start.X) * parameter, line.Start.Y + (line.End.Y - line.Start.Y) * parameter),
        LineArcCircularArc2D arc => (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians * parameter), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians * parameter)),
        _ => throw new NotSupportedException()
    };
    private static (double X, double Y) Tangent(LineArcProfileCurve2D curve, double parameter) => curve switch
    {
        LineArcLineSegment2D line => (line.End.X - line.Start.X, line.End.Y - line.Start.Y),
        LineArcCircularArc2D arc => (-arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians * parameter) * Math.Sign(arc.SweepAngleRadians), arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians * parameter) * Math.Sign(arc.SweepAngleRadians)),
        _ => throw new NotSupportedException()
    };
    private static ((double X, double Y) Start, (double X, double Y) End) Ends(LineArcProfileCurve2D curve) => (At(curve, 0d), At(curve, 1d));
    private static double SignedAreaContribution(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => line.Start.X * line.End.Y - line.End.X * line.Start.Y,
        LineArcCircularArc2D arc => ArcArea(arc),
        _ => 0d
    };
    private static double ArcArea(LineArcCircularArc2D arc) { var a = arc.StartAngleRadians; var b = a + arc.SweepAngleRadians; return arc.Center.X * arc.Radius * (Math.Sin(b) - Math.Sin(a)) - arc.Center.Y * arc.Radius * (Math.Cos(b) - Math.Cos(a)) + arc.Radius * arc.Radius * (b - a); }
    private static double Length(LineArcProfileCurve2D curve) => curve switch { LineArcLineSegment2D line => Distance(line.Start, line.End), LineArcCircularArc2D arc => Math.Abs(arc.Radius * arc.SweepAngleRadians), _ => 0d };
    private static ArrangementPointLocation PointInLoop(IReadOnlyList<LineArcProfileCurve2D> curves, (double X, double Y) point)
    {
        if (curves.Any(curve => OnCurve(curve, point, out _))) return ArrangementPointLocation.OnBoundary;
        // A horizontal ray to +X; half-open endpoint ownership avoids ray-through-vertex double counting.
        var crossings = 0;
        foreach (var curve in curves)
        {
            if (curve is LineArcLineSegment2D line) crossings += RayLine(point, line);
            else if (curve is LineArcCircularArc2D arc) crossings += RayArc(point, arc);
        }
        return crossings % 2 == 1 ? ArrangementPointLocation.Inside : ArrangementPointLocation.Outside;
    }
    private static int RayLine((double X, double Y) p, LineArcLineSegment2D line)
    { var a = line.Start; var b = line.End; if ((a.Y > p.Y) == (b.Y > p.Y)) return 0; var x = a.X + (p.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y); return x > p.X + Tol ? 1 : 0; }
    private static int RayArc((double X, double Y) p, LineArcCircularArc2D arc)
    {
        var dy = p.Y - arc.Center.Y; if (Math.Abs(dy) >= arc.Radius - Tol) return 0; var dx = Math.Sqrt(Math.Max(0d, arc.Radius * arc.Radius - dy * dy)); var count = 0;
        foreach (var x in new[] { arc.Center.X - dx, arc.Center.X + dx }) if (x > p.X + Tol && OnCurve(arc, (x, p.Y), out var t) && t > Tol && t <= 1d + Tol) count++;
        return count;
    }
    private static string GeometryKey(LineArcProfileCurve2D curve)
    {
        var (a, b) = Ends(curve); return curve switch
        {
            LineArcLineSegment2D => $"L:{VertexKey(a)}:{VertexKey(b)}",
            LineArcCircularArc2D arc => $"A:{VertexKey(arc.Center)}:{arc.Radius:R}:{a.X:R}:{a.Y:R}:{b.X:R}:{b.Y:R}:{Math.Sign(arc.SweepAngleRadians)}",
            _ => throw new NotSupportedException()
        };
    }
    private static string VertexKey((double X, double Y) p) => $"{Math.Round(p.X / Tol):F0},{Math.Round(p.Y / Tol):F0}";
    private static double Cross((double X, double Y) a, (double X, double Y) b) => a.X * b.Y - a.Y * b.X;
    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}

public enum ArrangementPointLocation { Outside, Inside, OnBoundary, AmbiguousWithinTolerance }
