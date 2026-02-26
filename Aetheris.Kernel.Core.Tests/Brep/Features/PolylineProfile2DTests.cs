using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;

namespace Aetheris.Kernel.Core.Tests.Brep.Features;

public sealed class PolylineProfile2DTests
{
    [Fact]
    public void Create_ValidPolygon_Succeeds()
    {
        var result = PolylineProfile2D.Create(
        [
            new ProfilePoint2D(0d, 0d),
            new ProfilePoint2D(2d, 0d),
            new ProfilePoint2D(1d, 1d),
        ]);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Vertices.Count);
    }

    [Fact]
    public void Create_TooFewVertices_Fails()
    {
        var result = PolylineProfile2D.Create(
        [
            new ProfilePoint2D(0d, 0d),
            new ProfilePoint2D(2d, 0d),
        ]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument);
    }

    [Fact]
    public void Create_DuplicateAdjacentVertices_Fails()
    {
        var result = PolylineProfile2D.Create(
        [
            new ProfilePoint2D(0d, 0d),
            new ProfilePoint2D(1d, 0d),
            new ProfilePoint2D(1d, 0d),
            new ProfilePoint2D(0d, 1d),
        ]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("zero-length segment", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_NonFiniteCoordinates_Fails()
    {
        var result = PolylineProfile2D.Create(
        [
            new ProfilePoint2D(0d, 0d),
            new ProfilePoint2D(double.NaN, 0d),
            new ProfilePoint2D(0d, 1d),
        ]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("finite", StringComparison.Ordinal));
    }

    [Fact]
    public void RectangleHelper_ProducesFourVerticesInCounterClockwiseOrder()
    {
        var rectangle = PolylineProfile2D.Rectangle(4d, 2d);

        Assert.Equal(4, rectangle.Vertices.Count);
        Assert.Equal(new ProfilePoint2D(-2d, -1d), rectangle.Vertices[0]);
        Assert.Equal(new ProfilePoint2D(2d, -1d), rectangle.Vertices[1]);
        Assert.Equal(new ProfilePoint2D(2d, 1d), rectangle.Vertices[2]);
        Assert.Equal(new ProfilePoint2D(-2d, 1d), rectangle.Vertices[3]);
    }
}
