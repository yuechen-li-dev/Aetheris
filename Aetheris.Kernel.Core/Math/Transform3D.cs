using System.Numerics;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Math;

public readonly struct Transform3D
{
    private readonly Matrix4x4 _matrix;

    private Transform3D(Matrix4x4 matrix)
    {
        _matrix = matrix;
    }

    public static Transform3D Identity { get; } = new(Matrix4x4.Identity);

    public static Transform3D CreateTranslation(Vector3D translation) =>
        new(Matrix4x4.CreateTranslation((float)translation.X, (float)translation.Y, (float)translation.Z));

    public static Transform3D CreateScale(double uniformScale) =>
        new(Matrix4x4.CreateScale((float)uniformScale));

    public static Transform3D CreateScale(Vector3D scale) =>
        new(Matrix4x4.CreateScale((float)scale.X, (float)scale.Y, (float)scale.Z));

    public static Transform3D CreateRotationX(double radians) => new(Matrix4x4.CreateRotationX((float)radians));

    public static Transform3D CreateRotationY(double radians) => new(Matrix4x4.CreateRotationY((float)radians));

    public static Transform3D CreateRotationZ(double radians) => new(Matrix4x4.CreateRotationZ((float)radians));

    public static Transform3D operator *(Transform3D left, Transform3D right) =>
        new(Matrix4x4.Multiply(left._matrix, right._matrix));

    public static Transform3D Compose(Transform3D first, Transform3D second) => first * second;

    public bool TryInverse(out Transform3D inverse)
    {
        var success = Matrix4x4.Invert(_matrix, out var inverseMatrix);
        inverse = success ? new Transform3D(inverseMatrix) : default;
        return success;
    }

    public Transform3D Inverse()
    {
        if (!TryInverse(out var inverse))
        {
            throw new InvalidOperationException("Transform is singular and cannot be inverted.");
        }

        return inverse;
    }

    public Point3D Apply(Point3D point)
    {
        var transformed = Vector3.Transform(ToNumerics(point), _matrix);
        return new Point3D(transformed.X, transformed.Y, transformed.Z);
    }

    public Vector3D Apply(Vector3D vector)
    {
        var transformed = Vector3.TransformNormal(ToNumerics(vector), _matrix);
        return new Vector3D(transformed.X, transformed.Y, transformed.Z);
    }

    public Direction3D Apply(Direction3D direction, ToleranceContext? toleranceContext = null)
    {
        var transformed = Apply(direction.ToVector());
        if (!Direction3D.TryCreate(transformed, out var transformedDirection, toleranceContext))
        {
            throw new InvalidOperationException("Transform collapses direction to near-zero length.");
        }

        return transformedDirection;
    }

    private static Vector3 ToNumerics(Point3D point) => new((float)point.X, (float)point.Y, (float)point.Z);

    private static Vector3 ToNumerics(Vector3D vector) => new((float)vector.X, (float)vector.Y, (float)vector.Z);
}
