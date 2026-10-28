using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Backends.Sdf;

/// <summary>Encloses the occupied fraction of a box between two affine level
/// sets. The plane/box integral is analytic; curvature remains an uncertainty
/// band, never an empirically sampled error estimate.</summary>
internal static class SdfAffineCellVolume
{
    internal static (double Lower, double Upper) Enclose(SdfAffineEnclosure field, SdfBounds box, double padding)
    {
        double[] weights = [double.Abs(field.Gradient.X) * box.SizeX,
            double.Abs(field.Gradient.Y) * box.SizeY, double.Abs(field.Gradient.Z) * box.SizeZ];
        var max = weights.Max();
        // Tiny coefficients cause cancellation in the inclusion-exclusion
        // formula. Carry their FULL variation into the uncertainty instead.
        var dropped = weights.Where(w => w < max * 1e-5).Sum() / 2;
        var active = weights.Where(w => w > 0 && w >= max * 1e-5).Select(w => w / max).ToArray();
        if (active.Length == 0)
            return (field.Value + field.UpperError + padding <= 0 ? 1 : 0,
                field.Value - field.LowerError - padding <= 0 ? 1 : 0);
        var halfWidth = active.Sum() / 2;
        var lower = Fraction(active, halfWidth - (field.Value + field.UpperError + padding + dropped) / max);
        var upper = Fraction(active, halfWidth - (field.Value - field.LowerError - padding - dropped) / max);
        return (lower.Lower, upper.Upper);
    }

    private static (double Lower, double Upper) Fraction(double[] widths, double threshold)
    {
        var total = widths.Sum();
        if (threshold <= 0) return (0, 0);
        if (threshold >= total) return (1, 1);
        // Complement symmetry reduces subtraction near a full cell.
        if (threshold > total / 2)
        {
            var other = Fraction(widths, total - threshold);
            return (1 - other.Upper, 1 - other.Lower);
        }
        var longest = widths.Max();
        var remainder = total - longest;
        // In the central slab, integrate the longest uniform axis first.
        // This is linear and avoids subtracting nearly equal cubic terms
        // for cuts with two tiny normal components.
        if (threshold >= remainder && threshold <= longest)
        {
            var value = (threshold - remainder / 2) / longest;
            const double guard = 64 * 2.2204460492503131e-16;
            return (double.Clamp(value - guard, 0, 1), double.Clamp(value + guard, 0, 1));
        }
        var n = widths.Length; double sum = 0, absoluteSum = 0;
        for (var subset = 0; subset < 1 << n; subset++)
        {
            var t = threshold; var sign = 1;
            for (var axis = 0; axis < n; axis++) if ((subset & (1 << axis)) != 0) { t -= widths[axis]; sign = -sign; }
            var term = n == 1 ? double.Max(t, 0) : n == 2 ? double.Pow(double.Max(t, 0), 2) : double.Pow(double.Max(t, 0), 3);
            sum += sign * term; absoluteSum += term;
        }
        var denominator = widths.Aggregate(n == 3 ? 6d : n == 2 ? 2d : 1d, (p, w) => p * w);
        var fraction = sum / denominator;
        var roundoff = 64 * 2.2204460492503131e-16 * (absoluteSum / denominator + 1);
        return (double.Clamp(fraction - roundoff, 0, 1), double.Clamp(fraction + roundoff, 0, 1));
    }
}
