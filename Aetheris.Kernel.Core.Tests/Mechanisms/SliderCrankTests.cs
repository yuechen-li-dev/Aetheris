using Aetheris.Kernel.Core.Mechanisms;

namespace Aetheris.Kernel.Core.Tests.Mechanisms;

public sealed class SliderCrankTests
{
    [Theory]
    [InlineData(44, 145)]
    [InlineData(48, 155)]
    public void FullCycleClosesAtBothBanksAndFourCrankpinPhases(double r, double length)
    {
        var mechanism = new SliderCrank(r, length);
        foreach (var bank in new[] { -45d, 45d })
        foreach (var phase in new[] { 45d, 135d, 225d, 315d })
        for (var degree = 0; degree <= 720; degree++)
        {
            var pose = mechanism.Evaluate(degree * double.Pi / 180, bank * double.Pi / 180, phase * double.Pi / 180);
            Assert.InRange(double.Abs((pose.WristPin - pose.CrankPin).Length - length), 0, 1e-10);
            Assert.InRange(pose.WristPin.Cross(pose.BoreAxis).Length, 0, 1e-10);
            Assert.InRange(double.Abs(pose.CrankPin.Length - r), 0, 1e-10);
        }
        Assert.Equal(2 * r, mechanism.Evaluate(0).PistonPositionMm - mechanism.Evaluate(double.Pi).PistonPositionMm, 10);
        Assert.NotEqual(length, mechanism.Evaluate(double.Pi / 2).PistonPositionMm);
    }

    [Fact]
    public void RatioAndWrappedLiftAreBoundedAndPeriodic()
    {
        Assert.Equal(2 * double.Pi, PrescribedMotion.AngularRatio(4 * double.Pi, .5));
        for (var degree = -720; degree <= 1440; degree++)
        {
            var lift = PrescribedMotion.ValveLift(degree, 700, 180, 10);
            Assert.InRange(lift, 0, 10);
            Assert.Equal(lift, PrescribedMotion.ValveLift(degree + 720, 700, 180, 10), 12);
        }
        Assert.Equal(10, PrescribedMotion.ValveLift(70, 700, 180, 10), 12);
        Assert.Equal(0, PrescribedMotion.ValveLift(160, 700, 180, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SliderCrank(44, 44).Evaluate(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SliderCrank(44, 145).Evaluate(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrescribedMotion.ValveLift(0, 0, 720, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SliderCrank(44, double.MaxValue).Evaluate(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrescribedMotion.ValveLift(double.MaxValue, -double.MaxValue, 180, 10));
    }
}
