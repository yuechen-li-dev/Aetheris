namespace Aetheris.Kernel.Core.Judgment;

public sealed record JudgmentRuleEvidence(bool Passed, string Code, double Measured, double Limit, string Evidence);
public sealed record JudgmentScoreEvidence(double Utility, double RawValue, string Unit, string Evidence);
public sealed record JudgmentRule<TCandidate, TContext>(string Id,
    Func<TCandidate, TContext, JudgmentRuleEvidence> Evaluate);
public sealed record JudgmentScoreTerm<TCandidate, TContext>(string Id, double Weight,
    Func<TCandidate, TContext, JudgmentScoreEvidence> Evaluate);
public sealed record JudgmentOption<TCandidate>(string Id, TCandidate Value);
public sealed record JudgmentRuleTrace(string Id, JudgmentRuleEvidence Result);
public sealed record JudgmentScoreTrace(string Id, double Weight, JudgmentScoreEvidence Result);
public sealed record JudgmentCandidateTrace(string Id, bool Admissible,
    IReadOnlyList<JudgmentRuleTrace> Rules, IReadOnlyList<JudgmentScoreTrace> Scores, double? TotalScore);
public sealed record JudgmentModelResult<TCandidate>(string PolicyVersion,
    JudgmentOption<TCandidate>? Winner, string? RunnerUp, bool TieBroken,
    IReadOnlyList<JudgmentCandidateTrace> Candidates);

/// <summary>
/// Inspectable policy adapter over the existing chooser. It does not implement another argmax.
/// Utilities are finite [0,1], positive weights are normalized by Utility.Weighted, and hard
/// rules run before any score term. Existing JudgmentEngine callers retain their exact behavior.
/// </summary>
public sealed class JudgmentModel<TCandidate, TContext>(string version,
    IReadOnlyList<JudgmentRule<TCandidate, TContext>> rules,
    IReadOnlyList<JudgmentScoreTerm<TCandidate, TContext>> scoreTerms)
{
    public string Version { get; } = version;
    public IReadOnlyList<JudgmentRule<TCandidate, TContext>> Rules { get; } = Array.AsReadOnly(rules.ToArray());
    public IReadOnlyList<JudgmentScoreTerm<TCandidate, TContext>> ScoreTerms { get; } = Array.AsReadOnly(scoreTerms.ToArray());

    public JudgmentModelResult<TCandidate> Evaluate(TContext context, IReadOnlyList<JudgmentOption<TCandidate>> options,
        Action<string, double>? recordTiming = null)
    {
        if (string.IsNullOrWhiteSpace(Version) || Rules.Count == 0 || ScoreTerms.Count == 0 ||
            Rules.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != Rules.Count ||
            ScoreTerms.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count() != ScoreTerms.Count ||
            ScoreTerms.Any(t => !double.IsFinite(t.Weight) || t.Weight <= 0) ||
            !double.IsFinite(ScoreTerms.Sum(t => t.Weight)))
            throw new ArgumentException("Judgment policy needs a version, unique rules/terms, and finite positive score weights.");
        var ordered = options.OrderBy(o => o.Id, StringComparer.Ordinal).ToArray();
        if (ordered.Any(o => string.IsNullOrWhiteSpace(o.Id)) || ordered.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new ArgumentException("Candidate IDs must be nonempty and unique.");
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var traces = new List<JudgmentCandidateTrace>();
        foreach (var option in ordered)
        {
            var ruleResults = Rules.Select(r =>
            {
                var result = r.Evaluate(option.Value, context);
                if (!double.IsFinite(result.Measured) || !double.IsFinite(result.Limit))
                    result = new(false, "judgment-nonfinite-rule", 0, 0, "Rule returned nonfinite measurement or limit.");
                return new JudgmentRuleTrace(r.Id, result);
            }).ToArray();
            var admitted = ruleResults.All(r => r.Result.Passed);
            var scores = admitted ? ScoreTerms.Select(t => new JudgmentScoreTrace(t.Id, t.Weight, t.Evaluate(option.Value, context))).ToArray() : [];
            if (scores.Any(s => !double.IsFinite(s.Result.Utility) || s.Result.Utility < 0 || s.Result.Utility > 1 || !double.IsFinite(s.Result.RawValue)))
            {
                admitted = false;
                ruleResults = [.. ruleResults, new("finite-bounded-utility", new(false, "judgment-invalid-score", 0, 1, "Score term returned a nonfinite or out-of-range utility/raw value."))];
                scores = []; // Do not serialize NaN/Infinity or silently normalize invalid scores.
            }
            double? total = admitted ? Utility.Weighted(scores.Select(s => (s.Result.Utility, s.Weight)).ToArray()) : null;
            traces.Add(new(option.Id, admitted, Array.AsReadOnly(ruleResults), Array.AsReadOnly(scores), total));
        }
        recordTiming?.Invoke("admissibilityAndUtilityMs", timer.Elapsed.TotalMilliseconds);
        timer.Restart();
        var lookup = traces.ToDictionary(t => t.Id, StringComparer.Ordinal);
        var candidates = ordered.Select(o => new JudgmentCandidate<IReadOnlyDictionary<string, JudgmentCandidateTrace>>(
            o.Id, c => c[o.Id].Admissible, c => c[o.Id].TotalScore!.Value,
            c => string.Join("; ", c[o.Id].Rules.Where(r => !r.Result.Passed).Select(r => r.Result.Code + ": " + r.Result.Evidence)))).ToArray();
        var engine = new JudgmentEngine<IReadOnlyDictionary<string, JudgmentCandidateTrace>>();
        var decision = engine.Evaluate(lookup, candidates);
        if (!decision.IsSuccess)
        {
            recordTiming?.Invoke("selectionMs", timer.Elapsed.TotalMilliseconds);
            return new(Version, null, null, false, traces.AsReadOnly());
        }
        var selected = decision.Selection!.Value;
        var runner = engine.Evaluate(lookup, candidates.Where(c => c.Name != selected.Candidate.Name).ToArray()).Selection;
        recordTiming?.Invoke("selectionMs", timer.Elapsed.TotalMilliseconds);
        return new(Version, ordered.Single(o => o.Id == selected.Candidate.Name), runner?.Candidate.Name,
            runner is { } r && double.Abs(r.Score - selected.Score) <= 1e-12, traces.AsReadOnly());
    }
}
