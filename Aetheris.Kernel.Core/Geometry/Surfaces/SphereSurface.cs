using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Surfaces;

/// <summary>
/// Sphere parameterization:
/// S(u,v) = Center + Radius * (cos(v) * cos(u) * XAxis + cos(v) * sin(u) * YAxis + sin(v) * Axis),
/// where u is azimuth around Axis and v is elevation from the equatorial plane.
/// </summary>
public readonly record struct SphereSurface
{
    private readonly Direction3D _xAxis;
    private readonly Direction3D _yAxis;

    public SphereSurface(Point3D center, Direction3D axis, double radius, Direction3D referenceAxis)
    {
        if (!double.IsFinite(radius) || radius <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be finite and greater than zero.");
        }

        var axisVector = axis.ToVector();
        var reference = referenceAxis.ToVector();
        var projected = reference - (axisVector * reference.Dot(axisVector));

        if (!Direction3D.TryCreate(projected, out var xAxis))
        {
            throw new ArgumentOutOfRangeException(nameof(referenceAxis), "Reference axis must not be parallel to sphere axis.");
        }

        if (!Direction3D.TryCreate(axisVector.Cross(xAxis.ToVector()), out var yAxis))
        {
            throw new ArgumentOutOfRangeException(nameof(axis), "Axis must define a valid sphere frame.");
        }

        Center = center;
        Axis = axis;
        Radius = radius;
        _xAxis = xAxis;
        _yAxis = yAxis;
    }

    public Point3D Center { get; }

    public Direction3D Axis { get; }

    public double Radius { get; }

    public Direction3D XAxis => _xAxis;

    public Direction3D YAxis => _yAxis;

    public Point3D Evaluate(double u, double v)
    {
        var cosV = double.Cos(v);
        var sinV = double.Sin(v);
        var cosU = double.Cos(u);
        var sinU = double.Sin(u);

        var offset = (_xAxis.ToVector() * (Radius * cosV * cosU))
                   + (_yAxis.ToVector() * (Radius * cosV * sinU))
                   + (Axis.ToVector() * (Radius * sinV));

        return Center + offset;
    }

    public Direction3D Normal(double u, double v)
    {
        var point = Evaluate(u, v);
        return Direction3D.Create(point - Center);
    }
}
