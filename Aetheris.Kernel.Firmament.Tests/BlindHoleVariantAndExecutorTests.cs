using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class BlindHoleVariantAndExecutorTests
{
    [Fact]
    public void BlindHoleVariant_AdmitsSimpleBoxBlindHolePlan()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCanonicalBlindHole()));
        Assert.True(eval.Admissible);
        Assert.Contains("selected-variant:BlindHoleVariant", eval.Evidence);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(HoleKind.Blind, plan.HoleKind);
        Assert.Equal(HoleDepthKind.Blind, plan.DepthKind);
        Assert.Single(plan.ProfileStack);
        Assert.Contains(plan.ExpectedSurfacePatches, p => p.Role == HoleSurfacePatchRole.CylindricalWall);
        Assert.Contains(plan.ExpectedSurfacePatches, p => p.Role == HoleSurfacePatchRole.BlindBottomCap);
    }

    [Fact]
    public void BlindHoleVariant_RejectsThroughHole()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20, 10, 8), new CirCylinderNode(2, 20))));
        Assert.Contains("selected-variant:ThroughHoleVariant", eval.Evidence);
        Assert.DoesNotContain("selected-variant:BlindHoleVariant", eval.Evidence);
    }

    [Fact]
    public void BlindHoleVariant_RejectsMissOrOutside()
    {
        var miss = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirTransformNode(new CirCylinderNode(2, 6), Transform3D.CreateTranslation(new Vector3D(0, 0, 0))));
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(miss));
        Assert.False(eval.Admissible);
        Assert.Contains("UnsupportedMissingEntryFace", string.Join("|", eval.RejectionReasons), StringComparison.Ordinal);
    }

    [Fact]
    public void BlindHoleExecutor_CanonicalBlindHole_ProducesBrepBody()
    {
        var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCanonicalBlindHole())).Plan);
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("Blind cylinder constructed", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Contains("Blind subtract succeeded", StringComparison.Ordinal));
    }

    [Fact]
    public void BlindHoleStepSmoke_CanonicalBlindHole_ExportsStep()
    {
        var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCanonicalBlindHole())).Plan);
        var execution = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, execution.Status);
        var step = Step242Exporter.ExportBody(execution.Body!, new Step242ExportOptions { ProductName = "v8-blind-hole" });
        Assert.True(step.IsSuccess, string.Join(" | ", step.Diagnostics.Select(d => d.Message)));
        Assert.Contains("ISO-10303-21", step.Value, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", step.Value, StringComparison.Ordinal);
        Assert.Contains("ADVANCED_FACE", step.Value, StringComparison.Ordinal);
        Assert.Contains("CYLINDRICAL_SURFACE", step.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", step.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void BlindHole_Rematerializer_ProducesBrepBody()
    {
        var result = FrepSemanticRecoveryRematerializer.TryRecover(BuildCanonicalBlindHole());
        Assert.True(result.Succeeded);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.ExecutionStatus);
        Assert.NotNull(result.Body);
    }

    [Fact]
    public void BlindHole_Unsupported_DoesNotFalseSucceed()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(6, 4));
        var result = FrepSemanticRecoveryRematerializer.TryRecover(root);
        Assert.False(result.Succeeded);
        Assert.Null(result.Body);
    }

    private static CirNode BuildCanonicalBlindHole()
        => new CirSubtractNode(new CirBoxNode(20d, 20d, 10d), new CirTransformNode(new CirCylinderNode(2d, 4d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, 3d))));
}
