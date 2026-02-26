using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Geometry;

/// <summary>
/// Represents an inclusive 1D parameter interval [Start, End].
/// Degenerate intervals (Start == End) are allowed.
/// </summary>
public readonly record struct ParameterInterval
{
    public ParameterInterval(double start, double end)
    {
        if (!double.IsFinite(start))
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start must be finite.");
        }

        if (!double.IsFinite(end))
        {
            throw new ArgumentOutOfRangeException(nameof(end), "End must be finite.");
        }

        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "End must be greater than or equal to Start.");
        }

        Start = start;
        End = end;
    }

    public double Start { get; }

    public double End { get; }

    public bool Contains(double value, ToleranceContext? toleranceContext = null)
    {
        if (!double.IsFinite(value))
        {
            return false;
        }

        var context = toleranceContext ?? ToleranceContext.Default;
        return ToleranceMath.GreaterThanOrAlmostEqual(value, Start, context)
            && ToleranceMath.LessThanOrAlmostEqual(value, End, context);
    }
}
