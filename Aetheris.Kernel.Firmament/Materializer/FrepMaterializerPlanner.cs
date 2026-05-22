using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Materializer;

public interface IFrepMaterializerPolicy
{
    string Name { get; }

    FrepMaterializerPolicyEvaluation Evaluate(FrepMaterializerContext context);
}

public sealed record FrepMaterializerContext(
    CirNode Root,
    NativeGeometryReplayLog? ReplayLog = null,
    string? SourceLabel = null);

public enum FrepMaterializerCapability
{
    ExactBRep,
    DiagnosticOnly,
    CirOnly,
    Unsupported
}

public sealed record FrepMaterializerPolicyEvaluation(
    string PolicyName,
    bool Admissible,
    double Score,
    FrepMaterializerCapability Capability,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> RejectionReasons,
    IReadOnlyList<string> Diagnostics,
    object? Plan = null)
{
    public static FrepMaterializerPolicyEvaluation Admitted(
        string policyName,
        double score,
        FrepMaterializerCapability capability,
        IReadOnlyList<string>? evidence = null,
        IReadOnlyList<string>? diagnostics = null,
        object? plan = null)
        => new(policyName, true, score, capability, evidence ?? Array.Empty<string>(), Array.Empty<string>(), diagnostics ?? Array.Empty<string>(), plan);

    public static FrepMaterializerPolicyEvaluation Rejected(
        string policyName,
        params string[] rejectionReasons)
        => new(policyName, false, 0d, FrepMaterializerCapability.Unsupported, Array.Empty<string>(), rejectionReasons, Array.Empty<string>());

    public static FrepMaterializerPolicyEvaluation Rejected(
        string policyName,
        IReadOnlyList<string>? evidence = null,
        IReadOnlyList<string>? diagnostics = null,
        params string[] rejectionReasons)
        => new(policyName, false, 0d, FrepMaterializerCapability.Unsupported, evidence ?? Array.Empty<string>(), rejectionReasons, diagnostics ?? Array.Empty<string>());
}

public enum FrepMaterializerDecisionStatus
{
    Selected,
    NoAdmissiblePolicy,
    InvalidInput
}

public sealed record FrepMaterializerDecision(
    FrepMaterializerDecisionStatus Status,
    string? SelectedPolicyName,
    FrepMaterializerCapability Capability,
    IReadOnlyList<FrepMaterializerPolicyEvaluation> Evaluations,
    IReadOnlyList<string> Diagnostics);

public static class FrepMaterializerPlanner
{
    private static readonly JudgmentEngine<FrepMaterializerContext> Engine = new();

    public static FrepMaterializerDecision Decide(
        FrepMaterializerContext context,
        IReadOnlyList<IFrepMaterializerPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policies);

        var diagnostics = new List<string>
        {
            "FrepMaterializerPlanner started.",
            $"Policy count: {policies.Count}."
        };

        if (context.Root is null)
        {
            diagnostics.Add("Invalid input: CIR root is null.");
            return new(FrepMaterializerDecisionStatus.InvalidInput, null, FrepMaterializerCapability.Unsupported, Array.Empty<FrepMaterializerPolicyEvaluation>(), diagnostics);
        }

        var evaluations = policies.Select(p => p.Evaluate(context)).ToArray();
        var candidates = evaluations
            .Select((evaluation, index) => new JudgmentCandidate<FrepMaterializerContext>(
                evaluation.PolicyName,
                _ => evaluation.Admissible,
                _ => evaluation.Score,
                _ => evaluation.RejectionReasons.Count == 0
                    ? "Policy evaluation was non-admissible."
                    : string.Join(" | ", evaluation.RejectionReasons),
                index))
            .ToArray();

        var judgment = Engine.Evaluate(context, candidates);
        var rejectedCount = evaluations.Count(e => !e.Admissible);
        diagnostics.Add($"Rejected policy count: {rejectedCount}.");

        if (!judgment.IsSuccess)
        {
            diagnostics.Add("No admissible policy found.");
            return new(FrepMaterializerDecisionStatus.NoAdmissiblePolicy, null, FrepMaterializerCapability.Unsupported, evaluations, diagnostics);
        }

        var selectedName = judgment.Selection!.Value.Candidate.Name;
        var selected = evaluations.Single(e => string.Equals(e.PolicyName, selectedName, StringComparison.Ordinal));
        diagnostics.Add($"Selected policy: {selectedName}.");

        var tiedPeers = evaluations
            .Where(e => e.Admissible && double.Abs(e.Score - selected.Score) <= 1e-12)
            .Select(e => e.PolicyName)
            .ToArray();
        if (tiedPeers.Length > 1)
        {
            diagnostics.Add($"Deterministic tie-break applied among: {string.Join(", ", tiedPeers)}.");
        }

        return new(FrepMaterializerDecisionStatus.Selected, selected.PolicyName, selected.Capability, evaluations, diagnostics);
    }
}
