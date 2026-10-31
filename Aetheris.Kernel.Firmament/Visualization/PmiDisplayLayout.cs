using System.Globalization;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Visualization;

/// <summary>
/// 2D drafting canvas for one annotation block: polylines, filled glyphs and symbols, and a
/// background panel. Units are millimetres; x runs along the text, y up.
/// </summary>
internal sealed class PmiCanvas
{
    public List<(double X, double Y)[]> Lines { get; } = [];
    public List<(double X, double Y)> Fill { get; } = [];
    public List<int> FillIndices { get; } = [];
    public List<(double X, double Y)> Panel { get; } = [];
    public List<int> PanelIndices { get; } = [];

    public void Line(params (double X, double Y)[] points) => Lines.Add(points);

    public void Rect(double x, double y, double w, double h) => Line((x, y), (x + w, y), (x + w, y + h), (x, y + h), (x, y));

    public void Circle(double cx, double cy, double r, int segments = 32) =>
        Line(Enumerable.Range(0, segments + 1).Select(i => (cx + r * Math.Cos(2 * Math.PI * i / segments), cy + r * Math.Sin(2 * Math.PI * i / segments))).ToArray());

    public void Triangle((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
    {
        var o = Fill.Count; Fill.Add(a); Fill.Add(b); Fill.Add(c); FillIndices.AddRange([o, o + 1, o + 2]);
    }

    public void PanelRect(double x, double y, double w, double h)
    {
        var o = Panel.Count;
        Panel.AddRange([(x, y), (x + w, y), (x + w, y + h), (x, y + h)]);
        PanelIndices.AddRange([o, o + 1, o + 2, o, o + 2, o + 3]);
    }

    /// <summary>Filled text with its baseline at <paramref name="y"/>; returns the advance width.</summary>
    public double Text(string text, double x, double y, double height)
    {
        var (contours, width) = PlanarTextProfiles.PresentationPolygons(text, height);
        // Fill glyph by glyph: bands stay local, so the triangle count follows the glyph outlines.
        foreach (var glyph in contours.GroupBy(c => c.Glyph))
            FillNonZero(glyph.Select(c => c.Points.Select(p => (x + p.X, y + p.Y)).ToArray()).ToArray());
        // Outlines too: line-only consumers (and very small zoom levels) still read the text.
        foreach (var contour in contours)
            Lines.Add(contour.Points.Append(contour.Points[0]).Select(p => (x + p.X, y + p.Y)).ToArray());
        return width;
    }

    /// <summary>
    /// Nonzero-winding fill by horizontal trapezoids. Font outlines may overlap (variable-font
    /// masters keep overlapping contours), so bands also break at every edge crossing.
    /// </summary>
    private void FillNonZero(IReadOnlyList<(double X, double Y)[]> contours)
    {
        var edges = contours.SelectMany(c => c.Select((p, i) => (A: p, B: c[(i + 1) % c.Length])))
            .Where(e => Math.Abs(e.A.Y - e.B.Y) > 1e-12).ToArray();
        var levels = edges.SelectMany(e => new[] { e.A.Y, e.B.Y }).ToList();
        for (var i = 0; i < edges.Length; i++)
            for (var j = i + 1; j < edges.Length; j++)
                if (Crossing(edges[i], edges[j]) is { } yc) levels.Add(yc);
        var ys = levels.Order().Aggregate(new List<double>(), (list, v) => { if (list.Count == 0 || v - list[^1] > 1e-9) list.Add(v); return list; });
        static double XAt(((double X, double Y) A, (double X, double Y) B) e, double yy) => e.A.X + (e.B.X - e.A.X) * (yy - e.A.Y) / (e.B.Y - e.A.Y);
        for (var k = 0; k + 1 < ys.Count; k++)
        {
            var y0 = ys[k]; var y1 = ys[k + 1]; var mid = (y0 + y1) / 2;
            var active = edges.Where(e => Math.Min(e.A.Y, e.B.Y) <= mid && Math.Max(e.A.Y, e.B.Y) >= mid)
                .Select(e => (X0: XAt(e, y0), X1: XAt(e, y1), XM: XAt(e, mid), W: e.B.Y > e.A.Y ? 1 : -1)).OrderBy(e => e.XM).ToArray();
            var winding = 0;
            for (var a = 0; a + 1 < active.Length; a++)
            {
                winding += active[a].W;
                if (winding == 0) continue;
                var l = active[a]; var r = active[a + 1];
                var o = Fill.Count;
                Fill.AddRange([(l.X0, y0), (r.X0, y0), (r.X1, y1), (l.X1, y1)]);
                FillIndices.AddRange([o, o + 1, o + 2, o, o + 2, o + 3]);
            }
        }
    }

    private static double? Crossing(((double X, double Y) A, (double X, double Y) B) p, ((double X, double Y) A, (double X, double Y) B) q)
    {
        var r = (X: p.B.X - p.A.X, Y: p.B.Y - p.A.Y); var s = (X: q.B.X - q.A.X, Y: q.B.Y - q.A.Y);
        var denominator = r.X * s.Y - r.Y * s.X;
        if (Math.Abs(denominator) < 1e-15) return null;
        var t = ((q.A.X - p.A.X) * s.Y - (q.A.Y - p.A.Y) * s.X) / denominator;
        var u = ((q.A.X - p.A.X) * r.Y - (q.A.Y - p.A.Y) * r.X) / denominator;
        return t > 1e-9 && t < 1 - 1e-9 && u > 1e-9 && u < 1 - 1e-9 ? p.A.Y + t * r.Y : null;
    }

    public static double TextWidth(string text, double height) => PlanarTextProfiles.PresentationPolygons(text, height).Width;

    public void Append(PmiCanvas other, double dx, double dy)
    {
        Lines.AddRange(other.Lines.Select(l => l.Select(p => (p.X + dx, p.Y + dy)).ToArray()));
        var o = Fill.Count; Fill.AddRange(other.Fill.Select(p => (p.X + dx, p.Y + dy))); FillIndices.AddRange(other.FillIndices.Select(i => i + o));
        o = Panel.Count; Panel.AddRange(other.Panel.Select(p => (p.X + dx, p.Y + dy))); PanelIndices.AddRange(other.PanelIndices.Select(i => i + o));
    }
}

/// <summary>A laid-out drafting block: its canvas spans [0, Width] x [0, Height].</summary>
internal sealed record PmiBlock(PmiCanvas Canvas, double Width, double Height);

/// <summary>
/// Drafting conventions for 3D callouts (ASME Y14.5 / Y14.41 shapes): feature control frames,
/// datum feature symbols, size callouts with quantity, basic dimensions and notes.
/// </summary>
internal static class PmiDrafting
{
    /// <summary>Tolerance-zone prefix: Ø cylindrical, SØ spherical, none between two surfaces (the default form).</summary>
    public static string ZonePrefix(IReadOnlyList<string> zoneForms) => string.Concat(zoneForms.Select(form => form switch
    {
        "cylindrical or circular" => "Ø", "spherical" => "SØ", "between two equidistant surfaces" => "",
        _ => throw new InvalidOperationException($"pmi-display-zone-unsupported: unsupported zone form '{form}'.")
    }));

    public static string Number(double value) => Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);

    public static string Size(Aetheris.Kernel.Core.Step242.Step242SemanticPmiInspectionItem item)
    {
        var diameter = item.Kind == "Diameter" || item.DimensionKind?.Equals("diameter", StringComparison.OrdinalIgnoreCase) == true;
        var quantity = item.Quantity is > 1 ? $"{item.Quantity}X " : "";
        var nominal = (diameter ? "Ø" : "") + Number(item.Value!.Value);
        var plus = item.TolerancePlus; var minus = item.ToleranceMinus;
        if (plus is null || minus is null) return quantity + nominal;
        if (Math.Abs(plus.Value - minus.Value) < 1e-12) return $"{quantity}{nominal} ±{Number(plus.Value)}";
        string Signed(double v, bool upper) => v == 0 ? "0" : (upper ? "+" : "-") + Number(v);
        return $"{quantity}{nominal} {Signed(plus.Value, true)}/{Signed(minus.Value, false)}";
    }

    /// <summary>One-line text block, optionally boxed (a basic dimension).</summary>
    public static PmiBlock TextBlock(string text, double h, bool boxed = false)
    {
        var c = new PmiCanvas();
        var pad = .35 * h; var height = 1.7 * h;
        var width = PmiCanvas.TextWidth(text, h) + 2 * pad;
        c.PanelRect(0, 0, width, height);
        c.Text(text, pad, .45 * h, h);
        if (boxed) c.Rect(0, 0, width, height);
        return new(c, width, height);
    }

    public static PmiBlock DatumBox(string label, double h)
    {
        var c = new PmiCanvas();
        var size = 1.9 * h;
        var width = Math.Max(size, PmiCanvas.TextWidth(label, h) + .8 * h);
        c.PanelRect(0, 0, width, size);
        c.Rect(0, 0, width, size);
        c.Text(label, (width - PmiCanvas.TextWidth(label, h)) / 2, .55 * h, h);
        return new(c, width, size);
    }

    /// <summary>A feature control frame: characteristic | zone/tolerance/modifiers | datums.</summary>
    public static PmiBlock ControlFrame(string kind, string zonePrefix, double tolerance, IReadOnlyList<string> modifiers,
        IReadOnlyList<string> datums, double h)
    {
        var c = new PmiCanvas();
        var H = 1.9 * h; var pad = .4 * h;
        var x = 0d;
        // Characteristic cell.
        Symbol(c, kind, x + H / 2, H / 2, .62 * h);
        x += H; c.Line((x, 0), (x, H));
        // Tolerance cell: zone, value, material modifiers drawn as circled letters.
        x += pad;
        if (zonePrefix.Length > 0) x += c.Text(zonePrefix, x, .5 * h, h);
        x += c.Text(Number(tolerance), x, .5 * h, h);
        foreach (var modifier in modifiers)
        {
            var letter = modifier switch
            {
                "MAXIMUM_MATERIAL_REQUIREMENT" => "M", "LEAST_MATERIAL_REQUIREMENT" => "L", "FREE_STATE" => "F",
                "STATISTICAL_TOLERANCE" => "ST", "COMMON_ZONE" => "CZ", _ => throw new InvalidOperationException($"pmi-display-modifier-unsupported: {modifier}")
            };
            x += .25 * h;
            if (letter.Length == 1)
            {
                var r = .62 * h; var w = PmiCanvas.TextWidth(letter, .78 * h);
                c.Circle(x + r, H / 2, r);
                c.Text(letter, x + r - w / 2, H / 2 - .29 * h, .78 * h);
                x += 2 * r;
            }
            else x += c.Text(letter, x, .5 * h, h);
        }
        x += pad;
        c.Line((x, 0), (x, H));
        foreach (var datum in datums)
        {
            var w = Math.Max(H, PmiCanvas.TextWidth(datum, h) + 2 * pad);
            c.Text(datum, x + (w - PmiCanvas.TextWidth(datum, h)) / 2, .5 * h, h);
            x += w; c.Line((x, 0), (x, H));
        }
        c.Rect(0, 0, x, H);
        c.PanelRect(0, 0, x, H);
        return new(c, x, H);
    }

    /// <summary>Geometric characteristic symbols drawn as vectors (fonts rarely carry them).</summary>
    private static void Symbol(PmiCanvas c, string kind, double cx, double cy, double s)
    {
        switch (kind)
        {
            case "Position":
                c.Circle(cx, cy, .62 * s); c.Line((cx - s, cy), (cx + s, cy)); c.Line((cx, cy - s), (cx, cy + s)); break;
            case "Flatness":
                c.Line((cx - s, cy - .45 * s), (cx + .55 * s, cy - .45 * s), (cx + s, cy + .45 * s), (cx - .55 * s, cy + .45 * s), (cx - s, cy - .45 * s)); break;
            case "Perpendicularity":
                c.Line((cx - s, cy - .8 * s), (cx + s, cy - .8 * s)); c.Line((cx, cy - .8 * s), (cx, cy + .9 * s)); break;
            case "Parallelism":
                c.Line((cx - .9 * s, cy - .8 * s), (cx - .1 * s, cy + .8 * s)); c.Line((cx + .1 * s, cy - .8 * s), (cx + .9 * s, cy + .8 * s)); break;
            case "Straightness":
                c.Line((cx - s, cy), (cx + s, cy)); break;
            case "Roundness":
                c.Circle(cx, cy, .75 * s); break;
            case "Cylindricity":
                c.Circle(cx, cy, .6 * s); c.Line((cx - .95 * s, cy - .85 * s), (cx - .25 * s, cy + .85 * s)); c.Line((cx + .25 * s, cy - .85 * s), (cx + .95 * s, cy + .85 * s)); break;
            case "Angularity":
                c.Line((cx + s, cy - .75 * s), (cx - s, cy - .75 * s), (cx + .6 * s, cy + .8 * s)); break;
            case "LineProfile":
                c.Line(Enumerable.Range(0, 17).Select(i => (cx + .9 * s * Math.Cos(Math.PI * i / 16), cy - .5 * s + .9 * s * Math.Sin(Math.PI * i / 16))).ToArray()); break;
            case "SurfaceProfile":
                c.Line(Enumerable.Range(0, 17).Select(i => (cx + .9 * s * Math.Cos(Math.PI * i / 16), cy - .5 * s + .9 * s * Math.Sin(Math.PI * i / 16))).ToArray());
                c.Line((cx - .9 * s, cy - .5 * s), (cx + .9 * s, cy - .5 * s)); break;
            case "CircularRunout":
                c.Line((cx - .6 * s, cy - .8 * s), (cx + .5 * s, cy + .8 * s));
                c.Triangle((cx + .5 * s, cy + .8 * s), (cx + .05 * s, cy + .55 * s), (cx + .5 * s, cy + .2 * s)); break;
            case "TotalRunout":
                c.Line((cx - .9 * s, cy - .8 * s), (cx + .1 * s, cy + .8 * s)); c.Line((cx - .2 * s, cy - .8 * s), (cx + .8 * s, cy + .8 * s));
                c.Line((cx - .9 * s, cy - .8 * s), (cx - .2 * s, cy - .8 * s)); break;
            default: throw new InvalidOperationException($"pmi-display-kind-unsupported: {kind} has no drafting symbol.");
        }
    }

    /// <summary>Stack blocks top to bottom, left aligned.</summary>
    public static PmiBlock Stack(IReadOnlyList<PmiBlock> blocks, double gap)
    {
        var canvas = new PmiCanvas();
        var height = blocks.Sum(b => b.Height) + gap * Math.Max(0, blocks.Count - 1);
        var y = height;
        foreach (var block in blocks)
        {
            y -= block.Height;
            canvas.Append(block.Canvas, 0, y);
            y -= gap;
        }
        return new(canvas, blocks.Count == 0 ? 0 : blocks.Max(b => b.Width), height);
    }

    public static PmiBlock Notes(IReadOnlyList<string> notes, double h)
    {
        var lines = new List<string> { "NOTES" };
        for (var i = 0; i < notes.Count; i++)
        {
            var words = notes[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = $"{i + 1}.";
            foreach (var word in words)
            {
                if (current.Length + word.Length + 1 > 52) { lines.Add(current); current = "    "; }
                current += " " + word;
            }
            lines.Add(current);
        }
        var c = new PmiCanvas();
        var lineHeight = 1.55 * h; var pad = .6 * h;
        var width = lines.Max(l => PmiCanvas.TextWidth(l.Trim().Length == 0 ? "." : l, h)) + 2 * pad;
        var height = lines.Count * lineHeight + pad;
        c.PanelRect(0, 0, width, height);
        c.Rect(0, 0, width, height);
        for (var i = 0; i < lines.Count; i++)
            if (lines[i].Trim().Length > 0) c.Text(lines[i], pad, height - pad - (i + 1) * lineHeight + .45 * h, h);
        return new(c, width, height);
    }
}

/// <summary>An annotation plane in model space: text runs along U, up is V, the reader looks along -N.</summary>
internal readonly record struct PmiPlane(Point3D Origin, Vector3D U, Vector3D V)
{
    public Vector3D N => U.Cross(V);
    public Point3D At(double x, double y, double lift = 0) => Origin + U * x + V * y + N * lift;
    public (double X, double Y) Local(Point3D p) { var d = p - Origin; return (d.Dot(U), d.Dot(V)); }
}
