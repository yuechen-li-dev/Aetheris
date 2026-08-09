using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

/// <summary>
/// Deterministic approximation-layer sampling for non-rational foreign STEP
/// trim curves. This is deliberately not a NURBS support-surface capability.
/// </summary>
public static class BSplineTrimSampler
{
    public static BSplineTrimSamplingResult Sample(BSpline3Curve curve, ParameterInterval interval, SurfaceMeshPolicy policy)
    {
        var spans = curve.GetNonZeroKnotSpans(interval);
        if (spans.Count == 0) return BSplineTrimSamplingResult.Failed("The requested interval contains no non-zero B-spline knot spans.");
        var samples = new List<BSplineTrimSample>
        {
            new(spans[0].Start, curve.Evaluate(spans[0].Start))
        };
        var maximumChordalDeviation = 0d;
        var maximumTangentDeviation = 0d;
        foreach (var span in spans)
        {
            var startPoint = curve.Evaluate(span.Start);
            var endPoint = curve.Evaluate(span.End);
            if (!AppendSegment(curve, span.Start, span.End, startPoint, endPoint, policy, samples, 0,
                    ref maximumChordalDeviation, ref maximumTangentDeviation, out var failure))
                return BSplineTrimSamplingResult.Failed(failure!);
        }
        return new BSplineTrimSamplingResult(true, samples, maximumChordalDeviation, maximumTangentDeviation, null);
    }

    private static bool AppendSegment(
        BSpline3Curve curve,
        double start,
        double end,
        Point3D startPoint,
        Point3D endPoint,
        SurfaceMeshPolicy policy,
        List<BSplineTrimSample> samples,
        int depth,
        ref double maximumChordalDeviation,
        ref double maximumTangentDeviation,
        out string? failure)
    {
        var middle = (start + end) * 0.5d;
        var middlePoint = curve.Evaluate(middle);
        var chordalDeviation = PointToSegmentDistance(middlePoint, startPoint, endPoint);
        var tangentDeviation = MaximumTangentAngle(curve, start, middle, end);
        var accepted = chordalDeviation <= policy.TargetChordalError && tangentDeviation <= policy.TargetNormalErrorRadians;
        if (accepted)
        {
            if (samples.Count >= policy.MaxBoundarySamples)
            {
                failure = $"B-spline trim sampling exceeded the maximum of {policy.MaxBoundarySamples} samples.";
                return false;
            }
            samples.Add(new BSplineTrimSample(end, endPoint));
            maximumChordalDeviation = double.Max(maximumChordalDeviation, chordalDeviation);
            maximumTangentDeviation = double.Max(maximumTangentDeviation, tangentDeviation);
            failure = null;
            return true;
        }
        if (depth >= policy.MaxRefinementDepth)
        {
            failure = $"B-spline trim sampling could not meet chordal/tangent tolerances within refinement depth {policy.MaxRefinementDepth} (chord={chordalDeviation:R}, tangent={tangentDeviation:R}).";
            return false;
        }
        if (!AppendSegment(curve, start, middle, startPoint, middlePoint, policy, samples, depth + 1,
                ref maximumChordalDeviation, ref maximumTangentDeviation, out failure)) return false;
        return AppendSegment(curve, middle, end, middlePoint, endPoint, policy, samples, depth + 1,
            ref maximumChordalDeviation, ref maximumTangentDeviation, out failure);
    }

    private static double PointToSegmentDistance(Point3D point, Point3D start, Point3D end)
    {
        var chord = end - start;
        if (chord.LengthSquared <= 1e-24d) return (point - start).Length;
        var t = double.Clamp((point - start).Dot(chord) / chord.LengthSquared, 0d, 1d);
        return (point - (start + (chord * t))).Length;
    }

    private static double MaximumTangentAngle(BSpline3Curve curve, double start, double middle, double end)
    {
        var a = curve.EvaluateTangent(start);
        var b = curve.EvaluateTangent(middle);
        var c = curve.EvaluateTangent(end);
        return double.Max(Angle(a, b), Angle(b, c));
    }

    private static double Angle(Vector3D left, Vector3D right)
    {
        if (left.LengthSquared <= 1e-24d || right.LengthSquared <= 1e-24d) return double.Pi;
        return double.Acos(double.Clamp(left.Dot(right) / double.Sqrt(left.LengthSquared * right.LengthSquared), -1d, 1d));
    }
}

public sealed record BSplineTrimSample(double Parameter, Point3D Point);

public sealed record BSplineTrimSamplingResult(
    bool IsSuccess,
    IReadOnlyList<BSplineTrimSample> Samples,
    double MaxChordalDeviation,
    double MaxTangentDeviationRadians,
    string? Failure)
{
    internal static BSplineTrimSamplingResult Failed(string failure) => new(false, [], 0d, 0d, failure);
}
