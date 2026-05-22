using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Cir;

public sealed class CirAdaptiveVolumeEstimatorTests
{
    [Fact]
    public void AdaptiveVolume_Box_IsReasonable()
    {
        var box = new CirBoxNode(6d, 4d, 2d);
        var tape = CirTapeLowerer.Lower(box);

        var result = CirAdaptiveVolumeEstimator.EstimateVolume(tape, box.Bounds, new CirAdaptiveVolumeOptions(MaxDepth: 5, DirectSampleGrid: 2));

        Assert.InRange(result.EstimatedVolume, 47.5d, 48.5d);
    }

    [Fact]
    public void AdaptiveVolume_BoxMinusCylinder_IsReasonable()
    {
        var box = new CirBoxNode(8d, 8d, 8d);
        var cut = new CirSubtractNode(box, new CirCylinderNode(2d, 8d));
        var tape = CirTapeLowerer.Lower(cut);
        var expected = (8d * 8d * 8d) - (System.Math.PI * 4d * 8d);

        var result = CirAdaptiveVolumeEstimator.EstimateVolume(tape, box.Bounds, new CirAdaptiveVolumeOptions(MaxDepth: 7, DirectSampleGrid: 2));

        Assert.InRange(result.EstimatedVolume, expected - 15d, expected + 15d);
    }

    [Fact]
    public void AdaptiveVolume_UsesPlannerCounters()
    {
        var box = new CirBoxNode(8d, 8d, 8d);
        var tape = CirTapeLowerer.Lower(box);
        var options = new CirAdaptiveVolumeOptions(MaxDepth: 6, DirectSampleGrid: 2);

        var insideRegion = new CirBounds(new Point3D(-1d, -1d, -1d), new Point3D(1d, 1d, 1d));
        var outsideRegion = new CirBounds(new Point3D(10d, 10d, 10d), new Point3D(12d, 12d, 12d));
        var mixedRegion = new CirBounds(new Point3D(-6d, -6d, -6d), new Point3D(6d, 6d, 6d));

        var insideResult = CirAdaptiveVolumeEstimator.EstimateVolume(tape, insideRegion, options);
        var outsideResult = CirAdaptiveVolumeEstimator.EstimateVolume(tape, outsideRegion, options);
        var mixedResult = CirAdaptiveVolumeEstimator.EstimateVolume(tape, mixedRegion, options);

        Assert.True(insideResult.RegionsClassifiedInside > 0);
        Assert.True(outsideResult.RegionsClassifiedOutside > 0);
        Assert.True(mixedResult.RegionsSubdivided > 0 || mixedResult.RegionsSampledDirectly > 0);
    }

    [Fact]
    public void AdaptiveVolume_Deterministic()
    {
        var cut = new CirSubtractNode(new CirBoxNode(8d, 8d, 8d), new CirCylinderNode(2d, 8d));
        var tape = CirTapeLowerer.Lower(cut);
        var options = new CirAdaptiveVolumeOptions(MaxDepth: 6, DirectSampleGrid: 2, MaxTraceEvents: 24);

        var first = CirAdaptiveVolumeEstimator.EstimateVolume(tape, cut.Bounds, options);
        var second = CirAdaptiveVolumeEstimator.EstimateVolume(tape, cut.Bounds, options);

        Assert.Equal(first.EstimatedVolume, second.EstimatedVolume);
        Assert.Equal(first.TotalRegionsVisited, second.TotalRegionsVisited);
        Assert.Equal(first.RegionsSubdivided, second.RegionsSubdivided);
        Assert.Equal(first.RegionsSampledDirectly, second.RegionsSampledDirectly);
        Assert.Equal(first.TraceEvents, second.TraceEvents);
    }

    [Fact]
    public void AdaptiveVolume_TraceIncludesPlannerDecision()
    {
        var cut = new CirSubtractNode(new CirBoxNode(8d, 8d, 8d), new CirCylinderNode(2d, 8d));
        var tape = CirTapeLowerer.Lower(cut);

        var result = CirAdaptiveVolumeEstimator.EstimateVolume(tape, cut.Bounds, new CirAdaptiveVolumeOptions(MaxDepth: 6, DirectSampleGrid: 2, MaxTraceEvents: 32));

        Assert.NotEmpty(result.TraceEvents);
        Assert.Contains(result.TraceEvents, e => !string.IsNullOrWhiteSpace(e.Candidate));
        Assert.Contains(result.TraceEvents, e => e.Action is CirRegionPlanAction.Subdivide or CirRegionPlanAction.SampleDirectly or CirRegionPlanAction.ClassifyInside or CirRegionPlanAction.ClassifyOutside);
    }

    [Fact]
    public void NaiveVolumeComparison()
    {
        var cut = new CirSubtractNode(new CirBoxNode(8d, 8d, 8d), new CirCylinderNode(2d, 8d));
        var tape = CirTapeLowerer.Lower(cut);
        var dense = CirVolumeEstimator.EstimateVolume(cut, resolution: 30);

        var adaptive = CirAdaptiveVolumeEstimator.EstimateVolume(tape, cut.Bounds, new CirAdaptiveVolumeOptions(MaxDepth: 7, DirectSampleGrid: 2));

        Assert.InRange(System.Math.Abs(adaptive.EstimatedVolume - dense), 0d, 25d);
    }
}
