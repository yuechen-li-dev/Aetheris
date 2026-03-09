using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Curves;

/// <summary>
/// Ellipse parameterization: P(t) = Center + cos(t) * MajorRadius * XAxis + sin(t) * MinorRadius * YAxis,
/// where YAxis = Normal x XAxis.
/// </summary>
public readonly record struct Ellipse3Curve
{
    private readonly Direction3D _xAxis;
    private readonly Direction3D _yAxis;

    public Ellipse3Curve(Point3D center, Direction3D normal, double majorRadius, double minorRadius, Direction3D referenceAxis)
    {
        if (!double.IsFinite(majorRadius) || majorRadius <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(majorRadius), "Major radius must be finite and greater than zero.");
        }

        if (!double.IsFinite(minorRadius) || minorRadius <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(minorRadius), "Minor radius must be finite and greater than zero.");
        }

        if (minorRadius > majorRadius)
        {
            throw new ArgumentOutOfRangeException(nameof(minorRadius), "Minor radius must be less than or equal to major radius.");
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
            throw new ArgumentOutOfRangeException(nameof(normal), "Normal must define a valid ellipse plane.");
        }

        Center = center;
        Normal = normal;
        MajorRadius = majorRadius;
        MinorRadius = minorRadius;
        _xAxis = xAxis;
        _yAxis = yAxis;
    }

    public Point3D Center { get; }

    public Direction3D Normal { get; }

    public double MajorRadius { get; }

    public double MinorRadius { get; }

    public Direction3D XAxis => _xAxis;

    public Direction3D YAxis => _yAxis;

    public Point3D Evaluate(double angleRadians)
    {
        var cosine = double.Cos(angleRadians);
        var sine = double.Sin(angleRadians);
        var offset = (_xAxis.ToVector() * (MajorRadius * cosine)) + (_yAxis.ToVector() * (MinorRadius * sine));
        return Center + offset;
    }
}
