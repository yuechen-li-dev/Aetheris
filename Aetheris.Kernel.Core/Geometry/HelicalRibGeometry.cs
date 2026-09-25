using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Geometry;

/// <summary>Exact intent for one additive, right-hand, single-start rib on finite cylindrical stock.</summary>
public sealed record HelicalRibParameters(
    Point3D AxisOrigin,
    Direction3D Axis,
    Direction3D StartRadial,
    double SupportAxialMinMm,
    double SupportAxialMaxMm,
    double RootRadiusMm,
    double CrestRadiusMm,
    double PitchMm,
    double AxialStartMm,
    double AxialEndMm,
    double RootWidthMm,
    double CrestWidthMm,
    double StartAngleRadians = 0d);

public enum HelicalRibBoundaryRole { LeadingRoot, LeadingCrest, TrailingCrest, TrailingRoot }
public enum HelicalRibSideRole { LeadingFlank, Crest, TrailingFlank }

/// <summary>
/// Exact cylindrical frame and trapezoidal profile geometry. This is the
/// authoritative law for future BRep faces; it is not a BRep materializer.
/// </summary>
public sealed class HelicalRibGeometry
{
    private HelicalRibGeometry(HelicalRibParameters parameters)
    {
        Parameters = parameters;
        EndAngleRadians = parameters.StartAngleRadians
            + 2d * double.Pi * (parameters.AxialEndMm - parameters.AxialStartMm) / parameters.PitchMm;
    }

    public HelicalRibParameters Parameters { get; }
    public double EndAngleRadians { get; }
    public double Turns => (Parameters.AxialEndMm - Parameters.AxialStartMm) / Parameters.PitchMm;
    public double LeadMm => Parameters.PitchMm;
    public double MinimumAdjacentTurnClearanceMm => Parameters.PitchMm - Parameters.RootWidthMm;

    public static KernelResult<HelicalRibGeometry> Create(HelicalRibParameters p, ToleranceContext? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(p);
        var linear = (tolerance ?? ToleranceContext.Default).Linear;
        if (!Finite(p.SupportAxialMinMm, p.SupportAxialMaxMm, p.RootRadiusMm,
                p.CrestRadiusMm, p.PitchMm, p.AxialStartMm, p.AxialEndMm,
                p.RootWidthMm, p.CrestWidthMm, p.StartAngleRadians,
                p.AxisOrigin.X, p.AxisOrigin.Y, p.AxisOrigin.Z))
            return Failure("nonfinite", "All rib dimensions and placement coordinates must be finite.");
        if (p.SupportAxialMaxMm <= p.SupportAxialMinMm)
            return Failure("support-bounds", "Support axial maximum must exceed minimum.");
        if (p.RootRadiusMm <= 0d || p.CrestRadiusMm <= p.RootRadiusMm)
            return Failure("radii", "Crest radius must exceed a positive root/support radius.");
        if (p.PitchMm <= 0d) return Failure("pitch", "Pitch must be positive.");
        if (p.AxialEndMm <= p.AxialStartMm)
            return Failure("span", "Rib axial end must exceed start.");
        if (p.CrestWidthMm <= 0d || p.RootWidthMm <= p.CrestWidthMm)
            return Failure("profile-width", "Root width must exceed a positive crest width.");
        if (p.RootWidthMm >= p.PitchMm - linear)
            return Failure("adjacent-turn-overlap", "Root profile width must be smaller than pitch with positive clearance.");
        if (p.AxialStartMm - p.RootWidthMm / 2d < p.SupportAxialMinMm - linear
            || p.AxialEndMm + p.RootWidthMm / 2d > p.SupportAxialMaxMm + linear)
            return Failure("support-extent", "The entire rib profile must lie within the support axial bounds.");
        if (double.Abs(p.Axis.ToVector().LengthSquared - 1d) > 1e-9d
            || double.Abs(p.StartRadial.ToVector().LengthSquared - 1d) > 1e-9d
            || double.Abs(p.Axis.ToVector().Dot(p.StartRadial.ToVector())) > 1e-9d)
            return Failure("frame", "Axis and start radial must be unit and perpendicular.");
        var endAngle = p.StartAngleRadians + 2d * double.Pi * (p.AxialEndMm - p.AxialStartMm) / p.PitchMm;
        if (!double.IsFinite(endAngle) || endAngle <= p.StartAngleRadians)
            return Failure("turn-count", "Derived angular span is nonfinite or degenerate.");
        return KernelResult<HelicalRibGeometry>.Success(new HelicalRibGeometry(p));
    }

    public CylindricalHelix3 Boundary(HelicalRibBoundaryRole role)
    {
        var (radius, axialOffset) = Corner(role);
        return new CylindricalHelix3(Parameters.AxisOrigin, Parameters.Axis, Parameters.StartRadial,
            radius, Parameters.PitchMm, Parameters.AxialStartMm + axialOffset,
            Parameters.StartAngleRadians, EndAngleRadians);
    }

    /// <summary>Exact point on a ruled helical flank or crest; u is angular position, v crosses the profile.</summary>
    public Point3D Evaluate(HelicalRibSideRole role, double angle, double v)
    {
        if (!double.IsFinite(v) || v < 0d || v > 1d) throw new ArgumentOutOfRangeException(nameof(v));
        var (a, b) = role switch
        {
            HelicalRibSideRole.LeadingFlank => (HelicalRibBoundaryRole.LeadingRoot, HelicalRibBoundaryRole.LeadingCrest),
            HelicalRibSideRole.Crest => (HelicalRibBoundaryRole.LeadingCrest, HelicalRibBoundaryRole.TrailingCrest),
            HelicalRibSideRole.TrailingFlank => (HelicalRibBoundaryRole.TrailingCrest, HelicalRibBoundaryRole.TrailingRoot),
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
        var (r0, z0) = Corner(a);
        var (r1, z1) = Corner(b);
        var radius = r0 + (r1 - r0) * v;
        var offset = z0 + (z1 - z0) * v;
        var helix = new CylindricalHelix3(Parameters.AxisOrigin, Parameters.Axis, Parameters.StartRadial,
            radius, Parameters.PitchMm, Parameters.AxialStartMm + offset,
            Parameters.StartAngleRadians, EndAngleRadians);
        return helix.Evaluate(angle);
    }

    public IReadOnlyList<Point3D> CapCorners(bool end) => Enum.GetValues<HelicalRibBoundaryRole>()
        .Select(role => Boundary(role).Evaluate(end ? EndAngleRadians : Parameters.StartAngleRadians)).ToArray();

    private (double Radius, double AxialOffset) Corner(HelicalRibBoundaryRole role) => role switch
    {
        HelicalRibBoundaryRole.LeadingRoot => (Parameters.RootRadiusMm, -Parameters.RootWidthMm / 2d),
        HelicalRibBoundaryRole.LeadingCrest => (Parameters.CrestRadiusMm, -Parameters.CrestWidthMm / 2d),
        HelicalRibBoundaryRole.TrailingCrest => (Parameters.CrestRadiusMm, Parameters.CrestWidthMm / 2d),
        HelicalRibBoundaryRole.TrailingRoot => (Parameters.RootRadiusMm, Parameters.RootWidthMm / 2d),
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static bool Finite(params double[] values) => values.All(double.IsFinite);

    private static KernelResult<HelicalRibGeometry> Failure(string code, string message) =>
        KernelResult<HelicalRibGeometry>.Failure([
            new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error,
                $"helical-rib-{code}: {message}", "Geometry.HelicalRib")]);
}
