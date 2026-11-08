using Aetheris.Kernel.Core.Geometry.Planar;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Exact inward offset of a line/arc material region: every point of the result lies at least the inset distance
/// from the region's boundary, and the result's boundary lies exactly at that distance. Lines stay parallel lines;
/// arcs stay concentric; a reflex corner rounds with the inset distance about the corner, which keeps the wall
/// constant; a convex arc no larger than the inset vanishes and its neighbours meet at a sharp corner. Any other
/// collapse (a curve that reverses, a neck that closes, loops that meet) is refused with its location named.
/// </summary>
public static class ProfileInset2D
{
    public sealed record InsetResult(ResolvedProfile2D? Profile, IReadOnlyList<string> Diagnostics);

    public static InsetResult Inset(PrismaticSectionRegion region, double distance, string name, string context)
    {
        var diagnostics = new List<string>();
        var loops = new List<ResolvedProfileLoop2D>();
        var outer = InsetLoop(Curves(region.Outer), distance, outer: true, context + ".outer", diagnostics);
        if (outer is null) return new(null, diagnostics);
        loops.Add(Loop(name, "Outer", true, outer));
        for (var i = 0; i < region.Holes.Count; i++)
        {
            var hole = InsetLoop(Curves(region.Holes[i]), distance, outer: false, $"{context}.hole{i}", diagnostics);
            if (hole is null) return new(null, diagnostics);
            loops.Add(Loop(name, $"Hole{i}", false, hole));
        }
        // Loops that touch or cross would change the region's topology: a neck closed, or a hole's wall reaching the
        // outer wall. Either way the wall would be thinner than asked somewhere.
        var all = loops.Select(l => l.Segments.Select(s => s.Geometry).ToArray()).ToArray();
        for (var a = 0; a < all.Length; a++)
            for (var b = a; b < all.Length; b++)
                for (var i = 0; i < all[a].Length; i++)
                    for (var j = a == b ? i + 1 : 0; j < all[b].Length; j++)
                    {
                        if (a == b && (j == i + 1 || (i == 0 && j == all[a].Length - 1))) continue;
                        var hit = ProfileArrangementBuilder.IntersectBounded(all[a][i], all[b][j]);
                        if (hit.Intersections.Count > 0 || hit.HasBoundedOverlap)
                        {
                            diagnostics.Add($"hollow-wall-offset-collapse:{context}:the inset wall meets itself (loop {a} curve {i}, loop {b} curve {j}); a section is narrower than twice the wall");
                            return new(null, diagnostics);
                        }
                    }
        for (var h = 1; h < all.Length; h++)
            if (ProfileArrangementBuilder.PointInProfile(new ResolvedProfile2D(name, region.Outer.PlaneFrame, [loops[0]]), StartPoint(all[h][0])) != ArrangementPointLocation.Inside)
            {
                diagnostics.Add($"hollow-wall-offset-collapse:{context}: a hole's inset wall leaves the inset outer wall");
                return new(null, diagnostics);
            }
        var profile = new ResolvedProfile2D(name, region.Outer.PlaneFrame, loops, region.Outer.ConstructionPlane);
        return new(profile, diagnostics);
    }

    /// <summary>Insets one closed loop; the material lies inside an outer loop and outside a hole loop.</summary>
    public static IReadOnlyList<LineArcProfileCurve2D>? InsetLoop(IReadOnlyList<LineArcProfileCurve2D> curves, double distance, bool outer,
        string context, List<string> diagnostics)
    {
        if (curves.Any(c => c is not (LineArcLineSegment2D or LineArcCircularArc2D or LineArcFullCircle2D)))
        {
            diagnostics.Add($"hollow-offset-curve-unsupported:{context}: only lines and circular arcs have exact offsets of the same kind");
            return null;
        }
        var core = new List<string>();
        var inset = LineArcInset.InsetLoop(curves.Select(ToPlanar).ToArray(), distance, outer, context, core);
        diagnostics.AddRange(core.Select(d => d.StartsWith("inset-", StringComparison.Ordinal) ? "hollow-" + d[6..] : d));
        return inset?.Select(FromPlanar).ToArray();
    }

    public static PlanarSpan ToPlanar(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => new PlanarLine(line.Start, line.End),
        LineArcCircularArc2D arc => new PlanarArc(arc.Center, arc.Radius, arc.StartAngleRadians, arc.SweepAngleRadians),
        LineArcFullCircle2D circle => new PlanarCircle(circle.Center, circle.Radius),
        _ => throw new NotSupportedException()
    };

    public static LineArcProfileCurve2D FromPlanar(PlanarSpan span) => span switch
    {
        PlanarLine line => new LineArcLineSegment2D(line.Start, line.End),
        PlanarArc arc => new LineArcCircularArc2D(arc.Center, arc.Radius, arc.StartAngle, arc.Sweep),
        PlanarCircle circle => new LineArcFullCircle2D(circle.Center, circle.Radius),
        _ => throw new NotSupportedException()
    };

    public static double SignedArea(IReadOnlyList<LineArcProfileCurve2D> curves) => LineArcInset.SignedArea(curves.Select(ToPlanar).ToArray());

    private static ResolvedProfileLoop2D Loop(string name, string loop, bool isOuter, IReadOnlyList<LineArcProfileCurve2D> curves) =>
        new($"{name}.{loop}", isOuter, curves.Select((curve, i) => new ResolvedProfileSegment2D($"{name}.{loop}.{i}", curve,
            new ProfileSegmentProvenance($"hollow:{name}.{loop}.{i}", $"hollow:{name}", "generated", "hollow-inset", "XY"))).ToArray());

    private static IReadOnlyList<LineArcProfileCurve2D> Curves(ResolvedProfile2D profile) =>
        profile.Loops[0].Segments.Select(s => s.Geometry).ToArray();

    private static (double X, double Y) StartPoint(LineArcProfileCurve2D curve) => LineArcInset.StartPoint(ToPlanar(curve));
}
