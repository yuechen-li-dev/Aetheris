using Aetheris.Kernel.Core.Geometry;

namespace Aetheris.Kernel.Core.Tests.Geometry;

public sealed class ParameterIntervalTests
{
    [Fact]
    public void Constructor_ValidRange_AllowsEqualOrIncreasingBounds()
    {
        var degenerate = new ParameterInterval(2, 2);
        var regular = new ParameterInterval(-1, 5);

        Assert.Equal(2, degenerate.Start);
        Assert.Equal(2, degenerate.End);
        Assert.Equal(-1, regular.Start);
        Assert.Equal(5, regular.End);
    }

    [Fact]
    public void Constructor_InvalidRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParameterInterval(5, 4));
    }

    [Fact]
    public void Contains_InclusiveBoundarySemantics()
    {
        var interval = new ParameterInterval(1, 3);

        Assert.True(interval.Contains(1));
        Assert.True(interval.Contains(3));
        Assert.True(interval.Contains(2));
        Assert.False(interval.Contains(0.5));
        Assert.False(interval.Contains(3.5));
    }
}
