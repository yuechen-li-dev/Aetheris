using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum RestrictedFieldContourExtractionMethod
{
    MarchingSquares,
}

internal sealed record SurfaceTrimContourPoint2D(double U, double V, double? ValueEstimate = null);

internal sealed record SurfaceTrimContourPoint3D(Point3D Point, double SourceU, double SourceV);

internal sealed record SurfaceTrimContourSegment2D(
    int CellI,
    int CellJ,
    SurfaceTrimContourPoint2D A,
    SurfaceTrimContourPoint2D B,
    RestrictedFieldCellClassification Classification,
    IReadOnlyList<string> Diagnostics,
    SurfaceTrimContourPoint3D? A3D = null,
    SurfaceTrimContourPoint3D? B3D = null);

internal sealed record SurfaceTrimContourExtractionResult(
    bool Success,
    RestrictedFieldContourExtractionMethod Method,
    int SegmentCount,
    IReadOnlyList<SurfaceTrimContourSegment2D> Segments,
    IReadOnlyList<string> Diagnostics,
    bool ContourStitchingImplemented = false,
    bool AnalyticSnapImplemented = false,
    bool ExactExportAvailable = false);

internal static class RestrictedFieldMarchingSquaresExtractor
{
    internal static SurfaceTrimContourExtractionResult Extract(RestrictedFieldSampleGrid grid, PlanarRectangleParameterization? parameterization = null, ToleranceContext? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var tol = tolerance ?? ToleranceContext.Default;
        var segments = new List<SurfaceTrimContourSegment2D>();
        var diagnostics = new List<string> { "contour-extraction-started:method-marching-squares" };
        var skippedUnknown = 0;
        var ambiguous = 0;
        var boundaryCells = 0;

        foreach (var cell in grid.Cells.OrderBy(c => c.CellJ).ThenBy(c => c.CellI))
        {
            if (cell.Classification == RestrictedFieldCellClassification.Unknown)
            {
                skippedUnknown++;
                continue;
            }

            if (cell.Classification is RestrictedFieldCellClassification.Inside or RestrictedFieldCellClassification.Outside)
            {
                continue;
            }

            if (cell.Classification == RestrictedFieldCellClassification.Boundary)
            {
                boundaryCells++;
            }

            var corners = cell.CornerSampleIndices.Select(i => grid.Samples[i]).ToArray(); // BL, BR, TL, TR
            var cornerValues = corners.Select(c => c.Sample.Value).ToArray();
            var cornerSigns = cornerValues.Select(v => ClassifySign(v, tol)).ToArray();

            var edgeHits = new List<(int edge, SurfaceTrimContourPoint2D point, SurfaceTrimContourPoint3D? point3D)>();
            TryAddEdgeHit(0, corners[0], corners[1], tol, parameterization, edgeHits); // bottom
            TryAddEdgeHit(1, corners[1], corners[3], tol, parameterization, edgeHits); // right
            TryAddEdgeHit(2, corners[2], corners[3], tol, parameterization, edgeHits); // top
            TryAddEdgeHit(3, corners[0], corners[2], tol, parameterization, edgeHits); // left

            if (edgeHits.Count < 2)
            {
                continue;
            }

            var localDiagnostics = new List<string>();
            if (edgeHits.Count > 2)
            {
                ambiguous++;
                localDiagnostics.Add("ambiguous-cell:multi-edge-intersections");
            }

            edgeHits.Sort((a, b) => a.edge.CompareTo(b.edge));
            if (edgeHits.Count == 2)
            {
                segments.Add(new SurfaceTrimContourSegment2D(cell.CellI, cell.CellJ, edgeHits[0].point, edgeHits[1].point, cell.Classification, localDiagnostics, edgeHits[0].point3D, edgeHits[1].point3D));
                continue;
            }

            // Deterministic saddle handling: use asymptotic-decider style center sign to pair edges.
            var center = 0.25d * (cornerValues[0] + cornerValues[1] + cornerValues[2] + cornerValues[3]);
            localDiagnostics.Add($"ambiguous-center:{center:R}");
            var centerInside = center < -tol.Linear;
            var pairing = centerInside
                ? new[] { (0, 3), (1, 2) }
                : new[] { (0, 1), (2, 3) };

            foreach (var (ea, eb) in pairing)
            {
                var a = edgeHits.FirstOrDefault(h => h.edge == ea);
                var b = edgeHits.FirstOrDefault(h => h.edge == eb);
                if (a == default || b == default)
                {
                    continue;
                }

                segments.Add(new SurfaceTrimContourSegment2D(cell.CellI, cell.CellJ, a.point, b.point, cell.Classification, localDiagnostics, a.point3D, b.point3D));
            }
        }

        diagnostics.Add($"contour-segment-count:{segments.Count}");
        diagnostics.Add($"contour-skipped-unknown-cell-count:{skippedUnknown}");
        diagnostics.Add($"contour-ambiguous-cell-count:{ambiguous}");
        diagnostics.Add($"contour-boundary-cell-count:{boundaryCells}");
        diagnostics.Add("contour-stitching-not-implemented");
        diagnostics.Add("contour-analytic-snap-not-implemented");
        diagnostics.Add("contour-exact-export-not-available");

        var success = segments.Count > 0 || grid.Counts.MixedCellCount == 0;
        if (segments.Count == 0)
        {
            diagnostics.Add("no-contour-segments-detected");
        }

        return new SurfaceTrimContourExtractionResult(success, RestrictedFieldContourExtractionMethod.MarchingSquares, segments.Count, segments, diagnostics);
    }

    private static int ClassifySign(double value, ToleranceContext tol)
        => double.Abs(value) <= tol.Linear ? 0 : value < 0d ? -1 : 1;

    private static void TryAddEdgeHit(
        int edgeId,
        RestrictedFieldGridSample a,
        RestrictedFieldGridSample b,
        ToleranceContext tol,
        PlanarRectangleParameterization? parameterization,
        List<(int edge, SurfaceTrimContourPoint2D point, SurfaceTrimContourPoint3D? point3D)> hits)
    {
        var va = a.Sample.Value;
        var vb = b.Sample.Value;
        var sa = ClassifySign(va, tol);
        var sb = ClassifySign(vb, tol);

        if (sa == 0 && sb == 0)
        {
            return;
        }

        double t;
        if (sa == 0)
        {
            t = 0d;
        }
        else if (sb == 0)
        {
            t = 1d;
        }
        else if (sa == sb)
        {
            return;
        }
        else
        {
            var denom = va - vb;
            if (ToleranceMath.AlmostZero(denom, tol))
            {
                t = 0.5d;
            }
            else
            {
                t = va / (va - vb);
            }
        }

        t = double.Clamp(t, 0d, 1d);
        var u = a.U + (b.U - a.U) * t;
        var v = a.V + (b.V - a.V) * t;
        var value = a.Sample.Value + (b.Sample.Value - a.Sample.Value) * t;
        SurfaceTrimContourPoint3D? point3D = null;
        if (parameterization is { } p)
        {
            point3D = new SurfaceTrimContourPoint3D(p.Evaluate(u, v), u, v);
        }

        hits.Add((edgeId, new SurfaceTrimContourPoint2D(u, v, value), point3D));
    }
}
