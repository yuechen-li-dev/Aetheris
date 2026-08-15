using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.SheetMetal;

public sealed record RecoveredFlatComparisonReport(
    SheetMetalComparisonStatus Status,
    SheetResidualStatistics SourceToNative,
    SheetResidualStatistics NativeToSource,
    double WidthResidual,
    double HeightResidual,
    int CutCountDelta,
    int BendLineCountDelta,
    IReadOnlyList<string> LocalizedDifferences);

public static class RecoveredFlatComparer
{
    public static RecoveredFlatComparisonReport Compare(RecoveredFlatReference source, SheetMetalFlatPatternIr native,
        double tolerance = .1d)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(native);
        var sourcePoints = Points(source.OuterAndInnerContours, source.Regions.SelectMany(x => x.Boundary));
        var nativePoints = Points(native.ExactBlankContour, native.Boundary);
        var sourceOrigin = Origin(sourcePoints); var nativeOrigin = Origin(nativePoints);
        sourcePoints = Translate(sourcePoints, sourceOrigin); nativePoints = Translate(nativePoints, nativeOrigin);
        var sourceToNative = Stats(sourcePoints.Select(x => Nearest(x, nativePoints)));
        var nativeToSource = Stats(nativePoints.Select(x => Nearest(x, sourcePoints)));
        var width = Math.Abs((source.Bounds?.Width ?? Extent(sourcePoints, true)) - (native.Bounds?.Width ?? Extent(nativePoints, true)));
        var height = Math.Abs((source.Bounds?.Height ?? Extent(sourcePoints, false)) - (native.Bounds?.Height ?? Extent(nativePoints, false)));
        var cuts = native.CutLoops.Count - source.InnerContours.Count;
        var bends = native.BendLines.Count - source.BendLines.Count;
        var differences = new List<string>();
        if (width > tolerance) differences.Add($"Outer width differs by {width:G6} mm.");
        if (height > tolerance) differences.Add($"Outer height differs by {height:G6} mm.");
        if (sourceToNative.P95 > tolerance) differences.Add($"Source outer contour has unmatched detail (p95 {sourceToNative.P95:G6} mm, max {sourceToNative.Maximum:G6} mm).");
        if (nativeToSource.P95 > tolerance) differences.Add($"Native outer contour has unmatched/shifted detail (p95 {nativeToSource.P95:G6} mm, max {nativeToSource.Maximum:G6} mm).");
        if (cuts != 0) differences.Add($"Cut inventory differs: source {source.InnerContours.Count}, native {native.CutLoops.Count}.");
        if (bends != 0) differences.Add($"Bend-line inventory differs: source {source.BendLines.Count}, native {native.BendLines.Count}.");
        foreach (var region in source.Regions.Where(x => x.Kind == SheetRegionKind.Planar))
        {
            var local = Translate(region.Boundary, sourceOrigin).Select(x => Nearest(x, nativePoints)).DefaultIfEmpty(double.PositiveInfinity).Max();
            if (local > tolerance) differences.Add($"{region.SourceRegionId}.Outer has local residual {local:G6} mm.");
        }
        var status = source.Status == FlatPatternStatus.Valid && native.Status is FlatPatternStatus.Valid or FlatPatternStatus.Partial &&
            width <= tolerance && height <= tolerance && sourceToNative.P95 <= tolerance && nativeToSource.P95 <= tolerance && cuts == 0 && bends == 0
            ? SheetMetalComparisonStatus.Pass : SheetMetalComparisonStatus.NeedsReview;
        return new(status, sourceToNative, nativeToSource, width, height, cuts, bends, differences);
    }

    private static IReadOnlyList<SheetPoint2> Points(PlanarContour2? contour, IEnumerable<SheetPoint2> fallback)
    {
        if (contour is null) return fallback.ToArray();
        return contour.OuterLoop.Segments.SelectMany(x => x.Geometry switch
        {
            LineArcLineSegment2D line => Enumerable.Range(0, 5).Select(i => new SheetPoint2(line.Start.X + (line.End.X - line.Start.X) * i / 4d, line.Start.Y + (line.End.Y - line.Start.Y) * i / 4d)),
            LineArcCircularArc2D arc => Enumerable.Range(0, Math.Max(4, (int)Math.Ceiling(Math.Abs(arc.SweepAngleRadians) / (Math.PI / 36d)))).Select(i =>
            {
                var count = Math.Max(4, (int)Math.Ceiling(Math.Abs(arc.SweepAngleRadians) / (Math.PI / 36d))); var a = arc.StartAngleRadians + arc.SweepAngleRadians * i / (count - 1d);
                return new SheetPoint2(arc.Center.X + arc.Radius * Math.Cos(a), arc.Center.Y + arc.Radius * Math.Sin(a));
            }),
            LineArcFullCircle2D circle => Enumerable.Range(0, 72).Select(i => new SheetPoint2(circle.Center.X + circle.Radius * Math.Cos(Math.PI * 2d * i / 72d), circle.Center.Y + circle.Radius * Math.Sin(Math.PI * 2d * i / 72d))),
            _ => []
        }).ToArray();
    }

    private static SheetPoint2 Origin(IReadOnlyList<SheetPoint2> points) => points.Count == 0 ? default : new(points.Min(x => x.X), points.Min(x => x.Y));
    private static IReadOnlyList<SheetPoint2> Translate(IReadOnlyList<SheetPoint2> points,SheetPoint2 origin) => points.Select(x => new SheetPoint2(x.X-origin.X,x.Y-origin.Y)).ToArray();
    private static double Extent(IReadOnlyList<SheetPoint2> p, bool x) => p.Count == 0 ? 0d : x ? p.Max(q => q.X) - p.Min(q => q.X) : p.Max(q => q.Y) - p.Min(q => q.Y);
    private static double Nearest(SheetPoint2 p, IReadOnlyList<SheetPoint2> q) => q.Count == 0 ? double.PositiveInfinity : q.Min(x => Math.Sqrt((p.X - x.X) * (p.X - x.X) + (p.Y - x.Y) * (p.Y - x.Y)));
    private static SheetResidualStatistics Stats(IEnumerable<double> values)
    {
        var v = values.Where(double.IsFinite).Order().ToArray();
        return v.Length == 0 ? new(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, 0) :
            new(Math.Sqrt(v.Average(x => x * x)), v[(int)Math.Ceiling(.95d * v.Length) - 1], v[^1], v.Length);
    }
}
