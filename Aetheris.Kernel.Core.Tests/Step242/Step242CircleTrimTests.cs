using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242CircleTrimTests
{
    private static Circle3Curve CircleAt(double radius)
        => new(Point3D.Origin, Direction3D.Create(new Vector3D(0d, 0d, 1d)), radius, Direction3D.Create(new Vector3D(1d, 0d, 0d)));

    private static Point3D OnCircle(double radius, double angle)
        => new(radius * double.Cos(angle), radius * double.Sin(angle), 0d);

    [Theory]
    [InlineData(25d, 0.011d)]
    [InlineData(25d, 0.002d)]
    [InlineData(100d, 0.004d)]
    public void ShortArcOnLargeCircle_IsNotTreatedAsFullCircle(double radius, double arcAngle)
    {
        // Gear-tooth sub-arcs: the chord (~0.27 mm) is well above the linear trim tolerance, but the arc angle is
        // smaller than that tolerance expressed as a raw number. It must not collapse into a 0..2pi trim.
        var trim = Step242Importer.ComputeCircleTrim(CircleAt(radius), OnCircle(radius, 0d), OnCircle(radius, arcAngle), edgeSameSense: true);

        Assert.True(trim.IsSuccess);
        Assert.Equal(arcAngle, trim.Value.End - trim.Value.Start, 6);
    }

    [Fact]
    public void ClosedCircle_StartsAtItsSeamVertex()
    {
        var seam = OnCircle(35d, double.Pi);

        var trim = Step242Importer.ComputeCircleTrim(CircleAt(35d), seam, seam, edgeSameSense: true);

        Assert.True(trim.IsSuccess);
        Assert.Equal(2d * double.Pi, trim.Value.End - trim.Value.Start, 9);
        Assert.Equal(double.Pi, trim.Value.Start, 9);
    }

    [Fact]
    public void ClosedCircle_WithSeamAtParameterOrigin_KeepsCanonicalTrim()
    {
        var trim = Step242Importer.ComputeCircleTrim(CircleAt(35d), OnCircle(35d, 0d), OnCircle(35d, 0d), edgeSameSense: true);

        Assert.True(trim.IsSuccess);
        Assert.Equal(0d, trim.Value.Start, 9);
        Assert.Equal(2d * double.Pi, trim.Value.End, 9);
    }
}
