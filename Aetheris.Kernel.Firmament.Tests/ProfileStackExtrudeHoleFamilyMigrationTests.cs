using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ProfileStackExtrudeHoleFamilyMigrationTests
{
    [Fact]
    public void ProfileStack_ThroughHole_UsesProfileStackExecutor()
    {
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)))).Plan!;
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Contains("hole-family executor route: profile-stack-extrude", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Contains("no 3D subtract route used", StringComparison.Ordinal));
        Assert.True(Step242Exporter.ExportBody(result.Body!).IsSuccess);
    }

    [Fact]
    public void ProfileStack_BlindHole_UsesProfileStackExecutorOrReportsPreciseBlocker()
    {
        var root = new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirTransformNode(new CirCylinderNode(2, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, 3))));
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root)).Plan!;
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Contains("air-profile-stack-v1-blind-deferred", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("Blind subtract succeeded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProfileStack_Counterbore_UsesProfileStackExecutor()
    {
        var root = new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)), new CirTransformNode(new CirCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))));
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root)).Plan!;
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Contains("air-profile-stack-v1-counterbore-deferred", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("Second subtract succeeded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProfileStack_Countersink_RemainsConeBooleanRoute()
    {
        var root = new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20,20,10), new CirCylinderNode(2,20)), new CirTransformNode(new CirConeNode(2, 4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, 3))));
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root)).Plan!;
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("cylindrical-only profile-stack accepted", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("conical route", StringComparison.OrdinalIgnoreCase));
    }
}
