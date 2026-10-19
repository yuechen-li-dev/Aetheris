namespace Aetheris.Kernel.Firmament.Drawing;

/// <summary>Shared paper-space annotation strokes for PDF, SVG and editable PPTX.</summary>
internal static class DrawingAnnotationGeometry
{
    public static IEnumerable<DrawingDimensionLineIr> Strokes(DrawingAnnotationIr annotation)
    {
        var leader = annotation.SelectedCandidate.Leader;
        for (var i = 1; i < leader.Count; i++)
            foreach (var line in Expand(new(leader[i - 1], leader[i], ArrowStart: i == 1))) yield return line;
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
