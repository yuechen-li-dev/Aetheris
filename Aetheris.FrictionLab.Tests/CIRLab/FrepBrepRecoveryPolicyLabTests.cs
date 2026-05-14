using Aetheris.FrictionLab;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class FrepBrepRecoveryPolicyLabTests
{
    [Fact]
    public void RecoveryPolicy_BoxCylinder_SelectsSemanticPolicy()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 8, 6), new CirCylinderNode(2, 8));
        var result = FrepBrepRecoveryPolicyLab.SelectBestPolicy(new(root));
        Assert.Equal("BoxCylinderThroughHolePolicy", result.SelectedPolicy);
    }

    [Fact]
    public void RecoveryPolicy_TranslatedBoxCylinder_SelectsSemanticPolicy()
    {
        var root = new CirSubtractNode(
            new CirTransformNode(new CirBoxNode(10, 8, 6), Transform3D.CreateTranslation(new(2, 1, 5))),
            new CirTransformNode(new CirCylinderNode(2, 8), Transform3D.CreateTranslation(new(2.5, 1.5, 5))));
        var result = FrepBrepRecoveryPolicyLab.SelectBestPolicy(new(root));
        Assert.Equal("BoxCylinderThroughHolePolicy", result.SelectedPolicy);
    }

    [Fact]
    public void RecoveryPolicy_BoxSphere_DoesNotSelectBoxCylinderPolicy()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 8, 6), new CirSphereNode(2));
        var result = FrepBrepRecoveryPolicyLab.SelectBestPolicy(new(root));
        Assert.NotEqual("BoxCylinderThroughHolePolicy", result.SelectedPolicy);
    }

    [Fact]
    public void RecoveryPolicy_BlindCylinder_RejectsSemanticPolicy()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 8, 6), new CirCylinderNode(2, 4));
        var result = FrepBrepRecoveryPolicyLab.SelectBestPolicy(new(root));
        Assert.Contains(result.Candidates, c => c.PolicyName == "BoxCylinderThroughHolePolicy" && !c.Admissible && c.RejectionReasons.Any(r => r.Contains("recognizer_failed")));
    }

    [Fact]
    public void RecoveryPolicy_TangentCylinder_RejectsSemanticPolicy()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 8, 6), new CirTransformNode(new CirCylinderNode(2, 8), Transform3D.CreateTranslation(new(3,0,0))));
        var result = FrepBrepRecoveryPolicyLab.SelectBestPolicy(new(root));
        Assert.Contains(result.Candidates, c => c.PolicyName == "BoxCylinderThroughHolePolicy" && !c.Admissible);
    }

    [Fact]
    public void RecoveryPolicy_UnsupportedTransform_RejectsSemanticPolicy()
    {
        var root = new CirSubtractNode(new CirTransformNode(new CirBoxNode(10, 8, 6), Transform3D.CreateRotationX(Math.PI/4)), new CirCylinderNode(2, 8));
        var result = FrepBrepRecoveryPolicyLab.SelectBestPolicy(new(root));
        Assert.Contains(result.Candidates, c => c.PolicyName == "BoxCylinderThroughHolePolicy" && !c.Admissible);
    }

    [Fact]
    public void RecoveryPolicy_DiagnosticsIncludeAllCandidates()
    {
        var result = FrepBrepRecoveryPolicyLab.SelectBestPolicy(new(new CirBoxNode(1,1,1)));
        Assert.Contains("candidate:BoxCylinderThroughHolePolicy", string.Join(";", result.Diagnostics));
        Assert.Contains("candidate:GenericNumericalContourPolicy", string.Join(";", result.Diagnostics));
        Assert.Contains("candidate:CirOnlyFallbackPolicy", string.Join(";", result.Diagnostics));
    }

    [Fact]
    public void RecoveryPolicy_TieBreakIsDeterministic()
    {
        var policies = new IFrepBrepRecoveryPolicy[] { new SameScore("z"), new SameScore("a") };
        var result = FrepBrepRecoveryPolicyLab.SelectBestPolicy(new(new CirBoxNode(1,1,1)), policies);
        Assert.Equal("a", result.SelectedPolicy);
    }

    [Fact]
    public void RecoveryPolicy_ReportEvidenceShape()
    {
        var result = FrepBrepRecoveryPolicyLab.SelectBestPolicy(new(new CirSubtractNode(new CirBoxNode(10,8,6), new CirCylinderNode(2,8))));
        var semantic = result.Candidates.Single(c => c.PolicyName == "BoxCylinderThroughHolePolicy");
        Assert.NotEmpty(semantic.Evidence);
        Assert.False(string.IsNullOrWhiteSpace(semantic.RecommendedRoute));
        Assert.NotNull(result.SelectedRoute);
    }

    private sealed class SameScore(string name) : IFrepBrepRecoveryPolicy
    {
        public string Name => name;
        public int TieBreakerPriority => 0;
        public FrepBrepRecoveryPolicyEvaluation Evaluate(FrepBrepRecoveryContext context) => new(Name, true, 10, FrepBrepRecoveryCapability.IntentOnlyFallback, "r", [], [], []);
    }
}
