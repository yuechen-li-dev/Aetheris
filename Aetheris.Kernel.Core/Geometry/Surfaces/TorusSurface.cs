using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Surfaces;

public readonly record struct TorusSurface
{
    private readonly Direction3D _xAxis;
    private readonly Direction3D _yAxis;

    public TorusSurface(Point3D center, Direction3D axis, double majorRadius, double minorRadius, Direction3D referenceAxis)
    {
        if (double.IsNaN(majorRadius) || double.IsInfinity(majorRadius) || majorRadius <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(majorRadius), "Major radius must be positive and finite.");
        }

        if (double.IsNaN(minorRadius) || double.IsInfinity(minorRadius) || minorRadius <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(minorRadius), "Minor radius must be positive and finite.");
        }

        Center = center;
        Axis = axis;
        MajorRadius = majorRadius;
        MinorRadius = minorRadius;

        var rawU = referenceAxis.ToVector();
        if (!rawU.TryNormalize(out var u))
        {
            throw new ArgumentException("Reference axis must be non-degenerate.", nameof(referenceAxis));
        }
        var z = axis.ToVector();
        var v = z.Cross(u);
        if (v.LengthSquared <= 1e-24d)
        {
            throw new ArgumentException("Reference axis must not be collinear with axis.", nameof(referenceAxis));
        }

        _xAxis = Direction3D.Create(u);
        _yAxis = Direction3D.Create(v);
    }

    public Point3D Center { get; }

    public Direction3D Axis { get; }

    public double MajorRadius { get; }

    public double MinorRadius { get; }

    public Direction3D XAxis => _xAxis;

    public Direction3D YAxis => _yAxis;

    public Point3D Evaluate(double u, double v)
    {
        var cosU = double.Cos(u);
        var sinU = double.Sin(u);
        var cosV = double.Cos(v);
        var sinV = double.Sin(v);

        var majorDirection = (_xAxis.ToVector() * cosU) + (_yAxis.ToVector() * sinU);
        var radialDistance = MajorRadius + (MinorRadius * cosV);
        return Center + (majorDirection * radialDistance) + (Axis.ToVector() * (MinorRadius * sinV));
    }

    public Direction3D Normal(double u, double v)
    {
        var cosU = double.Cos(u);
        var sinU = double.Sin(u);
        var cosV = double.Cos(v);
        var sinV = double.Sin(v);

        var majorDirection = (_xAxis.ToVector() * cosU) + (_yAxis.ToVector() * sinU);
        var normal = (majorDirection * cosV) + (Axis.ToVector() * sinV);
        return Direction3D.Create(normal);
    }
}
