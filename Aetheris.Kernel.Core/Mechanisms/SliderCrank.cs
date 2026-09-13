using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Mechanisms;

/// <summary>Analytical, in-line slider-crank in the XY plane; shaft axis is +Z.
/// Angles are radians measured from +Y toward +X. This prescribes motion, not forces.</summary>
public sealed record SliderCrank(double CrankRadiusMm, double RodLengthMm)
{
    public SliderCrankPose Evaluate(double shaftAngleRadians, double bankAngleRadians = 0, double crankpinPhaseRadians = 0)
    {
        if (!double.IsFinite(CrankRadiusMm) || !double.IsFinite(RodLengthMm) || CrankRadiusMm <= 0 || RodLengthMm <= CrankRadiusMm)
            throw new ArgumentOutOfRangeException(nameof(RodLengthMm), "mechanism-slider-crank-domain: require finite L > r > 0.");
        if (!double.IsFinite(shaftAngleRadians) || !double.IsFinite(bankAngleRadians) || !double.IsFinite(crankpinPhaseRadians))
            throw new ArgumentOutOfRangeException(nameof(shaftAngleRadians), "mechanism-angle-nonfinite");
        var angle = shaftAngleRadians + crankpinPhaseRadians;
        var pin = new Vector3D(CrankRadiusMm * double.Sin(angle), CrankRadiusMm * double.Cos(angle), 0);
        var axis = new Vector3D(double.Sin(bankAngleRadians), double.Cos(bankAngleRadians), 0);
        var along = pin.Dot(axis);
        var perpendicular = pin - axis * along;
        var position = along + double.Sqrt(RodLengthMm * RodLengthMm - perpendicular.LengthSquared);
        if (!double.IsFinite(position))
            throw new ArgumentOutOfRangeException(nameof(RodLengthMm), "mechanism-slider-crank-nonfinite-solution");
        var wrist = axis * position;
        var rod = wrist - pin;
        return new(pin, wrist, axis, position, double.Atan2(rod.X, rod.Y));
    }
}

public sealed record SliderCrankPose(Vector3D CrankPin, Vector3D WristPin, Vector3D BoreAxis,
    double PistonPositionMm, double RodAngleRadians);

public static class PrescribedMotion
{
    public static double AngularRatio(double inputRadians, double ratio)
    {
        if (!double.IsFinite(inputRadians) || !double.IsFinite(ratio) || !double.IsFinite(inputRadians * ratio))
            throw new ArgumentOutOfRangeException(nameof(inputRadians), "mechanism-angular-ratio-nonfinite");
        return inputRadians * ratio;
    }

    /// <summary>Periodic, idealized sin-squared lift. Opening/closing define a forward interval within one cycle.</summary>
    public static double ValveLift(double phaseDegrees, double openingDegrees, double durationDegrees, double maximumLiftMm, double cycleDegrees = 720)
    {
        if (!double.IsFinite(phaseDegrees) || !double.IsFinite(openingDegrees) || !double.IsFinite(durationDegrees)
            || !double.IsFinite(maximumLiftMm) || !double.IsFinite(cycleDegrees)
            || cycleDegrees <= 0 || durationDegrees <= 0 || durationDegrees >= cycleDegrees || maximumLiftMm < 0)
            throw new ArgumentOutOfRangeException(nameof(durationDegrees), "mechanism-lift-domain");
        var relative = ((phaseDegrees - openingDegrees) % cycleDegrees + cycleDegrees) % cycleDegrees;
        if (!double.IsFinite(relative))
            throw new ArgumentOutOfRangeException(nameof(phaseDegrees), "mechanism-lift-nonfinite-phase");
        return relative >= durationDegrees ? 0 : maximumLiftMm * double.Pow(double.Sin(double.Pi * relative / durationDegrees), 2);
    }
}
