using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CounterboreRecoveryExecutorTests
{
    [Fact]
    public void CounterboreExecutor_CanonicalPlan_ProducesBrepBody()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCanonicalCounterbore()));
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);

        var result = HoleRecoveryExecutor.Execute(plan);

        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("First subtract succeeded", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Contains("Second subtract succeeded", StringComparison.Ordinal));
    }

    [Fact]
    public void CounterboreExecutor_Rematerializer_ProducesBrepBody()
    {
        var result = FrepSemanticRecoveryRematerializer.TryRecover(BuildCanonicalCounterbore());
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("rematerializer executing hole-family plan", StringComparison.Ordinal));
    }

    [Fact]
    public void CounterboreExecutor_RejectsUnsupportedPlanShape()
    {
        var plan = new HoleRecoveryPlan(HoleHostKind.RectangularBox, HoleAxisKind.Z, HoleKind.Counterbore, HoleDepthKind.ThroughWithEntryRelief, HoleEntryFeatureKind.Counterbore, HoleExitFeatureKind.Plain, 10, 20, 20, 10, Vector3D.Zero, Vector3D.Zero,
            [new(HoleProfileSegmentKind.Cylindrical, 2,2,0,4)], [], [], FrepMaterializerCapability.ExactBRep, []);
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.UnsupportedPlan, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Contains("shape mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public void CounterboreExecutor_ThroughHoleStillWorks()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(3, 20))));
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("delegated", StringComparison.Ordinal));
    }

    [Fact]
    public void CounterboreExecutor_BodyHasExpectedSanity()
    {
        var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCanonicalCounterbore())).Plan);
        var result = HoleRecoveryExecutor.Execute(plan);
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(result.Body);
        Assert.True(body.Topology.Faces.Any());
        Assert.Contains(body.Topology.Faces, face => body.TryGetFaceSurfaceGeometry(face.Id, out var s) && s?.Kind == SurfaceGeometryKind.Cylinder);
        Assert.Contains(body.Topology.Faces, face => body.TryGetFaceSurfaceGeometry(face.Id, out var s) && s?.Kind == SurfaceGeometryKind.Plane);
    }

    [Fact]
    public void CounterboreExecutor_DoesNotExportStep()
    {
        var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCanonicalCounterbore())).Plan);
        var result = HoleRecoveryExecutor.Execute(plan);
        Assert.Contains(result.Diagnostics, d => d.Contains("No STEP export attempted", StringComparison.Ordinal));
    }

    private static CirNode BuildCanonicalCounterbore()
        => new CirSubtractNode(
            new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)),
            new CirTransformNode(new CirCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))));
}
