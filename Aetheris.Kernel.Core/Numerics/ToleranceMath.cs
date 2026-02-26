namespace Aetheris.Kernel.Core.Numerics;

public static class ToleranceMath
{
    public static bool AlmostEqual(double a, double b, double tolerance)
    {
        var validatedTolerance = ValidateTolerance(tolerance);
        return Math.Abs(a - b) <= validatedTolerance;
    }

    public static bool AlmostEqual(double a, double b, ToleranceContext context) => AlmostEqual(a, b, context.Linear);

    public static bool AlmostZero(double value, double tolerance)
    {
        var validatedTolerance = ValidateTolerance(tolerance);
        return Math.Abs(value) <= validatedTolerance;
    }

    public static bool AlmostZero(double value, ToleranceContext context) => AlmostZero(value, context.Linear);

    public static bool LessThanOrAlmostEqual(double a, double b, double tolerance)
    {
        var validatedTolerance = ValidateTolerance(tolerance);
        return a <= b + validatedTolerance;
    }

    public static bool LessThanOrAlmostEqual(double a, double b, ToleranceContext context) =>
        LessThanOrAlmostEqual(a, b, context.Linear);

    public static bool GreaterThanOrAlmostEqual(double a, double b, double tolerance)
    {
        var validatedTolerance = ValidateTolerance(tolerance);
        return a + validatedTolerance >= b;
    }

    public static bool GreaterThanOrAlmostEqual(double a, double b, ToleranceContext context) =>
        GreaterThanOrAlmostEqual(a, b, context.Linear);

    public static double ClampToZero(double value, double tolerance) => AlmostZero(value, tolerance) ? 0d : value;

    private static double ValidateTolerance(double tolerance)
    {
        if (!double.IsFinite(tolerance) || tolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be finite and greater than zero.");
        }

        return tolerance;
    }
}
