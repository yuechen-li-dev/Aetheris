using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Curves;

/// <summary>Adaptive, parameter-preserving cubic approximation of a rational STEP edge.</summary>
public static class BSplineCurveRationalReduction
{
    private const int MaximumSegments = 2048;
    private const int MaximumDepth = 14;

    public static bool TryReduce(
        BSpline3Curve source,
        IReadOnlyList<double> weights,
        double toleranceMillimetres,
        out BSpline3Curve reduced,
        out double measuredMaxDeviationMillimetres,
        out string reason)
    {
        reduced = default;
        measuredMaxDeviationMillimetres = double.PositiveInfinity;
        if (!double.IsFinite(toleranceMillimetres) || toleranceMillimetres <= 0d
            || weights.Count != source.ControlPoints.Count
            || weights.Any(weight => !double.IsFinite(weight) || weight <= 0d))
        {
            reason = "Recovery tolerance and all rational weights must be positive and finite, with one weight per control point.";
            return false;
        }

        var intervals = source.GetNonZeroKnotSpans(new ParameterInterval(source.DomainStart, source.DomainEnd));
        if (intervals.Count == 0)
        {
            reason = "The rational source has no nonzero knot span.";
            return false;
        }

        var accepted = new List<Segment>();
        var measured = 0d;
        foreach (var interval in intervals)
        {
            if (!Refine(interval.Start, interval.End, 0))
            {
                reason = $"Adaptive cubic refinement could not meet {toleranceMillimetres:G6} mm within {MaximumSegments} segments (last accepted span deviation {measured:G6} mm).";
                measuredMaxDeviationMillimetres = double.PositiveInfinity;
                return false;
            }
        }

        var controls = new List<Point3D>(accepted.Count * 3 + 1);
        var knots = new List<double>(accepted.Count + 1);
        var multiplicities = new List<int>(accepted.Count + 1);
        for (var index = 0; index < accepted.Count; index++)
        {
            var segment = accepted[index];
            if (index == 0)
            {
                knots.Add(segment.Start);
                multiplicities.Add(4);
                controls.Add(segment.P0);
            }
            controls.Add(segment.P1);
            controls.Add(segment.P2);
            controls.Add(segment.P3);
            knots.Add(segment.End);
            multiplicities.Add(index == accepted.Count - 1 ? 4 : 3);
        }

        try
        {
            reduced = new BSpline3Curve(3, controls, multiplicities, knots,
                source.CurveForm, source.ClosedCurve, source.SelfIntersect, "UNSPECIFIED");
        }
        catch (ArgumentException ex)
        {
            reason = $"Recovered spline construction failed: {ex.Message}";
            return false;
        }

        measuredMaxDeviationMillimetres = measured;
        reason = $"adaptive cubic recovery used {accepted.Count} spans; maximum sampled deviation {measured:G6} mm within {toleranceMillimetres:G6} mm.";
        return true;

        bool Refine(double start, double end, int depth)
        {
            if (accepted.Count >= MaximumSegments) return false;
            if (!TryEvaluate(start, out var p0) || !TryEvaluate(end, out var p3)) return false;
            var h = end - start;
            // Interior secants avoid evaluating a derivative across repeated source knots.
            var delta = h * 1e-4d;
            if (!TryEvaluate(start + delta, out var nearStart) || !TryEvaluate(end - delta, out var nearEnd)) return false;
            var p1 = p0 + ((nearStart - p0) * (h / (3d * delta)));
            var p2 = p3 - ((p3 - nearEnd) * (h / (3d * delta)));
            var candidate = new Segment(start, end, p0, p1, p2, p3);
            var error = 0d;
            foreach (var fraction in new[] { 0.125d, 0.25d, 0.375d, 0.5d, 0.625d, 0.75d, 0.875d })
            {
                if (!TryEvaluate(start + h * fraction, out var actual)) return false;
                error = double.Max(error, (actual - candidate.Evaluate(fraction)).Length);
            }
            if (error <= toleranceMillimetres)
            {
                accepted.Add(candidate);
                measured = double.Max(measured, error);
                return true;
            }
            if (depth >= MaximumDepth || accepted.Count + 2 > MaximumSegments) return false;
            var middle = start + h * 0.5d;
            return Refine(start, middle, depth + 1) && Refine(middle, end, depth + 1);
        }

        bool TryEvaluate(double parameter, out Point3D point)
        {
            var evaluated = source.EvaluateRational(weights, parameter);
            point = evaluated ?? default;
            return evaluated.HasValue;
        }
    }

    private readonly record struct Segment(double Start, double End, Point3D P0, Point3D P1, Point3D P2, Point3D P3)
    {
        public Point3D Evaluate(double fraction)
        {
            var opposite = 1d - fraction;
            return Point3D.Origin
                + ((P0 - Point3D.Origin) * (opposite * opposite * opposite))
                + ((P1 - Point3D.Origin) * (3d * opposite * opposite * fraction))
                + ((P2 - Point3D.Origin) * (3d * opposite * fraction * fraction))
                + ((P3 - Point3D.Origin) * (fraction * fraction * fraction));
        }
    }
}
