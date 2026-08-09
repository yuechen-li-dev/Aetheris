using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Sampling;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Lattice;

public enum CellClassification
{
    Outside,
    Inside,
    Cut,
}

public sealed record CutCell(
    CellIndex Index,
    BoundingBox3D Bounds,
    RegionId ContinuumRegionId,
    IReadOnlyList<BoundaryReference> BoundaryReferences,
    GeometrySamplePlan GeometrySamplePlan,
    double OccupancyEstimate);

public readonly record struct ContinuumCell(CellIndex Index, BoundingBox3D Bounds, CellClassification Classification, double OccupancyEstimate);

public sealed record ContinuumGridResult(
    LatticeSpec Lattice,
    RegionId RegionId,
    IReadOnlyList<ContinuumCell> Cells,
    IReadOnlyList<CutCell> CutCells,
    int GeometrySampleCount,
    double EstimatedOccupiedVolume)
{
    public int InsideCellCount => Cells.Count(cell => cell.Classification == CellClassification.Inside);
    public int OutsideCellCount => Cells.Count(cell => cell.Classification == CellClassification.Outside);
    public int CutCellCount => CutCells.Count;
}

public static class ContinuumGridClassifier
{
    private readonly record struct StrategyContext(IContinuumRegion Region, BoundingBox3D Bounds);

    private static readonly JudgmentEngine<StrategyContext> StrategyEngine = new();
    private static readonly IReadOnlyList<JudgmentCandidate<StrategyContext>> Strategies =
    [
        new JudgmentCandidate<StrategyContext>(
            "backend-bounds-capability",
            context => context.Region is IBoundsClassificationCapability,
            _ => 100d,
            _ => "Region does not expose bounded cell classification.",
            0),
        new JudgmentCandidate<StrategyContext>(
            "conservative-bounds-fallback",
            _ => true,
            _ => 10d,
            _ => "Fallback is always admissible.",
            1),
    ];

    public static ContinuumGridResult Classify(IContinuumRegion region, LatticeSpec lattice, int cutCellSamplesPerAxis = 2)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(lattice);
        var cells = new List<ContinuumCell>(lattice.TotalCellCount);
        var cutCells = new List<CutCell>();
        var sampleCount = 0;
        var volume = 0d;
        var size = lattice.CellSize;
        var cellVolume = size.X * size.Y * size.Z;

        foreach (var index in lattice.Indices())
        {
            var bounds = lattice.CellBounds(index);
            var classification = ClassifyCell(region, bounds);
            if (classification == CellClassification.Inside)
            {
                cells.Add(new ContinuumCell(index, bounds, classification, 1d));
                volume += cellVolume;
                continue;
            }

            if (classification == CellClassification.Outside)
            {
                cells.Add(new ContinuumCell(index, bounds, classification, 0d));
                continue;
            }

            var plan = GeometrySampler.Sample(region, bounds, cutCellSamplesPerAxis);
            sampleCount += plan.GeometrySampleCount;
            volume += cellVolume * plan.CoverageEstimate;
            cells.Add(new ContinuumCell(index, bounds, classification, plan.CoverageEstimate));
            cutCells.Add(new CutCell(index, bounds, region.Id, plan.BoundaryCandidates, plan, plan.CoverageEstimate));
        }

        return new ContinuumGridResult(lattice, region.Id, cells, cutCells, sampleCount, volume);
    }

    private static CellClassification ClassifyCell(IContinuumRegion region, BoundingBox3D bounds)
    {
        var context = new StrategyContext(region, bounds);
        var selection = StrategyEngine.Evaluate(context, Strategies);
        if (!selection.IsSuccess)
        {
            return CellClassification.Cut;
        }

        if (selection.Selection!.Value.Candidate.Name == "backend-bounds-capability")
        {
            return ((IBoundsClassificationCapability)region).ClassifyBounds(bounds) switch
            {
                ContinuumBoundsClassification.Inside => CellClassification.Inside,
                ContinuumBoundsClassification.Outside => CellClassification.Outside,
                _ => CellClassification.Cut,
            };
        }

        return HasPositiveVolumeIntersection(region.Bounds, bounds)
            ? CellClassification.Cut
            : CellClassification.Outside;
    }

    private static bool HasPositiveVolumeIntersection(BoundingBox3D left, BoundingBox3D right) =>
        double.Min(left.Max.X, right.Max.X) > double.Max(left.Min.X, right.Min.X)
        && double.Min(left.Max.Y, right.Max.Y) > double.Max(left.Min.Y, right.Min.Y)
        && double.Min(left.Max.Z, right.Max.Z) > double.Max(left.Min.Z, right.Min.Z);
}
