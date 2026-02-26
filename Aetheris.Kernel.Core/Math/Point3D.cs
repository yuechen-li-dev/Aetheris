namespace Aetheris.Kernel.Core.Math;

public readonly record struct Point3D(double X, double Y, double Z)
{
    public static Point3D Origin => new(0d, 0d, 0d);

    public static Point3D operator +(Point3D point, Vector3D vector) =>
        new(point.X + vector.X, point.Y + vector.Y, point.Z + vector.Z);

    public static Point3D operator -(Point3D point, Vector3D vector) =>
        new(point.X - vector.X, point.Y - vector.Y, point.Z - vector.Z);

    public static Vector3D operator -(Point3D left, Point3D right) =>
        new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
}
