using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Curves;

/// <summary>
/// Circle parameterization: P(t) = Center + Radius * (cos(t) * XAxis + sin(t) * YAxis),
/// where YAxis = Normal x XAxis.
/// </summary>
public readonly record struct Circle3Curve
{
    private readonly Direction3D _xAxis;
    private readonly Direction3D _yAxis;

    public Circle3Curve(Point3D center, Direction3D normal, double radius, Direction3D referenceAxis)
    {
        if (!double.IsFinite(radius) || radius <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be finite and greater than zero.");
        }

        var n = normal.ToVector();
        var reference = referenceAxis.ToVector();
        var projected = reference - (n * reference.Dot(n));

        if (!Direction3D.TryCreate(projected, out var xAxis))
        {
            throw new ArgumentOutOfRangeException(nameof(referenceAxis), "Reference axis must not be parallel to the normal.");
        }

        if (!Direction3D.TryCreate(n.Cross(xAxis.ToVector()), out var yAxis))
        {
            throw new ArgumentOutOfRangeException(nameof(normal), "Normal must define a valid circle plane.");
        }

        Center = center;
        Normal = normal;
        Radius = radius;
        _xAxis = xAxis;
        _yAxis = yAxis;
    }

    public Point3D Center { get; }

    public Direction3D Normal { get; }

    public double Radius { get; }

    public Direction3D XAxis => _xAxis;

    public Direction3D YAxis => _yAxis;

    public Point3D Evaluate(double angleRadians)
    {
        var cosine = double.Cos(angleRadians);
        var sine = double.Sin(angleRadians);
        var offset = (_xAxis.ToVector() * (Radius * cosine)) + (_yAxis.ToVector() * (Radius * sine));
        return Center + offset;
    }

    public Vector3D Tangent(double angleRadians)
    {
        var cosine = double.Cos(angleRadians);
        var sine = double.Sin(angleRadians);
        return (_xAxis.ToVector() * (-Radius * sine)) + (_yAxis.ToVector() * (Radius * cosine));
    }
}
