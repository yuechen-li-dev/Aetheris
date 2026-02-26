using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Tests;

public sealed class ToleranceMathTests
{
    [Fact]
    public void AlmostEqual_WithinTolerance_ReturnsTrue()
    {
        var result = ToleranceMath.AlmostEqual(10, 10.0009, 0.001);

        Assert.True(result);
    }

    [Fact]
    public void AlmostEqual_OutsideTolerance_ReturnsFalse()
    {
        var result = ToleranceMath.AlmostEqual(10, 10.002, 0.001);

        Assert.False(result);
    }

    [Fact]
    public void AlmostZero_UsesInclusiveBoundary()
    {
        Assert.True(ToleranceMath.AlmostZero(0.001, 0.001));
        Assert.False(ToleranceMath.AlmostZero(0.0011, 0.001));
    }

    [Fact]
    public void LessThanOrAlmostEqual_HandlesBelowWithinAndAboveTolerance()
    {
        Assert.True(ToleranceMath.LessThanOrAlmostEqual(9.99, 10, 0.001));
        Assert.True(ToleranceMath.LessThanOrAlmostEqual(10.001, 10, 0.001));
        Assert.False(ToleranceMath.LessThanOrAlmostEqual(10.002, 10, 0.001));
    }

    [Fact]
    public void GreaterThanOrAlmostEqual_HandlesAboveWithinAndBelowTolerance()
    {
        Assert.True(ToleranceMath.GreaterThanOrAlmostEqual(10.01, 10, 0.001));
        Assert.True(ToleranceMath.GreaterThanOrAlmostEqual(9.999, 10, 0.001));
        Assert.False(ToleranceMath.GreaterThanOrAlmostEqual(9.998, 10, 0.001));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Methods_InvalidTolerance_Throws(double tolerance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ToleranceMath.AlmostEqual(1, 1, tolerance));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToleranceMath.AlmostZero(0, tolerance));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToleranceMath.LessThanOrAlmostEqual(1, 1, tolerance));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToleranceMath.GreaterThanOrAlmostEqual(1, 1, tolerance));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToleranceMath.ClampToZero(0, tolerance));
    }

    [Fact]
    public void ContextOverload_UsesLinearToleranceConsistently()
    {
        var context = new ToleranceContext(linear: 0.001, angular: 1e-8);

        var byContext = ToleranceMath.AlmostEqual(5, 5.0008, context);
        var byLinear = ToleranceMath.AlmostEqual(5, 5.0008, context.Linear);

        Assert.Equal(byLinear, byContext);
    }
}
