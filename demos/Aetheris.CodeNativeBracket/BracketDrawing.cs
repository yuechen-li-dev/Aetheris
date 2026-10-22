using Aetheris.Kernel.Firmament.Drawing;
using static Aetheris.CodeNativeBracket.BracketDimensions;

namespace Aetheris.CodeNativeBracket;

/// <summary>Authored drawing for this family only. Views remain compiler projections of the exported body.</summary>
internal static class BracketDrawing
{
    public static DrawingIr Complete(DrawingIr projected, BracketDimensions p, IReadOnlyList<int> coveredFaces, IReadOnlySet<string> holeRims, string holeIdentity)
    {
        var original = projected.Pages.Single();
        var scale = Math.Min(98d / p.Width, 76d / p.Height);
        var top = Place(original.Views.Single(v => v.Identity == "TOP"), 43, 35, scale);
        var edge = Place(original.Views.Single(v => v.Identity == "EDGE"), 43, 135, scale);
        var iso = Place(original.Views.Single(v => v.Identity == "ISO"), 183, 89, .75);
        // Edge projection alone cannot supply a cylinder/cone's apparent contour.
        // Author this family's hidden bore silhouette, replacing only the actual
        // associated rim edges (whose side projections otherwise look like stubs).
        var centerX = edge.GeometryBounds.X + p.HoleX * scale;
        var shaftRadius = p.ShaftDiameter / 2d * scale; var entryRadius = p.EntryDiameter / 2d * scale;
        var shoulder = edge.GeometryBounds.Y + (entryRadius - shaftRadius); // 90-degree included angle
        var boreLines = new[] { -1d, 1d }.Select(side => new DrawingProjectedPrimitiveIr("H1-hidden-" + side, DrawingPrimitiveKind.Hidden,
            [new(centerX + side * shaftRadius, edge.GeometryBounds.Bottom), new(centerX + side * shaftRadius, shoulder),
             new(centerX + side * entryRadius, edge.GeometryBounds.Y)], null, 0)).ToArray();
        edge = edge with { Primitives = edge.Primitives.Where(line => line.SourceEdgeIdentity is null || !holeRims.Contains(line.SourceEdgeIdentity)).Concat(boreLines).ToArray() };
        var annotations = new List<DrawingAnnotationIr>();
        DrawingPoint2 Top(double x, double y) => new(top.GeometryBounds.X + x * scale, top.GeometryBounds.Bottom - y * scale);
        DrawingAnnotationIr Label(string id, string text, double x, double y, double width, DrawingPoint2 anchor, params DrawingPoint2[] leader)
        {
            var candidate = new DrawingAnnotationCandidateIr(id, new(x, y, width, 5), leader, "authored", 0, 0, []);
            return new(id, id, "TOP", DrawingAnnotationKind.Note, text, anchor, candidate, [candidate], "Authored bracket dimensions from typed specification");
        }
        void Horizontal(string id, double length, DrawingPoint2 a, DrawingPoint2 b, double y)
        {
            annotations.Add(Label(id, F(length) + " ±0.1", (a.X + b.X) / 2 - 11, y - 6, 24, a) with
            { Kind = DrawingAnnotationKind.LinearDimension, DimensionLines = [new(a, new(a.X, y + 2)), new(b, new(b.X, y + 2)), new(new(a.X, y), new(b.X, y), true, true)] });
        }
        void Vertical(string id, double length, DrawingPoint2 a, DrawingPoint2 b, double x)
        {
            annotations.Add(Label(id, F(length) + " ±0.1", id == "overall-height" ? x - 24 : x + 3, (a.Y + b.Y) / 2 - 3, 23, a) with
            { Kind = DrawingAnnotationKind.LinearDimension, DimensionLines = [new(a, new(x - 2, a.Y)), new(b, new(x - 2, b.Y)), new(new(x, a.Y), new(x, b.Y), true, true)] });
        }
        Horizontal("overall-width", p.Width, Top(0, 0), Top(p.Width, 0), top.GeometryBounds.Bottom + 12);
        Vertical("overall-height", p.Height, Top(0, 0), Top(0, p.Height), top.GeometryBounds.X - 10);
        Horizontal("leg-width", p.LegWidth, Top(0, p.Height), Top(p.LegWidth, p.Height), top.GeometryBounds.Y - 9);
        Vertical("leg-height", p.LegHeight, Top(p.Width, 0), Top(p.Width, p.LegHeight), top.GeometryBounds.Right + 10);
        var hole = Top(p.HoleX, p.HoleY);
        top = top with { Primitives = top.Primitives.Concat(new[]
        {
            new DrawingProjectedPrimitiveIr("H1-center-x", DrawingPrimitiveKind.CenterMark, [new(hole.X - 5, hole.Y), new(hole.X + 5, hole.Y)], null, 0),
            new DrawingProjectedPrimitiveIr("H1-center-y", DrawingPrimitiveKind.CenterMark, [new(hole.X, hole.Y - 5), new(hole.X, hole.Y + 5)], null, 0)
        }).ToArray() };
        annotations.Add(Label("hole-id", "H1", hole.X + 7, hole.Y - 5, 8, hole));
        var zero = Top(0, 0);
        annotations.Add(Label("coordinate-origin", "O (X0, Y0)", zero.X - 30, zero.Y + 3, 26, zero, zero, new(zero.X - 3, zero.Y + 5), new(zero.X - 6, zero.Y + 5)));
        var plane = Top(p.LegWidth / 2, p.Height * .6);
        annotations.Add(Label("datum-a", "DATUM A", plane.X + 15, plane.Y - 3, 24, plane, plane, new(plane.X + 15, plane.Y)) with { Kind = DrawingAnnotationKind.Datum });
        Vertical("thickness", p.Thickness, new(edge.GeometryBounds.Right, edge.GeometryBounds.Y), new(edge.GeometryBounds.Right, edge.GeometryBounds.Bottom), edge.GeometryBounds.Right + 10);
        var tables = new DrawingTableIr[]
        {
            new("H1 CENTER FROM O / mm", ["ID", "X ±0.1", "Y ±0.1"], [["H1", F(p.HoleX), F(p.HoleY)]], "dimensions", "Authored coordinate intervals", DrawingTableKind.HoleCoordinates, new(180, 32, 104, 14)),
            new("H1 COUNTERSUNK THROUGH HOLE", ["THRU Ø ±0.1", "CSK Ø ±0.1", "Angle ±0.5°"], [[F(p.ShaftDiameter), F(p.EntryDiameter), "90°"]], "dimensions", "TOP entry, included cone angle", DrawingTableKind.Design, new(180, 59, 104, 14))
        };
        var notes = new[]
        {
            "ALL DIMENSIONS mm. DO NOT SCALE.",
            "O: lower-left TOP vertex. +X right, +Y up.",
            "Six straight edges. Unspecified angles 90° ±0.5°.",
            "Uniform thickness; hole axis normal to A.",
            "CSK enters DATUM A (TOP); shaft is THRU.",
            "X/Y are independent ± limits, not GD&T position.",
            "Process capability / inspection approval pending."
        };
        var page = original with
        {
            Views = [top, edge, iso], Annotations = annotations, Tables = tables, Notes = notes,
            LocatedNotes = notes.Select((text, i) => new DrawingNoteIr("note-" + i, text, new(10, 159 + i * 5.4, 106, 5), new(1, "D1"))).ToArray()
        };
        // Paper-space checks guard the authored layout as the parameters change.
        var labels = annotations.Select(a => a.SelectedCandidate.Body).ToArray();
        if (labels.Any(r => r.X < 7 || r.Right > 290 || r.Y < 15 || r.Bottom > 157)) throw new InvalidOperationException("Bracket drawing labels do not fit the authored sheet.");
        if (labels.Where((a, i) => labels.Skip(i + 1).Any(a.Intersects)).Any()) throw new InvalidOperationException("Bracket drawing labels overlap.");
        return projected with
        {
            Pages = [page],
            LayoutEvidence = new(annotations.Count, 0, 0, 0, new Dictionary<string, int> { ["authored"] = annotations.Count }, 0, 0),
            DefinitionEvidence = new(true, true, true, false, "AuthoredSixEdgeLWithNormalCountersink",
                coveredFaces, ["Process capability and independent human approval are unqualified."],
                new Dictionary<string, double> { ["width"] = p.Width, ["height"] = p.Height, ["legWidth"] = p.LegWidth, ["legHeight"] = p.LegHeight, ["thickness"] = p.Thickness },
                [new("H1", p.HoleX, p.HoleY, p.ShaftDiameter, "THRU; CSK " + F(p.EntryDiameter) + " x 90deg on A", "H1", [holeIdentity])], .1)
                { AngularToleranceDegrees = .5, ToleranceScreenScope = "Authored dimensions; no claim of machine/process qualification." }
        };
    }

    private static DrawingViewIr Place(DrawingViewIr view, double left, double top, double scale)
    {
        var ratio = scale / view.Scale;
        DrawingPoint2 Move(DrawingPoint2 p) => new(left + (p.X - view.GeometryBounds.X) * ratio, top + (p.Y - view.GeometryBounds.Y) * ratio);
        var bounds = new DrawingRect(left, top, view.GeometryBounds.Width * ratio, view.GeometryBounds.Height * ratio);
        return view with
        {
            GeometryBounds = bounds, Viewport = new(left - 5, top - 7, bounds.Width + 10, bounds.Height + 10), Scale = scale,
            Primitives = view.Primitives.Select(p => p with { Points = p.Points.Select(Move).ToArray() }).ToArray(),
            SemanticAnchors = view.SemanticAnchors.ToDictionary(a => a.Key, a => Move(a.Value)),
            Camera = view.Camera is { } c ? c with { OriginX = Move(new(c.OriginX, c.OriginY)).X, OriginY = Move(new(c.OriginX, c.OriginY)).Y } : null
        };
    }
}
