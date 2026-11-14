namespace Aetheris.Kernel.Firmament.Drawing;

/// <summary>Shared paper-space annotation strokes for PDF, SVG and editable PPTX.</summary>
internal static class DrawingAnnotationGeometry
{
    public static IEnumerable<DrawingDimensionLineIr> Strokes(DrawingAnnotationIr annotation)
    {
        var leader = annotation.SelectedCandidate.Leader;
        var datum = annotation.Shape == DrawingAnnotationShape.DatumFeature;
        var dot = annotation.DotAnchor;
        for (var i = 1; i < leader.Count; i++)
            foreach (var line in Expand(new(leader[i - 1], leader[i], ArrowStart: i == 1 && !datum && !dot))) yield return line;
        if (dot && leader.Count >= 1)
        {
            // A filled dot on the surface: concentric rings dense enough to read as solid.
            var c0 = leader[0];
            foreach (var r in new[] { .7, .5, .3, .12 })
                for (var k = 0; k < 12; k++)
                {
                    var a0 = 2 * Math.PI * k / 12; var a1 = 2 * Math.PI * (k + 1) / 12;
                    yield return new(new(c0.X + r * Math.Cos(a0), c0.Y + r * Math.Sin(a0)), new(c0.X + r * Math.Cos(a1), c0.Y + r * Math.Sin(a1)));
                }
        }
        if (annotation.Shape == DrawingAnnotationShape.Balloon && annotation.BalloonRadius > 0)
        {
            // The balloon circle, as short chords so every writer draws it with the same strokes.
            var c = annotation.SelectedCandidate.Body.Center; var r = annotation.BalloonRadius;
            for (var k = 0; k < 32; k++)
            {
                var a0 = 2 * Math.PI * k / 32; var a1 = 2 * Math.PI * (k + 1) / 32;
                yield return new(new(c.X + r * Math.Cos(a0), c.Y + r * Math.Sin(a0)), new(c.X + r * Math.Cos(a1), c.Y + r * Math.Sin(a1)));
            }
        }
        if (datum && !dot && leader.Count >= 2)
        {
            // Datum feature triangle on the feature: base across the leader at the anchor, filled by hatching.
            var tip = leader[0]; var next = leader[1];
            var dx = next.X - tip.X; var dy = next.Y - tip.Y; var l = Math.Sqrt(dx * dx + dy * dy);
            if (l > 1e-9)
            {
                var ux = dx / l; var uy = dy / l; const double half = 1.3, height = 2.2;
                var b0 = new DrawingPoint2(tip.X - uy * half, tip.Y + ux * half); var b1 = new DrawingPoint2(tip.X + uy * half, tip.Y - ux * half);
                var apex = new DrawingPoint2(tip.X + ux * height, tip.Y + uy * height);
                yield return new(b0, b1); yield return new(b0, apex); yield return new(b1, apex);
                for (var f = 1; f < 6; f++)
                {
                    var t = f / 6d;
                    yield return new(new(b0.X + (apex.X - b0.X) * t, b0.Y + (apex.Y - b0.Y) * t), new(b1.X + (apex.X - b1.X) * t, b1.Y + (apex.Y - b1.Y) * t));
                }
            }
        }
        foreach (var dimension in annotation.DimensionLines)
            foreach (var line in Expand(dimension)) yield return line;
    }

    private static IEnumerable<DrawingDimensionLineIr> Expand(DrawingDimensionLineIr line)
    {
        yield return line with { ArrowStart = false, ArrowEnd = false };
        foreach (var (tip, tail) in new[] { (line.Start, line.End, line.ArrowStart), (line.End, line.Start, line.ArrowEnd) }
                     .Where(a => a.Item3).Select(a => (a.Item1, a.Item2)))
        {
            var dx = tail.X - tip.X; var dy = tail.Y - tip.Y; var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-9) continue;
            var ux = dx / length; var uy = dy / length; var size = Math.Min(2.5, length / 3);
            yield return new(tip, new(tip.X + ux * size - uy * size / 3, tip.Y + uy * size + ux * size / 3));
            yield return new(tip, new(tip.X + ux * size + uy * size / 3, tip.Y + uy * size - ux * size / 3));
        }
    }
}
