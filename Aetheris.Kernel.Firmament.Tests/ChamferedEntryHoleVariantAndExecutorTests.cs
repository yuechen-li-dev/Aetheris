using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ChamferedEntryHoleVariantAndExecutorTests
{
    [Fact]
    public void ChamferedEntryVariant_AdmitsSimpleBoxChamferedThroughHolePlan()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildChamfered()));
        Assert.Contains("selected-variant:ChamferedEntryHoleVariant", eval.Evidence);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(HoleKind.ChamferedEntry, plan.HoleKind);
        Assert.Equal(HoleProfileSegmentKind.Conical, plan.ProfileStack[0].SegmentKind);
        Assert.Equal(HoleProfileSegmentKind.Cylindrical, plan.ProfileStack[1].SegmentKind);
        Assert.Contains(plan.ExpectedSurfacePatches, p => p.Role == HoleSurfacePatchRole.ChamferedEntryWall);
    }

    [Fact]
    public void ChamferedEntryVariant_RejectsCountersinkSizedCone()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCountersink()));
        Assert.Contains("selected-variant:CountersinkVariant", eval.Evidence);
        Assert.Contains(eval.Diagnostics, d => d.Contains("Variant rejected: ChamferedEntryHoleVariant", StringComparison.Ordinal));
    }

    [Fact]
    public void CountersinkVariant_RejectsChamferSizedCone()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildChamfered()));
        Assert.Contains("selected-variant:ChamferedEntryHoleVariant", eval.Evidence);
        Assert.Contains(eval.Diagnostics, d => d.Contains("Variant rejected: CountersinkVariant", StringComparison.Ordinal));
    }

    [Fact]
    public void ChamferedEntryVariant_RejectsNonCoaxialConeCylinder()
    {
        var cone = new CirTransformNode(new CirConeNode(2d, 2.8d, 1d), Transform3D.CreateTranslation(new Vector3D(1d, 0d, 4.5d)));
        var root = new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20d,20d,10d), new CirCylinderNode(2d,20d)), cone);
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));
        Assert.Contains(eval.EvaluationsFor(nameof(ChamferedEntryHoleVariant)), d => d.Contains("not coaxial", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ChamferedEntryExecutor_CanonicalChamferedEntry_ProducesBrepBody()
    {
        var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildChamfered())).Plan);
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("cylinder subtract invoked", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("chamfer cone subtract invoked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ChamferedEntryStepSmoke_CanonicalChamferedEntry_ExportsStep()
    {
        var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildChamfered())).Plan);
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

    [Fact]
    public void ChamferedEntry_Unsupported_DoesNotFalseSucceed()
    {
        var cone = new CirTransformNode(new CirConeNode(2d, 2.8d, 1d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, 1.5d)));
        var root = new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20d,20d,10d), new CirCylinderNode(2d,20d)), cone);
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));
        Assert.False(eval.Admissible);
    }

    private static CirNode BuildChamfered()
        => new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20d,20d,10d), new CirCylinderNode(2d,20d)), new CirTransformNode(new CirConeNode(2d, 2.8d, 1d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, 4.5d))));
    private static CirNode BuildCountersink()
        => new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20d,20d,10d), new CirCylinderNode(2d,20d)), new CirTransformNode(new CirConeNode(2d, 4d, 4d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, 3d))));
}
