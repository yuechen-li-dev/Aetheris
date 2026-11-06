using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Geometry.Curves;

/// <summary>
/// Finite, non-rational representation subordinate to an exact cylinder intersection.
/// The bound follows the cubic Hermite remainder on every adaptively selected span.
/// It bounds distance to the exact curve, and therefore distance to each parent surface
/// and mismatch with either exact lifted pcurve, in real arithmetic.
/// </summary>
public sealed record QualifiedCylinderIntersectionRepresentation(
    CylinderCylinderIntersectionCurve Authority,
    BSpline3Curve Curve,
    double RequestedToleranceMm,
    double AnalyticErrorBoundMm,
    double NumericalAllowanceMm,
    int SegmentCount)
{
    public double CertifiedDeviationBoundMm => AnalyticErrorBoundMm + NumericalAllowanceMm;
}

public static class CylinderIntersectionCurveRealizer
{
    private const int MaxSegments = 16_384;
    private const double MachineEpsilon = 2.2204460492503131e-16d;

    public static KernelResult<QualifiedCylinderIntersectionRepresentation> Realize(
        CylinderCylinderIntersectionCurve authority, ToleranceContext? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(authority);
        var tol = tolerance ?? ToleranceContext.Default;
        var scale = double.Max(1d, double.Max(
            double.Max(double.Abs(authority.Center.X), double.Abs(authority.Center.Y)),
            double.Max(double.Abs(authority.Center.Z), authority.Host.Radius + authority.Tool.Radius + double.Abs(authority.Offset))));
        // Keep roundoff separate from the analytic Hermite remainder. Large coordinates
        // whose floating-point allowance consumes the tolerance fail explicitly.
        var numericalAllowance = 256d * MachineEpsilon * scale;
        var interpolationBudget = tol.Linear - numericalAllowance;
        if (interpolationBudget <= 0d)
            return Failure("Coordinate scale consumes the linear tolerance before interpolation.",
                "Geometry.CylinderIntersection.NumericToleranceExhausted");

        var spans = new List<BezierSpan>();
        var pending = new Stack<(double Start, double End)>();
        pending.Push((authority.Domain.Start, authority.Domain.End));
        while (pending.Count > 0)
        {
            var (start, end) = pending.Pop();
            var bound = authority.FourthDerivativeBound(start, end) * double.Pow(end - start, 4d) / 384d;
            if (!double.IsFinite(bound))
                return Failure("The intersection remainder bound is nonfinite.",
                    "Geometry.CylinderIntersection.NonFiniteBound");
            if (bound > interpolationBudget)
            {
                if (spans.Count + pending.Count + 2 > MaxSegments || end - start <= 1e-12d)
                    return Failure("Adaptive realization exceeded its bounded segment budget.",
                        "Geometry.CylinderIntersection.RefinementLimit");
                var mid = start + ((end - start) * 0.5d);
                pending.Push((mid, end));
                pending.Push((start, mid));
                continue;
            }

            var h = end - start;
            var p0 = authority.Evaluate(start);
            var p3 = authority.Evaluate(end);
            spans.Add(new BezierSpan(start, end, bound,
                p0, p0 + authority.Derivative(start) * (h / 3d),
                p3 - authority.Derivative(end) * (h / 3d), p3));
        }

        var controls = new List<Point3D>(spans.Count * 4);
        var knots = new List<double>(spans.Count + 1);
        var multiplicities = new List<int>(spans.Count + 1);
        knots.Add(spans[0].Start);
        multiplicities.Add(4);
        foreach (var span in spans)
        {
            controls.Add(span.P0);
            controls.Add(span.P1);
            controls.Add(span.P2);
            controls.Add(span.P3);
            knots.Add(span.End);
            multiplicities.Add(4);
        }

        var curve = new BSpline3Curve(3, controls, multiplicities, knots,
            "UNSPECIFIED", false, false, "UNSPECIFIED");
        return KernelResult<QualifiedCylinderIntersectionRepresentation>.Success(new(
            authority, curve, tol.Linear, spans.Max(span => span.Bound),
            numericalAllowance, spans.Count));
    }

    private static KernelResult<QualifiedCylinderIntersectionRepresentation> Failure(string message, string source)
        => KernelResult<QualifiedCylinderIntersectionRepresentation>.Failure([
            new KernelDiagnostic(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message, source)]);

    private readonly record struct BezierSpan(double Start, double End, double Bound,
        Point3D P0, Point3D P1, Point3D P2, Point3D P3);
}
