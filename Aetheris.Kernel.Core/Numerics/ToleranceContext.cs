namespace Aetheris.Kernel.Core.Numerics;

/// <summary>
/// Immutable tolerance settings for kernel numeric comparisons.
/// Linear applies to model-space distances and scalar values; Angular applies to radians.
/// </summary>
public readonly record struct ToleranceContext
{
    /// <summary>
    /// Gets a default kernel tolerance profile suitable for general model-space operations.
    /// </summary>
    public static ToleranceContext Default { get; } = CreateDefault();

    public ToleranceContext(double linear, double angular, double relative = 1e-12)
    {
        Linear = EnsurePositiveFinite(linear, nameof(linear));
        Angular = EnsurePositiveFinite(angular, nameof(angular));
        Relative = EnsurePositiveFinite(relative, nameof(relative));
    }

    /// <summary>
    /// Gets the linear (model-space) tolerance.
    /// </summary>
    public double Linear { get; }

    /// <summary>
    /// Gets the angular tolerance in radians.
    /// </summary>
    public double Angular { get; }

    /// <summary>
    /// Gets the relative tolerance for scale-aware comparisons.
    /// </summary>
    public double Relative { get; }

    public static ToleranceContext CreateDefault() => new(linear: 1e-6, angular: 1e-9, relative: 1e-12);

    private static double EnsurePositiveFinite(double value, string paramName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, "Tolerance values must be finite and greater than zero.");
        }

        return value;
    }
}
