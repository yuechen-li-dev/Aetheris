using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Continuum.Backends.Sdf;

public sealed record SdfAdaptiveVolumeOptions(
    int MaxDepth = 6,
    int DirectSampleGrid = 2,
    int MaxTraceEvents = 64,
    double MinimumRegionExtent = 0.05d,
    bool TreatRejectUnknownAsSample = true)
{
    public static SdfAdaptiveVolumeOptions Default { get; } = new();
}

public readonly record struct SdfAdaptiveTraceEvent(
    int RegionId,
    int? ParentRegionId,
    int Depth,
    CirRegionPlanAction Action,
    string Candidate,
    FieldInterval Interval,
    string? Note);

public sealed record SdfAdaptiveVolumeResult(
    double EstimatedVolume,
    SdfAdaptiveVolumeOptions Options,
    int TotalRegionsVisited,
    int RegionsClassifiedInside,
    int RegionsClassifiedOutside,
    int RegionsSubdivided,
    int RegionsSampledDirectly,
    int UnknownOrRejectedRegions,
    int MaxDepthReached,
    IReadOnlyList<SdfAdaptiveTraceEvent> TraceEvents,
    IReadOnlyList<string> Notes);

public sealed record SdfBoundedVolumeResult(double LowerBound, double UpperBound, int RegionsVisited, bool BudgetExhausted)
{
    public double EstimatedVolume => (LowerBound + UpperBound) * .5;
    public double ErrorBound => (UpperBound - LowerBound) * .5;
}

public static class SdfAdaptiveVolumeEstimator
{
    /// <summary>Occupied-volume enclosure. Mixed cells contribute [0, cell volume],
    /// never a sampling-based error claim. Refine the largest uncertainty first.
    /// Bounds assume the tape's supported analytic interval contract and finite
    /// enclosing input bounds; roundoff guards are included, not formal interval arithmetic.</summary>
    public static SdfBoundedVolumeResult EstimateBounded(SdfTape tape, SdfBounds bounds, double absoluteError,
        int maximumRegions = 100_001, int maximumDepth = 30)
    {
        ArgumentNullException.ThrowIfNull(tape);
        var enclosingVolume = RegionVolume(bounds);
        if (!double.IsFinite(absoluteError) || absoluteError < 0 || maximumRegions < 1 || maximumDepth < 0
            || !double.IsFinite(enclosingVolume) || enclosingVolume <= 0
            || bounds.SizeX <= 0 || bounds.SizeY <= 0 || bounds.SizeZ <= 0)
            throw new ArgumentException("Bounded volume requires finite positive bounds, a nonnegative error and a positive region budget.");
        var queue = new PriorityQueue<(SdfBounds Region, int Depth), (double Volume, int Order)>();
        double inside = 0, uncertain = 0;
        var visited = 0;
        var padding = 1e-12 * Math.Max(1, new[] { bounds.Min.X, bounds.Min.Y, bounds.Min.Z, bounds.Max.X, bounds.Max.Y, bounds.Max.Z }.Max(Math.Abs));
        void Admit(SdfBounds region, int depth)
        {
            var interval = tape.EvaluateInterval(region);
            var volume = RegionVolume(region);
            visited++;
            if (!double.IsFinite(interval.MinValue) || !double.IsFinite(interval.MaxValue) || interval.MinValue > interval.MaxValue)
                throw new InvalidOperationException("sdf-volume-interval-invalid: The field returned no finite conservative interval.");
            if (interval.MaxValue < -padding) inside += volume;
            else if (interval.MinValue <= padding)
            {
                uncertain += volume;
                if (depth < maximumDepth) queue.Enqueue((region, depth), (-volume, visited));
            }
        }
        Admit(bounds, 0);
        while (uncertain > 2 * absoluteError && queue.Count > 0 && visited + 2 <= maximumRegions)
        {
            var item = queue.Dequeue();
            uncertain -= RegionVolume(item.Region);
            foreach (var child in SplitLongestAxis(item.Region)) Admit(child, item.Depth + 1);
        }
        var roundoff = enclosingVolume * 1e-12;
        var lower = Math.Max(0, inside - roundoff);
        var upper = Math.Min(enclosingVolume, inside + Math.Max(0, uncertain) + roundoff);
        return new(lower, upper, visited, upper - lower > 2 * absoluteError);
    }

    public static SdfAdaptiveVolumeResult EstimateVolume(SdfTape tape, SdfBounds bounds, SdfAdaptiveVolumeOptions? options = null, ToleranceContext? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(tape);

        var effectiveOptions = options ?? SdfAdaptiveVolumeOptions.Default;
        var effectiveTolerance = tolerance ?? ToleranceContext.Default;
        var planner = new CirRegionPlanner();
        var plannerOptions = new CirRegionPlannerOptions(effectiveOptions.MaxDepth, DirectSampleThreshold: effectiveOptions.DirectSampleGrid * effectiveOptions.DirectSampleGrid * effectiveOptions.DirectSampleGrid, effectiveOptions.MinimumRegionExtent);
        var queue = new Queue<RegionWorkItem>();
        var trace = new List<SdfAdaptiveTraceEvent>(capacity: effectiveOptions.MaxTraceEvents);
        var notes = new List<string>();

        queue.Enqueue(new RegionWorkItem(0, null, bounds, 0));
        var nextId = 1;
        var totalVisited = 0;
        var inside = 0;
        var outside = 0;
        var subdivided = 0;
        var sampled = 0;
        var unknown = 0;
        var maxDepthReached = 0;
        var totalVolume = 0d;

        while (queue.Count > 0)
        {
            var item = queue.Dequeue();
            totalVisited++;
            maxDepthReached = int.Max(maxDepthReached, item.Depth);

            var plan = planner.Plan(tape, item.Region, item.Depth, plannerOptions, effectiveTolerance);
            TryRecordTrace(trace, effectiveOptions.MaxTraceEvents, item, plan);

            switch (plan.Action)
            {
                case CirRegionPlanAction.ClassifyInside:
                    inside++;
                    totalVolume += RegionVolume(item.Region);
                    break;
                case CirRegionPlanAction.ClassifyOutside:
                    outside++;
                    break;
                case CirRegionPlanAction.Subdivide:
                    subdivided++;
                    foreach (var child in SplitLongestAxis(item.Region))
                    {
                        queue.Enqueue(new RegionWorkItem(nextId++, item.RegionId, child, item.Depth + 1));
                    }
                    break;
                case CirRegionPlanAction.MarkMixed:
                case CirRegionPlanAction.SampleDirectly:
                    sampled++;
                    totalVolume += SampleRegionVolume(tape, item.Region, effectiveOptions.DirectSampleGrid);
                    break;
                case CirRegionPlanAction.RejectUnknown:
                    unknown++;
                    if (effectiveOptions.TreatRejectUnknownAsSample)
                    {
                        sampled++;
                        totalVolume += SampleRegionVolume(tape, item.Region, effectiveOptions.DirectSampleGrid);
                    }
                    else
                    {
                        notes.Add($"Region {item.RegionId} rejected by planner and skipped: {plan.Note ?? "no note"}.");
                    }
                    break;
            }
        }

        return new SdfAdaptiveVolumeResult(totalVolume, effectiveOptions, totalVisited, inside, outside, subdivided, sampled, unknown, maxDepthReached, trace, notes);
    }

    private static void TryRecordTrace(List<SdfAdaptiveTraceEvent> trace, int maxTraceEvents, RegionWorkItem item, CirRegionPlanResult plan)
    {
        if (trace.Count >= maxTraceEvents)
        {
            return;
        }

        trace.Add(new SdfAdaptiveTraceEvent(item.RegionId, item.ParentRegionId, item.Depth, plan.Action, plan.SelectedCandidate, plan.Interval, plan.Note));
    }

    private static double SampleRegionVolume(SdfTape tape, SdfBounds region, int grid)
    {
        var sampleGrid = int.Max(grid, 1);
        var dx = region.SizeX / sampleGrid;
        var dy = region.SizeY / sampleGrid;
        var dz = region.SizeZ / sampleGrid;
        var insideCount = 0;
        var totalSamples = sampleGrid * sampleGrid * sampleGrid;

        for (var ix = 0; ix < sampleGrid; ix++)
        for (var iy = 0; iy < sampleGrid; iy++)
        for (var iz = 0; iz < sampleGrid; iz++)
        {
            var point = new Point3D(region.Min.X + ((ix + 0.5d) * dx), region.Min.Y + ((iy + 0.5d) * dy), region.Min.Z + ((iz + 0.5d) * dz));
            if (tape.Evaluate(point) <= 0d)
            {
                insideCount++;
            }
        }

        return RegionVolume(region) * (insideCount / (double)totalSamples);
    }

    private static double RegionVolume(SdfBounds region) => double.Max(region.SizeX, 0d) * double.Max(region.SizeY, 0d) * double.Max(region.SizeZ, 0d);

    private static IReadOnlyList<SdfBounds> SplitLongestAxis(SdfBounds region)
    {
        var sizeX = region.SizeX;
        var sizeY = region.SizeY;
        var sizeZ = region.SizeZ;

        if (sizeX >= sizeY && sizeX >= sizeZ)
        {
            var mid = (region.Min.X + region.Max.X) * 0.5d;
            return
            [
                new SdfBounds(region.Min, new Point3D(mid, region.Max.Y, region.Max.Z)),
                new SdfBounds(new Point3D(mid, region.Min.Y, region.Min.Z), region.Max),
            ];
        }

        if (sizeY >= sizeX && sizeY >= sizeZ)
        {
            var mid = (region.Min.Y + region.Max.Y) * 0.5d;
            return
            [
                new SdfBounds(region.Min, new Point3D(region.Max.X, mid, region.Max.Z)),
                new SdfBounds(new Point3D(region.Min.X, mid, region.Min.Z), region.Max),
            ];
        }

        var midZ = (region.Min.Z + region.Max.Z) * 0.5d;
        return
        [
            new SdfBounds(region.Min, new Point3D(region.Max.X, region.Max.Y, midZ)),
            new SdfBounds(new Point3D(region.Min.X, region.Min.Y, midZ), region.Max),
        ];
    }

    private readonly record struct RegionWorkItem(int RegionId, int? ParentRegionId, SdfBounds Region, int Depth);
}
