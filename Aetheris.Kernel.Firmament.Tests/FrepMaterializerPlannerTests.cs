using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FrepMaterializerPlannerTests
{
    [Fact]
    public void Planner_SelectsHighestScoringAdmissiblePolicy()
    {
        var decision = FrepMaterializerPlanner.Decide(BuildContext(), [
            new FakePolicy("low", FrepMaterializerPolicyEvaluation.Admitted("low", 1d, FrepMaterializerCapability.CirOnly)),
            new FakePolicy("high", FrepMaterializerPolicyEvaluation.Admitted("high", 5d, FrepMaterializerCapability.ExactBRep))
        ]);

        Assert.Equal(FrepMaterializerDecisionStatus.Selected, decision.Status);
        Assert.Equal("high", decision.SelectedPolicyName);
    }

    [Fact]
    public void Planner_IgnoresRejectedPoliciesButKeepsEvaluations()
    {
        var rejection = FrepMaterializerPolicyEvaluation.Rejected("reject", "not canonical");
        var decision = FrepMaterializerPlanner.Decide(BuildContext(), [
            new FakePolicy("reject", rejection),
            new FakePolicy("winner", FrepMaterializerPolicyEvaluation.Admitted("winner", 2d, FrepMaterializerCapability.DiagnosticOnly))
        ]);

        Assert.Equal("winner", decision.SelectedPolicyName);
        Assert.Contains(decision.Evaluations, e => e.PolicyName == "reject" && !e.Admissible && e.RejectionReasons.Contains("not canonical"));
    }

    [Fact]
    public void Planner_NoAdmissiblePolicy_ReturnsNoAdmissibleDecision()
    {
        var decision = FrepMaterializerPlanner.Decide(BuildContext(), [
            new FakePolicy("reject1", FrepMaterializerPolicyEvaluation.Rejected("reject1", "x")),
            new FakePolicy("reject2", FrepMaterializerPolicyEvaluation.Rejected("reject2", "y"))
        ]);

        Assert.Equal(FrepMaterializerDecisionStatus.NoAdmissiblePolicy, decision.Status);
        Assert.Contains("No admissible policy found.", decision.Diagnostics);
    }

    [Fact]
    public void Planner_TieBreakIsDeterministic()
    {
        var decision = FrepMaterializerPlanner.Decide(BuildContext(), [
            new FakePolicy("beta", FrepMaterializerPolicyEvaluation.Admitted("beta", 3d, FrepMaterializerCapability.CirOnly)),
            new FakePolicy("alpha", FrepMaterializerPolicyEvaluation.Admitted("alpha", 3d, FrepMaterializerCapability.ExactBRep))
        ]);

        Assert.Equal("beta", decision.SelectedPolicyName);
        Assert.Contains(decision.Diagnostics, d => d.Contains("Deterministic tie-break applied", StringComparison.Ordinal));
    }

    [Fact]
    public void Context_IsNodeFirstAndReplayOptional()
    {
        var root = new CirBoxNode(1, 2, 3);
        var rootOnly = new FrepMaterializerContext(root);
        var replay = new NativeGeometryReplayLog([]);
        var withReplay = new FrepMaterializerContext(root, replay, "fixture");

        Assert.Same(root, rootOnly.Root);
        Assert.Null(rootOnly.ReplayLog);
        Assert.Same(replay, withReplay.ReplayLog);
        Assert.Equal("fixture", withReplay.SourceLabel);
    }

    [Fact]
    public void Decision_PreservesAllEvaluationsAndDiagnostics()
    {
        var decision = FrepMaterializerPlanner.Decide(BuildContext(), [
            new FakePolicy("r", FrepMaterializerPolicyEvaluation.Rejected("r", "reason")),
            new FakePolicy("w", FrepMaterializerPolicyEvaluation.Admitted("w", 1d, FrepMaterializerCapability.DiagnosticOnly, diagnostics: ["diag"]))
        ]);

        Assert.Equal(2, decision.Evaluations.Count);
        Assert.Contains("FrepMaterializerPlanner started.", decision.Diagnostics);
        Assert.Contains("Policy count: 2.", decision.Diagnostics);
        Assert.Contains("Rejected policy count: 1.", decision.Diagnostics);
        Assert.Contains("Selected policy: w.", decision.Diagnostics);
    }

    private static FrepMaterializerContext BuildContext() => new(new CirBoxNode(2, 2, 2));

    private sealed class FakePolicy(string name, FrepMaterializerPolicyEvaluation evaluation) : IFrepMaterializerPolicy
    {
        public string Name => name;

        public FrepMaterializerPolicyEvaluation Evaluate(FrepMaterializerContext context) => evaluation;
    }
}
