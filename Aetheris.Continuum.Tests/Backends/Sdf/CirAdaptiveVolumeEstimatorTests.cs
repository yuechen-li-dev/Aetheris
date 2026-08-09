using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Tests.Backends.Sdf;

public sealed class SdfAdaptiveVolumeEstimatorTests
{
    [Fact]
    public void AdaptiveVolume_Box_IsReasonable()
    {
        var box = new SdfBoxNode(6d, 4d, 2d);
        var tape = SdfTapeLowerer.Lower(box);

        var result = SdfAdaptiveVolumeEstimator.EstimateVolume(tape, box.Bounds, new SdfAdaptiveVolumeOptions(MaxDepth: 5, DirectSampleGrid: 2));

        Assert.InRange(result.EstimatedVolume, 47.5d, 48.5d);
    }

    [Fact]
    public void AdaptiveVolume_BoxMinusCylinder_IsReasonable()
    {
        var box = new SdfBoxNode(8d, 8d, 8d);
        var cut = new SdfSubtractNode(box, new SdfCylinderNode(2d, 8d));
        var tape = SdfTapeLowerer.Lower(cut);
        var expected = (8d * 8d * 8d) - (System.Math.PI * 4d * 8d);

        var result = SdfAdaptiveVolumeEstimator.EstimateVolume(tape, box.Bounds, new SdfAdaptiveVolumeOptions(MaxDepth: 7, DirectSampleGrid: 2));

        Assert.InRange(result.EstimatedVolume, expected - 15d, expected + 15d);
    }

    [Fact]
    public void AdaptiveVolume_UsesPlannerCounters()
    {
        var box = new SdfBoxNode(8d, 8d, 8d);
        var tape = SdfTapeLowerer.Lower(box);
        var options = new SdfAdaptiveVolumeOptions(MaxDepth: 6, DirectSampleGrid: 2);

        var insideRegion = new SdfBounds(new Point3D(-1d, -1d, -1d), new Point3D(1d, 1d, 1d));
        var outsideRegion = new SdfBounds(new Point3D(10d, 10d, 10d), new Point3D(12d, 12d, 12d));
        var mixedRegion = new SdfBounds(new Point3D(-6d, -6d, -6d), new Point3D(6d, 6d, 6d));

        var insideResult = SdfAdaptiveVolumeEstimator.EstimateVolume(tape, insideRegion, options);
        var outsideResult = SdfAdaptiveVolumeEstimator.EstimateVolume(tape, outsideRegion, options);
        var mixedResult = SdfAdaptiveVolumeEstimator.EstimateVolume(tape, mixedRegion, options);

        Assert.True(insideResult.RegionsClassifiedInside > 0);
        Assert.True(outsideResult.RegionsClassifiedOutside > 0);
        Assert.True(mixedResult.RegionsSubdivided > 0 || mixedResult.RegionsSampledDirectly > 0);
    }

    [Fact]
    public void AdaptiveVolume_Deterministic()
    {
        var cut = new SdfSubtractNode(new SdfBoxNode(8d, 8d, 8d), new SdfCylinderNode(2d, 8d));
        var tape = SdfTapeLowerer.Lower(cut);
        var options = new SdfAdaptiveVolumeOptions(MaxDepth: 6, DirectSampleGrid: 2, MaxTraceEvents: 24);

        var first = SdfAdaptiveVolumeEstimator.EstimateVolume(tape, cut.Bounds, options);
        var second = SdfAdaptiveVolumeEstimator.EstimateVolume(tape, cut.Bounds, options);

        Assert.Equal(first.EstimatedVolume, second.EstimatedVolume);
        Assert.Equal(first.TotalRegionsVisited, second.TotalRegionsVisited);
        Assert.Equal(first.RegionsSubdivided, second.RegionsSubdivided);
        Assert.Equal(first.RegionsSampledDirectly, second.RegionsSampledDirectly);
        Assert.Equal(first.TraceEvents, second.TraceEvents);
    }

    [Fact]
    public void AdaptiveVolume_TraceIncludesPlannerDecision()
    {
        var cut = new SdfSubtractNode(new SdfBoxNode(8d, 8d, 8d), new SdfCylinderNode(2d, 8d));
        var tape = SdfTapeLowerer.Lower(cut);

        var result = SdfAdaptiveVolumeEstimator.EstimateVolume(tape, cut.Bounds, new SdfAdaptiveVolumeOptions(MaxDepth: 6, DirectSampleGrid: 2, MaxTraceEvents: 32));

        Assert.NotEmpty(result.TraceEvents);
        Assert.Contains(result.TraceEvents, e => !string.IsNullOrWhiteSpace(e.Candidate));
        Assert.Contains(result.TraceEvents, e => e.Action is CirRegionPlanAction.Subdivide or CirRegionPlanAction.SampleDirectly or CirRegionPlanAction.ClassifyInside or CirRegionPlanAction.ClassifyOutside);
    }

    [Fact]
    public void NaiveVolumeComparison()
    {
        var cut = new SdfSubtractNode(new SdfBoxNode(8d, 8d, 8d), new SdfCylinderNode(2d, 8d));
        var tape = SdfTapeLowerer.Lower(cut);
        var dense = SdfVolumeEstimator.EstimateVolume(cut, resolution: 30);

        var adaptive = SdfAdaptiveVolumeEstimator.EstimateVolume(tape, cut.Bounds, new SdfAdaptiveVolumeOptions(MaxDepth: 7, DirectSampleGrid: 2));

        Assert.InRange(System.Math.Abs(adaptive.EstimatedVolume - dense), 0d, 25d);
    }
}
