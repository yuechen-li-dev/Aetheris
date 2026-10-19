using Aetheris.Kernel.Core.Judgment;

namespace Aetheris.Kernel.Firmament.Drawing;

internal static class DrawingAnnotationLayout
{
    private static readonly Lazy<DrawingInterFont> Inter = new(DrawingInterFont.Load);
    internal sealed record Result(IReadOnlyList<DrawingPageIr> Pages, DrawingLayoutEvidenceIr Evidence, IReadOnlyList<string> Diagnostics);

    public static Result Layout(IReadOnlyList<DrawingPageIr> pages, IReadOnlyDictionary<string, FirmamentDrawingCompiler.PmiPresentation> pmi)
    {
        var laneOccupancy = new Dictionary<string, int>(StringComparer.Ordinal);
        var rejected = 0; var failed = 0; var before = 0;
        var output = new List<DrawingPageIr>();
        var diagnostics = new List<string>();

        foreach (var page in pages)
        {
            // Page coordinates are independent. Titles, notes and information blocks are real keepouts.
            var occupied = new List<DrawingRect>();
            occupied.AddRange(page.Views.Select(v => new DrawingRect(v.Viewport.X, v.Viewport.Y, v.Viewport.Width, 6)));
            occupied.AddRange((page.LocatedNotes ?? []).Select(n => n.Bounds));
            if (page.InformationBlock is { } info) occupied.Add(info.Bounds);
            var annotations = new List<DrawingAnnotationIr>(page.Annotations);
            occupied.AddRange(annotations.Select(a => a.SelectedCandidate.Body));
            foreach (var seed in annotations)
            {
                var owner = page.Views.Single(v => v.Identity == seed.AssignedView);
                if (!Contains(owner.Viewport, seed.SelectedCandidate.Body) || page.Views.Any(v => CollidesModel(seed.SelectedCandidate.Body, v)))
                {
                    failed++;
                    diagnostics.Add($"{FirmamentDrawingCompiler.DrawingLayoutImpossible}: Reference measurement '{seed.Identity}' exceeds its sheet allocation or overlaps geometry.");
                }
            }
            foreach (var view in page.Views)
            {
                foreach (var reference in view.AssignedPmi)
                {
                    var semantic = pmi[reference];
                    var anchor = view.SemanticAnchors.GetValueOrDefault(reference,
                        new(view.GeometryBounds.X + view.GeometryBounds.Width / 2, view.GeometryBounds.Y + view.GeometryBounds.Height / 2));
                    if (semantic.Kind == DrawingAnnotationKind.Datum && semantic.Provenance == "Firmament Semantic PMI")
                        anchor = new(view.GeometryBounds.X + view.GeometryBounds.Width / 2, view.GeometryBounds.Bottom);
                    var candidates = GenerateCandidates(view, semantic, anchor);
                    before += CollidesModel(candidates[0].Body, view) ? 1 : 0;
                    DrawingAnnotationCandidateIr? selected = null;
                    var evaluated = new List<DrawingAnnotationCandidateIr>();
                    foreach (var candidate in candidates)
                    {
                        var rejections = new List<string>();
                        if (!Contains(view.Viewport, candidate.Body)) rejections.Add("outside-view-allocation");
                        if (page.Views.Any(v => CollidesModel(candidate.Body, v))) rejections.Add("text-model-overlap");
                        if (occupied.Any(rect => Pad(rect).Intersects(candidate.Body))) rejections.Add("text-text-overlap");
                        if (annotations.Any(a => a.Strokes.Any(s => SegmentIntersects(s.Start, s.End, Pad(candidate.Body))))) rejections.Add("text-leader-overlap");
                        if (occupied.Any(rect => Segments(candidate.Leader).Any(s => SegmentIntersects(s.A, s.B, Pad(rect))))) rejections.Add("leader-text-overlap");
                        var crossings = annotations.Sum(a => a.Strokes.Sum(old => Segments(candidate.Leader).Count(next => Crosses(old.Start, old.End, next.A, next.B))));
                        var modelCrossings = page.Views.SelectMany(v => v.Primitives).Sum(primitive =>
                            Segments(primitive.Points).Sum(edge => Segments(candidate.Leader).Count(leader => Crosses(edge.A, edge.B, leader.A, leader.B))));
                        var current = candidate with { Rejections = rejections, Cost = candidate.Cost + crossings * 100 + modelCrossings * 20 };
                        evaluated.Add(current);
                        if (rejections.Count > 0) rejected++;
                    }
                    var judgment = new JudgmentEngine<int>().Evaluate(0, evaluated.Select((c, i) =>
                        new JudgmentCandidate<int>(c.Identity, _ => c.Rejections.Count == 0, _ => -c.Cost,
                            _ => string.Join(",", c.Rejections), i)).ToArray());
                    if (judgment.IsSuccess) selected = evaluated.Single(c => c.Identity == judgment.Selection!.Value.Candidate.Name);
                    if (selected is null)
                    {
                        failed++;
                        diagnostics.Add($"{FirmamentDrawingCompiler.DrawingLayoutImpossible}: '{reference}' in view '{view.Identity}', page {page.PageNumber}, zone {view.Location?.Zone ?? "unknown"}, has no collision-free bounded candidate.");
                        continue;
                    }
                    occupied.Add(selected.Body);
                    laneOccupancy[selected.Lane] = laneOccupancy.GetValueOrDefault(selected.Lane) + 1;
                    annotations.Add(new($"annotation:{view.Identity}:{reference}", reference, view.Identity, semantic.Kind,
                        semantic.Display, anchor, selected, evaluated, semantic.Provenance));
                }
            }
            output.Add(page with { Annotations = annotations });
        }

        var modelAfter = output.Sum(page => page.Annotations.Count(a => page.Views.Any(v => CollidesModel(a.SelectedCandidate.Body, v))));
        var textAfter = output.Sum(page => page.Annotations.SelectMany((a, i) => page.Annotations.Skip(i + 1).Select(b => a.SelectedCandidate.Body.Intersects(b.SelectedCandidate.Body))).Count(c => c));
        var leaderAfter = output.Sum(page => page.Annotations.Sum(a => page.Annotations.Where(b => b.Identity != a.Identity)
            .Count(b => b.Strokes.Any(s => SegmentIntersects(s.Start, s.End, Pad(a.SelectedCandidate.Body))))));
        if (modelAfter + textAfter + leaderAfter > 0)
            diagnostics.Add($"{FirmamentDrawingCompiler.DrawingLayoutImpossible}: Final layout audit found text/model={modelAfter}, text/text={textAfter}, text/stroke={leaderAfter} overlaps.");
        var evidence = new DrawingLayoutEvidenceIr(output.Sum(page => page.Annotations.Count), before, modelAfter, textAfter,
            laneOccupancy, rejected, failed) { TextLeaderCollisionsAfter = leaderAfter };
        return new(output, evidence, diagnostics);
    }

    private static IReadOnlyList<DrawingAnnotationCandidateIr> GenerateCandidates(
        DrawingViewIr view, FirmamentDrawingCompiler.PmiPresentation pmi, DrawingPoint2 anchor)
    {
        // The same embedded Inter advance widths drive both layout and native PDF text.
        var width = Math.Max(18, Inter.Value.MeasureMillimetres(pmi.Display, 3.2) + 3);
        const double height = 6;
        var bounds = view.GeometryBounds;
        var candidates = new List<DrawingAnnotationCandidateIr>();
        var ordinal = 0;
        void Add(string lane, int laneIndex, double x, double y)
        {
            var body = new DrawingRect(x, y, width, height);
            var attach = new DrawingPoint2(anchor.X <= body.Center.X ? body.X : body.Right, body.Center.Y);
            var elbow = new DrawingPoint2(attach.X + (attach.X == body.X ? -3 : 3), attach.Y);
            var length = Math.Sqrt(Math.Pow(anchor.X - attach.X, 2) + Math.Pow(anchor.Y - attach.Y, 2));
            var edgePenalty = Math.Min(Math.Min(body.X - view.Viewport.X, view.Viewport.Right - body.Right),
                Math.Min(body.Y - view.Viewport.Y, view.Viewport.Bottom - body.Bottom)) < 3 ? 20 : 0;
            candidates.Add(new($"candidate:{ordinal++}:{lane}:{laneIndex}", body, [anchor, elbow, attach], lane, laneIndex,
                length + laneIndex * 8 + edgePenalty + ordinal * 1e-5, []));
        }

        if (pmi.Kind is DrawingAnnotationKind.DiameterDimension or DrawingAnnotationKind.RadiusDimension or DrawingAnnotationKind.Datum or DrawingAnnotationKind.FeatureControlFrame)
        {
            Add("free-ne", 0, bounds.Right + 5, bounds.Y - 10);
            Add("free-nw", 0, bounds.X - width - 5, bounds.Y - 10);
            Add("free-se", 0, bounds.Right + 5, bounds.Bottom + 4);
            Add("free-sw", 0, bounds.X - width - 5, bounds.Bottom + 4);
        }
        for (var lane = 1; lane <= 3; lane++)
        {
            Add("top", lane, bounds.X + (bounds.Width - width) / 2, bounds.Y - lane * 8);
            Add("bottom", lane, bounds.X + (bounds.Width - width) / 2, bounds.Bottom + 2 + (lane - 1) * 8);
            Add("left", lane, bounds.X - width - 3 - (lane - 1) * 8, bounds.Y + (bounds.Height - height) / 2);
            Add("right", lane, bounds.Right + 3 + (lane - 1) * 8, bounds.Y + (bounds.Height - height) / 2);
        }
        // Deterministic callout-column slots support dense PMI without reducing text size.
        for (var row = 0; row < 16; row++)
        {
            Add("column-right", row, view.Viewport.Right - width - 2, view.Viewport.Y + 9 + row * 8);
            Add("column-left", row, view.Viewport.X + 2, view.Viewport.Y + 9 + row * 8);
        }
        return candidates.OrderBy(candidate => candidate.Cost).ThenBy(candidate => candidate.Identity, StringComparer.Ordinal).ToArray();
    }

    private static bool Contains(DrawingRect outer, DrawingRect inner) => inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

    private static DrawingRect Pad(DrawingRect r) => new(r.X - .7, r.Y - .7, r.Width + 1.4, r.Height + 1.4);
    private static IEnumerable<(DrawingPoint2 A, DrawingPoint2 B)> Segments(IReadOnlyList<DrawingPoint2> points)
    { for (var i = 1; i < points.Count; i++) yield return (points[i - 1], points[i]); }

    private static bool Crosses(DrawingPoint2 a, DrawingPoint2 b, DrawingPoint2 c, DrawingPoint2 d)
    {
        static double Side(DrawingPoint2 p, DrawingPoint2 q, DrawingPoint2 r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        return Side(a, b, c) * Side(a, b, d) < -1e-8 && Side(c, d, a) * Side(c, d, b) < -1e-8;
    }

    internal static bool SegmentIntersects(DrawingPoint2 a, DrawingPoint2 b, DrawingRect rect)
    {
        double t0 = 0, t1 = 1;
        bool Clip(double p, double q)
        {
            if (Math.Abs(p) < 1e-12) return q >= 0;
            var r = q / p;
            if (p < 0) { if (r > t1) return false; t0 = Math.Max(t0, r); }
            else { if (r < t0) return false; t1 = Math.Min(t1, r); }
            return true;
        }
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        return Clip(-dx, a.X - rect.X) && Clip(dx, rect.Right - a.X) && Clip(-dy, a.Y - rect.Y) && Clip(dy, rect.Bottom - a.Y);
    }

    private static bool CollidesModel(DrawingRect body, DrawingViewIr view) => view.Primitives.Any(primitive =>
        Segments(primitive.Points).Any(s => SegmentIntersects(s.A, s.B, Pad(body))));
}
