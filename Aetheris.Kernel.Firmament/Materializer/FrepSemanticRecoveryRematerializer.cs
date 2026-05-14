using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Materializer;

internal sealed record FrepSemanticRecoveryResult(
    bool Attempted,
    bool Succeeded,
    string? SelectedPolicy,
    ThroughHoleRecoveryPlan? Plan,
    ThroughHoleRecoveryExecutionStatus ExecutionStatus,
    BrepBody? Body,
    IReadOnlyList<string> Diagnostics,
    FrepMaterializerDecision Decision);

internal static class FrepSemanticRecoveryRematerializer
{
    private static readonly ThroughHoleRecoveryPolicy ThroughHolePolicy = new();

    internal static FrepSemanticRecoveryResult TryRecover(
        CirNode root,
        NativeGeometryReplayLog? replayLog = null,
        string? sourceLabel = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        var diagnostics = new List<string> { "semantic recovery attempted", "planner ran" };
        var decision = FrepMaterializerPlanner.Decide(new FrepMaterializerContext(root, replayLog, sourceLabel), [ThroughHolePolicy]);
        diagnostics.AddRange(decision.Diagnostics);

        if (decision.Status != FrepMaterializerDecisionStatus.Selected)
        {
            diagnostics.Add("no admissible policy");
            return new(true, false, null, null, ThroughHoleRecoveryExecutionStatus.Failed, null, diagnostics, decision);
        }

        diagnostics.Add($"selected policy: {decision.SelectedPolicyName}");
        if (!string.Equals(decision.SelectedPolicyName, nameof(ThroughHoleRecoveryPolicy), StringComparison.Ordinal))
        {
            diagnostics.Add("selected policy was not ThroughHoleRecoveryPolicy");
            return new(true, false, decision.SelectedPolicyName, null, ThroughHoleRecoveryExecutionStatus.Failed, null, diagnostics, decision);
        }

        var selectedEval = decision.Evaluations.Single(e => string.Equals(e.PolicyName, decision.SelectedPolicyName, StringComparison.Ordinal));
        if (selectedEval.Plan is not ThroughHoleRecoveryPlan plan)
        {
            diagnostics.Add("selected policy did not provide through-hole plan");
            return new(true, false, decision.SelectedPolicyName, null, ThroughHoleRecoveryExecutionStatus.Failed, null, diagnostics, decision);
        }

        diagnostics.Add("through-hole plan extracted");
        var execution = ThroughHoleRecoveryExecutor.Execute(plan);
        diagnostics.AddRange(execution.Diagnostics);
        if (execution.Status != ThroughHoleRecoveryExecutionStatus.Succeeded || execution.Body is null)
        {
            diagnostics.Add("executor failed");
            return new(true, false, decision.SelectedPolicyName, plan, execution.Status, null, diagnostics, decision);
        }

        diagnostics.Add("executor succeeded");
        diagnostics.Add("brep body recovered");
        return new(true, true, decision.SelectedPolicyName, plan, execution.Status, execution.Body, diagnostics, decision);
    }
}
