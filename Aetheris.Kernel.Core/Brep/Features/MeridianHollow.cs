using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry.Planar;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Brep.Features;

/// <summary>
/// The wall meridian of a hollow solid of revolution. A surface of revolution's normal lies in its meridian plane, so
/// the 3D offset of the solid is the revolution of the 2D offset of its full axial section. The section is the
/// meridian and its mirror across the axis; its exact inset (<see cref="LineArcInset"/>) is the cavity, which is cut
/// back to the meridian half-plane. An open end cap is pushed outward before the inset, so the cavity runs through it,
/// and the cavity is then cut at the cap's plane. The wall is the meridian less the cavity.
/// </summary>
public static class MeridianHollow
{
    private const double Tol = 1e-7;

    /// <summary>
    /// <paramref name="exterior"/> is a closed meridian (x = radius ≥ 0, y = axial position) with one span on the axis,
    /// whose ends are flat caps where they are opened.
    /// </summary>
    public static KernelResult<IReadOnlyList<PlanarSpan>> Wall(IReadOnlyList<PlanarSpan> exterior, double wallThickness, bool openTop, bool openBottom)
    {
        var t = wallThickness;
        if (!(t > 0d) || !double.IsFinite(t)) return Failure("hollow-wall-thickness-must-be-positive", "The wall must be positive.");
        if (!openTop && !openBottom)
            return Failure("hollow-closed-vessel-unsupported", "A closed hollow needs an inner void shell, which is not built yet; open Top or Bottom.");
        var loop = LineArcInset.SignedArea(exterior) > 0d ? exterior.ToList() : LineArcInset.Reverse(exterior).ToList();
        var axisSpans = Enumerable.Range(0, loop.Count).Where(i => loop[i] is PlanarLine line && OnAxis(line.Start) && OnAxis(line.End)).ToArray();
        if (axisSpans.Length != 1)
            return Failure("hollow-revolve-meridian-invalid", "The meridian must close along the axis with exactly one span.");
        if (loop.Any(s => LineArcInset.StartPoint(s).X < -Tol))
            return Failure("hollow-revolve-meridian-invalid", "The meridian crosses the axis.");
        // Rotate so the chain runs from the bottom of the axis span, away from the axis, back to its top.
        var chain = loop.Skip(axisSpans[0] + 1).Concat(loop.Take(axisSpans[0])).ToList();
        var bottomZ = LineArcInset.StartPoint(chain[0]).Y;
        var topZ = LineArcInset.EndPoint(chain[^1]).Y;
        var allZ = chain.SelectMany(s => new[] { LineArcInset.StartPoint(s).Y, LineArcInset.EndPoint(s).Y }).ToArray();

        var extended = chain.ToList();
        var reach = 2d * t;
        if (openTop)
        {
            if (!IsCap(chain[^1]) || allZ.Max() > topZ + Tol || chain.Count < 2 || chain[^2] is not PlanarLine side || side.End.Y - side.Start.Y <= Tol)
                return Failure("hollow-opening-needs-flat-cap", "An open top must be a flat cap at the highest point, reached by a straight wall rising to it.");
            var end = Along(side, topZ + reach);
            extended[^2] = new PlanarLine(side.Start, end);
            extended[^1] = new PlanarLine(end, (0d, topZ + reach));
        }
        if (openBottom)
        {
            if (!IsCap(chain[0]) || allZ.Min() < bottomZ - Tol || chain.Count < 2 || extended[1] is not PlanarLine side || side.End.Y - side.Start.Y <= Tol)
                return Failure("hollow-opening-needs-flat-cap", "An open bottom must be a flat cap at the lowest point, left by a straight wall rising from it.");
            var start = Along(side, bottomZ - reach);
            extended[1] = new PlanarLine(start, side.End);
            extended[0] = new PlanarLine((0d, bottomZ - reach), start);
        }

        // The full axial section: the meridian and its mirror, closed without the axis.
        var mirrored = LineArcInset.Reverse(extended.Select(Mirror).ToArray());
        var section = extended.Concat(mirrored).ToArray();
        var diagnostics = new List<string>();
        var cavity = LineArcInset.InsetLoop(section, t, outer: true, "revolve", diagnostics);
        if (cavity is null)
            return Failure("hollow-wall-offset-collapse", string.Join("; ", diagnostics));
        var clips = new HashSet<PlanarSpan>(ReferenceEqualityComparer.Instance);
        IReadOnlyList<PlanarSpan>? half = cavity;
        if (openTop) half = Clip(half, vertical: false, topZ, keepBelow: true, clips);
        if (half is not null && openBottom) half = Clip(half, vertical: false, bottomZ, keepBelow: false, clips);
        if (half is not null) half = Clip(half, vertical: true, 0d, keepBelow: false, clips);
        if (half is null)
            return Failure("hollow-wall-offset-collapse", "The cavity does not meet the axis and the openings in one piece each.");

        var inner = half.Where(s => !clips.Contains(s) && !(s is PlanarLine l && OnAxis(l.Start) && OnAxis(l.End))).ToList();
        var outer = chain.ToList();
        if (openTop) outer.RemoveAt(outer.Count - 1);
        if (openBottom) outer.RemoveAt(0);
        var ordered = Chain(inner);
        if (ordered is null) return Failure("hollow-wall-offset-collapse", "The cavity wall is not one chain.");
        var wall = new List<PlanarSpan>(outer);
        AddLine(wall, LineArcInset.EndPoint(outer[^1]), LineArcInset.EndPoint(ordered[^1]));
        wall.AddRange(LineArcInset.Reverse(ordered));
        AddLine(wall, LineArcInset.StartPoint(ordered[0]), LineArcInset.StartPoint(outer[0]));
        return KernelResult<IReadOnlyList<PlanarSpan>>.Success(wall);
    }

    private static bool OnAxis((double X, double Y) p) => double.Abs(p.X) <= Tol;

    private static bool IsCap(PlanarSpan span) => span is PlanarLine line && double.Abs(line.Start.Y - line.End.Y) <= Tol
        && (OnAxis(line.Start) || OnAxis(line.End));

    private static (double X, double Y) Along(PlanarLine line, double y)
    {
        var s = (y - line.Start.Y) / (line.End.Y - line.Start.Y);
        return (line.Start.X + (line.End.X - line.Start.X) * s, y);
    }

    private static PlanarSpan Mirror(PlanarSpan span) => span switch
    {
        PlanarLine line => new PlanarLine((-line.Start.X, line.Start.Y), (-line.End.X, line.End.Y)),
        PlanarArc arc => new PlanarArc((-arc.Center.X, arc.Center.Y), arc.Radius, double.Pi - arc.StartAngle, -arc.Sweep),
        _ => throw new NotSupportedException()
    };

    private static void AddLine(List<PlanarSpan> spans, (double X, double Y) from, (double X, double Y) to)
    {
        if (double.Hypot(to.X - from.X, to.Y - from.Y) > Tol) spans.Add(new PlanarLine(from, to));
    }

    private static List<PlanarSpan>? Chain(List<PlanarSpan> spans)
    {
        if (spans.Count == 0) return null;
        var starts = spans.Where(s => !spans.Any(o => !ReferenceEquals(o, s) && Close(LineArcInset.EndPoint(o), LineArcInset.StartPoint(s)))).ToArray();
        if (starts.Length != 1) return null;
        var ordered = new List<PlanarSpan> { starts[0] };
        while (ordered.Count < spans.Count)
        {
            var end = LineArcInset.EndPoint(ordered[^1]);
            var next = spans.FirstOrDefault(s => !ordered.Contains(s) && Close(LineArcInset.StartPoint(s), end));
            if (next is null) return null;
            ordered.Add(next);
        }
        return ordered;
    }

    private static bool Close((double X, double Y) a, (double X, double Y) b) => double.Hypot(a.X - b.X, a.Y - b.Y) <= 1e-6;

    /// <summary>
    /// Keeps the part of a closed loop on one side of a vertical (x = value) or horizontal (y = value) line, closing it
    /// along the line. The loop must cross the line exactly twice, or not at all on the kept side.
    /// </summary>
    private static IReadOnlyList<PlanarSpan>? Clip(IReadOnlyList<PlanarSpan> loop, bool vertical, double value, bool keepBelow, HashSet<PlanarSpan> clips)
    {
        double Coordinate((double X, double Y) p) => vertical ? p.X : p.Y;
        bool Kept((double X, double Y) p) => keepBelow ? Coordinate(p) < value : Coordinate(p) > value;
        var pieces = new List<PlanarSpan>();
        foreach (var span in loop)
        {
            var split = Split(span, vertical, value).ToArray();
            // Pieces of an earlier closing segment are closing segments too.
            if (clips.Contains(span)) foreach (var piece in split) clips.Add(piece);
            pieces.AddRange(split);
        }
        var keep = pieces.Select(p => Kept(Mid(p))).ToArray();
        if (keep.All(k => k)) return loop;
        if (!keep.Any(k => k)) return null;
        // Rotate so the kept run starts the list; one run means exactly two crossings.
        var first = Enumerable.Range(0, pieces.Count).First(i => keep[i] && !keep[(i + pieces.Count - 1) % pieces.Count]);
        var rotated = pieces.Skip(first).Concat(pieces.Take(first)).ToArray();
        var kept = rotated.Zip(keep.Skip(first).Concat(keep.Take(first))).ToArray();
        var run = kept.TakeWhile(p => p.Second).Select(p => p.First).ToList();
        if (kept.Skip(run.Count).Any(p => p.Second)) return null;
        var closing = new PlanarLine(LineArcInset.EndPoint(run[^1]), LineArcInset.StartPoint(run[0]));
        clips.Add(closing);
        run.Add(closing);
        return run;
    }

    private static (double X, double Y) Mid(PlanarSpan span) => span switch
    {
        PlanarLine line => ((line.Start.X + line.End.X) / 2d, (line.Start.Y + line.End.Y) / 2d),
        PlanarArc arc => (arc.Center.X + arc.Radius * double.Cos(arc.StartAngle + arc.Sweep / 2d), arc.Center.Y + arc.Radius * double.Sin(arc.StartAngle + arc.Sweep / 2d)),
        _ => throw new NotSupportedException()
    };

    private static IEnumerable<PlanarSpan> Split(PlanarSpan span, bool vertical, double value)
    {
        switch (span)
        {
            case PlanarLine line:
            {
                var a = vertical ? line.Start.X : line.Start.Y;
                var b = vertical ? line.End.X : line.End.Y;
                if ((a - value) * (b - value) < 0d && double.Abs(a - value) > Tol && double.Abs(b - value) > Tol)
                {
                    var s = (value - a) / (b - a);
                    var p = (line.Start.X + (line.End.X - line.Start.X) * s, line.Start.Y + (line.End.Y - line.Start.Y) * s);
                    yield return new PlanarLine(line.Start, p);
                    yield return new PlanarLine(p, line.End);
                }
                else yield return line;
                break;
            }
            case PlanarArc arc:
            {
                var c = vertical ? arc.Center.X : arc.Center.Y;
                var ratio = (value - c) / arc.Radius;
                var cuts = new List<double>();
                if (double.Abs(ratio) < 1d)
                {
                    var basis = vertical ? double.Acos(ratio) : double.Asin(ratio);
                    var candidates = vertical ? new[] { basis, -basis } : new[] { basis, double.Pi - basis };
                    foreach (var angle in candidates)
                        for (var k = -2; k <= 2; k++)
                        {
                            var offset = (angle + 2d * double.Pi * k - arc.StartAngle) / arc.Sweep;
                            if (offset > 1e-9 && offset < 1d - 1e-9) cuts.Add(offset);
                        }
                }
                var bounds = cuts.Distinct().Order().Prepend(0d).Append(1d).ToArray();
                for (var i = 0; i + 1 < bounds.Length; i++)
                    yield return new PlanarArc(arc.Center, arc.Radius, arc.StartAngle + arc.Sweep * bounds[i], arc.Sweep * (bounds[i + 1] - bounds[i]));
                break;
            }
        }
    }

    private static KernelResult<IReadOnlyList<PlanarSpan>> Failure(string code, string message) =>
        KernelResult<IReadOnlyList<PlanarSpan>>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, $"{code}: {message}", "MeridianHollow")]);
}
