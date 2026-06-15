using Aetheris.Kernel.Core.Air;

namespace Aetheris.Kernel.Core.Tests.Air;

public sealed class AirRouteSelectorTests
{
    [Fact]
    public void AirRouteSelector_PrismaticSectionTransition_UsesDirectSelection()
    {
        var decision = AirRouteSelector.Decide(AirRouteSelector.ForPrismaticSectionTransition());

        Assert.True(decision.Succeeded);
        Assert.Equal(AirRouteSelectionMode.Direct, decision.SelectionMode);
        Assert.Equal(AirRouteKind.PrismaticSectionTransitionEmitter, decision.SelectedRouteKind);
        Assert.Single(decision.Candidates, c => c.Status == AirRouteCandidateStatus.Admitted);
        Assert.DoesNotContain(decision.Diagnostics, d => d.Code.Contains("judgment-utility", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(decision.Diagnostics, d => d.Code == "air-x2-no-production-route-replacement");
    }

    [Fact]
    public void AirRouteSelector_ProfileExtrude_UsesDirectSelection()
    {
        var decision = AirRouteSelector.Decide(AirRouteSelector.ForProfileExtrude());

        Assert.True(decision.Succeeded);
        Assert.Equal(AirRouteSelectionMode.Direct, decision.SelectionMode);
        Assert.Equal(AirRouteKind.ProfileExtrudeEmitter, decision.SelectedRouteKind);
        Assert.Single(decision.Candidates, c => c.Status == AirRouteCandidateStatus.Admitted);
        Assert.DoesNotContain(decision.Diagnostics, d => d.Code.Contains("judgment-utility", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(decision.Diagnostics, d => d.Code == "air-x2-no-production-route-replacement");
    }

    [Fact]
    public void AirRouteSelector_TopFaceLoopChamfer_UsesSwitchMatchSelection()
    {
        var decision = AirRouteSelector.Decide(AirRouteSelector.ForEdgeFinish("top-face-loop-chamfer", AirSelectionClass.FaceBoundaryLoop, AirRuleKind.UniformChamfer, "history-known top-face loop"));

        Assert.True(decision.Succeeded);
        Assert.Equal(AirRouteSelectionMode.SwitchMatch, decision.SelectionMode);
        Assert.Equal(AirRouteKind.TopFaceLoopChamferPrismatic, decision.SelectedRouteKind);
        Assert.Equal(AirSelectionClass.FaceBoundaryLoop, decision.Summary.SelectionClass);
        Assert.Equal(AirRuleKind.UniformChamfer, decision.Summary.RuleKind);
        Assert.Contains(decision.Diagnostics, d => d.Code == "air-x2-switch-match-selection");
        Assert.Contains(decision.Guarantees, g => g == "Class B face-boundary loop");
        Assert.Contains(decision.Guarantees, g => g == "not four independent single-edge chamfers");
        Assert.DoesNotContain(decision.Guarantees, g => g.Contains("AirEdgeSweep", StringComparison.OrdinalIgnoreCase) || g.Contains("BrepBoundedChamfer", StringComparison.OrdinalIgnoreCase) || g.Contains("Boolean", StringComparison.OrdinalIgnoreCase) || g.Contains("graft", StringComparison.OrdinalIgnoreCase) || g.Contains("merge", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AirRouteSelector_ArbitraryGraph_IsRejected()
    {
        var decision = AirRouteSelector.Decide(AirRouteSelector.ForEdgeFinish("arbitrary-graph-chamfer", AirSelectionClass.ArbitraryGraph, AirRuleKind.UniformChamfer, "history-known arbitrary graph"));

        Assert.False(decision.Succeeded);
        Assert.Null(decision.SelectedRouteKind);
        var candidate = Assert.Single(decision.Candidates);
        Assert.Equal(AirRouteCandidateStatus.Rejected, candidate.Status);
        Assert.Equal(AirRouteRejectionReason.ArbitraryGraphUnsupported, candidate.ReasonCode);
        Assert.Contains(decision.Diagnostics, d => d.Code == "air-x2-arbitrary-graph-rejected");
    }

    [Fact]
    public void AirRouteSelector_LoopFillet_IsDeferred()
    {
        var decision = AirRouteSelector.Decide(AirRouteSelector.ForEdgeFinish("loop-fillet", AirSelectionClass.FaceBoundaryLoop, AirRuleKind.ConstantRadiusFillet, "history-known face loop"));

        Assert.False(decision.Succeeded);
        Assert.Equal(AirSelectionClass.FaceBoundaryLoop, decision.Summary.SelectionClass);
        Assert.Equal(AirRuleKind.ConstantRadiusFillet, decision.Summary.RuleKind);
        var candidate = Assert.Single(decision.Candidates);
        Assert.Equal(AirRouteCandidateStatus.Deferred, candidate.Status);
        Assert.Equal(AirRouteRejectionReason.LoopFilletDeferredUntilSingleEdgeEvidence, candidate.ReasonCode);
        Assert.Contains("single-edge", candidate.Reason);
        Assert.Null(decision.SelectedRouteKind);
    }

    [Fact]
    public void AirRouteSelector_NonUniformRule_IsRejectedOrDeferred()
    {
        var decision = AirRouteSelector.Decide(AirRouteSelector.ForEdgeFinish("non-uniform-loop-rule", AirSelectionClass.FaceBoundaryLoop, AirRuleKind.Unsupported, "history-known top-face loop"));

        Assert.False(decision.Succeeded);
        Assert.Null(decision.SelectedRouteKind);
        var candidate = Assert.Single(decision.Candidates);
        Assert.Contains(candidate.Status, new[] { AirRouteCandidateStatus.Rejected, AirRouteCandidateStatus.Deferred });
        Assert.Equal(AirRouteRejectionReason.NonUniformRuleUnsupported, candidate.ReasonCode);
        Assert.Contains(decision.Diagnostics, d => d.Code == "air-x2-non-uniform-rule-rejected");
    }

    [Fact]
    public void AirRouteSelection_IsDeterministic()
    {
        AssertStable(AirRouteSelector.Decide(AirRouteSelector.ForPrismaticSectionTransition()), AirRouteSelector.Decide(AirRouteSelector.ForPrismaticSectionTransition()));
        AssertStable(AirRouteSelector.Decide(AirRouteSelector.ForEdgeFinish("top-face-loop-chamfer", AirSelectionClass.FaceBoundaryLoop, AirRuleKind.UniformChamfer, "history-known top-face loop")), AirRouteSelector.Decide(AirRouteSelector.ForEdgeFinish("top-face-loop-chamfer", AirSelectionClass.FaceBoundaryLoop, AirRuleKind.UniformChamfer, "history-known top-face loop")));
    }

    [Fact]
    public void AirRouteSelector_JudgmentUtility_IsRepresentedButDeferred()
    {
        var direct = AirRouteSelector.Decide(AirRouteSelector.ForProfileExtrude());
        var contested = AirRouteSelector.Decide(AirRouteSelector.ForJudgmentUtilityProbe());

        Assert.NotEqual(AirRouteSelectionMode.JudgmentUtility, direct.SelectionMode);
        Assert.Equal(AirRouteSelectionMode.JudgmentUtility, contested.SelectionMode);
        Assert.False(contested.Succeeded);
        Assert.Contains(contested.Diagnostics, d => d.Code == "air-x2-judgment-utility-deferred");
    }

    [Fact]
    public void AirRouteDecision_TopFaceLoopChamfer_CanInvokeExistingAirWrapper()
    {
        var decision = AirRouteSelector.Decide(AirRouteSelector.ForEdgeFinish("top-face-loop-chamfer", AirSelectionClass.FaceBoundaryLoop, AirRuleKind.UniformChamfer, "history-known top-face loop"));
        var wrapper = AirTopFaceLoopChamferWrapper.LowerCanonicalTopFaceLoopChamfer();

        Assert.True(decision.Succeeded);
        Assert.Equal(AirRouteKind.TopFaceLoopChamferPrismatic, decision.SelectedRouteKind);
        Assert.True(wrapper.Succeeded);
        Assert.Equal(decision.SelectedRouteKind, wrapper.RouteKind);
        Assert.Equal("AIR-X2", decision.Provenance.Milestone);
        Assert.Contains(decision.Diagnostics, d => d.Code == "air-x2-no-production-route-replacement");
    }

    private static void AssertStable(AirRouteDecision a, AirRouteDecision b)
    {
        Assert.Equal(a.SelectedRouteKind, b.SelectedRouteKind);
        Assert.Equal(a.SelectionMode, b.SelectionMode);
        Assert.Equal(a.Candidates.Select(c => c.Status), b.Candidates.Select(c => c.Status));
        Assert.Equal(a.Candidates.Select(c => c.ReasonCode), b.Candidates.Select(c => c.ReasonCode));
        Assert.Equal(a.Diagnostics.Select(d => d.Code), b.Diagnostics.Select(d => d.Code));
        Assert.Equal(a.Guarantees, b.Guarantees);
    }
}
