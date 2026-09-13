using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// Exact semantic rigid transforms used before AIR/BRep materialization. Reflection is
/// deliberately absent from BodyState and BRep APIs: safe CAD transforms authoring values.
/// </summary>
public static class SemanticSymmetryTransform
{
    public sealed record Plane(Point3D Origin, Direction3D Normal);
    public sealed record Axis(Point3D Origin, Direction3D Direction);
    public sealed record Frame(Point3D Origin, Direction3D X, Direction3D Y, Direction3D Z)
    {
        public double Determinant => X.ToVector().Cross(Y.ToVector()).Dot(Z.ToVector());
    }

    public static Point3D Reflect(Point3D point, Plane across)
    {
        var n = across.Normal.ToVector();
        return point - n * (2d * (point - across.Origin).Dot(n));
    }

    public static Vector3D Reflect(Vector3D vector, Plane across)
    {
        var n = across.Normal.ToVector();
        return vector - n * (2d * vector.Dot(n));
    }

    public static Axis Reflect(Axis axis, Plane across) =>
        new(Reflect(axis.Origin, across), Direction3D.Create(Reflect(axis.Direction.ToVector(), across)));

    public static Plane Reflect(Plane plane, Plane across) =>
        new(Reflect(plane.Origin, across), Direction3D.Create(Reflect(plane.Normal.ToVector(), across)));

    public static Frame Reflect(Frame frame, Plane across)
    {
        // Reflecting all three basis vectors has determinant -1. Preserve reflected X/Z
        // semantics, then reconstruct Y through the existing right-handed cross-product law.
        var x = Direction3D.Create(Reflect(frame.X.ToVector(), across));
        var z = Direction3D.Create(Reflect(frame.Z.ToVector(), across));
        var y = Direction3D.Create(z.ToVector().Cross(x.ToVector()));
        x = Direction3D.Create(y.ToVector().Cross(z.ToVector()));
        return new(Reflect(frame.Origin, across), x, y, z);
    }

    public static Point3D Rotate(Point3D point, Axis about, double radians) =>
        about.Origin + Rotate(point - about.Origin, about.Direction, radians);

    public static Vector3D Rotate(Vector3D vector, Axis about, double radians) =>
        Rotate(vector, about.Direction, radians);

    public static Frame Rotate(Frame frame, Axis about, double radians)
    {
        var x = Direction3D.Create(Rotate(frame.X.ToVector(), about.Direction, radians));
        var z = Direction3D.Create(Rotate(frame.Z.ToVector(), about.Direction, radians));
        var y = Direction3D.Create(z.ToVector().Cross(x.ToVector()));
        return new(Rotate(frame.Origin, about, radians), x, y, z);
    }

    private static Vector3D Rotate(Vector3D vector, Direction3D direction, double radians)
    {
        var axis = direction.ToVector();
        var c = Math.Cos(radians);
        var s = Math.Sin(radians);
        return vector * c + axis.Cross(vector) * s + axis * (axis.Dot(vector) * (1d - c));
    }
}
