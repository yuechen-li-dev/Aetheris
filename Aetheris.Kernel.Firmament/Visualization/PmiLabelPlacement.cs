namespace Aetheris.Kernel.Firmament.Visualization;

/// <summary>Drawing-only placement for a stack of related labels in Z-up millimetres.
/// Line and vertex spans identify moving label ink in the flat presentation buffers.
/// The support, arrowhead, any vertical drop and independent dimension lines stay fixed.</summary>
public sealed record PmiLabelPlacement(string Key, double[] Normal, double[][]? Bounds)
{
    public int LineStart { get; private init; }
    public int LineCount { get; private init; }
    public int GlyphStart { get; private init; }
    public int GlyphCount { get; private init; }
    public int FillVertices { get; private init; }
    public int PanelVertices { get; private init; }
    public int? LeaderLine { get; private init; }
    public bool Alias { get; private init; }

    internal PmiLabelPlacement ForRow(PmiCanvas label, PmiCanvas combined, bool ownsLeader, int dropCount, bool firstRow) =>
        this with { Bounds = firstRow ? Bounds : null, LineStart = dropCount, LineCount = label.Lines.Count,
            GlyphStart = dropCount + combined.Lines.Count, GlyphCount = label.GlyphLines.Count,
            FillVertices = label.Fill.Count, PanelVertices = label.Panel.Count,
            LeaderLine = ownsLeader ? dropCount + label.Lines.Count : null };

    internal PmiLabelPlacement AsAlias() => new(Key, Normal, null) { Alias = true };

    public static IReadOnlyList<PmiDisplayAnnotation> Apply(IReadOnlyList<PmiDisplayAnnotation> annotations, IReadOnlyDictionary<string, double[]>? offsets)
    {
        if (offsets is not { Count: > 0 }) return annotations;
        foreach (var offset in offsets.Values)
            if (offset is not { Length: 3 } || offset.Any(value => !double.IsFinite(value) || Math.Abs(value) > 1e8))
                throw new InvalidOperationException("pmi-layout-invalid-offset: Use three finite millimetre coordinates.");
        return annotations.Select(annotation =>
            annotation.Placement is { } placement && offsets.TryGetValue(placement.Key, out var offset) && offset.Any(value => value != 0)
                ? placement.Move(annotation, offset) : annotation).ToArray();
    }

    private PmiDisplayAnnotation Move(PmiDisplayAnnotation annotation, double[] offset)
    {
        var outsidePlane = Enumerable.Range(0, 3).Sum(i => Normal[i] * offset[i]);
        var delta = Enumerable.Range(0, 3).Select(i => offset[i] - outsidePlane * Normal[i]).ToArray();
        double[] Translate(double[] point) => [point[0] + delta[0], point[1] + delta[1], point[2] + delta[2]];
        double[][] MoveLeader(double[][] line)
        {
            var tip = line[0];
            var next = line[1];
            var length = Math.Sqrt(Enumerable.Range(0, 3).Sum(i => Math.Pow(next[i] - tip[i], 2)));
            // Preserve the exit direction at the fixed arrowhead or datum triangle.
            var fraction = length > 0 ? Math.Min(.5, annotation.TextHeight / length) : 0;
            var neck = Enumerable.Range(0, 3).Select(i => tip[i] + (next[i] - tip[i]) * fraction).ToArray();
            return [tip, neck, .. line.Skip(1).Select(Translate)];
        }
        IReadOnlyList<double[][]> MoveLines(IReadOnlyList<double[][]> lines, bool glyphs) =>
            lines.Select((line, index) => index == LeaderLine ? MoveLeader(line)
                : (index >= LineStart && index < LineStart + LineCount) || (glyphs && index >= GlyphStart && index < GlyphStart + GlyphCount)
                    ? line.Select(Translate).ToArray() : line).ToArray();
        PmiDisplayMesh? MoveMesh(PmiDisplayMesh? mesh, int vertices) =>
            mesh is null ? null : mesh with { Positions = mesh.Positions.Select((value, i) => i < vertices * 3 ? value + delta[i % 3] : value).ToArray() };

        var label = Translate(annotation.LabelPosition);
        return annotation with
        {
            LabelPosition = label,
            Lines = Alias ? [[annotation.Anchor, label]] : MoveLines(annotation.Lines, true),
            DraftingLines = annotation.DraftingLines is { } drafting ? MoveLines(drafting, false) : null,
            TextFill = MoveMesh(annotation.TextFill, FillVertices),
            Panel = MoveMesh(annotation.Panel, PanelVertices),
        };
    }
}
