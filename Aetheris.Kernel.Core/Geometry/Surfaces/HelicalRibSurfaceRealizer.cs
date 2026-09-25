using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Geometry.Surfaces;

/// <summary>Finite flank/crest surface subordinate to an exact constant-profile helical rib.</summary>
public sealed record QualifiedHelicalRibSurface(
    HelicalRibGeometry Authority,
    HelicalRibSideRole Role,
    BSplineSurfaceWithKnots Surface,
    double RequestedToleranceMm,
    double AnalyticErrorBoundMm,
    double NumericalAllowanceMm,
    int SegmentCount)
{
    public double CertifiedDeviationBoundMm => AnalyticErrorBoundMm + NumericalAllowanceMm;
}

public static class HelicalRibSurfaceRealizer
{
    private const int MaxSegments = 16_384;
    private const double MachineEpsilon = 2.2204460492503131e-16d;

    public static KernelResult<QualifiedHelicalRibSurface> Realize(
        HelicalRibGeometry rib, HelicalRibSideRole role, ToleranceContext? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(rib);
        var (firstRole, secondRole) = role switch
        {
            HelicalRibSideRole.LeadingFlank => (HelicalRibBoundaryRole.LeadingRoot, HelicalRibBoundaryRole.LeadingCrest),
            HelicalRibSideRole.Crest => (HelicalRibBoundaryRole.LeadingCrest, HelicalRibBoundaryRole.TrailingCrest),
            HelicalRibSideRole.TrailingFlank => (HelicalRibBoundaryRole.TrailingCrest, HelicalRibBoundaryRole.TrailingRoot),
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
        var first = rib.Boundary(firstRole);
        var second = rib.Boundary(secondRole);
        var requested = (tolerance ?? ToleranceContext.Default).Linear;
        var p = rib.Parameters;
        var scale = double.Max(1d, double.Max(p.CrestRadiusMm,
            double.Max(double.Abs(p.SupportAxialMinMm),
                double.Max(double.Abs(p.SupportAxialMaxMm),
                    double.Max(double.Abs(p.AxisOrigin.X),
                        double.Max(double.Abs(p.AxisOrigin.Y), double.Abs(p.AxisOrigin.Z)))))));
        var allowance = 256d * MachineEpsilon * scale;
        var budget = requested - allowance;
        if (budget <= 0d)
            return Failure("numeric-tolerance", "Coordinate scale consumes the requested tolerance.");
        var maximumRadius = double.Max(first.RadiusMm, second.RadiusMm);
        // The surface is linear in profile coordinate v. Hermite interpolation
        // in angle has fourth derivative norm <= max endpoint radius.
        var maximumSpan = double.Pow(384d * budget / maximumRadius, 0.25d);
        var angleRange = rib.EndAngleRadians - p.StartAngleRadians;
        var countReal = double.Ceiling(angleRange / maximumSpan);
        if (!double.IsFinite(countReal) || countReal < 1d || countReal > MaxSegments)
            return Failure("segment-budget", "Certified helical surface exceeds the bounded segment budget.");
        var count = (int)countReal;
        var h = angleRange / count;
        var analyticError = maximumRadius * double.Pow(h, 4d) / 384d;
        var rows = new List<IReadOnlyList<Point3D>>(count * 4);
        var knots = new List<double>(count + 1) { p.StartAngleRadians };
        var multiplicities = new List<int>(count + 1) { 4 };
        for (var i = 0; i < count; i++)
        {
            var a = p.StartAngleRadians + angleRange * i / count;
            var b = i == count - 1 ? rib.EndAngleRadians : p.StartAngleRadians + angleRange * (i + 1) / count;
            var width = b - a;
            var a0 = first.Evaluate(a);
            var b0 = first.Evaluate(b);
            var a1 = second.Evaluate(a);
            var b1 = second.Evaluate(b);
            rows.Add([a0, a1]);
            rows.Add([a0 + first.Derivative(a) * (width / 3d),
                a1 + second.Derivative(a) * (width / 3d)]);
            rows.Add([b0 - first.Derivative(b) * (width / 3d),
                b1 - second.Derivative(b) * (width / 3d)]);
            rows.Add([b0, b1]);
            knots.Add(b);
            multiplicities.Add(4);
        }
        var surface = new BSplineSurfaceWithKnots(3, 1, rows,
            "UNSPECIFIED", false, false, false,
            multiplicities, [2, 2], knots, [0d, 1d], "UNSPECIFIED");
        return KernelResult<QualifiedHelicalRibSurface>.Success(new(
            rib, role, surface, requested, analyticError, allowance, count));
    }

    private static KernelResult<QualifiedHelicalRibSurface> Failure(string code, string message) =>
        KernelResult<QualifiedHelicalRibSurface>.Failure([
            new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error,
                $"helical-rib-surface-{code}: {message}", "Geometry.HelicalRib.Surface")]);
}
