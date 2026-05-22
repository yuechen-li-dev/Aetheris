using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Cir;

public sealed record CirAdaptiveCalibrationPolicy(string Name, CirAdaptiveVolumeOptions AdaptiveOptions, int DenseResolution);

public sealed record CirAdaptiveCalibrationMetrics(
    string ShapeName,
    string PolicyName,
    double? ExpectedVolume,
    double DenseEstimate,
    double AdaptiveEstimate,
    double DenseAbsoluteError,
    double DenseRelativeError,
    double AdaptiveAbsoluteError,
    double AdaptiveRelativeError,
    double AdaptiveDenseDelta,
    int DenseSampleCount,
    int AdaptiveSampledPointCount,
    int RegionsVisited,
    int RegionsClassifiedInside,
    int RegionsClassifiedOutside,
    int RegionsSubdivided,
    int RegionsSampledDirectly,
    int UnknownOrRejectedRegions,
    int MaxDepthReached,
    IReadOnlyDictionary<CirRegionPlanAction, int> ActionCounts,
    IReadOnlyDictionary<string, int> CandidateCounts,
    IReadOnlyList<CirAdaptiveTraceEvent> TraceHead);

public sealed record CirAdaptiveCalibrationCase(
    string ShapeName,
    CirAdaptiveCalibrationPolicy Policy,
    CirAdaptiveCalibrationMetrics Metrics,
    IReadOnlyList<string> Notes);

public sealed record CirAdaptiveCalibrationReport(IReadOnlyList<CirAdaptiveCalibrationCase> Cases);

internal static class CirAdaptiveCalibrationHarness
{
    public static CirAdaptiveCalibrationReport Run(IEnumerable<(string ShapeName, CirNode Node, double? ExpectedVolume)> shapes, IEnumerable<CirAdaptiveCalibrationPolicy> policies)
    {
        var cases = new List<CirAdaptiveCalibrationCase>();

        foreach (var (shapeName, node, expectedVolume) in shapes)
        {
            var tape = CirTapeLowerer.Lower(node);
            foreach (var policy in policies)
            {
                var dense = CirVolumeEstimator.EstimateVolume(node, policy.DenseResolution);
                var adaptive = CirAdaptiveVolumeEstimator.EstimateVolume(tape, node.Bounds, policy.AdaptiveOptions);
                var denseSamples = policy.DenseResolution * policy.DenseResolution * policy.DenseResolution;
                var adaptiveSamplePoints = adaptive.RegionsSampledDirectly * policy.AdaptiveOptions.DirectSampleGrid * policy.AdaptiveOptions.DirectSampleGrid * policy.AdaptiveOptions.DirectSampleGrid;

                var actionCounts = adaptive.TraceEvents
                    .GroupBy(e => e.Action)
                    .ToDictionary(g => g.Key, g => g.Count());
                var candidateCounts = adaptive.TraceEvents
                    .Where(e => !string.IsNullOrWhiteSpace(e.Candidate))
                    .GroupBy(e => e.Candidate)
                    .ToDictionary(g => g.Key, g => g.Count());

                var denseAbsError = expectedVolume.HasValue ? System.Math.Abs(dense - expectedVolume.Value) : 0d;
                var adaptiveAbsError = expectedVolume.HasValue ? System.Math.Abs(adaptive.EstimatedVolume - expectedVolume.Value) : 0d;
                var denseRelError = expectedVolume.HasValue && expectedVolume.Value != 0d ? denseAbsError / expectedVolume.Value : 0d;
                var adaptiveRelError = expectedVolume.HasValue && expectedVolume.Value != 0d ? adaptiveAbsError / expectedVolume.Value : 0d;

                var metrics = new CirAdaptiveCalibrationMetrics(
                    shapeName,
                    policy.Name,
                    expectedVolume,
                    dense,
                    adaptive.EstimatedVolume,
                    denseAbsError,
                    denseRelError,
                    adaptiveAbsError,
                    adaptiveRelError,
                    System.Math.Abs(adaptive.EstimatedVolume - dense),
                    denseSamples,
                    adaptiveSamplePoints,
                    adaptive.TotalRegionsVisited,
                    adaptive.RegionsClassifiedInside,
                    adaptive.RegionsClassifiedOutside,
                    adaptive.RegionsSubdivided,
                    adaptive.RegionsSampledDirectly,
                    adaptive.UnknownOrRejectedRegions,
                    adaptive.MaxDepthReached,
                    actionCounts,
                    candidateCounts,
                    adaptive.TraceEvents.Take(12).ToArray());

                cases.Add(new CirAdaptiveCalibrationCase(shapeName, policy, metrics, adaptive.Notes));
            }
        }

        return new CirAdaptiveCalibrationReport(cases);
    }
}

public sealed class CirAdaptiveCalibrationTests
{
    [Fact]
    public void AdaptiveCalibration_Box_ReportIsDeterministic()
    {
        var shape = ("box", (CirNode)new CirBoxNode(6d, 4d, 2d), 48d as double?);
        var policy = ConservativePolicy();

        var first = CirAdaptiveCalibrationHarness.Run([shape], [policy]);
        var second = CirAdaptiveCalibrationHarness.Run([shape], [policy]);

        var a = first.Cases.Single().Metrics;
        var b = second.Cases.Single().Metrics;

        Assert.Equal(a.AdaptiveEstimate, b.AdaptiveEstimate);
        Assert.Equal(a.DenseEstimate, b.DenseEstimate);
        Assert.Equal(a.RegionsVisited, b.RegionsVisited);
        Assert.Equal(a.RegionsSubdivided, b.RegionsSubdivided);
        Assert.Equal(a.RegionsSampledDirectly, b.RegionsSampledDirectly);
        Assert.Equal(a.MaxDepthReached, b.MaxDepthReached);
        Assert.Equal(a.ActionCounts.OrderBy(kv => kv.Key).ToArray(), b.ActionCounts.OrderBy(kv => kv.Key).ToArray());
        Assert.Equal(a.CandidateCounts.OrderBy(kv => kv.Key).ToArray(), b.CandidateCounts.OrderBy(kv => kv.Key).ToArray());
        Assert.Equal(a.TraceHead, b.TraceHead);
    }

    [Fact]
    public void AdaptiveCalibration_BoxMinusCylinder_ReportsUsefulMetrics()
    {
        var shape = BuildBoxMinusCylinder();
        var report = CirAdaptiveCalibrationHarness.Run([shape], [ConservativePolicy()]);
        var metrics = report.Cases.Single().Metrics;

        Assert.True(metrics.DenseEstimate > 0d);
        Assert.True(metrics.AdaptiveEstimate > 0d);
        Assert.True(metrics.DenseSampleCount > 0);
        Assert.True(metrics.RegionsVisited > 0);
        Assert.True(metrics.ActionCounts.Count > 0);
        Assert.True(metrics.CandidateCounts.Count > 0);
    }

    [Fact]
    public void AdaptiveCalibration_AdaptiveWithinTolerance()
    {
        var report = CirAdaptiveCalibrationHarness.Run(BuildShapes(), [ConservativePolicy()]);

        foreach (var c in report.Cases)
        {
            Assert.True(c.Metrics.AdaptiveDenseDelta <= 18d, $"{c.ShapeName} delta too high: {c.Metrics.AdaptiveDenseDelta:R}");

            if (c.Metrics.ExpectedVolume.HasValue)
            {
                Assert.True(c.Metrics.AdaptiveRelativeError <= 0.08d, $"{c.ShapeName} relative error too high: {c.Metrics.AdaptiveRelativeError:R}");
            }
        }
    }

    [Fact]
    public void AdaptiveCalibration_PolicyComparisonShowsDifferentWorkProfiles()
    {
        var report = CirAdaptiveCalibrationHarness.Run(BuildShapes(), [ConservativePolicy(), CoarsePolicy()]);

        foreach (var shape in report.Cases.GroupBy(c => c.ShapeName))
        {
            var conservative = shape.Single(c => c.Policy.Name == "conservative").Metrics;
            var coarse = shape.Single(c => c.Policy.Name == "coarse").Metrics;
            Assert.True(
                conservative.RegionsVisited != coarse.RegionsVisited
                || conservative.AdaptiveSampledPointCount != coarse.AdaptiveSampledPointCount
                || conservative.MaxDepthReached != coarse.MaxDepthReached,
                $"Expected different work profile for {shape.Key}.");
        }
    }

    [Fact]
    public void AdaptiveCalibration_TraceSummaryPresent()
    {
        var report = CirAdaptiveCalibrationHarness.Run([BuildBoxMinusCylinder()], [ConservativePolicy()]);
        var metrics = report.Cases.Single().Metrics;

        Assert.NotEmpty(metrics.TraceHead);
        Assert.NotEmpty(metrics.ActionCounts);
        Assert.NotEmpty(metrics.CandidateCounts);
    }

    private static IEnumerable<(string ShapeName, CirNode Node, double? ExpectedVolume)> BuildShapes()
    {
        yield return ("box", new CirBoxNode(6d, 4d, 2d), 48d);
        yield return BuildBoxMinusCylinder();
        yield return ("sphere", new CirSphereNode(2d), (4d / 3d) * System.Math.PI * 8d);
        var transformedSphere = new CirTransformNode(new CirSphereNode(2d), Transform3D.CreateTranslation(new Vector3D(3d, -1d, 2d)));
        yield return ("transformed_sphere", transformedSphere, (4d / 3d) * System.Math.PI * 8d);
    }

    private static (string ShapeName, CirNode Node, double? ExpectedVolume) BuildBoxMinusCylinder()
    {
        var box = new CirBoxNode(8d, 8d, 8d);
        var cut = new CirSubtractNode(box, new CirCylinderNode(2d, 8d));
        var expected = (8d * 8d * 8d) - (System.Math.PI * 4d * 8d);
        return ("box_minus_cylinder", cut, expected);
    }

    private static CirAdaptiveCalibrationPolicy ConservativePolicy()
        => new("conservative", new CirAdaptiveVolumeOptions(MaxDepth: 8, DirectSampleGrid: 2, MaxTraceEvents: 80, MinimumRegionExtent: 0.04d), DenseResolution: 32);

    private static CirAdaptiveCalibrationPolicy CoarsePolicy()
        => new("coarse", new CirAdaptiveVolumeOptions(MaxDepth: 4, DirectSampleGrid: 3, MaxTraceEvents: 80, MinimumRegionExtent: 0.08d), DenseResolution: 32);
}
