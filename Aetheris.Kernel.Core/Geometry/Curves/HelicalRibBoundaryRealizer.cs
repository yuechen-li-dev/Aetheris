using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Geometry.Curves;

/// <summary>A finite polynomial curve subordinate to one exact helical rib boundary.</summary>
public sealed record QualifiedHelicalRibBoundary(
    CylindricalHelix3 Authority,
    BSpline3Curve Curve,
    double RequestedToleranceMm,
    double AnalyticErrorBoundMm,
    double NumericalAllowanceMm,
    int SegmentCount)
{
    public double CertifiedDeviationBoundMm => AnalyticErrorBoundMm + NumericalAllowanceMm;
}

public static class HelicalRibBoundaryRealizer
{
    private const int MaxSegments = 16_384;
    private const double MachineEpsilon = 2.2204460492503131e-16d;

    public static KernelResult<QualifiedHelicalRibBoundary> Realize(
        CylindricalHelix3 authority, ToleranceContext? tolerance = null)
    {
        var requested = (tolerance ?? ToleranceContext.Default).Linear;
        var axialEnd = authority.AxialStartMm + authority.PitchMm * authority.Turns;
        var scale = double.Max(1d, double.Max(authority.RadiusMm,
            double.Max(double.Max(double.Abs(authority.AxialStartMm), double.Abs(axialEnd)),
                double.Max(double.Abs(authority.AxisOrigin.X),
                    double.Max(double.Abs(authority.AxisOrigin.Y), double.Abs(authority.AxisOrigin.Z))))));
        var allowance = 256d * MachineEpsilon * scale;
        var budget = requested - allowance;
        if (budget <= 0d)
            return Failure("numeric-tolerance", "Coordinate scale consumes the requested tolerance.");

        // Cubic Hermite remainder: |f''''| h^4 / 384. The axial coordinate is
        // linear in angle; the fourth derivative is purely radial with norm r.
        var maximumSpan = double.Pow(384d * budget / authority.RadiusMm, 0.25d);
        var angleRange = authority.EndAngleRadians - authority.StartAngleRadians;
        var segmentCountReal = double.Ceiling(angleRange / maximumSpan);
        if (!double.IsFinite(segmentCountReal) || segmentCountReal < 1d || segmentCountReal > MaxSegments)
            return Failure("segment-budget", "Certified helix realization exceeds the bounded segment budget.");
        var segmentCount = (int)segmentCountReal;
        var h = angleRange / segmentCount;
        var error = authority.RadiusMm * double.Pow(h, 4d) / 384d;
        var controls = new List<Point3D>(4 * segmentCount);
        var knots = new List<double>(segmentCount + 1);
        var multiplicities = new List<int>(segmentCount + 1);
        knots.Add(authority.StartAngleRadians);
        multiplicities.Add(4);
        for (var i = 0; i < segmentCount; i++)
        {
            var a = authority.StartAngleRadians + angleRange * i / segmentCount;
            var b = i == segmentCount - 1 ? authority.EndAngleRadians
                : authority.StartAngleRadians + angleRange * (i + 1) / segmentCount;
            var width = b - a;
            var p0 = authority.Evaluate(a);
            var p3 = authority.Evaluate(b);
            controls.Add(p0);
            controls.Add(p0 + authority.Derivative(a) * (width / 3d));
            controls.Add(p3 - authority.Derivative(b) * (width / 3d));
            controls.Add(p3);
            knots.Add(b);
            multiplicities.Add(4);
        }
        var curve = new BSpline3Curve(3, controls, multiplicities, knots,
            "UNSPECIFIED", false, false, "UNSPECIFIED");
        return KernelResult<QualifiedHelicalRibBoundary>.Success(new(
            authority, curve, requested, error, allowance, segmentCount));
    }

    private static KernelResult<QualifiedHelicalRibBoundary> Failure(string code, string message) =>
        KernelResult<QualifiedHelicalRibBoundary>.Failure([
            new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error,
                $"helical-rib-realization-{code}: {message}", "Geometry.HelicalRib.Boundary")]);
}
