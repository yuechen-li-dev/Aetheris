using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Tests;

public sealed class ToleranceContextTests
{
    [Fact]
    public void Constructor_WithPositiveFiniteValues_CreatesContext()
    {
        var context = new ToleranceContext(linear: 1e-5, angular: 1e-7, relative: 1e-9);

        Assert.Equal(1e-5, context.Linear);
        Assert.Equal(1e-7, context.Angular);
        Assert.Equal(1e-9, context.Relative);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_InvalidLinear_Throws(double linear)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToleranceContext(linear, angular: 1e-7));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_InvalidAngular_Throws(double angular)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToleranceContext(linear: 1e-6, angular));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_InvalidRelative_Throws(double relative)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToleranceContext(linear: 1e-6, angular: 1e-7, relative));
    }

    [Fact]
    public void Default_HasPositiveFiniteValues()
    {
        var context = ToleranceContext.Default;

        Assert.True(double.IsFinite(context.Linear));
        Assert.True(double.IsFinite(context.Angular));
        Assert.True(double.IsFinite(context.Relative));
        Assert.True(context.Linear > 0);
        Assert.True(context.Angular > 0);
        Assert.True(context.Relative > 0);
    }
}
