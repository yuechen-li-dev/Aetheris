using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Tests.Math;

public sealed class Primitives3DTests
{
    [Fact]
    public void VectorArithmetic_WorksAsExpected()
    {
        var a = new Vector3D(1, 2, 3);
        var b = new Vector3D(4, 5, 6);

        Assert.Equal(new Vector3D(5, 7, 9), a + b);
        Assert.Equal(new Vector3D(-3, -3, -3), a - b);
        Assert.Equal(new Vector3D(2, 4, 6), a * 2);
        Assert.Equal(new Vector3D(0.5, 1, 1.5), a / 2);
    }

    [Fact]
    public void DotAndCross_BasicsAreCorrect()
    {
        var x = new Vector3D(1, 0, 0);
        var y = new Vector3D(0, 1, 0);

        Assert.Equal(0, x.Dot(y));
        Assert.Equal(new Vector3D(0, 0, 1), x.Cross(y));
    }

    [Fact]
    public void LengthAndLengthSquared_AreConsistent()
    {
        var vector = new Vector3D(2, 3, 6);

        Assert.Equal(49, vector.LengthSquared);
        Assert.Equal(7, vector.Length, 10);
    }

    [Fact]
    public void Normalize_NonZeroVector_Succeeds()
    {
        var vector = new Vector3D(10, 0, 0);

        var success = vector.TryNormalize(out var normalized);

        Assert.True(success);
        Assert.Equal(new Vector3D(1, 0, 0), normalized);
    }

    [Fact]
    public void Normalize_ZeroVector_Fails()
    {
        var success = Vector3D.Zero.TryNormalize(out var normalized);

        Assert.False(success);
        Assert.Equal(Vector3D.Zero, normalized);
    }

    [Fact]
    public void PointAndVectorOperators_HaveExpectedSemantics()
    {
        var point = new Point3D(10, 20, 30);
        var vector = new Vector3D(1, 2, 3);

        Assert.Equal(new Point3D(11, 22, 33), point + vector);
        Assert.Equal(new Point3D(9, 18, 27), point - vector);
        Assert.Equal(vector, (point + vector) - point);
    }

    [Fact]
    public void DirectionCreate_NormalizesInput()
    {
        var direction = Direction3D.Create(new Vector3D(0, 3, 0));

        Assert.Equal(new Vector3D(0, 1, 0), direction.ToVector());
    }

    [Fact]
    public void DirectionCreate_RejectsNearZeroInput()
    {
        var context = new ToleranceContext(linear: 1e-6, angular: 1e-9);

        Assert.False(Direction3D.TryCreate(new Vector3D(1e-8, 0, 0), out _, context));
        Assert.Throws<ArgumentOutOfRangeException>(() => Direction3D.Create(new Vector3D(1e-8, 0, 0), context));
    }
}
