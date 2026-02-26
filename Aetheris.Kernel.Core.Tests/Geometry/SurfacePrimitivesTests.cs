using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Tests.Geometry;

public sealed class SurfacePrimitivesTests
{
    [Fact]
    public void Plane_EvaluateAndNormal_AreConsistent()
    {
        var plane = new PlaneSurface(
            origin: new Point3D(1, 2, 3),
            normal: Direction3D.Create(new Vector3D(0, 0, 1)),
            uAxis: Direction3D.Create(new Vector3D(1, 0, 0)));

        var point = plane.Evaluate(2, -3);

        AssertPoint(point, new Point3D(3, -1, 3));
        AssertVector(plane.Normal.ToVector(), new Vector3D(0, 0, 1));
        Assert.True(ToleranceMath.AlmostEqual(plane.Normal.ToVector().Length, 1, ToleranceContext.Default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Cylinder_InvalidRadius_Throws(double radius)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CylinderSurface(Point3D.Origin, Direction3D.Create(new Vector3D(0, 0, 1)), radius, Direction3D.Create(new Vector3D(1, 0, 0))));
    }

    [Fact]
    public void Cylinder_EvaluateAndNormal_AreCorrect()
    {
        var cylinder = new CylinderSurface(
            origin: Point3D.Origin,
            axis: Direction3D.Create(new Vector3D(0, 0, 1)),
            radius: 2,
            referenceAxis: Direction3D.Create(new Vector3D(1, 0, 0)));

        AssertPoint(cylinder.Evaluate(0, 3), new Point3D(2, 0, 3));
        AssertPoint(cylinder.Evaluate(double.Pi / 2d, 1), new Point3D(0, 2, 1));

        var point = cylinder.Evaluate(1.234, -2.5);
        var axialProjection = cylinder.Axis.ToVector() * ((point - cylinder.Origin).Dot(cylinder.Axis.ToVector()));
        var radial = (point - cylinder.Origin) - axialProjection;

        Assert.True(ToleranceMath.AlmostEqual(radial.Length, cylinder.Radius, ToleranceContext.Default));

        var normal = cylinder.Normal(double.Pi / 2d).ToVector();
        AssertVector(normal, new Vector3D(0, 1, 0));
        Assert.True(ToleranceMath.AlmostEqual(normal.Length, 1, ToleranceContext.Default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Sphere_InvalidRadius_Throws(double radius)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SphereSurface(Point3D.Origin, Direction3D.Create(new Vector3D(0, 0, 1)), radius, Direction3D.Create(new Vector3D(1, 0, 0))));
    }

    [Fact]
    public void Sphere_EvaluateAndNormal_AreCorrect()
    {
        var sphere = new SphereSurface(
            center: Point3D.Origin,
            axis: Direction3D.Create(new Vector3D(0, 0, 1)),
            radius: 3,
            referenceAxis: Direction3D.Create(new Vector3D(1, 0, 0)));

        AssertPoint(sphere.Evaluate(0, 0), new Point3D(3, 0, 0));
        AssertPoint(sphere.Evaluate(double.Pi / 2d, 0), new Point3D(0, 3, 0));
        AssertPoint(sphere.Evaluate(0, double.Pi / 2d), new Point3D(0, 0, 3));

        var point = sphere.Evaluate(1.2, -0.7);
        Assert.True(ToleranceMath.AlmostEqual((point - sphere.Center).Length, sphere.Radius, ToleranceContext.Default));

        var normal = sphere.Normal(1.2, -0.7).ToVector();
        var radial = Direction3D.Create(point - sphere.Center).ToVector();
        AssertVector(normal, radial);
        Assert.True(ToleranceMath.AlmostEqual(normal.Length, 1, ToleranceContext.Default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.Pi / 2d)]
    public void Cone_InvalidSemiAngle_Throws(double semiAngle)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ConeSurface(Point3D.Origin, Direction3D.Create(new Vector3D(0, 0, 1)), semiAngle, Direction3D.Create(new Vector3D(1, 0, 0))));
    }

    [Fact]
    public void Cone_EvaluateAndNormal_ArePlausible()
    {
        var semiAngle = double.Pi / 6d;
        var cone = new ConeSurface(
            apex: Point3D.Origin,
            axis: Direction3D.Create(new Vector3D(0, 0, 1)),
            semiAngleRadians: semiAngle,
            referenceAxis: Direction3D.Create(new Vector3D(1, 0, 0)));

        var point = cone.Evaluate(0, 4);
        AssertPoint(point, new Point3D(4 * double.Tan(semiAngle), 0, 4));

        var sample = cone.Evaluate(1.1, 3);
        var radialLength = double.Sqrt((sample.X * sample.X) + (sample.Y * sample.Y));
        Assert.True(ToleranceMath.AlmostEqual(radialLength, sample.Z * double.Tan(semiAngle), ToleranceContext.Default));

        var normal = cone.Normal(0).ToVector();
        Assert.True(ToleranceMath.AlmostEqual(normal.Length, 1, ToleranceContext.Default));
        Assert.True(normal.X > 0);
    }

    private static void AssertPoint(Point3D actual, Point3D expected) => AssertVector(actual - expected, Vector3D.Zero);

    private static void AssertVector(Vector3D actual, Vector3D expected)
    {
        Assert.True(ToleranceMath.AlmostEqual(actual.X, expected.X, ToleranceContext.Default));
        Assert.True(ToleranceMath.AlmostEqual(actual.Y, expected.Y, ToleranceContext.Default));
        Assert.True(ToleranceMath.AlmostEqual(actual.Z, expected.Z, ToleranceContext.Default));
    }
}
