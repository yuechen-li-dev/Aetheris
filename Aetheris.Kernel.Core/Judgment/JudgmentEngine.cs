namespace Aetheris.Kernel.Core.Judgment;

/// <summary>
/// Tiny predicate combinator layer for readable bounded decision guards.
/// </summary>
public static class When
{
    public static Func<TContext, bool> All<TContext>(params Func<TContext, bool>[] predicates)
        => context => predicates.All(predicate => predicate(context));

    public static Func<TContext, bool> Any<TContext>(params Func<TContext, bool>[] predicates)
        => context => predicates.Any(predicate => predicate(context));

    public static Func<TContext, bool> Not<TContext>(Func<TContext, bool> predicate)
        => context => !predicate(context);
}

/// <summary>
/// Generic bounded candidate contract for deterministic judgment.
/// </summary>
public readonly record struct JudgmentCandidate<TContext>(
    string Name,
    Func<TContext, bool> IsAdmissible,
    Func<TContext, double> Score,
    Func<TContext, string>? RejectionReason = null,
    int TieBreakerPriority = 0);

public readonly record struct JudgmentRejection(string CandidateName, string Reason);

public readonly record struct JudgmentSelection<TContext>(JudgmentCandidate<TContext> Candidate, double Score);

public sealed class JudgmentResult<TContext>
{
    private JudgmentResult(JudgmentSelection<TContext>? selection, IReadOnlyList<JudgmentRejection> rejections)
    {
        Selection = selection;
        Rejections = rejections;
    }

    public bool IsSuccess => Selection.HasValue;

    public JudgmentSelection<TContext>? Selection { get; }

    public IReadOnlyList<JudgmentRejection> Rejections { get; }

    public static JudgmentResult<TContext> Success(JudgmentSelection<TContext> selection)
        => new(selection, []);

    public static JudgmentResult<TContext> Failure(IReadOnlyList<JudgmentRejection> rejections)
        => new(null, rejections);
}

/// <summary>
/// Deterministic bounded chooser over candidate admissibility and score.
/// </summary>
public sealed class JudgmentEngine<TContext>
{
    public JudgmentResult<TContext> Evaluate(TContext context, IReadOnlyList<JudgmentCandidate<TContext>> candidates)
    {
        if (candidates.Count == 0)
        {
            return JudgmentResult<TContext>.Failure([new JudgmentRejection("<none>", "No candidates were supplied.")]);
        }

        var rejections = new List<JudgmentRejection>();
        JudgmentSelection<TContext>? best = null;
        var bestIndex = -1;

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (!candidate.IsAdmissible(context))
            {
                var reason = candidate.RejectionReason?.Invoke(context) ?? "Candidate predicates were not satisfied.";
                rejections.Add(new JudgmentRejection(candidate.Name, reason));
                continue;
            }

            var score = candidate.Score(context);
            if (!double.IsFinite(score))
            {
                rejections.Add(new JudgmentRejection(candidate.Name, "Candidate produced a non-finite score."));
                continue;
            }

            if (!best.HasValue || IsBetter(score, candidate, i, best.Value, bestIndex))
            {
                best = new JudgmentSelection<TContext>(candidate, score);
                bestIndex = i;
            }
        }

        return best.HasValue
            ? JudgmentResult<TContext>.Success(best.Value)
            : JudgmentResult<TContext>.Failure(rejections);
    }

    private static bool IsBetter(
        double score,
        JudgmentCandidate<TContext> candidate,
        int index,
        JudgmentSelection<TContext> currentBest,
        int currentBestIndex)
    {
        const double epsilon = 1e-12;
        if (score > currentBest.Score + epsilon)
        {
            return true;
        }

        if (score < currentBest.Score - epsilon)
        {
            return false;
        }

        if (candidate.TieBreakerPriority != currentBest.Candidate.TieBreakerPriority)
        {
            return candidate.TieBreakerPriority < currentBest.Candidate.TieBreakerPriority;
        }

        var nameOrder = StringComparer.Ordinal.Compare(candidate.Name, currentBest.Candidate.Name);
        if (nameOrder != 0)
        {
            return nameOrder < 0;
        }

        return index < currentBestIndex;
    }
}
