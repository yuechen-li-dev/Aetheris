using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CounterboreVariantTests
{
    [Fact]
    public void CounterboreVariant_AdmitsSimpleBoxCounterborePlan()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCanonicalCounterbore()));
        Assert.True(eval.Admissible);
        Assert.Contains("selected-variant:CounterboreVariant", eval.Evidence);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(HoleKind.Counterbore, plan.HoleKind);
        Assert.Equal(HoleDepthKind.ThroughWithEntryRelief, plan.DepthKind);
        Assert.Equal(2, plan.ProfileStack.Count);
        Assert.True(plan.ProfileStack[0].RadiusStart > plan.ProfileStack[1].RadiusStart);
        Assert.Contains(plan.ExpectedSurfacePatches, p => p.Role == HoleSurfacePatchRole.CounterboreFloorAnnulus);
        Assert.Contains(plan.ExpectedSurfacePatches, p => p.Role == HoleSurfacePatchRole.CounterboreWall);
    }

    [Fact]
    public void CounterboreVariant_RejectsSimpleThroughHole()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20, 10, 8), new CirCylinderNode(2, 20))));
        Assert.True(eval.Admissible);
        Assert.Contains("selected-variant:ThroughHoleVariant", eval.Evidence);
    }

    [Fact]
    public void CounterboreVariant_RejectsNonCoaxialCylinders()
    {
        var root = new CirSubtractNode(
            new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)),
            new CirTransformNode(new CirCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(1, 0, -3))));
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));
        Assert.False(eval.Evidence.Contains("selected-variant:CounterboreVariant"));
        Assert.Contains("coaxial", string.Join("|", eval.EvaluationsFor(nameof(CounterboreVariant))).ToLowerInvariant());
    }

    [Fact]
    public void Rematerializer_CounterborePlan_DoesNotFalseSucceedWithoutExecutor()
    {
        var result = FrepSemanticRecoveryRematerializer.TryRecover(BuildCanonicalCounterbore());
        Assert.False(result.Succeeded);
        Assert.Null(result.Body);
        Assert.Equal(ThroughHoleRecoveryExecutionStatus.UnsupportedPlan, result.ExecutionStatus);
        Assert.Contains(result.Diagnostics, d => d.Contains("selected policy: HoleRecoveryPolicy", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Contains("non-executable", StringComparison.Ordinal));
    }


    [Fact]
    public void CounterboreVariant_RejectsLargeRadiusNotGreaterThanSmall()
    {
        var root = new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(3, 20)), new CirTransformNode(new CirCylinderNode(3, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))));
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));
        Assert.Contains("radius", string.Join("|", eval.EvaluationsFor(nameof(CounterboreVariant))).ToLowerInvariant());
    }

    [Fact]
    public void CounterboreVariant_RejectsLargeCylinderThroughFullDepth()
    {
        var root = new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)), new CirCylinderNode(4, 20));
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));
        Assert.Contains("not a counterbore relief", string.Join("|", eval.EvaluationsFor(nameof(CounterboreVariant))).ToLowerInvariant());
    }

    [Fact]
    public void CounterboreVariant_RejectsSmallCylinderNotThrough()
    {
        var root = new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 6)), new CirTransformNode(new CirCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))));
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));
        Assert.DoesNotContain("selected-variant:CounterboreVariant", eval.Evidence);
    }

    private static CirNode BuildCanonicalCounterbore()
        => new CirSubtractNode(
            new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)),
            new CirTransformNode(new CirCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))));
}

internal static class HoleRecoveryPolicyEvalExtensions
{
    public static IReadOnlyList<string> EvaluationsFor(this FrepMaterializerPolicyEvaluation eval, string variantName)
        => eval.Diagnostics.Where(d => d.StartsWith($"variant:{variantName}:", StringComparison.Ordinal)).ToArray();
}
