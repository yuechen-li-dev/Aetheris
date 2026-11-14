using Aetheris.Kernel.Core.Judgment;

namespace Aetheris.Kernel.Firmament.Drawing;

/// <summary>Where an author wants one balloon: which side of its view it sits on, or exactly where (sheet millimetres
/// from the top-left corner). Either may be absent; absent fields are placed automatically.</summary>
internal sealed record DrawingLabelPlacement(DrawingBalloonSide? Side, DrawingPoint2? At);

internal enum DrawingBalloonSide { Left, Right, Above, Below }

/// <summary>
/// Places requirement balloons around one view. Balloons sit on rings around the view's geometry, so they never cover
/// it. Each takes the free slot nearest the direction of its anchor from the view centre, which keeps leaders short and
/// in angular order (orderly leaders do not cross); a slot's cost also counts crossings with leaders already drawn and
/// with model edges. Remaining crossings are removed by swapping pairs of balloons. Authored placements are honoured
/// first: a side restricts the slots, a position fixes the balloon.
/// </summary>
internal static class DrawingBalloonLayout
{
    private static readonly Lazy<DrawingInterFont> Inter = new(DrawingInterFont.Load);
    public const double TextSize = 3.2;
    private const double Gap = 1.4;

    internal sealed record Request(string Reference, string Display, DrawingAnnotationKind Kind, string Provenance, bool Datum, DrawingLabelPlacement? Placement)
    {
        /// <summary>The anchor lies inside a surface seen face-on: the leader ends in a dot.</summary>
        public bool SurfaceAnchor { get; init; }
    }

    public static (IReadOnlyList<DrawingAnnotationIr> Annotations, IReadOnlyList<string> Diagnostics) Place(DrawingViewIr view,
        IReadOnlyList<Request> requests, IReadOnlyList<DrawingRect> keepouts, IReadOnlyList<DrawingDimensionLineIr> keepoutStrokes)
    {
        var diagnostics = new List<string>();
        var g = view.GeometryBounds; var center = g.Center;
        var radius = Math.Max(3.0, requests.Select(r => Inter.Value.MeasureMillimetres(r.Display, TextSize)).DefaultIfEmpty(0).Max() / 2 + 1.3);
        var pitch = 2 * radius + Gap;
        // Slots on up to four rings around the geometry, clear of the reference envelope dimensions.
        var slots = new List<(DrawingPoint2 Point, int Ring)>();
        for (var ring = 0; ring < 4; ring++)
        {
            var inset = 7 + radius + ring * pitch;
            var r = new DrawingRect(g.X - inset, g.Y - inset, g.Width + 2 * inset, g.Height + 2 * inset);
            var perimeter = 2 * (r.Width + r.Height);
            var count = (int)Math.Floor(perimeter / pitch);
            for (var k = 0; k < count; k++) slots.Add((PerimeterPoint(r, k * perimeter / count), ring));
        }
        // The square around a balloon: the circle, its number, and a datum box all fit inside it.
        DrawingRect Box(DrawingPoint2 c) => new(c.X - radius, c.Y - radius, 2 * radius, 2 * radius);
        bool Inside(DrawingRect box) => box.X >= view.Viewport.X + 1 && box.Right <= view.Viewport.Right - 1 && box.Y >= view.Viewport.Y + 7 && box.Bottom <= view.Viewport.Bottom - 1;
        bool Clear(DrawingRect box) => !keepouts.Any(k => Pad(k).Intersects(box)) && !keepoutStrokes.Any(s => DrawingAnnotationLayout.SegmentIntersects(s.Start, s.End, Pad(box)))
            && !view.Primitives.Any(p => Segments(p.Points).Any(e => DrawingAnnotationLayout.SegmentIntersects(e.A, e.B, box)));
        var usable = slots.Where(s => Inside(Box(s.Point)) && Clear(Box(s.Point))).ToList();

        double Angle(DrawingPoint2 p) => Math.Atan2(p.Y - center.Y, p.X - center.X);
        var anchors = requests.ToDictionary(r => r.Reference, r => view.SemanticAnchors.GetValueOrDefault(r.Reference, center), StringComparer.Ordinal);
        var placed = new List<(Request Request, DrawingPoint2 Center, DrawingPoint2 Anchor)>();
        IReadOnlyList<DrawingPoint2> Leader(DrawingPoint2 anchor, DrawingPoint2 c)
        {
            var dx = anchor.X - c.X; var dy = anchor.Y - c.Y; var l = Math.Sqrt(dx * dx + dy * dy);
            return l < 1e-9 ? [anchor, c] : [anchor, new(c.X + dx / l * radius, c.Y + dy / l * radius)];
        }
        int Crossings(IReadOnlyList<DrawingPoint2> leader, IEnumerable<IReadOnlyList<DrawingPoint2>> others) =>
            others.Count(o => Crosses(leader[0], leader[^1], o[0], o[^1]));
        int EdgeCrossings(IReadOnlyList<DrawingPoint2> leader) =>
            view.Primitives.Sum(p => Segments(p.Points).Count(e => Crosses(leader[0], leader[^1], e.A, e.B)));
        bool Free(DrawingPoint2 c, DrawingPoint2 anchor)
        {
            var box = Box(c); var leader = Leader(anchor, c);
            return placed.All(p => Distance(p.Center, c) >= 2 * radius + Gap * .5
                && !DrawingAnnotationLayout.SegmentIntersects(leader[0], leader[^1], Pad(Box(p.Center)))
                && !DrawingAnnotationLayout.SegmentIntersects(p.Anchor, Leader(p.Anchor, p.Center)[^1], Pad(box)))
                && !keepouts.Any(k => DrawingAnnotationLayout.SegmentIntersects(leader[0], leader[^1], Pad(k)));
        }
        bool OnSide(DrawingPoint2 c, DrawingBalloonSide side) => side switch
        {
            DrawingBalloonSide.Left => c.X < g.X, DrawingBalloonSide.Right => c.X > g.Right,
            DrawingBalloonSide.Above => c.Y < g.Y, _ => c.Y > g.Bottom
        };

        // Authored positions first; then authored sides; then everything else in angular order around the view.
        foreach (var request in requests.Where(r => r.Placement?.At is not null))
        {
            var at = request.Placement!.At!.Value;
            if (!Inside(Box(at))) diagnostics.Add($"drawing-label-outside-view: '{request.Reference}' at ({at.X:0.#}, {at.Y:0.#}) mm is outside view '{view.Identity}'.");
            placed.Add((request, at, anchors[request.Reference]));
        }
        var ordered = requests.Where(r => r.Placement?.At is null)
            .OrderBy(r => r.Placement?.Side is null ? 1 : 0).ThenBy(r => Angle(anchors[r.Reference])).ToArray();
        foreach (var request in ordered)
        {
            var anchor = anchors[request.Reference]; var theta = Angle(anchor);
            var candidates = usable.Where(s => request.Placement?.Side is not { } side || OnSide(s.Point, side)).Where(s => Free(s.Point, anchor)).ToArray();
            var judgment = new JudgmentEngine<int>().Evaluate(0, candidates.Select((s, i) =>
            {
                var leader = Leader(anchor, s.Point);
                var turn = Math.Abs(Math.IEEERemainder(Angle(s.Point) - theta, 2 * Math.PI));
                var cost = turn * 30 + s.Ring * 12 + Distance(anchor, s.Point) * .15
                    + Crossings(leader, placed.Select(p => Leader(p.Anchor, p.Center))) * 200 + EdgeCrossings(leader) * 4;
                return new JudgmentCandidate<int>($"slot:{i}", _ => true, _ => -cost, TieBreakerPriority: i);
            }).ToArray());
            if (!judgment.IsSuccess)
            {
                diagnostics.Add(FormattableString.Invariant($"{FirmamentDrawingCompiler.DrawingLayoutImpossible}: balloon '{request.Display}' ({request.Reference}) anchored at ({anchor.X:0.#}, {anchor.Y:0.#}) mm has no free slot around view '{view.Identity}' (geometry {g.X:0.#}..{g.Right:0.#} x {g.Y:0.#}..{g.Bottom:0.#} mm)")
                    + (request.Placement?.Side is { } s0 ? $" on its authored side {s0}." : "."));
                continue;
            }
            var chosen = candidates[int.Parse(judgment.Selection!.Value.Candidate.Name["slot:".Length..], System.Globalization.CultureInfo.InvariantCulture)];
            placed.Add((request, chosen.Point, anchor));
        }

        // Swap pairs of automatically placed balloons whose leaders cross when the swap removes the crossing.
        for (var pass = 0; pass < 6; pass++)
        {
            var changed = false;
            for (var i = 0; i < placed.Count; i++)
                for (var j = i + 1; j < placed.Count; j++)
                {
                    var a = placed[i]; var b = placed[j];
                    if (a.Request.Placement is not null || b.Request.Placement is not null) continue;
                    if (!Crosses(a.Anchor, a.Center, b.Anchor, b.Center)) continue;
                    if (Crosses(a.Anchor, b.Center, b.Anchor, a.Center)) continue;
                    var others = placed.Where((_, k) => k != i && k != j).ToArray();
                    bool Clean(DrawingPoint2 anchor, DrawingPoint2 c) => others.All(o => !DrawingAnnotationLayout.SegmentIntersects(anchor, Leader(anchor, c)[^1], Pad(Box(o.Center))));
                    if (!Clean(a.Anchor, b.Center) || !Clean(b.Anchor, a.Center)) continue;
                    placed[i] = a with { Center = b.Center }; placed[j] = b with { Center = a.Center }; changed = true;
                }
            if (!changed) break;
        }

        var annotations = placed.Select(p =>
        {
            var width = Inter.Value.MeasureMillimetres(p.Request.Display, TextSize) + 2;
            var body = p.Request.Datum ? new DrawingRect(p.Center.X - radius, p.Center.Y - 3, 2 * radius, 6) : new DrawingRect(p.Center.X - width / 2, p.Center.Y - 3, width, 6);
            var leader = p.Request.Datum ? BoxLeader(p.Anchor, body) : Leader(p.Anchor, p.Center);
            var candidate = new DrawingAnnotationCandidateIr($"balloon:{p.Request.Reference}", body, leader,
                p.Request.Placement is null ? "balloon-ring" : "authored", 0, 0, []);
            return new DrawingAnnotationIr($"annotation:{view.Identity}:{p.Request.Reference}", p.Request.Reference, view.Identity, p.Request.Kind,
                p.Request.Display, p.Anchor, candidate, [candidate], p.Request.Provenance)
            { Shape = p.Request.Datum ? DrawingAnnotationShape.DatumFeature : DrawingAnnotationShape.Balloon, BalloonRadius = p.Request.Datum ? 0 : radius,
                DotAnchor = p.Request.SurfaceAnchor };
        }).ToArray();
        return (annotations, diagnostics);
    }

    private static IReadOnlyList<DrawingPoint2> BoxLeader(DrawingPoint2 anchor, DrawingRect box)
    {
        var c = box.Center; var dx = anchor.X - c.X; var dy = anchor.Y - c.Y;
        var t = Math.Min(Math.Abs(dx) > 1e-9 ? box.Width / 2 / Math.Abs(dx) : double.MaxValue, Math.Abs(dy) > 1e-9 ? box.Height / 2 / Math.Abs(dy) : double.MaxValue);
        return [anchor, new(c.X + dx * Math.Min(1, t), c.Y + dy * Math.Min(1, t))];
    }

    private static DrawingPoint2 PerimeterPoint(DrawingRect r, double s)
    {
        if (s < r.Width) return new(r.X + s, r.Y); s -= r.Width;
        if (s < r.Height) return new(r.Right, r.Y + s); s -= r.Height;
        if (s < r.Width) return new(r.Right - s, r.Bottom); s -= r.Width;
        return new(r.X, r.Bottom - s);
    }

    private static double Distance(DrawingPoint2 a, DrawingPoint2 b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private static DrawingRect Pad(DrawingRect r) => new(r.X - .7, r.Y - .7, r.Width + 1.4, r.Height + 1.4);
    private static IEnumerable<(DrawingPoint2 A, DrawingPoint2 B)> Segments(IReadOnlyList<DrawingPoint2> points)
    { for (var i = 1; i < points.Count; i++) yield return (points[i - 1], points[i]); }

    private static bool Crosses(DrawingPoint2 a, DrawingPoint2 b, DrawingPoint2 c, DrawingPoint2 d)
    {
        static double Side(DrawingPoint2 p, DrawingPoint2 q, DrawingPoint2 r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        return Side(a, b, c) * Side(a, b, d) < -1e-8 && Side(c, d, a) * Side(c, d, b) < -1e-8;
    }

    private static bool SegmentHitsCircle(DrawingPoint2 a, DrawingPoint2 b, DrawingPoint2 c, double r)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y; var l2 = dx * dx + dy * dy;
        var t = l2 < 1e-12 ? 0 : Math.Clamp(((c.X - a.X) * dx + (c.Y - a.Y) * dy) / l2, 0, 1);
        return Distance(new(a.X + t * dx, a.Y + t * dy), c) < r;
    }
}
