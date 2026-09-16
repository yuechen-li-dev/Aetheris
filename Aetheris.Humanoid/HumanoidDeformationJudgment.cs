using System.Numerics;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Humanoid;

public sealed record DeformationMetrics(bool Finite, double MinimumEdgeRatio, double MaximumEdgeRatio,
    double EdgeDistortionMedian, double EdgeDistortionP95, double EdgeDistortionP99, double EdgeDistortionMaximum,
    double MinimumAreaRatio, double MaximumAreaRatio, double MeanAbsoluteLogEdgeRatio, double MeanAbsoluteLogAreaRatio,
    int CollapsedFaces, int OrientationReversalProxies, double FixedAttachmentMaximumMm,
    double BoundaryMaximumMm, double BaselineDriftRmsMm, IReadOnlyList<string> FlaggedFaceIds);
public sealed record MeasuredDeformationCandidate(DeformationCandidate Candidate, DeformationMetrics Metrics);
public sealed record DeformationPolicy(string Version, double MinimumEdgeRatio = .25, double MaximumEdgeRatio = 4,
    double MinimumAreaRatio = .1, double MaximumAreaRatio = 8, double FixedAttachmentToleranceMm = .001,
    double BoundaryToleranceMm = .001, double MetricWeight = .65, double AreaWeight = .25, double BaselineAgreementWeight = .10)
{
    public static DeformationPolicy HipV1 { get; } = new("humanoid.hip.deformation.v1");
    public static DeformationPolicy KneeV1 { get; } = new("humanoid.knee.deformation.v1");
}
public sealed record HumanoidDeformationDecision(string PolicyVersion, string? WinnerId, string? RunnerUpId,
    bool TieBroken, IReadOnlyList<JudgmentCandidateTrace> Trace,
    IReadOnlyList<MeasuredDeformationCandidate> Candidates, IReadOnlyList<Point3D>? Positions,
    ConstrainedSurfaceEvidence? FinalSurfaceEvidence, IReadOnlyList<KinematicDiagnostic> Diagnostics)
{
    public bool IsSuccess => Positions is not null;
}

/// <summary>Region policy is separate from construction. The shared JudgmentModel owns scoring and selection.</summary>
public static class HumanoidDeformationJudgment
{
    public static JudgmentModel<MeasuredDeformationCandidate, HumanoidDeformationContext> Model(DeformationPolicy policy,
        IReadOnlyList<JudgmentScoreTerm<MeasuredDeformationCandidate, HumanoidDeformationContext>>? extraTerms = null)
    {
        if (string.IsNullOrWhiteSpace(policy.Version) || !double.IsFinite(policy.MinimumEdgeRatio) || policy.MinimumEdgeRatio is <= 0 or > 1 ||
            !double.IsFinite(policy.MaximumEdgeRatio) || policy.MaximumEdgeRatio < 1 ||
            !double.IsFinite(policy.MinimumAreaRatio) || policy.MinimumAreaRatio is <= 0 or > 1 ||
            !double.IsFinite(policy.MaximumAreaRatio) || policy.MaximumAreaRatio < 1 ||
            !double.IsFinite(policy.FixedAttachmentToleranceMm) || policy.FixedAttachmentToleranceMm <= 0 ||
            !double.IsFinite(policy.BoundaryToleranceMm) || policy.BoundaryToleranceMm <= 0)
            throw new ArgumentException("Invalid deformation policy thresholds.");
        JudgmentRule<MeasuredDeformationCandidate, HumanoidDeformationContext> Rule(string id, string code,
            Func<DeformationMetrics, double> metric, double limit, Func<double, double, bool> predicate, string evidence)
            => new(id, (c, _) => new(predicate(metric(c.Metrics), limit), code, metric(c.Metrics), limit, evidence));
        var rules = new JudgmentRule<MeasuredDeformationCandidate, HumanoidDeformationContext>[]
        {
            Rule("finite", "HUM303", m => m.Finite ? 0 : 1, 0, (x,l) => x == l, "All patch positions must be finite."),
            Rule("noncollapsed", "HUM303", m => m.CollapsedFaces, 0, (x,l) => x == l, "Triangle area >= 0.0001 mm2."),
            Rule("orientation-proxy", "HUM303", m => m.OrientationReversalProxies, 0, (x,l) => x == l, "No dominant-influence transported-normal reversals; intersection certification unavailable."),
            Rule("compression", "HUM301", m => m.MinimumEdgeRatio, policy.MinimumEdgeRatio, (x,l) => x >= l, "Each unique patch edge: deformed/rest >= lower bound."),
            Rule("stretch", "HUM302", m => m.MaximumEdgeRatio, policy.MaximumEdgeRatio, (x,l) => x <= l, "Each unique patch edge: deformed/rest <= upper bound."),
            Rule("area-compression", "HUM301", m => m.MinimumAreaRatio, policy.MinimumAreaRatio, (x,l) => x >= l, "Every patch triangle retains bounded area."),
            Rule("area-stretch", "HUM302", m => m.MaximumAreaRatio, policy.MaximumAreaRatio, (x,l) => x <= l, "Every patch triangle has bounded area expansion."),
            Rule("fixed-attachment", "HUM304", m => m.FixedAttachmentMaximumMm, policy.FixedAttachmentToleranceMm, (x,l) => x <= l, "Protected vertices follow the existing solved evaluation."),
            Rule("boundary", "HUM305", m => m.BoundaryMaximumMm, policy.BoundaryToleranceMm, (x,l) => x <= l, "Shared boundary vertices coincide with unchanged neighboring surface.")
        };
        var terms = new List<JudgmentScoreTerm<MeasuredDeformationCandidate, HumanoidDeformationContext>>
        {
            new("edge-metric", policy.MetricWeight, (c, _) => new(1 / (1 + c.Metrics.MeanAbsoluteLogEdgeRatio), c.Metrics.MeanAbsoluteLogEdgeRatio, "mean(abs(log(ratio)))", "1/(1+mean absolute log edge ratio); neutral=1.")),
            new("area-metric", policy.AreaWeight, (c, _) => new(1 / (1 + c.Metrics.MeanAbsoluteLogAreaRatio), c.Metrics.MeanAbsoluteLogAreaRatio, "mean(abs(log(ratio)))", "1/(1+mean absolute log area ratio); neutral=1.")),
            new("baseline-agreement", policy.BaselineAgreementWeight, (c, _) => new(1 / (1 + c.Metrics.BaselineDriftRmsMm / 10), c.Metrics.BaselineDriftRmsMm, "mm RMS", "1/(1+RMS/10mm). X1 baseline agreement, NOT Antonia source agreement."))
        };
        if (extraTerms is not null) terms.AddRange(extraTerms);
        return new(policy.Version, rules, terms);
    }

    public static HumanoidDeformationDecision Evaluate(HumanoidDeformationContext context, DeformationPolicy policy,
        IReadOnlyList<TransitionCandidateSpec>? specs = null,
        IReadOnlyList<JudgmentScoreTerm<MeasuredDeformationCandidate, HumanoidDeformationContext>>? extraTerms = null,
        Action<string, double>? recordTiming = null)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var generated = (specs ?? HumanoidDeformationCandidates.Default).Select(s => HumanoidDeformationCandidates.Generate(context, s)).ToArray();
        recordTiming?.Invoke("candidateGenerationMs", timer.Elapsed.TotalMilliseconds);
        timer.Restart();
        var candidates = generated.Select(c => new MeasuredDeformationCandidate(c, Measure(context, c))).ToArray();
        recordTiming?.Invoke("metricMeasurementMs", timer.Elapsed.TotalMilliseconds);
        var result = Model(policy, extraTerms).Evaluate(context, candidates.Select(c => new JudgmentOption<MeasuredDeformationCandidate>(c.Candidate.Id, c)).ToArray(), recordTiming);
        timer.Restart();
        var diagnostics = new List<KinematicDiagnostic>
        {
            new("HUM307", "No admitted, correspondence-verified source posed surface is available. Source-agreement scoring is disabled.")
        };
        IReadOnlyList<Point3D>? positions = null;
        ConstrainedSurfaceEvidence? final = null;
        if (result.Winner is { } winner)
        {
            var assembled = context.AssembleDiagnostic(winner.Value.Candidate);
            final = HumanoidConstrainedSurface.Inspect(context.Surface, context.Skeleton, context.Solved.GlobalTransforms, assembled);
            if (final.IsAdmissible) positions = assembled;
            else diagnostics.Add(new("HUM300", "Local selection failed final whole-surface screening, including unported regions; no mesh admitted."));
            if (result.TieBroken) diagnostics.Add(new("HUM306", "Utilities tie within shared-engine epsilon 1e-12; ordinal stable candidate ID wins."));
        }
        else diagnostics.Add(new("HUM300", "No admissible local deformation candidate. No fallback mesh is returned."));
        recordTiming?.Invoke("assemblyAndFinalValidationMs", timer.Elapsed.TotalMilliseconds);
        return new(policy.Version, result.Winner?.Id, result.RunnerUp, result.TieBroken, result.Candidates,
            Array.AsReadOnly(candidates), positions, final, diagnostics.AsReadOnly());
    }

    public static DeformationMetrics Measure(HumanoidDeformationContext context, DeformationCandidate candidate)
    {
        if (candidate.PatchPositions.Count != context.Vertices.Count) throw new ArgumentException("Patch position count mismatch.");
        var finite = candidate.PatchPositions.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z));
        if (!finite) return new(false, 0, double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue,
            0, double.MaxValue, double.MaxValue, double.MaxValue, 0, 0, double.MaxValue, double.MaxValue, double.MaxValue, []);
        Point3D P(int i) => candidate.PatchPositions[context.LocalIndex[i]];
        Point3D R(int i) => context.Surface.Vertices[i].Position;
        var ratios = context.Edges.Select(e => Ratio((P(e.A) - P(e.B)).Length, (R(e.A) - R(e.B)).Length)).ToArray();
        var distortion = ratios.Select(r => r > 0 ? Math.Max(r, 1 / r) : double.MaxValue).Order().ToArray();
        var areas = new List<double>(); var flags = new List<string>(); var collapsed = 0;
        foreach (var f in context.Faces)
        {
            var oldNormal = (R(f.B) - R(f.A)).Cross(R(f.C) - R(f.A));
            var normal = (P(f.B) - P(f.A)).Cross(P(f.C) - P(f.A));
            areas.Add(Ratio(normal.Length, oldNormal.Length));
            var joint = new[] { f.A, f.B, f.C }.SelectMany(i => context.Surface.SkinWeights[i].Weights)
                .GroupBy(w => w.JointIndex).OrderByDescending(g => g.Sum(w => w.Weight)).ThenBy(g => g.Key).First().Key;
            var expected = Vector3.TransformNormal(new((float)oldNormal.X, (float)oldNormal.Y, (float)oldNormal.Z), context.SkinTransforms[joint]);
            if (normal.LengthSquared < 4e-8) collapsed++;
            else if (normal.Dot(new(expected.X, expected.Y, expected.Z)) <= 0) flags.Add(f.Id);
        }
        var fixedDrift = 0d; var boundaryDrift = 0d; var totalDrift = 0d;
        for (var i = 0; i < context.Vertices.Count; i++)
        {
            var v = context.Vertices[i]; var drift = (candidate.PatchPositions[i] - context.Baseline[v.SurfaceIndex]).Length;
            totalDrift += drift * drift;
            if (v.Attachment == SurfaceAttachmentClass.FixedToExistingEvaluation) fixedDrift = Math.Max(fixedDrift, drift);
            if (v.Boundary) boundaryDrift = Math.Max(boundaryDrift, drift);
        }
        return new(true, ratios.Min(), ratios.Max(), Percentile(.5), Percentile(.95), Percentile(.99), distortion[^1],
            areas.Min(), areas.Max(), ratios.Average(Log), areas.Average(Log), collapsed, flags.Count,
            fixedDrift, boundaryDrift, Math.Sqrt(totalDrift / context.Vertices.Count), flags.AsReadOnly());
        double Percentile(double p) => distortion[(int)Math.Ceiling(p * distortion.Length) - 1];
        static double Ratio(double a, double b) => b > 0 && double.IsFinite(a) ? a / b : double.MaxValue;
        static double Log(double ratio) => ratio > 0 ? Math.Abs(Math.Log(ratio)) : Math.Log(double.MaxValue);
    }
}
