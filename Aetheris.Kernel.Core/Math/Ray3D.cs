namespace Aetheris.Kernel.Core.Math;

public readonly record struct Ray3D(Point3D Origin, Direction3D Direction)
{
    public Point3D PointAt(double parameter) => Origin + (Direction.ToVector() * parameter);
}
