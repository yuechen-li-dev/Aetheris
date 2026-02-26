using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Tests.Geometry;

public sealed class CurvePrimitivesTests
{
    [Fact]
    public void Line_EvaluateAndTangent_FollowDirection()
    {
        var line = new Line3Curve(new Point3D(1, 2, 3), Direction3D.Create(new Vector3D(2, 0, 0)));

        Assert.Equal(new Point3D(1, 2, 3), line.Evaluate(0));
        Assert.Equal(new Point3D(3, 2, 3), line.Evaluate(2));
        Assert.Equal(new Point3D(-1, 2, 3), line.Evaluate(-2));
        Assert.Equal(line.Direction, line.Tangent(0));
        Assert.Equal(line.Direction, line.Tangent(10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Circle_InvalidRadius_Throws(double radius)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Circle3Curve(Point3D.Origin, Direction3D.Create(new Vector3D(0, 0, 1)), radius, Direction3D.Create(new Vector3D(1, 0, 0))));
    }

    [Fact]
    public void Circle_Evaluate_KnownAnglesAndOrientation()
    {
        var circle = new Circle3Curve(
            center: Point3D.Origin,
            normal: Direction3D.Create(new Vector3D(0, 0, 1)),
            radius: 2,
            referenceAxis: Direction3D.Create(new Vector3D(1, 0, 0)));

        AssertPoint(circle.Evaluate(0), new Point3D(2, 0, 0));
        AssertPoint(circle.Evaluate(double.Pi / 2d), new Point3D(0, 2, 0));
        AssertPoint(circle.Evaluate(double.Pi), new Point3D(-2, 0, 0));

        var tangentAtZero = circle.Tangent(0);
        AssertVector(tangentAtZero, new Vector3D(0, 2, 0));

        var cross = circle.XAxis.ToVector().Cross(circle.YAxis.ToVector());
        AssertVector(cross, circle.Normal.ToVector());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.1)]
    [InlineData(2.7)]
    public void Circle_EvaluatedPoints_StayAtRadius(double angle)
    {
        var circle = new Circle3Curve(
            center: new Point3D(4, -3, 1),
            normal: Direction3D.Create(new Vector3D(0, 0, 1)),
            radius: 3,
            referenceAxis: Direction3D.Create(new Vector3D(1, 0, 0)));

        var point = circle.Evaluate(angle);
        var distance = (point - circle.Center).Length;

        Assert.True(ToleranceMath.AlmostEqual(distance, circle.Radius, ToleranceContext.Default));
    }

    private static void AssertPoint(Point3D actual, Point3D expected)
    {
        AssertVector(actual - expected, Vector3D.Zero);
    }

    private static void AssertVector(Vector3D actual, Vector3D expected)
    {
        Assert.True(ToleranceMath.AlmostEqual(actual.X, expected.X, ToleranceContext.Default));
        Assert.True(ToleranceMath.AlmostEqual(actual.Y, expected.Y, ToleranceContext.Default));
        Assert.True(ToleranceMath.AlmostEqual(actual.Z, expected.Z, ToleranceContext.Default));
    }
}
