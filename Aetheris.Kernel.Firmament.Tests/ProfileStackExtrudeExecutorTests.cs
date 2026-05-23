using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ProfileStackExtrudeExecutorTests
{
    [Fact]
    public void ProfileStackExtrudeExecutor_SteppedSpec_ProducesBrepBody()
    {
        var spec = new ProfileStackExtrudeSpec(20, 20, -5, 5,
            [new(-5,-1,2,"small",[]), new(-1,2,3,"medium",[]), new(2,5,4,"large",[])], []);
        var result = ProfileStackExtrudeExecutor.Execute(spec);
        Assert.Equal(ProfileStackExtrudeExecutionStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("composition build invoked", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("repeated-subtract", StringComparison.Ordinal));
    }

    [Fact]
    public void ProfileStackExtrudeExecutor_SteppedSpec_ExportsStep()
    {
        var spec = new ProfileStackExtrudeSpec(20, 20, -5, 5,
            [new(-5,-1,2,"small",[]), new(-1,2,3,"medium",[]), new(2,5,4,"large",[])], []);
        var result = ProfileStackExtrudeExecutor.Execute(spec);
        var step = Step242Exporter.ExportBody(result.Body!);
        Assert.True(step.IsSuccess);
        Assert.Contains("ISO-10303-21", step.Value, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", step.Value, StringComparison.Ordinal);
        Assert.Contains("ADVANCED_FACE", step.Value, StringComparison.Ordinal);
        Assert.Contains("CYLINDRICAL_SURFACE", step.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", step.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileStackExtrude_InvalidLayerOrdering_Rejects()
    {
        var spec = new ProfileStackExtrudeSpec(20,20,-5,5,[new(-5,0,2,"a",[]), new(1,5,3,"b",[])],[]);
        var result = ProfileStackExtrudeExecutor.Execute(spec);
        Assert.Equal(ProfileStackExtrudeExecutionStatus.InvalidProfileStack, result.Status);
    }

    [Fact]
    public void HoleRecoveryExecutor_SteppedPlan_UsesProfileStackRoute()
    {
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped())).Plan!;
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Contains("stepped executor route: profile-stack-extrude", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("repeated-subtract", StringComparison.Ordinal));
    }

    private static CirNode BuildStepped() => new CirSubtractNode(new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20,20,10), new CirCylinderNode(2,20)), new CirTransformNode(new CirCylinderNode(3,6), Transform3D.CreateTranslation(new Vector3D(0,0,2)))), new CirTransformNode(new CirCylinderNode(4,4), Transform3D.CreateTranslation(new Vector3D(0,0,3))));
}
