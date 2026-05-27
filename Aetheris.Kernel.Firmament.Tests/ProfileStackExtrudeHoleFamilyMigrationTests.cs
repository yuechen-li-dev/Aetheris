using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ProfileStackExtrudeHoleFamilyMigrationTests
{
    [Fact]
    public void ProfileStack_ThroughHole_UsesProfileHoleExtrudeRoute()
    {
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)))).Plan!;
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.Contains("v2-v2-profile-hole-extrude-attempted", result.Diagnostics);
        Assert.Contains("v2-v2-profile-hole-extrude-accepted", result.Diagnostics);
        Assert.Contains("v2-v2-profile-hole-extrude-no-3d-boolean-subtract", result.Diagnostics);
        Assert.Contains("v2-v2-profile-hole-extrude-succeeded", result.Diagnostics);
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("fallback-legacy-through-hole", StringComparison.Ordinal));

        var body = result.Body!;
        var planar = body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane);
        var cyl = body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder);
        Assert.Equal(6, planar);
        Assert.Equal(1, cyl);

        var step = Step242Exporter.ExportBody(body);
        Assert.True(step.IsSuccess);
        var text = step.Value!;
        Assert.Contains("ISO-10303-21", text);
        Assert.Contains("MANIFOLD_SOLID_BREP", text);
        Assert.Contains("ADVANCED_FACE", text);
        Assert.Contains("PLANE", text);
        Assert.Contains("CYLINDRICAL_SURFACE", text);
        Assert.DoesNotContain("BREP_WITH_VOIDS", text);
    }

    [Fact]
    public void ProfileStack_ThroughHole_OffCenter_UsesProfileHoleExtrudeRoute()
    {
        var root = new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirTransformNode(new CirCylinderNode(2, 20), Transform3D.CreateTranslation(new Vector3D(3, -2, 0))));
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root)).Plan!;
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.Contains("v2-v2-profile-hole-extrude-accepted", result.Diagnostics);
        Assert.Contains("v2-v2-profile-hole-extrude-succeeded", result.Diagnostics);
    }

    [Fact]
    public void ProfileStack_BlindHole_UsesProfileStackExecutorOrReportsPreciseBlocker()
    {
        var root = new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirTransformNode(new CirCylinderNode(2, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, 3))));
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root)).Plan!;
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Contains("air-profile-stack-v2b-blind-solid-interval-recognized", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("air-profile-stack-v2b-blind-emitter-deferred", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("air-profile-stack-v2b-fallback-legacy-blind", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("Blind subtract succeeded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProfileStack_Counterbore_UsesProfileStackExecutor()
    {
        var root = new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)), new CirTransformNode(new CirCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))));
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root)).Plan!;
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Contains("air-profile-stack-v2b-counterbore-contiguous-accepted", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("v2-v2-profile-hole-extrude-accepted", result.Diagnostics);
    }

    [Fact]
    public void ProfileStack_Countersink_RemainsConeBooleanRoute()
    {
        var root = new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20,20,10), new CirCylinderNode(2,20)), new CirTransformNode(new CirConeNode(2, 4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, 3))));
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root)).Plan!;
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("cylindrical-only profile-stack accepted", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("v2-v2-profile-hole-extrude-accepted", result.Diagnostics);
        Assert.Contains(result.Diagnostics, d => d.Contains("conical route", StringComparison.OrdinalIgnoreCase));
    }
}
