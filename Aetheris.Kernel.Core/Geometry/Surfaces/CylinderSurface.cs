using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Surfaces;

/// <summary>
/// Cylinder parameterization: S(u,v) = Origin + v * Axis + Radius * (cos(u) * XAxis + sin(u) * YAxis),
/// where YAxis = Axis x XAxis.
/// </summary>
public readonly record struct CylinderSurface
{
    private readonly Direction3D _xAxis;
    private readonly Direction3D _yAxis;

    public CylinderSurface(Point3D origin, Direction3D axis, double radius, Direction3D referenceAxis)
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
            throw new ArgumentOutOfRangeException(nameof(referenceAxis), "Reference axis must not be parallel to cylinder axis.");
        }

        if (!Direction3D.TryCreate(axisVector.Cross(xAxis.ToVector()), out var yAxis))
        {
            throw new ArgumentOutOfRangeException(nameof(axis), "Axis must define a valid cylinder frame.");
        }

        Origin = origin;
        Axis = axis;
        Radius = radius;
        _xAxis = xAxis;
        _yAxis = yAxis;
    }

    public Point3D Origin { get; }

    public Direction3D Axis { get; }

    public double Radius { get; }

    public Direction3D XAxis => _xAxis;

    public Direction3D YAxis => _yAxis;

    public Point3D Evaluate(double u, double v)
    {
        var radial = (_xAxis.ToVector() * (Radius * double.Cos(u))) + (_yAxis.ToVector() * (Radius * double.Sin(u)));
        var axial = Axis.ToVector() * v;
        return Origin + axial + radial;
    }

    public Direction3D Normal(double u)
    {
        var radial = (_xAxis.ToVector() * double.Cos(u)) + (_yAxis.ToVector() * double.Sin(u));
        return Direction3D.Create(radial);
    }
}
