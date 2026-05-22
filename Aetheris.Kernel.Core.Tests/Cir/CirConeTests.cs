using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Cir;

public sealed class CirConeTests
{
    [Fact]
    public void CirCone_EvaluatesInsideOutsideForFrustum()
    {
        var cone = new CirConeNode(bottomRadius: 4d, topRadius: 2d, height: 8d);

        Assert.True(cone.Evaluate(new Point3D(0d, 0d, 0d)) < 0d);
        Assert.True(cone.Evaluate(new Point3D(5d, 0d, 0d)) > 0d);
        Assert.True(cone.Evaluate(new Point3D(0d, 0d, 5d)) > 0d);
    }

    [Fact]
    public void CirCone_EvaluatesPointConeApexBehavior()
    {
        var cone = new CirConeNode(bottomRadius: 4d, topRadius: 0d, height: 8d);

        var apex = cone.Evaluate(new Point3D(0d, 0d, 4d));
        var nearApex = cone.Evaluate(new Point3D(0.01d, 0d, 3.99d));
        var center = cone.Evaluate(new Point3D(0d, 0d, 0d));

        Assert.False(double.IsNaN(apex) || double.IsInfinity(apex));
        Assert.False(double.IsNaN(nearApex) || double.IsInfinity(nearApex));
        Assert.False(double.IsNaN(center) || double.IsInfinity(center));
        Assert.InRange(double.Abs(apex), 0d, 1e-6d);
        Assert.True(center < 0d);
    }

    [Fact]
    public void CirCone_RejectsInvalidParameters()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CirConeNode(-1d, 1d, 2d));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CirConeNode(1d, -1d, 2d));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CirConeNode(0d, 0d, 2d));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CirConeNode(1d, 1d, 0d));
    }

    [Fact]
    public void CirCone_TapeLowering_EvaluatesDeterministically()
    {
        var node = new CirTransformNode(new CirConeNode(5d, 2d, 10d), Transform3D.CreateTranslation(new Vector3D(1d, -2d, 3d)));
        var tapeA = CirTapeLowerer.Lower(node);
        var tapeB = CirTapeLowerer.Lower(node);

        Assert.Equal(tapeA.Instructions, tapeB.Instructions);
        Assert.Equal(tapeA.ConePayloads, tapeB.ConePayloads);

        var probe = new Point3D(1.5d, -2d, 2d);
        Assert.Equal(tapeA.Evaluate(probe), tapeB.Evaluate(probe), 12);
        Assert.Equal(node.Evaluate(probe), tapeA.Evaluate(probe), 9);
    }
}
