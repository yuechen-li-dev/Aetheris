using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Math;

public readonly record struct Direction3D
{
    private Direction3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }

    public Vector3D ToVector() => new(X, Y, Z);

    public static Direction3D Create(Vector3D vector, ToleranceContext? toleranceContext = null)
    {
        if (!TryCreate(vector, out var direction, toleranceContext))
        {
            throw new ArgumentOutOfRangeException(nameof(vector), "Direction requires a non-zero vector within tolerance.");
        }

        return direction;
    }

    public static bool TryCreate(Vector3D vector, out Direction3D direction, ToleranceContext? toleranceContext = null)
    {
        if (!vector.TryNormalize(out var normalized, toleranceContext))
        {
            direction = default;
            return false;
        }

        direction = new Direction3D(normalized.X, normalized.Y, normalized.Z);
        return true;
    }
}
