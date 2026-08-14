using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ThroughHoleRecoveryExecutorTests
{
    private static readonly ThroughHoleRecoveryPolicy Policy = new();

    [Fact]
    public void ThroughHoleExecutor_BoxCylinderPlan_ProducesBrepBody()
    {
        var root = new SdfSubtractNode(new SdfBoxNode(20d, 10d, 8d), new SdfCylinderNode(2d, 20d));
        var plan = BuildPlanFromPlanner(root);

        var result = ThroughHoleRecoveryExecutor.Execute(plan);

        Assert.Equal(ThroughHoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("Recipe request constructed from recognized semantics", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Contains("no temporary tool BRep or Boolean recognition pass", StringComparison.Ordinal));
        Assert.Contains("Through-hole Recipe succeeded.", result.Diagnostics);
    }

    [Fact]
    public void ThroughHoleExecutor_TranslatedBoxCylinderPlan_ProducesBrepBody()
    {
        var root = new SdfSubtractNode(
            new SdfTransformNode(new SdfBoxNode(20, 20, 10), Transform3D.CreateTranslation(new Vector3D(5, 2, 4))),
            new SdfTransformNode(new SdfCylinderNode(3, 16), Transform3D.CreateTranslation(new Vector3D(4, 1, 4))));
        var plan = BuildPlanFromPlanner(root);

        var result = ThroughHoleRecoveryExecutor.Execute(plan);

        Assert.Equal(ThroughHoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
        Assert.Contains("Through-hole Recipe succeeded.", result.Diagnostics);
    }

    [Fact]
    public void ThroughHoleExecutor_RejectsUnsupportedPlanShape()
    {
        var unsupportedPlan = new ThroughHoleRecoveryPlan(
            ThroughHoleHostKind.Unsupported,
            ThroughHoleToolKind.Cylindrical,
            ThroughHoleProfileKind.Circular,
            ThroughHoleAxisKind.Z,
            10d,
            2d,
            20d,
            20d,
            10d,
            Vector3D.Zero,
            Vector3D.Zero,
            [],
            [],
            [],
            FrepMaterializerCapability.ExactBRep,
            []);

        var result = ThroughHoleRecoveryExecutor.Execute(unsupportedPlan);

        Assert.Equal(ThroughHoleRecoveryExecutionStatus.UnsupportedPlan, result.Status);
        Assert.Null(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("unsupported specialization", StringComparison.Ordinal));
    }

    [Fact]
    public void ThroughHoleExecutor_BodyHasExpectedSurfaceFamiliesOrTopologySummary()
    {
        var root = new SdfSubtractNode(new SdfBoxNode(30d, 20d, 12d), new SdfCylinderNode(4d, 24d));
        var plan = BuildPlanFromPlanner(root);

        var result = ThroughHoleRecoveryExecutor.Execute(plan);

        Assert.Equal(ThroughHoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
        var body = result.Body!;

        Assert.True(body.Topology.Faces.Any());
        Assert.True(body.Topology.Edges.Any());
        Assert.True(body.Topology.Loops.Any());
        Assert.Contains(body.Topology.Faces, face =>
        {
            Assert.True(body.TryGetFaceSurfaceGeometry(face.Id, out var surface));
            return surface?.Kind == SurfaceGeometryKind.Cylinder;
        });
        Assert.Contains(body.Topology.Faces, face =>
        {
            Assert.True(body.TryGetFaceSurfaceGeometry(face.Id, out var surface));
            return surface?.Kind == SurfaceGeometryKind.Plane;
        });
    }

    [Fact]
    public void ThroughHoleExecutor_DoesNotExportStep()
    {
        var root = new SdfSubtractNode(new SdfBoxNode(20d, 10d, 8d), new SdfCylinderNode(2d, 20d));
        var plan = BuildPlanFromPlanner(root);

        var result = ThroughHoleRecoveryExecutor.Execute(plan);

        Assert.Equal(ThroughHoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.Contains("No STEP export attempted in CIR-RECOVERY-V2.", result.Diagnostics);
    }

    private static ThroughHoleRecoveryPlan BuildPlanFromPlanner(SdfNode root)
    {
        var decision = FrepMaterializerPlanner.Decide(new FrepMaterializerContext(root), [Policy]);
        var evaluation = Assert.Single(decision.Evaluations);
        return Assert.IsType<ThroughHoleRecoveryPlan>(evaluation.Plan);
    }
}
