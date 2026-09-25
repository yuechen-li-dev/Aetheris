using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Curves;

/// <summary>
/// Exact, constant-pitch cylindrical helix. The angle is the parameter; a positive
/// angle advances along the axis for a right-hand helix. Finite splines may realize
/// this curve, but never replace its geometric authority.
/// </summary>
public readonly record struct CylindricalHelix3
{
    public CylindricalHelix3(Point3D axisOrigin, Direction3D axis, Direction3D startRadial,
        double radiusMm, double pitchMm, double axialStartMm, double startAngleRadians,
        double endAngleRadians, bool rightHanded = true)
    {
        if (!double.IsFinite(radiusMm) || radiusMm <= 0d) throw new ArgumentOutOfRangeException(nameof(radiusMm));
        if (!double.IsFinite(pitchMm) || pitchMm <= 0d) throw new ArgumentOutOfRangeException(nameof(pitchMm));
        if (!double.IsFinite(axialStartMm)) throw new ArgumentOutOfRangeException(nameof(axialStartMm));
        if (!double.IsFinite(startAngleRadians) || !double.IsFinite(endAngleRadians)
            || endAngleRadians <= startAngleRadians) throw new ArgumentOutOfRangeException(nameof(endAngleRadians));
        if (!double.IsFinite(axisOrigin.X) || !double.IsFinite(axisOrigin.Y) || !double.IsFinite(axisOrigin.Z))
            throw new ArgumentOutOfRangeException(nameof(axisOrigin));
        if (double.Abs(axis.ToVector().LengthSquared - 1d) > 1e-9d
            || double.Abs(startRadial.ToVector().LengthSquared - 1d) > 1e-9d)
            throw new ArgumentException("Axis and start radial must be unit directions.");
        if (double.Abs(axis.ToVector().Dot(startRadial.ToVector())) > 1e-9d)
            throw new ArgumentException("Start radial must be perpendicular to the helix axis.", nameof(startRadial));

        AxisOrigin = axisOrigin;
        Axis = axis;
        StartRadial = startRadial;
        RadiusMm = radiusMm;
        PitchMm = pitchMm;
        AxialStartMm = axialStartMm;
        StartAngleRadians = startAngleRadians;
        EndAngleRadians = endAngleRadians;
        RightHanded = rightHanded;
    }

    public Point3D AxisOrigin { get; }
    public Direction3D Axis { get; }
    public Direction3D StartRadial { get; }
    public double RadiusMm { get; }
    public double PitchMm { get; }
    public double LeadMm => PitchMm;
    public double AxialStartMm { get; }
    public double StartAngleRadians { get; }
    public double EndAngleRadians { get; }
    public bool RightHanded { get; }
    public double Turns => (EndAngleRadians - StartAngleRadians) / (2d * double.Pi);

    public Vector3D Radial(double angle)
    {
        var radial = StartRadial.ToVector();
        var circumferential = Axis.ToVector().Cross(radial);
        var signedAngle = RightHanded ? angle : -angle;
        return radial * double.Cos(signedAngle) + circumferential * double.Sin(signedAngle);
    }

    public Point3D Evaluate(double angle)
    {
        CheckDomain(angle);
        return AxisOrigin + Axis.ToVector() * (AxialStartMm + PitchMm * (angle - StartAngleRadians) / (2d * double.Pi))
            + Radial(angle) * RadiusMm;
    }

    public Vector3D Derivative(double angle)
    {
        CheckDomain(angle);
        var radial = StartRadial.ToVector();
        var circumferential = Axis.ToVector().Cross(radial);
        var sign = RightHanded ? 1d : -1d;
        var signedAngle = sign * angle;
        return Axis.ToVector() * (PitchMm / (2d * double.Pi))
            + (circumferential * double.Cos(signedAngle) - radial * double.Sin(signedAngle)) * (RadiusMm * sign);
    }

    private void CheckDomain(double angle)
    {
        if (!double.IsFinite(angle) || angle < StartAngleRadians - 1e-12d || angle > EndAngleRadians + 1e-12d)
            throw new ArgumentOutOfRangeException(nameof(angle));
    }
}
