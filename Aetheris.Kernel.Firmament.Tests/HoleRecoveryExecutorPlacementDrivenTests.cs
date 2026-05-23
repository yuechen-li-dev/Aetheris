using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class HoleRecoveryExecutorPlacementDrivenTests
{
    [Fact]
    public void PlacementDriven_BlindHole_UsesSegmentZSpan()
    {
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy()
            .Evaluate(new FrepMaterializerContext(
                new CirSubtractNode(
                    new CirBoxNode(20, 20, 10),
                    new CirTransformNode(new CirCylinderNode(2, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, 3))))))
            .Plan!;

        var result = HoleRecoveryExecutor.Execute(plan);

        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Contains("placement-driven segment=blind-cylinder", StringComparison.Ordinal));
    }

    [Fact]
    public void PlacementDriven_Counterbore_UsesSegmentZSpans()
    {
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy()
            .Evaluate(new FrepMaterializerContext(
                new CirSubtractNode(
                    new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)),
                    new CirTransformNode(new CirCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))))))
            .Plan!;

        var result = HoleRecoveryExecutor.Execute(plan);

        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Contains("segment=counterbore-through", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Contains("segment=counterbore-relief", StringComparison.Ordinal));
    }

    [Fact]
    public void PlacementDriven_Countersink_UsesConeSegmentZSpan()
    {
        var cone = new CirTransformNode(new CirConeNode(2, 4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, 3)));
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy()
            .Evaluate(new FrepMaterializerContext(
                new CirSubtractNode(
                    new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)),
                    cone)))
            .Plan!;

        var result = HoleRecoveryExecutor.Execute(plan);
        var step = Step242Exporter.ExportBody(result.Body!);

        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Contains("segment=cone", StringComparison.Ordinal));
        Assert.True(step.IsSuccess);
    }

    [Fact]
    public void PlacementDriven_ChamferedEntryTopBottom_UsesConeSegmentZSpan()
    {
        var top = new CirSubtractNode(
            new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)),
            new CirTransformNode(new CirConeNode(2, 2.8, 1), Transform3D.CreateTranslation(new Vector3D(0, 0, 4.5))));

        var bottom = new CirSubtractNode(
            new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)),
            new CirTransformNode(new CirConeNode(2.8, 2, 1), Transform3D.CreateTranslation(new Vector3D(0, 0, -4.5))));

        var topResult = HoleRecoveryExecutor.Execute((HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(top)).Plan!);
        var bottomResult = HoleRecoveryExecutor.Execute((HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(bottom)).Plan!);

        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, topResult.Status);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, bottomResult.Status);
        Assert.Contains(topResult.Diagnostics, d => d.Contains("segment=chamfer cone", StringComparison.Ordinal));
        Assert.Contains(bottomResult.Diagnostics, d => d.Contains("segment=chamfer cone", StringComparison.Ordinal));
    }

    [Fact]
    public void PlacementDriven_InvalidSegmentRejectsBeforeBoolean()
    {
        var plan = new HoleRecoveryPlan(
            HoleHostKind.RectangularBox,
            HoleAxisKind.Z,
            HoleKind.Blind,
            HoleDepthKind.Blind,
            HoleEntryFeatureKind.Plain,
            HoleExitFeatureKind.ClosedBottom,
            4,
            20,
            20,
            10,
            Vector3D.Zero,
            Vector3D.Zero,
            [new(HoleProfileSegmentKind.Cylindrical, 2, 2, 0, 4, HoleTierAnchorSide.Unknown, 4, 1, 1, false, ["x"])],
            [],
            [],
            FrepMaterializerCapability.ExactBRep,
            []);

        var result = HoleRecoveryExecutor.Execute(plan);

        Assert.Equal(HoleRecoveryExecutionStatus.UnsupportedPlan, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Contains("placement-validation-failed", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("subtract invoked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlacementDriven_SteppedPlacementValidatedAndExecutionSucceeds()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(
            new CirSubtractNode(
                new CirSubtractNode(
                    new CirSubtractNode(
                        new CirBoxNode(20, 20, 10),
                        new CirCylinderNode(2, 20)),
                    new CirTransformNode(new CirCylinderNode(3, 6), Transform3D.CreateTranslation(new Vector3D(0, 0, -2)))),
                new CirTransformNode(new CirCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))))));

        var result = HoleRecoveryExecutor.Execute((HoleRecoveryPlan)eval.Plan!);

        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Contains("Stepped explicit-placement validation succeeded", StringComparison.Ordinal));
    }
}
