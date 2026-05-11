using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Firmament.Execution;

internal sealed record RestrictedFieldGridOptions(int ResolutionU, int ResolutionV, bool IncludeWorldPoints = true);

internal enum RestrictedFieldCellClassification
{
    Inside,
    Outside,
    Boundary,
    Mixed,
    Unknown,
}

internal sealed record RestrictedFieldGridSample(
    int I,
    int J,
    double U,
    double V,
    RestrictedFieldSample Sample);

internal sealed record RestrictedFieldCell(
    int CellI,
    int CellJ,
    double UMin,
    double UMax,
    double VMin,
    double VMax,
    IReadOnlyList<int> CornerSampleIndices,
    RestrictedFieldCellClassification Classification,
    IReadOnlyList<string> Diagnostics);

internal sealed record RestrictedFieldGridCounts(
    int SampleCount,
    int CellCount,
    int InsideCellCount,
    int OutsideCellCount,
    int BoundaryCellCount,
    int MixedCellCount,
    int UnknownCellCount);

internal sealed record RestrictedFieldSampleGrid(
    SourceSurfaceDescriptor SourceSurface,
    int ResolutionU,
    int ResolutionV,
    IReadOnlyList<RestrictedFieldGridSample> Samples,
    IReadOnlyList<RestrictedFieldCell> Cells,
    RestrictedFieldGridCounts Counts,
    IReadOnlyList<string> Diagnostics);

internal static class RestrictedFieldGridSampler
{
    internal static RestrictedFieldSampleGrid Sample(SurfaceRestrictedField field, RestrictedFieldGridOptions options, ToleranceContext? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(options);

        var diagnostics = new List<string>
        {
            "grid-sampler-started",
            $"grid-resolution:{options.ResolutionU}x{options.ResolutionV}",
            "grid-cell-policy:boundary-no-disagreement=>Boundary,boundary-with-disagreement=>Mixed",
            "contour-extraction-not-implemented",
            "export-materialization-unchanged",
        };

        if (options.ResolutionU < 2 || options.ResolutionV < 2)
        {
            diagnostics.Add("invalid-grid-resolution:requires-min-2-per-axis");
            throw new ArgumentOutOfRangeException(nameof(options), "Grid resolution must be >= 2 for both axes.");
        }

        var sampleCount = options.ResolutionU * options.ResolutionV;
        var samples = new List<RestrictedFieldGridSample>(sampleCount);
        for (var j = 0; j < options.ResolutionV; j++)
        {
            for (var i = 0; i < options.ResolutionU; i++)
            {
                var u = i / (double)(options.ResolutionU - 1);
                var v = j / (double)(options.ResolutionV - 1);
                var sampled = field.Evaluate(u, v, tolerance);
                var retainedSample = options.IncludeWorldPoints
                    ? sampled
                    : sampled with { Point = default };
                samples.Add(new RestrictedFieldGridSample(i, j, u, v, retainedSample));
            }
        }

        var cells = new List<RestrictedFieldCell>((options.ResolutionU - 1) * (options.ResolutionV - 1));
        var inside = 0;
        var outside = 0;
        var boundary = 0;
        var mixed = 0;
        var unknown = 0;
        for (var j = 0; j < options.ResolutionV - 1; j++)
        {
            for (var i = 0; i < options.ResolutionU - 1; i++)
            {
                var bottomLeft = GetSampleIndex(i, j, options.ResolutionU);
                var bottomRight = GetSampleIndex(i + 1, j, options.ResolutionU);
                var topLeft = GetSampleIndex(i, j + 1, options.ResolutionU);
                var topRight = GetSampleIndex(i + 1, j + 1, options.ResolutionU);
                var cornerIndices = new[] { bottomLeft, bottomRight, topLeft, topRight };
                var corners = cornerIndices.Select(index => samples[index]).ToArray();
                var classification = ClassifyCell(corners);
                switch (classification)
                {
                    case RestrictedFieldCellClassification.Inside: inside++; break;
                    case RestrictedFieldCellClassification.Outside: outside++; break;
                    case RestrictedFieldCellClassification.Boundary: boundary++; break;
                    case RestrictedFieldCellClassification.Mixed: mixed++; break;
                    case RestrictedFieldCellClassification.Unknown: unknown++; break;
                }

                cells.Add(new RestrictedFieldCell(
                    i,
                    j,
                    corners.Min(s => s.U),
                    corners.Max(s => s.U),
                    corners.Min(s => s.V),
                    corners.Max(s => s.V),
                    cornerIndices,
                    classification,
                    []));
            }
        }

        var counts = new RestrictedFieldGridCounts(samples.Count, cells.Count, inside, outside, boundary, mixed, unknown);
        return new RestrictedFieldSampleGrid(field.SourceSurface, options.ResolutionU, options.ResolutionV, samples, cells, counts, diagnostics);
    }

    internal static RestrictedFieldCellClassification ClassifyCell(IReadOnlyList<RestrictedFieldGridSample> corners)
    {
        if (corners.Count != 4)
        {
            throw new ArgumentException("Cell classification requires exactly four corners.", nameof(corners));
        }

        if (corners.Any(c => double.IsNaN(c.Sample.Value) || c.Sample.Diagnostics.Contains("restricted-field-not-ready")))
        {
            return RestrictedFieldCellClassification.Unknown;
        }

        var hasInside = corners.Any(c => c.Sample.SignClassification == RestrictedFieldSignClassification.InsideOpposite);
        var hasOutside = corners.Any(c => c.Sample.SignClassification == RestrictedFieldSignClassification.OutsideOpposite);
        var hasBoundary = corners.Any(c => c.Sample.SignClassification == RestrictedFieldSignClassification.Boundary);

        if (hasInside && hasOutside)
        {
            return RestrictedFieldCellClassification.Mixed;
        }

        if (hasBoundary)
        {
            return (hasInside || hasOutside) ? RestrictedFieldCellClassification.Boundary : RestrictedFieldCellClassification.Boundary;
        }

        if (hasInside)
        {
            return RestrictedFieldCellClassification.Inside;
        }

        if (hasOutside)
        {
            return RestrictedFieldCellClassification.Outside;
        }

        return RestrictedFieldCellClassification.Unknown;
    }

    private static int GetSampleIndex(int i, int j, int resolutionU) => (j * resolutionU) + i;
}
