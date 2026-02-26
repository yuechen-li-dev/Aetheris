using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Math;

public sealed class SpatialPrimitivesTests
{
    [Fact]
    public void BoundingBox_Construction_ValidatesMinLessThanOrEqualToMax()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BoundingBox3D(new Point3D(2, 0, 0), new Point3D(1, 0, 0)));
    }

    [Fact]
    public void BoundingBox_Contains_UsesInclusiveBoundaries()
    {
        var box = new BoundingBox3D(new Point3D(0, 0, 0), new Point3D(1, 1, 1));

        Assert.True(box.Contains(new Point3D(0, 0, 0)));
        Assert.True(box.Contains(new Point3D(1, 1, 1)));
        Assert.False(box.Contains(new Point3D(1.1, 1, 1)));
    }

    [Fact]
    public void BoundingBox_Expand_UpdatesBounds()
    {
        var box = new BoundingBox3D(new Point3D(0, 0, 0), new Point3D(1, 1, 1));

        var expanded = box.Expand(new Point3D(-1, 2, 0.5));

        Assert.Equal(new Point3D(-1, 0, 0), expanded.Min);
        Assert.Equal(new Point3D(1, 2, 1), expanded.Max);
    }

    [Fact]
    public void BoundingBox_Union_CombinesBothBoxes()
    {
        var a = new BoundingBox3D(new Point3D(0, 0, 0), new Point3D(1, 1, 1));
        var b = new BoundingBox3D(new Point3D(-2, 0.5, 0), new Point3D(0.5, 3, 2));

        var union = a.Union(b);

        Assert.Equal(new Point3D(-2, 0, 0), union.Min);
        Assert.Equal(new Point3D(1, 3, 2), union.Max);
    }

    [Fact]
    public void Ray_PointAt_ReturnsPointAlongDirection()
    {
        var ray = new Ray3D(new Point3D(1, 1, 1), Direction3D.Create(new Vector3D(2, 0, 0)));

        Assert.Equal(new Point3D(4, 1, 1), ray.PointAt(3));
    }
}
