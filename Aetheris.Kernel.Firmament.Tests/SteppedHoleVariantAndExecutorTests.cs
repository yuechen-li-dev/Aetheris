using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SteppedHoleVariantAndExecutorTests
{
    [Fact]
    public void SteppedHolePlan_StillAdmitted()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped()));
        Assert.True(eval.Admissible);
        Assert.Contains("selected-variant:SteppedHoleVariant", eval.Evidence);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(HoleKind.Stepped, plan.HoleKind);
        Assert.Equal(3, plan.ProfileStack.Count);
        Assert.Equal(HoleEntryFeatureKind.Stepped, plan.EntryFeature);
        Assert.Contains(plan.ExpectedSurfacePatches, p => p.Role == HoleSurfacePatchRole.SteppedTransitionFloorAnnulus);
    }

    [Fact]
    public void SteppedHoleExecutor_CanonicalSteppedHole_ProducesBrepBody()
    {
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped())).Plan!;
        var exec = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, exec.Status);
        Assert.NotNull(exec.Body);
        Assert.Contains(exec.Diagnostics, d => d.Contains("Stepped plan shape validated", StringComparison.Ordinal));
        Assert.Contains(exec.Diagnostics, d => d.Contains("Stepped repeated-subtract route selected", StringComparison.Ordinal));
        Assert.Contains(exec.Diagnostics, d => d.Contains("Stepped subtract small invoked", StringComparison.Ordinal));
        Assert.Contains(exec.Diagnostics, d => d.Contains("Stepped subtract small succeeded", StringComparison.Ordinal));
        Assert.Contains(exec.Diagnostics, d => d.Contains("Stepped subtract medium succeeded", StringComparison.Ordinal));
        Assert.Contains(exec.Diagnostics, d => d.Contains("Stepped subtract large succeeded", StringComparison.Ordinal));
        Assert.Contains(exec.Diagnostics, d => d.Contains("Result BRep body produced", StringComparison.Ordinal));
    }

    [Fact]
    public void SteppedHoleStepSmoke_CanonicalSteppedHole_ExportsStep()
    {
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped())).Plan!;
        var exec = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, exec.Status);
        var step = Step242Exporter.ExportBody(exec.Body!);
        Assert.True(step.IsSuccess);
        Assert.Contains("ISO-10303-21", step.Value, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", step.Value, StringComparison.Ordinal);
        Assert.Contains("ADVANCED_FACE", step.Value, StringComparison.Ordinal);
        Assert.Contains("CYLINDRICAL_SURFACE", step.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", step.Value, StringComparison.Ordinal);
        Assert.Contains(exec.Diagnostics, d => d.Contains("No STEP export attempted", StringComparison.Ordinal));
    }

    [Fact]
    public void SteppedHole_InvalidPlanShape_RejectsBeforeBoolean()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped()));
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        var invalid = plan with { ProfileStack = [plan.ProfileStack[0], plan.ProfileStack[2]] };
        var exec = HoleRecoveryExecutor.Execute(invalid);
        Assert.Equal(HoleRecoveryExecutionStatus.UnsupportedPlan, exec.Status);
        Assert.Null(exec.Body);
        Assert.Contains(exec.Diagnostics, d => d.Contains("Stepped plan rejected before Boolean", StringComparison.Ordinal));
        Assert.DoesNotContain(exec.Diagnostics, d => d.Contains("Stepped subtract", StringComparison.Ordinal));
    }

    [Fact] public void SteppedHoleVariant_DoesNotStealCounterbore() => Assert.Contains("selected-variant:CounterboreVariant", new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCounterbore())).Evidence);
    [Fact] public void SteppedHoleVariant_DoesNotStealThroughOrBlind() { Assert.Contains("selected-variant:ThroughHoleVariant", new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20,20,10), new CirCylinderNode(2,20)))).Evidence); Assert.Contains("selected-variant:BlindHoleVariant", new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20,20,10), new CirTransformNode(new CirCylinderNode(2,4), Transform3D.CreateTranslation(new Vector3D(0,0,3)))))).Evidence); }
    [Fact] public void SteppedHoleVariant_RejectsNonCoaxialCylinders() => Assert.False(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped(new Vector3D(1,0,-2)))).Admissible);
    [Fact] public void SteppedHoleVariant_RejectsInvalidRadiusOrdering() => Assert.False(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped(mediumRadius:5))).Admissible);
    [Fact] public void SteppedHoleVariant_RejectsInvalidDepthOrdering() => Assert.False(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped(mediumHeight:2))).Admissible);

    private static CirNode BuildCounterbore() => new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)), new CirTransformNode(new CirCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))));
    private static CirNode BuildStepped(Vector3D? mediumTranslation = null, double mediumRadius = 3, double mediumHeight = 6)
    {
        var host = new CirBoxNode(20,20,10);
        var small = new CirCylinderNode(2,20);
        var medium = new CirTransformNode(new CirCylinderNode(mediumRadius, mediumHeight), Transform3D.CreateTranslation(mediumTranslation ?? new Vector3D(0,0,-2)));
        var large = new CirTransformNode(new CirCylinderNode(4,4), Transform3D.CreateTranslation(new Vector3D(0,0,-3)));
        return new CirSubtractNode(new CirSubtractNode(new CirSubtractNode(host, small), medium), large);
    }
}
