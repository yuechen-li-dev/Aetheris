using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class AirProfileStackExtrudeTests
{
    [Fact]
    public void AirProfileStack_FromSpec_ThroughHole_Validates()
    {
        var spec = new ProfileStackExtrudeSpec(20,20,-5,5,[new ProfileStackLayer(-5,5,2,"through",[])],[]);
        Assert.True(AirProfileStackExtrudeAdapter.TryFromProfileStackSpec(spec, out var air, out _));
        Assert.NotNull(air);
        Assert.True(AirProfileStackExtrudeAdapter.TryValidate(air!, out _));
    }

    [Fact]
    public void AirProfileStack_FromHolePlan_ThroughHole_ConvertsAndExecutes()
    {
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20,20,10), new CirCylinderNode(2,20)))).Plan!;
        Assert.True(AirProfileStackExtrudeAdapter.TryFromHoleRecoveryPlan(plan, out _, out var d));
        var exec = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, exec.Status);
        Assert.Contains(exec.Diagnostics, x => x.Contains("air-profile-stack-extrude", StringComparison.Ordinal));
        var step = Step242Exporter.ExportBody(exec.Body!);
        Assert.True(step.IsSuccess);
    }

    [Fact]
    public void ThroughAndStepped_AirRoutesRemainGreen()
    {
        var through = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20,20,10), new CirCylinderNode(2,20)))).Plan!;
        var stepped = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped())).Plan!;
        Assert.True(AirProfileStackExtrudeAdapter.TryFromHoleRecoveryPlan(through, out _, out _));
        Assert.True(AirProfileStackExtrudeAdapter.TryFromHoleRecoveryPlan(stepped, out _, out _));
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, HoleRecoveryExecutor.Execute(through).Status);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, HoleRecoveryExecutor.Execute(stepped).Status);
    }

    [Fact]
    public void BlindHole_AirV2B_FinalRouteStatus()
    {
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20,20,10), new CirTransformNode(new CirCylinderNode(2,4), Transform3D.CreateTranslation(new Vector3D(0,0,3)))))).Plan!;
        Assert.False(AirProfileStackExtrudeAdapter.TryFromHoleRecoveryPlan(plan, out _, out var d));
        Assert.Contains(d, x => x.Contains("air-profile-stack-v2b-blind-solid-interval-recognized", StringComparison.Ordinal));
        Assert.Contains(d, x => x.Contains("air-profile-stack-v2b-blind-emitter-deferred", StringComparison.Ordinal));
        var exec = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, exec.Status);
        Assert.Contains(exec.Diagnostics, x => x.Contains("air-profile-stack-v2b-fallback-legacy-blind", StringComparison.Ordinal));
        Assert.Contains(exec.Diagnostics, x => x.Contains("Blind subtract succeeded", StringComparison.Ordinal));
    }

    [Fact]
    public void Counterbore_AirV2B_FinalRouteStatus()
    {
        var root = new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20,20,10), new CirCylinderNode(2,20)), new CirTransformNode(new CirCylinderNode(4,4), Transform3D.CreateTranslation(new Vector3D(0,0,3))));
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root)).Plan!;
        Assert.True(AirProfileStackExtrudeAdapter.TryFromHoleRecoveryPlan(plan, out _, out var d));
        Assert.Contains(d, x => x.Contains("air-profile-stack-v2b-counterbore-contiguous-accepted", StringComparison.Ordinal));
        var exec = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, exec.Status);
        Assert.Contains(exec.Diagnostics, x => x.Contains("Second subtract succeeded", StringComparison.Ordinal));
    }

    [Fact]
    public void ConicalRoutesRemainConical()
    {
        var root = new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20,20,10), new CirCylinderNode(2,20)), new CirTransformNode(new CirConeNode(2, 4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, 3))));
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root)).Plan!;
        Assert.False(AirProfileStackExtrudeAdapter.TryFromHoleRecoveryPlan(plan, out _, out var d));
        Assert.Contains(d, x => x.Contains("air-profile-stack-v1-conical-deferred", StringComparison.Ordinal));
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, HoleRecoveryExecutor.Execute(plan).Status);
    }

    [Fact]
    public void NoHoleLayer_CurrentExpectedBehavior()
    {
        var air = new AirProfileStackExtrude([
            new AirProfileStackLayer(-5,0,new AirProfileRegion2D(new AirRectangleProfile(20,20),new AirCenteredCircleLoop(2),AirProfileStackLayerKind.CircularCutInterval,"a",[]),"a",[]),
            new AirProfileStackLayer(1,5,new AirProfileRegion2D(new AirRectangleProfile(20,20),new AirCenteredCircleLoop(3),AirProfileStackLayerKind.CircularCutInterval,"b",[]),"b",[])
        ],-5,5,[],[]);
        Assert.False(AirProfileStackExtrudeAdapter.TryToProfileStackSpec(air, out _, out _));
    }

    [Fact]
    public void SolidLayer_WithInnerLoop_IsDeterministicallyInvalid()
    {
        var air = new AirProfileStackExtrude([
            new AirProfileStackLayer(-5,5,new AirProfileRegion2D(new AirRectangleProfile(20,20),new AirCenteredCircleLoop(2),AirProfileStackLayerKind.SolidInterval,"bad",[]),"bad",[])
        ],-5,5,[],[]);
        Assert.False(AirProfileStackExtrudeAdapter.TryValidate(air, out var d));
        Assert.Contains(d, x => x.Contains("air-validation-failed-solid-interval-has-inner-loop", StringComparison.Ordinal));
    }

    private static CirNode BuildStepped() => new CirSubtractNode(new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20,20,10), new CirCylinderNode(2,20)), new CirTransformNode(new CirCylinderNode(3,6), Transform3D.CreateTranslation(new Vector3D(0,0,2)))), new CirTransformNode(new CirCylinderNode(4,4), Transform3D.CreateTranslation(new Vector3D(0,0,3))));
}
