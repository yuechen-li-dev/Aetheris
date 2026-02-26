using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Curves;

public readonly record struct Line3Curve(Point3D Origin, Direction3D Direction)
{
    public Point3D Evaluate(double t) => Origin + (Direction.ToVector() * t);

    public Direction3D Tangent(double t) => Direction;
}
