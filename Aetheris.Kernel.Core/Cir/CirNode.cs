using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Cir;

internal enum CirNodeKind
{
    Box,
    Cylinder,
    Sphere,
    Union,
    Subtract,
    Intersect,
    Transform,
}

internal readonly record struct CirBounds(Point3D Min, Point3D Max)
{
    public double SizeX => Max.X - Min.X;
    public double SizeY => Max.Y - Min.Y;
    public double SizeZ => Max.Z - Min.Z;

    public static CirBounds Union(CirBounds left, CirBounds right) =>
        new(
            new Point3D(double.Min(left.Min.X, right.Min.X), double.Min(left.Min.Y, right.Min.Y), double.Min(left.Min.Z, right.Min.Z)),
            new Point3D(double.Max(left.Max.X, right.Max.X), double.Max(left.Max.Y, right.Max.Y), double.Max(left.Max.Z, right.Max.Z)));
}

internal abstract record CirNode(CirNodeKind Kind)
{
    public abstract CirBounds Bounds { get; }

    public abstract double Evaluate(Point3D point);
}
