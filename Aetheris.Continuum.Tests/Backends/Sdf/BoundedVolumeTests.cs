using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Tests.Backends.Sdf;

public sealed class BoundedVolumeTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(10001)]
    [InlineData(100001)]
    public void SphereEnclosureContainsAnalyticVolumeEvenWhenBudgetRunsOut(int budget)
    {
        var sphere = new SdfSphereNode(10);
        var result = SdfAdaptiveVolumeEstimator.EstimateBounded(SdfTapeLowerer.Lower(sphere), sphere.Bounds, 50, budget);
        var expected = 4 * Math.PI * 1000 / 3;
        Assert.InRange(expected, result.LowerBound, result.UpperBound);
        Assert.InRange(result.RegionsVisited, 1, budget);
        Assert.Equal(result, SdfAdaptiveVolumeEstimator.EstimateBounded(SdfTapeLowerer.Lower(sphere), sphere.Bounds, 50, budget));
        if (budget == 1) Assert.True(result.BudgetExhausted);
    }

    [Fact]
    public void ConeIntervalIncludesInteriorEvenWhenEveryCornerIsOutside()
    {
        var cone = new SdfConeNode(4, 2, 8);
        var tape = SdfTapeLowerer.Lower(cone);
        var interval = tape.EvaluateInterval(cone.Bounds);
        Assert.True(tape.Evaluate(new Point3D(0, 0, 0)) < 0);
        Assert.True(interval.MinValue < 0);
        var result = SdfAdaptiveVolumeEstimator.EstimateBounded(tape, cone.Bounds, 10, 30001);
        var expected = Math.PI * 8 * (16 + 8 + 4) / 3;
        Assert.InRange(expected, result.LowerBound, result.UpperBound);
    }

    [Fact]
    public void InvalidBudgetsDoNotStartUnboundedWork()
    {
        var sphere = new SdfSphereNode(1);
        var tape = SdfTapeLowerer.Lower(sphere);
        Assert.Throws<ArgumentException>(() => SdfAdaptiveVolumeEstimator.EstimateBounded(tape, sphere.Bounds, double.NaN));
        Assert.Throws<ArgumentException>(() => SdfAdaptiveVolumeEstimator.EstimateBounded(tape, sphere.Bounds, 0, 0));
    }

    [Fact]
    public void BooleanCavityBoundsContainTheIndependentAnalyticDifference()
    {
        var shell = new SdfSubtractNode(new SdfSphereNode(10), new SdfSphereNode(5));
        var result = SdfAdaptiveVolumeEstimator.EstimateBounded(SdfTapeLowerer.Lower(shell), shell.Bounds, 50, 100001);
        Assert.InRange(4 * Math.PI * (1000 - 125) / 3, result.LowerBound, result.UpperBound);
        Assert.True(result.LowerBound > 0);
    }
}
