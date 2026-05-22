using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CountersinkVariantAndExecutorTests
{
    [Fact]
    public void CountersinkVariant_AdmitsSimpleBoxCountersinkPlan()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCanonical()));
        Assert.Contains("selected-variant:CountersinkVariant", eval.Evidence);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(HoleKind.Countersink, plan.HoleKind);
        Assert.Equal(HoleProfileSegmentKind.Conical, plan.ProfileStack[0].SegmentKind);
        Assert.Equal(HoleProfileSegmentKind.Cylindrical, plan.ProfileStack[1].SegmentKind);
    }

    [Fact]
    public void CountersinkVariant_RejectsNonCoaxialConeCylinder()
    {
        var cone = new CirTransformNode(new CirConeNode(2d, 4d, 4d), Transform3D.CreateTranslation(new Vector3D(3d, 0d, 3d)));
        var root = new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20,20,10), new CirCylinderNode(2,20)), cone);
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));
        Assert.Contains(eval.EvaluationsFor(nameof(CountersinkVariant)), d => d.Contains("not coaxial", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CountersinkExecutor_CanonicalCountersink_ProducesBrepBody()
    {
        var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCanonical())).Plan);
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("cylinder subtract invoked", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("cone subtract invoked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CountersinkStepSmoke_CanonicalCountersink_ExportsStep()
    {
        var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCanonical())).Plan);
        var exec = HoleRecoveryExecutor.Execute(plan);
        var step = Step242Exporter.ExportBody(exec.Body!);
        Assert.True(step.IsSuccess);
        Assert.Contains("ISO-10303-21", step.Value);
        Assert.Contains("MANIFOLD_SOLID_BREP", step.Value);
        Assert.Contains("ADVANCED_FACE", step.Value);
        Assert.Contains("CONICAL_SURFACE", step.Value);
        Assert.Contains("CYLINDRICAL_SURFACE", step.Value);
        Assert.DoesNotContain("BREP_WITH_VOIDS", step.Value);
    }

    private static CirNode BuildCanonical()
    {
        var cone = new CirTransformNode(new CirConeNode(2d, 4d, 4d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, 3d)));
        return new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20d, 20d, 10d), new CirCylinderNode(2d, 20d)), cone);
    }
}
