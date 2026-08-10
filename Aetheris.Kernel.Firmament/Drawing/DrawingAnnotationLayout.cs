namespace Aetheris.Kernel.Firmament.Drawing;

internal static class DrawingAnnotationLayout
{
    private static readonly Lazy<DrawingInterFont> Inter = new(DrawingInterFont.Load);
    internal sealed record Result(IReadOnlyList<DrawingPageIr> Pages, DrawingLayoutEvidenceIr Evidence, IReadOnlyList<string> Diagnostics);

    public static Result Layout(IReadOnlyList<DrawingPageIr> pages, IReadOnlyDictionary<string, FirmamentDrawingCompiler.PmiPresentation> pmi)
    {
        var occupied = new List<DrawingRect>();
        var laneOccupancy = new Dictionary<string, int>(StringComparer.Ordinal);
        var rejected = 0; var failed = 0; var before = 0;
        var output = new List<DrawingPageIr>();
        var diagnostics = new List<string>();

        foreach (var page in pages)
        {
            var annotations = new List<DrawingAnnotationIr>();
            foreach (var view in page.Views)
            {
                foreach (var reference in view.AssignedPmi)
                {
                    var semantic = pmi[reference];
                    var anchor = view.SemanticAnchors.GetValueOrDefault(reference,
                        new(view.GeometryBounds.X + view.GeometryBounds.Width / 2, view.GeometryBounds.Y + view.GeometryBounds.Height / 2));
                    if (semantic.Kind == DrawingAnnotationKind.Datum)
                        anchor = new(view.GeometryBounds.X + view.GeometryBounds.Width / 2, view.GeometryBounds.Bottom);
                    var candidates = GenerateCandidates(view, semantic, anchor);
                    before += CollidesModel(candidates[0].Body, view) ? 1 : 0;
                    DrawingAnnotationCandidateIr? selected = null;
                    var evaluated = new List<DrawingAnnotationCandidateIr>();
                    foreach (var candidate in candidates)
                    {
                        var rejections = new List<string>();
                        if (!Contains(view.Viewport, candidate.Body)) rejections.Add("outside-view-allocation");
                        if (CollidesModel(candidate.Body, view)) rejections.Add("text-model-overlap");
                        if (occupied.Any(rect => rect.Intersects(candidate.Body))) rejections.Add("text-text-overlap");
                        var current = candidate with { Rejections = rejections };
                        evaluated.Add(current);
                        if (rejections.Count == 0 && selected is null) selected = current;
                        else if (rejections.Count > 0) rejected++;
                    }
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

        var evidence = new DrawingLayoutEvidenceIr(output.Sum(page => page.Annotations.Count), before, 0, 0,
            laneOccupancy, rejected, failed);
        return new(output, evidence, diagnostics);
    }

    private static IReadOnlyList<DrawingAnnotationCandidateIr> GenerateCandidates(
        DrawingViewIr view, FirmamentDrawingCompiler.PmiPresentation pmi, DrawingPoint2 anchor)
    {
        // The same embedded Inter advance widths drive both layout and native PDF text.
        var width = Math.Clamp(Inter.Value.MeasureMillimetres(pmi.Display, 3.2) + 3, 18, 58);
        const double height = 6;
        var bounds = view.GeometryBounds;
        var candidates = new List<DrawingAnnotationCandidateIr>();
        var ordinal = 0;
        void Add(string lane, int laneIndex, double x, double y)
        {
            var body = new DrawingRect(x, y, width, height);
            var attach = new DrawingPoint2(Math.Clamp(anchor.X, body.X, body.Right), Math.Clamp(anchor.Y, body.Y, body.Bottom));
            var length = Math.Sqrt(Math.Pow(anchor.X - attach.X, 2) + Math.Pow(anchor.Y - attach.Y, 2));
            var edgePenalty = Math.Min(Math.Min(body.X - view.Viewport.X, view.Viewport.Right - body.Right),
                Math.Min(body.Y - view.Viewport.Y, view.Viewport.Bottom - body.Bottom)) < 3 ? 20 : 0;
            candidates.Add(new($"candidate:{ordinal++}:{lane}:{laneIndex}", body, [anchor, attach], lane, laneIndex,
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
        return candidates.OrderBy(candidate => candidate.Cost).ThenBy(candidate => candidate.Identity, StringComparer.Ordinal).ToArray();
    }

    private static bool Contains(DrawingRect outer, DrawingRect inner) => inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

    private static bool CollidesModel(DrawingRect body, DrawingViewIr view)
    {
        // Text uses a small safety margin; leaders may cross geometry as a penalized M0 fallback,
        // but annotation bodies never may.
        var expanded = new DrawingRect(body.X - 1, body.Y - 1, body.Width + 2, body.Height + 2);
        foreach (var primitive in view.Primitives)
        {
            for (var i = 1; i < primitive.Points.Count; i++)
            {
                var a = primitive.Points[i - 1]; var b = primitive.Points[i];
                var segmentBounds = new DrawingRect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(0.15, Math.Abs(a.X - b.X)), Math.Max(0.15, Math.Abs(a.Y - b.Y)));
                if (expanded.Intersects(segmentBounds)) return true;
            }
        }
        return false;
    }
}
