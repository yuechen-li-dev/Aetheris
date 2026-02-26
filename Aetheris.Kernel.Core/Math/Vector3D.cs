using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Math;

public readonly record struct Vector3D(double X, double Y, double Z)
{
    public static Vector3D Zero => new(0d, 0d, 0d);

    public double LengthSquared => (X * X) + (Y * Y) + (Z * Z);

    public double Length => double.Sqrt(LengthSquared);

    public static Vector3D operator +(Vector3D left, Vector3D right) =>
        new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    public static Vector3D operator -(Vector3D left, Vector3D right) =>
        new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    public static Vector3D operator -(Vector3D vector) =>
        new(-vector.X, -vector.Y, -vector.Z);

    public static Vector3D operator *(Vector3D vector, double scalar) =>
        new(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);

    public static Vector3D operator *(double scalar, Vector3D vector) => vector * scalar;

    public static Vector3D operator /(Vector3D vector, double scalar)
    {
        if (ToleranceMath.AlmostZero(scalar, ToleranceContext.Default))
        {
            throw new DivideByZeroException("Cannot divide vector by a value that is approximately zero.");
        }

        return new Vector3D(vector.X / scalar, vector.Y / scalar, vector.Z / scalar);
    }

    public double Dot(Vector3D other) => (X * other.X) + (Y * other.Y) + (Z * other.Z);

    public Vector3D Cross(Vector3D other) =>
        new(
            (Y * other.Z) - (Z * other.Y),
            (Z * other.X) - (X * other.Z),
            (X * other.Y) - (Y * other.X));

    public bool TryNormalize(out Vector3D normalized, ToleranceContext? toleranceContext = null)
    {
        var context = toleranceContext ?? ToleranceContext.Default;
        var length = Length;
        if (ToleranceMath.AlmostZero(length, context))
        {
            normalized = Zero;
            return false;
        }

        normalized = this / length;
        return true;
    }
}
