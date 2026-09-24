using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Geometry.Curves;

/// <summary>Finite UV representations subordinate to one exact cylinder intersection.</summary>
public sealed record QualifiedCylinderIntersectionPcurves(
    QualifiedCylinderIntersectionRepresentation Edge,
    BSpline3Curve Host,
    BSpline3Curve Tool,
    double HostLiftBoundMm,
    double ToolLiftBoundMm,
    double NumericalAllowanceMm,
    int HostSegmentCount,
    int ToolSegmentCount)
{
    public double HostEdgeMismatchBoundMm => Edge.CertifiedDeviationBoundMm + HostLiftBoundMm + NumericalAllowanceMm;
    public double ToolEdgeMismatchBoundMm => Edge.CertifiedDeviationBoundMm + ToolLiftBoundMm + NumericalAllowanceMm;
}

/// <summary>
/// Adaptive cubic Hermite pcurves. The host angular fourth derivative is bounded
/// using its rotated ±asin((r/R)cos(t)) form; the cutter angular coordinate is
/// affine and its axial fourth derivative shares the exact 3D x-coordinate bound.
/// </summary>
public static class CylinderIntersectionPcurveRealizer
{
    private const int MaxSegments = 16_384;
    private const double MachineEpsilon = 2.2204460492503131e-16d;

    public static KernelResult<QualifiedCylinderIntersectionPcurves> Realize(
        QualifiedCylinderIntersectionRepresentation edge, ToleranceContext? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(edge);
        var tol = tolerance ?? ToleranceContext.Default;
        var authority = edge.Authority;
        var scale = double.Max(1d, double.Max(
            double.Max(double.Abs(authority.Center.X), double.Abs(authority.Center.Y)),
            double.Max(double.Abs(authority.Center.Z), authority.Host.Radius + authority.Tool.Radius)));
        var numericalAllowance = 512d * MachineEpsilon * scale;
        var budget = tol.Linear - edge.CertifiedDeviationBoundMm - numericalAllowance;
        if (budget <= 0d)
            return Failure("The certified 3D edge consumes the pcurve mismatch tolerance.",
                "Geometry.CylinderIntersection.PcurveToleranceExhausted");

        var host = Build(authority, host: true, budget);
        if (!host.IsSuccess) return KernelResult<QualifiedCylinderIntersectionPcurves>.Failure(host.Diagnostics);
        var tool = Build(authority, host: false, budget);
        if (!tool.IsSuccess) return KernelResult<QualifiedCylinderIntersectionPcurves>.Failure(tool.Diagnostics);
        return KernelResult<QualifiedCylinderIntersectionPcurves>.Success(new(
            edge, host.Value.Curve, tool.Value.Curve, host.Value.LiftBound,
            tool.Value.LiftBound, numericalAllowance, host.Value.Segments, tool.Value.Segments));
    }

    private static KernelResult<(BSpline3Curve Curve, double LiftBound, int Segments)> Build(
        CylinderCylinderIntersectionCurve authority, bool host, double budget)
    {
        var spans = new List<Span>();
        var pending = new Stack<(double Start, double End)>();
        pending.Push((authority.Domain.Start, authority.Domain.End));
        while (pending.Count > 0)
        {
            var (start, end) = pending.Pop();
            var bound = host
                ? HostLiftBound(authority.Host.Radius, authority.Tool.Radius, start, end)
                : ToolLiftBound(authority.Host.Radius, authority.Tool.Radius, start, end);
            if (!double.IsFinite(bound))
                return FailureSpan("Pcurve lift bound is nonfinite.", "Geometry.CylinderIntersection.NonFinitePcurveBound");
            if (bound > budget)
            {
                if (spans.Count + pending.Count + 2 > MaxSegments || end - start <= 1e-12d)
                    return FailureSpan("Pcurve refinement exceeded its bounded segment budget.",
                        "Geometry.CylinderIntersection.PcurveRefinementLimit");
                var mid = start + (end - start) * 0.5d;
                pending.Push((mid, end));
                pending.Push((start, mid));
                continue;
            }
            var a = Evaluate(authority, host, start);
            var b = Evaluate(authority, host, end);
            var h = (end - start) / 3d;
            spans.Add(new Span(start, end, bound, a, a + Derivative(authority, host, start) * h,
                b - Derivative(authority, host, end) * h, b));
        }

        var controls = new List<Point3D>(spans.Count * 4);
        var knots = new List<double>(spans.Count + 1) { spans[0].Start };
        var multiplicities = new List<int>(spans.Count + 1) { 4 };
        foreach (var span in spans)
        {
            controls.AddRange([span.P0, span.P1, span.P2, span.P3]);
            knots.Add(span.End);
            multiplicities.Add(4);
        }
        return KernelResult<(BSpline3Curve, double, int)>.Success((
            new BSpline3Curve(3, controls, multiplicities, knots,
                "UNSPECIFIED", false, false, "UNSPECIFIED"),
            spans.Max(span => span.Bound), spans.Count));
    }

    private static Point3D Evaluate(CylinderCylinderIntersectionCurve curve, bool host, double t)
    {
        var uv = host ? curve.EvaluateHostPcurve(t) : curve.EvaluateToolPcurve(t);
        return new Point3D(uv.U, uv.V, 0d);
    }

    private static Vector3D Derivative(CylinderCylinderIntersectionCurve curve, bool host, double t)
    {
        var p = curve.Evaluate(t);
        var dp = curve.Derivative(t);
        if (!host)
            return new Vector3D(1d, dp.Dot(curve.Tool.Axis.ToVector()), 0d);
        var offset = p - curve.Host.Origin;
        var x = curve.Host.XAxis.ToVector();
        var y = curve.Host.YAxis.ToVector();
        var a = offset.Dot(x);
        var b = offset.Dot(y);
        var da = dp.Dot(x);
        var db = dp.Dot(y);
        return new Vector3D((a * db - b * da) / (curve.Host.Radius * curve.Host.Radius),
            dp.Dot(curve.Host.Axis.ToVector()), 0d);
    }

    private static double HostLiftBound(double radius, double toolRadius, double start, double end)
    {
        var q = toolRadius / radius;
        var maxS = q * double.Sqrt(MaxCosSquared(start, end));
        var oneMinus = 1d - maxS * maxS;
        if (oneMinus <= 0d) return double.PositiveInfinity;
        var sqrt = double.Sqrt(oneMinus);
        var h1 = 1d / sqrt;
        var h2 = maxS / (oneMinus * sqrt);
        var h3 = (1d + 2d * maxS * maxS) / (oneMinus * oneMinus * sqrt);
        var h4 = maxS * (9d + 6d * maxS * maxS) / (oneMinus * oneMinus * oneMinus * sqrt);
        var uFourth = h4 * double.Pow(q, 4d) + 6d * h3 * double.Pow(q, 3d)
            + 7d * h2 * q * q + h1 * q;
        return (radius * uFourth + toolRadius) * double.Pow(end - start, 4d) / 384d;
    }

    private static double ToolLiftBound(double radius, double toolRadius, double start, double end)
    {
        var r2 = toolRadius * toolRadius;
        var minG = radius * radius - r2 * MaxCosSquared(start, end);
        if (minG <= 0d) return double.PositiveInfinity;
        var sqrtG = double.Sqrt(minG);
        var g3 = minG * sqrtG;
        var g5 = minG * g3;
        var g7 = minG * g5;
        var fourth = (15d * double.Pow(r2, 4d) / (16d * g7))
            + (4.5d * double.Pow(r2, 3d) / g5)
            + (7d * r2 * r2 / g3)
            + (4d * r2 / sqrtG);
        return fourth * double.Pow(end - start, 4d) / 384d;
    }

    private static double MaxCosSquared(double start, double end)
    {
        var result = double.Max(double.Pow(double.Cos(start), 2d), double.Pow(double.Cos(end), 2d));
        return double.Ceiling(start / double.Pi) * double.Pi <= end ? 1d : result;
    }

    private static KernelResult<QualifiedCylinderIntersectionPcurves> Failure(string message, string source)
        => KernelResult<QualifiedCylinderIntersectionPcurves>.Failure([
            new KernelDiagnostic(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message, source)]);

    private static KernelResult<(BSpline3Curve, double, int)> FailureSpan(string message, string source)
        => KernelResult<(BSpline3Curve, double, int)>.Failure([
            new KernelDiagnostic(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message, source)]);

    private readonly record struct Span(double Start, double End, double Bound,
        Point3D P0, Point3D P1, Point3D P2, Point3D P3);
}
