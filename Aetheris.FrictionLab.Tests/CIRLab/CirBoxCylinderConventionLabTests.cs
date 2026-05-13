using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class CirBoxCylinderConventionLabTests
{
    [Fact]
    public void Box_IsCenteredOnOrigin_WithHalfExtents()
    {
        var box = new CirBoxNode(10d, 6d, 4d);
        Assert.Equal(new Point3D(-5d, -3d, -2d), box.Bounds.Min);
        Assert.Equal(new Point3D(5d, 3d, 2d), box.Bounds.Max);
        Assert.True(box.Evaluate(Point3D.Origin) < 0d);
    }

    [Fact]
    public void Cylinder_IsZAxisCenteredOnOrigin_WithHalfHeightExtents()
    {
        var cylinder = new CirCylinderNode(3d, 8d);
        Assert.Equal(new Point3D(-3d, -3d, -4d), cylinder.Bounds.Min);
        Assert.Equal(new Point3D(3d, 3d, 4d), cylinder.Bounds.Max);
        Assert.True(cylinder.Evaluate(new Point3D(0d, 0d, 3d)) < 0d);
        Assert.True(cylinder.Evaluate(new Point3D(3.1d, 0d, 0d)) > 0d);
    }
}
