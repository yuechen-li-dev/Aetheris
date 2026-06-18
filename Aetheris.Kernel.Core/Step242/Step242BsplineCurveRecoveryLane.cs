using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Step242;

internal static class Step242BsplineCurveRecoveryLane
{
    internal const string AnalyticCircleCandidate = "analytic_circle";
    internal const string RejectCandidate = "reject";
    private const double FitTolerance = 1e-6d;
    private const double DegenerateTolerance = 1e-10d;

    internal readonly record struct RecoveryDecision(string CandidateName, CurveGeometry? RecoveredCurve, string Reason);

    public static RecoveryDecision Decide(Step242ParsedEntity sourceEntity, BSpline3Curve curve, IReadOnlyList<double> weights)
    {
        var probe = ProbeCircleRecovery(curve, weights);
        var context = new RecoveryContext(
            IsRationalLike: Step242SubsetDecoder.TryGetConstructor(sourceEntity.Instance, "RATIONAL_B_SPLINE_CURVE") is not null,
            CircleProbe: probe);

        var engine = new JudgmentEngine<RecoveryContext>();
        var judgment = engine.Evaluate(context, BuildCandidates());
        if (!judgment.IsSuccess || !judgment.Selection.HasValue)
        {
            var reason = judgment.Rejections.Count == 0
                ? "step242-rational-bspline-circle-rejected: no bounded analytic recovery candidate was admissible."
                : string.Join(" ", judgment.Rejections.Select(r => $"{r.CandidateName}: {r.Reason}."));
            return new RecoveryDecision(RejectCandidate, null, reason);
        }

        if (string.Equals(judgment.Selection.Value.Candidate.Name, AnalyticCircleCandidate, StringComparison.Ordinal)
            && context.CircleProbe.Circle.HasValue)
        {
            return new RecoveryDecision(
                AnalyticCircleCandidate,
                CurveGeometry.FromCircle(context.CircleProbe.Circle.Value),
                "step242-rational-bspline-circle-recovered: recovered analytic circle from rational B-spline curve.");
        }

        var rejectReason = context.IsRationalLike
            ? context.CircleProbe.Reason
            : "step242-rational-bspline-circle-rejected: curve is not a rational B-spline representation.";
        return new RecoveryDecision(RejectCandidate, null, rejectReason);
    }

    private static IReadOnlyList<JudgmentCandidate<RecoveryContext>> BuildCandidates() =>
    [
        new JudgmentCandidate<RecoveryContext>(
            Name: AnalyticCircleCandidate,
            IsAdmissible: When.All<RecoveryContext>(
                context => context.IsRationalLike,
                context => context.CircleProbe.Circle.HasValue),
            Score: _ => 100d,
            RejectionReason: context => context.IsRationalLike
                ? context.CircleProbe.Reason
                : "step242-rational-bspline-circle-rejected: curve is not a rational B-spline representation.",
            TieBreakerPriority: 0),
        new JudgmentCandidate<RecoveryContext>(
            Name: RejectCandidate,
            IsAdmissible: _ => true,
            Score: _ => -1d,
            RejectionReason: context => context.CircleProbe.Reason,
            TieBreakerPriority: 1)
    ];

    private static CircleProbe ProbeCircleRecovery(BSpline3Curve curve, IReadOnlyList<double> weights)
    {
        if (weights.Count == 0) return new CircleProbe(null, "step242-rational-bspline-weights-missing: rational curve has no weights.");
        if (weights.Count != curve.ControlPoints.Count) return new CircleProbe(null, "step242-rational-bspline-circle-rejected: weight count does not match control point count.");
        if (curve.Degree != 2) return new CircleProbe(null, "step242-rational-bspline-unsupported-degree: only rational quadratic circle recovery is bounded.");
        if (weights.Any(w => !double.IsFinite(w) || w <= DegenerateTolerance)) return new CircleProbe(null, "step242-rational-bspline-circle-rejected: rational weights must be positive finite values.");

        var samples = new List<Point3D>();
        const int sampleCount = 9;
        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)(sampleCount - 1);
            var u = curve.DomainStart + ((curve.DomainEnd - curve.DomainStart) * t);
            var sample = EvaluateRational(curve, weights, u);
            if (!sample.HasValue) return new CircleProbe(null, "step242-rational-bspline-circle-rejected: rational evaluation produced a degenerate denominator.");
            samples.Add(sample.Value);
        }

        if (!TryFitCircle(samples, out var circle, out var reason)) return new CircleProbe(null, reason);

        foreach (var sample in samples)
        {
            var planeResidual = double.Abs((sample - circle.Center).Dot(circle.Normal.ToVector()));
            var radiusResidual = double.Abs((sample - circle.Center).Length - circle.Radius);
            if (planeResidual > FitTolerance || radiusResidual > FitTolerance)
            {
                return new CircleProbe(null, "step242-rational-bspline-circle-fit-residual-exceeded: sampled rational curve does not remain on the recovered circle.");
            }
        }

        return new CircleProbe(circle, "step242-rational-bspline-circle-recovered: bounded rational quadratic samples fit a circle.");
    }

    internal static Point3D? EvaluateRational(BSpline3Curve curve, IReadOnlyList<double> weights, double parameter)
    {
        var p = curve.Degree;
        var u = double.Min(double.Max(parameter, curve.DomainStart), curve.DomainEnd);
        if (double.Abs(u - curve.DomainEnd) <= 1e-12d) return curve.ControlPoints[^1];
        var span = FindSpan(curve, u);
        var points = new (double X, double Y, double Z, double W)[p + 1];
        for (var j = 0; j <= p; j++)
        {
            var index = span - p + j;
            var w = weights[index];
            var cp = curve.ControlPoints[index];
            points[j] = (cp.X * w, cp.Y * w, cp.Z * w, w);
        }

        for (var r = 1; r <= p; r++)
        {
            for (var j = p; j >= r; j--)
            {
                var leftKnot = curve.FullKnots[span - p + j];
                var rightKnot = curve.FullKnots[span + 1 + j - r];
                var denominator = rightKnot - leftKnot;
                var alpha = double.Abs(denominator) <= 1e-15d ? 0d : (u - leftKnot) / denominator;
                points[j] = Lerp(points[j - 1], points[j], alpha);
            }
        }

        if (double.Abs(points[p].W) <= DegenerateTolerance) return null;
        return new Point3D(points[p].X / points[p].W, points[p].Y / points[p].W, points[p].Z / points[p].W);
    }

    private static bool TryFitCircle(IReadOnlyList<Point3D> samples, out Circle3Curve circle, out string reason)
    {
        circle = default;
        var p0 = samples[0];
        var p1 = samples[samples.Count / 2];
        var p2 = samples[^1];
        var a = p1 - p0;
        var b = p2 - p0;
        var normalVector = a.Cross(b);
        if (!Direction3D.TryCreate(normalVector, out var normal))
        {
            reason = "step242-rational-bspline-circle-rejected: sampled rational curve is collinear or degenerate.";
            return false;
        }

        var aa = a.Dot(a);
        var ab = a.Dot(b);
        var bb = b.Dot(b);
        var determinant = (aa * bb) - (ab * ab);
        if (determinant <= DegenerateTolerance)
        {
            reason = "step242-rational-bspline-circle-rejected: sampled rational curve cannot define a stable circle.";
            return false;
        }

        var rhsA = aa * 0.5d;
        var rhsB = bb * 0.5d;
        var x = ((rhsA * bb) - (rhsB * ab)) / determinant;
        var y = ((aa * rhsB) - (ab * rhsA)) / determinant;
        var center = p0 + (a * x) + (b * y);
        var radius = (p0 - center).Length;
        if (!double.IsFinite(radius) || radius <= DegenerateTolerance || !Direction3D.TryCreate(p0 - center, out var xAxis))
        {
            reason = "step242-rational-bspline-circle-rejected: recovered circle radius/reference axis is degenerate.";
            return false;
        }

        circle = new Circle3Curve(center, normal, radius, xAxis);
        reason = string.Empty;
        return true;
    }

    private static int FindSpan(BSpline3Curve curve, double u)
    {
        var n = curve.ControlPoints.Count - 1;
        if (u >= curve.FullKnots[n + 1]) return n;
        var low = curve.Degree;
        var high = n + 1;
        var mid = (low + high) / 2;
        while (u < curve.FullKnots[mid] || u >= curve.FullKnots[mid + 1])
        {
            if (u < curve.FullKnots[mid]) high = mid; else low = mid;
            mid = (low + high) / 2;
        }
        return mid;
    }

    private static (double X, double Y, double Z, double W) Lerp((double X, double Y, double Z, double W) a, (double X, double Y, double Z, double W) b, double t) =>
        (a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t), a.Z + ((b.Z - a.Z) * t), a.W + ((b.W - a.W) * t));

    private readonly record struct RecoveryContext(bool IsRationalLike, CircleProbe CircleProbe);
    private readonly record struct CircleProbe(Circle3Curve? Circle, string Reason);
}
