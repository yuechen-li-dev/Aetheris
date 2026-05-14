using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Materializer;

internal sealed record FrepSemanticRecoveryResult(
    bool Attempted,
    bool Succeeded,
    string? SelectedPolicy,
    HoleRecoveryPlan? Plan,
    ThroughHoleRecoveryExecutionStatus ExecutionStatus,
    BrepBody? Body,
    IReadOnlyList<string> Diagnostics,
    FrepMaterializerDecision Decision);

internal static class FrepSemanticRecoveryRematerializer
{

    internal static FrepSemanticRecoveryResult TryRecover(
        CirNode root,
        NativeGeometryReplayLog? replayLog = null,
        string? sourceLabel = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        var diagnostics = new List<string> { "semantic recovery attempted", "planner ran" };
        var catalogSnapshot = FrepMaterializerPolicyCatalog.SnapshotDefault();
        diagnostics.AddRange(catalogSnapshot.Diagnostics);

        var decision = FrepMaterializerPlanner.Decide(new FrepMaterializerContext(root, replayLog, sourceLabel), FrepMaterializerPolicyCatalog.Default());
        diagnostics.AddRange(decision.Diagnostics);

        if (decision.Status != FrepMaterializerDecisionStatus.Selected)
        {
            diagnostics.Add("no admissible policy");
            return new(true, false, null, null, ThroughHoleRecoveryExecutionStatus.Failed, null, diagnostics, decision);
        }

        diagnostics.Add($"selected policy: {decision.SelectedPolicyName}");
        if (!string.Equals(decision.SelectedPolicyName, nameof(HoleRecoveryPolicy), StringComparison.Ordinal))
        {
            diagnostics.Add("selected policy was not HoleRecoveryPolicy");
            return new(true, false, decision.SelectedPolicyName, null, ThroughHoleRecoveryExecutionStatus.Failed, null, diagnostics, decision);
        }

        var selectedEval = decision.Evaluations.Single(e => string.Equals(e.PolicyName, decision.SelectedPolicyName, StringComparison.Ordinal));
        if (selectedEval.Plan is not HoleRecoveryPlan plan)
        {
            diagnostics.Add("selected policy did not provide hole-recovery plan");
            diagnostics.Add("selected policy is non-executable for exact BRep recovery");
            return new(true, false, decision.SelectedPolicyName, null, ThroughHoleRecoveryExecutionStatus.Failed, null, diagnostics, decision);
        }

        diagnostics.Add("hole-recovery plan extracted");
        if (!ThroughHoleRecoveryPlanAdapter.TryConvert(plan, out var throughPlan) || throughPlan is null)
        {
            diagnostics.Add("hole plan conversion to through-hole executor contract failed");
            diagnostics.Add("selected hole variant is currently non-executable");
            return new(true, false, decision.SelectedPolicyName, plan, ThroughHoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics, decision);
        }

        diagnostics.Add("hole plan converted to through-hole executor contract");
        var execution = ThroughHoleRecoveryExecutor.Execute(throughPlan);
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
