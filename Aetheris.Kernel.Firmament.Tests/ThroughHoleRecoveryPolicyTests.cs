using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ThroughHoleRecoveryPolicyTests
{
    private readonly ThroughHoleRecoveryPolicy _policy = new();

    [Fact]
    public void ThroughHolePolicy_DirectBoxCylinder_AdmitsAndCreatesPlan()
    {
        var eval = _policy.Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20, 10, 8), new CirCylinderNode(2, 20))));
        Assert.True(eval.Admissible);
        Assert.Equal(FrepMaterializerCapability.ExactBRep, eval.Capability);
        var plan = Assert.IsType<ThroughHoleRecoveryPlan>(eval.Plan);
        Assert.Equal(ThroughHoleHostKind.RectangularBox, plan.HostKind);
        Assert.Equal(ThroughHoleToolKind.Cylindrical, plan.ToolKind);
        Assert.Equal(ThroughHoleProfileKind.Circular, plan.ProfileKind);
        Assert.Equal(ThroughHoleAxisKind.Z, plan.Axis);
        Assert.Equal(20d, plan.ThroughLength, 6);
    }

    [Fact]
    public void ThroughHolePolicy_TranslatedBoxCylinder_Admits()
    {
        var root = new CirSubtractNode(
            new CirTransformNode(new CirBoxNode(20, 20, 10), Transform3D.CreateTranslation(new Vector3D(5, 2, 4))),
            new CirTransformNode(new CirCylinderNode(3, 16), Transform3D.CreateTranslation(new Vector3D(4, 1, 4))));
        var eval = _policy.Evaluate(new FrepMaterializerContext(root));
        Assert.True(eval.Admissible);
        Assert.Contains("translation-wrapper-supported", eval.Evidence);
    }

    [Fact]
    public void Planner_SelectsThroughHolePolicy_OverFallback()
    {
        var context = new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20, 10, 8), new CirCylinderNode(2, 20)));
        var decision = FrepMaterializerPlanner.Decide(context, [
            _policy,
            new FakePolicy("fallback", FrepMaterializerPolicyEvaluation.Admitted("fallback", 1d, FrepMaterializerCapability.CirOnly))
        ]);
        Assert.Equal(nameof(ThroughHoleRecoveryPolicy), decision.SelectedPolicyName);
    }

    [Fact]
    public void ThroughHolePolicy_RejectsBoxSphere()
    {
        var eval = _policy.Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(2))));
        Assert.False(eval.Admissible);
        Assert.Contains(eval.RejectionReasons, r => r.Contains("UnsupportedRightNotCylinder", StringComparison.Ordinal));
    }

    [Fact]
    public void ThroughHolePolicy_RejectsBlindCylinder()
    {
        var eval = _policy.Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8))));
        Assert.False(eval.Admissible);
        Assert.Contains(eval.RejectionReasons, r => r.Contains("UnsupportedNotThroughHole", StringComparison.Ordinal));
    }

    [Fact]
    public void ThroughHolePolicy_RejectsTangentOrGrazing()
    {
        var eval = _policy.Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirTransformNode(new CirCylinderNode(2, 20), Transform3D.CreateTranslation(new Vector3D(3, 0, 0))))));
        Assert.False(eval.Admissible);
        Assert.Contains(eval.RejectionReasons, r => r.Contains("UnsupportedTangentOrGrazing", StringComparison.Ordinal));
    }

    [Fact]
    public void ThroughHolePolicy_RejectsUnsupportedTransform()
    {
        var eval = _policy.Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirTransformNode(new CirCylinderNode(2, 20), Transform3D.CreateRotationX(0.2)))));
        Assert.False(eval.Admissible);
        Assert.Contains(eval.RejectionReasons, r => r.Contains("UnsupportedTransform", StringComparison.Ordinal));
    }

    [Fact]
    public void ThroughHolePolicy_DecisionTraceContainsPlanEvidence()
    {
        var decision = FrepMaterializerPlanner.Decide(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20, 10, 8), new CirCylinderNode(2, 20))), [_policy]);
        var eval = Assert.Single(decision.Evaluations);
        Assert.Contains("semantic-through-hole", eval.Evidence);
        Assert.Contains(eval.Evidence, e => e.Contains("expected-patches", StringComparison.Ordinal));
        Assert.Contains(eval.Evidence, e => e.Contains("expected-trims", StringComparison.Ordinal));
    }

    private sealed class FakePolicy(string name, FrepMaterializerPolicyEvaluation evaluation) : IFrepMaterializerPolicy
    {
        public string Name => name;

        public FrepMaterializerPolicyEvaluation Evaluate(FrepMaterializerContext context) => evaluation;
    }
}
