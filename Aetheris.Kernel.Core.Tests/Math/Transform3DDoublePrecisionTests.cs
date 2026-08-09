using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Math;

public sealed class Transform3DDoublePrecisionTests
{
    [Fact]
    public void RotationRoundTripIsAtDoublePrecision()
    {
        var transform=Transform3D.CreateRotationX(.317)*Transform3D.CreateRotationY(-.721)*Transform3D.CreateTranslation(new(.031,-.027,.019));
        var point=new Point3D(1.234567890123,-2.345678901234,3.456789012345);
        Assert.InRange((transform.Inverse().Apply(transform.Apply(point))-point).Length,0d,2e-15d);
    }

    [Fact]
    public void CompositionOrderRemainsFirstThenSecond()
    {
        var first=Transform3D.CreateTranslation(new(1,0,0));var second=Transform3D.CreateRotationZ(double.Pi/2d);
        var result=(first*second).Apply(Point3D.Origin);
        Assert.InRange((result-new Point3D(0,1,0)).Length,0d,2e-15d);
    }
}
