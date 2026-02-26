using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Math;

public readonly record struct BoundingBox3D
{
    public BoundingBox3D(Point3D min, Point3D max)
    {
        var context = ToleranceContext.Default;
        if (!ToleranceMath.LessThanOrAlmostEqual(min.X, max.X, context) ||
            !ToleranceMath.LessThanOrAlmostEqual(min.Y, max.Y, context) ||
            !ToleranceMath.LessThanOrAlmostEqual(min.Z, max.Z, context))
        {
            throw new ArgumentOutOfRangeException(nameof(min), "Min point must be less than or equal to max point on each axis.");
        }

        Min = min;
        Max = max;
    }

    public Point3D Min { get; }

    public Point3D Max { get; }

    public bool Contains(Point3D point)
    {
        var context = ToleranceContext.Default;
        return ToleranceMath.GreaterThanOrAlmostEqual(point.X, Min.X, context)
               && ToleranceMath.LessThanOrAlmostEqual(point.X, Max.X, context)
               && ToleranceMath.GreaterThanOrAlmostEqual(point.Y, Min.Y, context)
               && ToleranceMath.LessThanOrAlmostEqual(point.Y, Max.Y, context)
               && ToleranceMath.GreaterThanOrAlmostEqual(point.Z, Min.Z, context)
               && ToleranceMath.LessThanOrAlmostEqual(point.Z, Max.Z, context);
    }

    public BoundingBox3D Expand(Point3D point)
    {
        var min = new Point3D(
            double.Min(Min.X, point.X),
            double.Min(Min.Y, point.Y),
            double.Min(Min.Z, point.Z));

        var max = new Point3D(
            double.Max(Max.X, point.X),
            double.Max(Max.Y, point.Y),
            double.Max(Max.Z, point.Z));

        return new BoundingBox3D(min, max);
    }

    public BoundingBox3D Union(BoundingBox3D other)
    {
        var min = new Point3D(
            double.Min(Min.X, other.Min.X),
            double.Min(Min.Y, other.Min.Y),
            double.Min(Min.Z, other.Min.Z));

        var max = new Point3D(
            double.Max(Max.X, other.Max.X),
            double.Max(Max.Y, other.Max.Y),
            double.Max(Max.Z, other.Max.Z));

        return new BoundingBox3D(min, max);
    }
}
