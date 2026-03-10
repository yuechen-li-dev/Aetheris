using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Surfaces;

/// <summary>
/// Right circular cone parameterization with apex at Apex:
/// S(u,v) = Apex + v * Axis + (v * tan(SemiAngleRadians)) * (cos(u) * XAxis + sin(u) * YAxis), for v >= 0.
/// </summary>
public readonly record struct ConeSurface
{
    private readonly Direction3D _xAxis;
    private readonly Direction3D _yAxis;

    public ConeSurface(Point3D apex, Direction3D axis, double semiAngleRadians, Direction3D referenceAxis)
    {
        if (!double.IsFinite(semiAngleRadians) || semiAngleRadians <= 0d || semiAngleRadians >= (double.Pi / 2d))
        {
            throw new ArgumentOutOfRangeException(nameof(semiAngleRadians), "Semi-angle must be finite and in the range (0, pi/2). ");
        }

        Apex = apex;
        Axis = axis;
        SemiAngleRadians = semiAngleRadians;
        PlacementOrigin = apex;
        PlacementRadius = 0d;
        ReferenceAxis = referenceAxis;
        _xAxis = ResolveXAxis(axis, referenceAxis);
        _yAxis = ResolveYAxis(axis, _xAxis);
    }

    public ConeSurface(Point3D placementOrigin, Direction3D axis, double placementRadius, double semiAngleRadians, Direction3D referenceAxis)
    {
        if (!double.IsFinite(placementRadius) || placementRadius < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(placementRadius), "Placement radius must be finite and greater than or equal to zero.");
        }

        if (!double.IsFinite(semiAngleRadians) || semiAngleRadians <= 0d || semiAngleRadians >= (double.Pi / 2d))
        {
            throw new ArgumentOutOfRangeException(nameof(semiAngleRadians), "Semi-angle must be finite and in the range (0, pi/2). ");
        }

        var offset = axis.ToVector() * (placementRadius / double.Tan(semiAngleRadians));
        Apex = placementOrigin - offset;
        Axis = axis;
        SemiAngleRadians = semiAngleRadians;
        PlacementOrigin = placementOrigin;
        PlacementRadius = placementRadius;
        ReferenceAxis = referenceAxis;
        _xAxis = ResolveXAxis(axis, referenceAxis);
        _yAxis = ResolveYAxis(axis, _xAxis);
    }

    public Point3D Apex { get; }

    public Direction3D Axis { get; }

    public double SemiAngleRadians { get; }

    public Direction3D ReferenceAxis { get; }

    public Point3D PlacementOrigin { get; }

    public double PlacementRadius { get; }

    public double AxialParameterFromPoint(Point3D point)
    {
        var offset = PlacementRadius / double.Tan(SemiAngleRadians);
        return (point - PlacementOrigin).Dot(Axis.ToVector()) + offset;
    }

    public Point3D Evaluate(double u, double v)
    {
        var radialMagnitude = v * double.Tan(SemiAngleRadians);
        var radial = (_xAxis.ToVector() * (radialMagnitude * double.Cos(u))) + (_yAxis.ToVector() * (radialMagnitude * double.Sin(u)));
        var axial = Axis.ToVector() * v;
        return Apex + axial + radial;
    }

    public Direction3D Normal(double u)
    {
        var radial = (_xAxis.ToVector() * double.Cos(u)) + (_yAxis.ToVector() * double.Sin(u));
        var outward = radial - (Axis.ToVector() * double.Tan(SemiAngleRadians));
        return Direction3D.Create(outward);
    }

    private static Direction3D ResolveXAxis(Direction3D axis, Direction3D referenceAxis)
    {
        var axisVector = axis.ToVector();
        var reference = referenceAxis.ToVector();
        var projected = reference - (axisVector * reference.Dot(axisVector));

        if (!Direction3D.TryCreate(projected, out var xAxis))
        {
            throw new ArgumentOutOfRangeException(nameof(referenceAxis), "Reference axis must not be parallel to cone axis.");
        }

        return xAxis;
    }

    private static Direction3D ResolveYAxis(Direction3D axis, Direction3D xAxis)
    {
        if (!Direction3D.TryCreate(axis.ToVector().Cross(xAxis.ToVector()), out var yAxis))
        {
            throw new ArgumentOutOfRangeException(nameof(axis), "Axis must define a valid cone frame.");
        }

        return yAxis;
    }
}
