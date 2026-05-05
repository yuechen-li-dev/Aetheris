using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Cir;

public sealed record CirAdaptiveVolumeOptions(
    int MaxDepth = 6,
    int DirectSampleGrid = 2,
    int MaxTraceEvents = 64,
    double MinimumRegionExtent = 0.05d,
    bool TreatRejectUnknownAsSample = true)
{
    public static CirAdaptiveVolumeOptions Default { get; } = new();
}

public readonly record struct CirAdaptiveTraceEvent(
    int RegionId,
    int? ParentRegionId,
    int Depth,
    CirRegionPlanAction Action,
    string Candidate,
    FieldInterval Interval,
    string? Note);

public sealed record CirAdaptiveVolumeResult(
    double EstimatedVolume,
    CirAdaptiveVolumeOptions Options,
    int TotalRegionsVisited,
    int RegionsClassifiedInside,
    int RegionsClassifiedOutside,
    int RegionsSubdivided,
    int RegionsSampledDirectly,
    int UnknownOrRejectedRegions,
    int MaxDepthReached,
    IReadOnlyList<CirAdaptiveTraceEvent> TraceEvents,
    IReadOnlyList<string> Notes);

public static class CirAdaptiveVolumeEstimator
{
    public static CirAdaptiveVolumeResult EstimateVolume(CirTape tape, CirBounds bounds, CirAdaptiveVolumeOptions? options = null, ToleranceContext? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(tape);

        var effectiveOptions = options ?? CirAdaptiveVolumeOptions.Default;
        var effectiveTolerance = tolerance ?? ToleranceContext.Default;
        var planner = new CirRegionPlanner();
        var plannerOptions = new CirRegionPlannerOptions(effectiveOptions.MaxDepth, DirectSampleThreshold: effectiveOptions.DirectSampleGrid * effectiveOptions.DirectSampleGrid * effectiveOptions.DirectSampleGrid, effectiveOptions.MinimumRegionExtent);
        var queue = new Queue<RegionWorkItem>();
        var trace = new List<CirAdaptiveTraceEvent>(capacity: effectiveOptions.MaxTraceEvents);
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

        return new CirAdaptiveVolumeResult(totalVolume, effectiveOptions, totalVisited, inside, outside, subdivided, sampled, unknown, maxDepthReached, trace, notes);
    }

    private static void TryRecordTrace(List<CirAdaptiveTraceEvent> trace, int maxTraceEvents, RegionWorkItem item, CirRegionPlanResult plan)
    {
        if (trace.Count >= maxTraceEvents)
        {
            return;
        }

        trace.Add(new CirAdaptiveTraceEvent(item.RegionId, item.ParentRegionId, item.Depth, plan.Action, plan.SelectedCandidate, plan.Interval, plan.Note));
    }

    private static double SampleRegionVolume(CirTape tape, CirBounds region, int grid)
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

    private static double RegionVolume(CirBounds region) => double.Max(region.SizeX, 0d) * double.Max(region.SizeY, 0d) * double.Max(region.SizeZ, 0d);

    private static IReadOnlyList<CirBounds> SplitLongestAxis(CirBounds region)
    {
        var sizeX = region.SizeX;
        var sizeY = region.SizeY;
        var sizeZ = region.SizeZ;

        if (sizeX >= sizeY && sizeX >= sizeZ)
        {
            var mid = (region.Min.X + region.Max.X) * 0.5d;
            return
            [
                new CirBounds(region.Min, new Point3D(mid, region.Max.Y, region.Max.Z)),
                new CirBounds(new Point3D(mid, region.Min.Y, region.Min.Z), region.Max),
            ];
        }

        if (sizeY >= sizeX && sizeY >= sizeZ)
        {
            var mid = (region.Min.Y + region.Max.Y) * 0.5d;
            return
            [
                new CirBounds(region.Min, new Point3D(region.Max.X, mid, region.Max.Z)),
                new CirBounds(new Point3D(region.Min.X, mid, region.Min.Z), region.Max),
            ];
        }

        var midZ = (region.Min.Z + region.Max.Z) * 0.5d;
        return
        [
            new CirBounds(region.Min, new Point3D(region.Max.X, region.Max.Y, midZ)),
            new CirBounds(new Point3D(region.Min.X, region.Min.Y, midZ), region.Max),
        ];
    }

    private readonly record struct RegionWorkItem(int RegionId, int? ParentRegionId, CirBounds Region, int Depth);
}
