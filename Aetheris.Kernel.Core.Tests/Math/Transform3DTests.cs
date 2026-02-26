using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Math;

public sealed class Transform3DTests
{
    [Fact]
    public void Identity_LeavesPointAndVectorUnchanged()
    {
        var point = new Point3D(1, 2, 3);
        var vector = new Vector3D(4, 5, 6);

        Assert.Equal(point, Transform3D.Identity.Apply(point));
        Assert.Equal(vector, Transform3D.Identity.Apply(vector));
    }

    [Fact]
    public void Translation_AffectsPointsButNotVectors()
    {
        var translation = Transform3D.CreateTranslation(new Vector3D(10, 0, 0));

        Assert.Equal(new Point3D(11, 2, 3), translation.Apply(new Point3D(1, 2, 3)));
        Assert.Equal(new Vector3D(1, 2, 3), translation.Apply(new Vector3D(1, 2, 3)));
    }

    [Fact]
    public void Scale_AffectsPointAndVector()
    {
        var scale = Transform3D.CreateScale(new Vector3D(2, 3, 4));

        Assert.Equal(new Point3D(2, 6, 12), scale.Apply(new Point3D(1, 2, 3)));
        Assert.Equal(new Vector3D(2, 6, 12), scale.Apply(new Vector3D(1, 2, 3)));
    }

    [Fact]
    public void RotationZ_RotatesKnownCase()
    {
        var rotation = Transform3D.CreateRotationZ(double.Pi / 2);

        var rotated = rotation.Apply(new Vector3D(1, 0, 0));

        Assert.True(double.Abs(rotated.X) < 1e-6);
        Assert.True(double.Abs(rotated.Y - 1) < 1e-6);
        Assert.True(double.Abs(rotated.Z) < 1e-6);
    }

    [Fact]
    public void Composition_OrderIsApplyFirstThenSecond()
    {
        var translation = Transform3D.CreateTranslation(new Vector3D(1, 0, 0));
        var scale = Transform3D.CreateScale(2);

        var composed = Transform3D.Compose(translation, scale);
        var transformed = composed.Apply(new Point3D(1, 0, 0));

        Assert.Equal(new Point3D(4, 0, 0), transformed);
    }

    [Fact]
    public void Inverse_NonSingular_RoundTripsPoint()
    {
        var transform = Transform3D.CreateTranslation(new Vector3D(1, 2, 3)) * Transform3D.CreateRotationZ(0.1);
        var point = new Point3D(5, -1, 2);

        var transformed = transform.Apply(point);
        var roundTripped = transform.Inverse().Apply(transformed);

        Assert.True(double.Abs(roundTripped.X - point.X) < 1e-5);
        Assert.True(double.Abs(roundTripped.Y - point.Y) < 1e-5);
        Assert.True(double.Abs(roundTripped.Z - point.Z) < 1e-5);
    }

    [Fact]
    public void Inverse_SingularTransform_FailsExplicitly()
    {
        var singular = Transform3D.CreateScale(0);

        Assert.False(singular.TryInverse(out _));
        Assert.Throws<InvalidOperationException>(() => singular.Inverse());
    }

    [Fact]
    public void ApplyDirection_ReNormalizesDirection()
    {
        var scale = Transform3D.CreateScale(new Vector3D(2, 1, 1));
        var direction = Direction3D.Create(new Vector3D(1, 1, 0));

        var transformed = scale.Apply(direction);

        Assert.True(double.Abs(transformed.ToVector().Length - 1) < 1e-6);
    }
}
