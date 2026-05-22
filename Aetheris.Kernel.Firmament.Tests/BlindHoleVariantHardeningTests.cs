using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class BlindHoleVariantHardeningTests
{
    [Fact]
    public void BlindHole_TopEntry_AdmitsExecutesAndExports()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildTopEntryBlindHole()));
        Assert.True(eval.Admissible);
        Assert.Contains("selected-variant:BlindHoleVariant", eval.Evidence);
        Assert.Contains(eval.EvaluationsFor(nameof(BlindHoleVariant)), d => d.Contains("entry face detected: top(+Z)", StringComparison.OrdinalIgnoreCase));

        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        var execution = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, execution.Status);

        var step = Step242Exporter.ExportBody(execution.Body!, new Step242ExportOptions { ProductName = "v9-blind-top" });
        Assert.True(step.IsSuccess, string.Join(" | ", step.Diagnostics.Select(d => d.Message)));
        Assert.Contains("MANIFOLD_SOLID_BREP", step.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", step.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void BlindHole_BottomEntry_AdmitsExecutesAndExports()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildBottomEntryBlindHole()));
        Assert.True(eval.Admissible);
        Assert.Contains("selected-variant:BlindHoleVariant", eval.Evidence);
        Assert.Contains(eval.EvaluationsFor(nameof(BlindHoleVariant)), d => d.Contains("entry face detected: bottom(-Z)", StringComparison.OrdinalIgnoreCase));

        var execution = HoleRecoveryExecutor.Execute(Assert.IsType<HoleRecoveryPlan>(eval.Plan));
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, execution.Status);

        var step = Step242Exporter.ExportBody(execution.Body!, new Step242ExportOptions { ProductName = "v9-blind-bottom" });
        Assert.True(step.IsSuccess, string.Join(" | ", step.Diagnostics.Select(d => d.Message)));
        Assert.DoesNotContain("BREP_WITH_VOIDS", step.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void BlindHole_TranslatedInput_AdmitsExecutesAndExports()
    {
        var root = new CirSubtractNode(
            new CirTransformNode(new CirBoxNode(20d, 20d, 10d), Transform3D.CreateTranslation(new Vector3D(7d, -4d, 5d))),
            new CirTransformNode(new CirCylinderNode(2d, 4d), Transform3D.CreateTranslation(new Vector3D(8d, -3d, 8d))));

        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));
        Assert.True(eval.Admissible);
        Assert.Contains("selected-variant:BlindHoleVariant", eval.Evidence);
        Assert.Contains(eval.EvaluationsFor(nameof(BlindHoleVariant)), d => d.Contains("translated geometry normalized", StringComparison.OrdinalIgnoreCase));

        var execution = HoleRecoveryExecutor.Execute(Assert.IsType<HoleRecoveryPlan>(eval.Plan));
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, execution.Status);

        var step = Step242Exporter.ExportBody(execution.Body!, new Step242ExportOptions { ProductName = "v9-blind-translated" });
        Assert.True(step.IsSuccess, string.Join(" | ", step.Diagnostics.Select(d => d.Message)));
        Assert.DoesNotContain("BREP_WITH_VOIDS", step.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void BlindHole_DoesNotStealThroughHole()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20, 10, 8), new CirCylinderNode(2, 20))));
        Assert.Contains("selected-variant:ThroughHoleVariant", eval.Evidence);
        Assert.DoesNotContain("selected-variant:BlindHoleVariant", eval.Evidence);
        Assert.Contains(eval.EvaluationsFor(nameof(BlindHoleVariant)), d => d.Contains("through-hole rejected as not blind", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BlindHole_DoesNotStealCounterbore()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCounterbore()));
        Assert.True(eval.Admissible);
        Assert.Contains("selected-variant:CounterboreVariant", eval.Evidence);
        Assert.DoesNotContain("selected-variant:BlindHoleVariant", eval.Evidence);
    }

    [Fact]
    public void BlindHole_RejectsNearThroughWithinTolerance()
    {
        var tol = Aetheris.Kernel.Core.Numerics.ToleranceContext.Default.Linear;
        var boxDepth = 10d;
        var cylHeight = boxDepth - (tol * 0.25d);
        var cylCenterZ = (boxDepth * 0.5d) - (cylHeight * 0.5d);
        var root = new CirSubtractNode(new CirBoxNode(20d, 20d, boxDepth), new CirTransformNode(new CirCylinderNode(2d, cylHeight), Transform3D.CreateTranslation(new Vector3D(0d, 0d, cylCenterZ))));

        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));
        Assert.False(eval.Admissible);
        Assert.Contains(eval.RejectionReasons, r => r.Contains("Unsupported", StringComparison.Ordinal));
    }

    [Fact]
    public void BlindHole_RejectsNoEntryOpening()
    {
        var root = new CirSubtractNode(new CirBoxNode(20d, 20d, 10d), new CirCylinderNode(2d, 4d));
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));
        Assert.False(eval.Admissible);
        Assert.Contains("UnsupportedMissingEntryFace", string.Join("|", eval.RejectionReasons), StringComparison.Ordinal);
    }

    [Fact]
    public void BlindHole_RejectsTangentOrGrazingRadius()
    {
        var root = new CirSubtractNode(new CirBoxNode(20d, 20d, 10d), new CirTransformNode(new CirCylinderNode(10d, 4d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, 3d))));
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root));
        Assert.False(eval.Admissible);
        Assert.Contains("UnsupportedTangentOrOutsideClearance", string.Join("|", eval.RejectionReasons), StringComparison.Ordinal);
        Assert.Contains(eval.EvaluationsFor(nameof(BlindHoleVariant)), d => d.Contains("tangent/grazing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BlindHole_RejectsUnsupportedTransform()
    {
        var rotated = new CirSubtractNode(
            new CirBoxNode(20d, 20d, 10d),
            new CirTransformNode(new CirCylinderNode(2d, 4d), Transform3D.CreateRotationX(Math.PI / 6d) * Transform3D.CreateTranslation(new Vector3D(0d, 0d, 3d))));

        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(rotated));
        Assert.False(eval.Admissible);
        var remat = FrepSemanticRecoveryRematerializer.TryRecover(rotated);
        Assert.False(remat.Succeeded);
        Assert.Null(remat.Body);
    }

    private static CirNode BuildTopEntryBlindHole()
        => new CirSubtractNode(new CirBoxNode(20d, 20d, 10d), new CirTransformNode(new CirCylinderNode(2d, 4d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, 3d))));

    private static CirNode BuildBottomEntryBlindHole()
        => new CirSubtractNode(new CirBoxNode(20d, 20d, 10d), new CirTransformNode(new CirCylinderNode(2d, 4d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, -3d))));

    private static CirNode BuildCounterbore()
        => new CirSubtractNode(
            new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)),
            new CirTransformNode(new CirCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))));
}
