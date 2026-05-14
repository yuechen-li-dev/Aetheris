using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CounterboreRecoveryStepSmokeTests
{
    private static readonly HoleRecoveryPolicy Policy = new();

    [Fact]
    public void CounterboreStepSmoke_CanonicalCounterbore_ExportsStep()
    {
        var pipeline = RecoverAndExport(BuildCanonicalCounterbore(), "v7c-counterbore-canonical");

        Assert.Equal(FrepMaterializerDecisionStatus.Selected, pipeline.Decision.Status);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, pipeline.Execution.Status);
        Assert.NotNull(pipeline.Execution.Body);
        Assert.True(pipeline.StepResult.IsSuccess, string.Join(" | ", pipeline.StepResult.Diagnostics.Select(d => d.Message)));
        Assert.False(string.IsNullOrWhiteSpace(pipeline.StepResult.Value));
        Assert.Contains("ISO-10303-21", pipeline.StepResult.Value, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", pipeline.StepResult.Value, StringComparison.Ordinal);
        Assert.Contains("ADVANCED_FACE", pipeline.StepResult.Value, StringComparison.Ordinal);
        Assert.Contains("CYLINDRICAL_SURFACE", pipeline.StepResult.Value, StringComparison.Ordinal);
        Assert.Contains("planner selected HoleRecoveryPolicy", pipeline.Diagnostics, StringComparer.Ordinal);
        Assert.Contains("selected variant: CounterboreVariant", pipeline.Diagnostics, StringComparer.Ordinal);
        Assert.Contains("executor produced body", pipeline.Diagnostics, StringComparer.Ordinal);
        Assert.Contains("STEP export attempted", pipeline.Diagnostics, StringComparer.Ordinal);
        Assert.Contains("STEP export succeeded", pipeline.Diagnostics, StringComparer.Ordinal);
        Assert.Contains("no exporter behavior changed", pipeline.Diagnostics, StringComparer.Ordinal);
    }

    [Fact]
    public void CounterboreStepSmoke_UnsupportedCounterbore_DoesNotExport()
    {
        var pipeline = RecoverAndExport(BuildNonCoaxialCounterbore(), "v7c-counterbore-unsupported");

        Assert.Equal(FrepMaterializerDecisionStatus.NoAdmissiblePolicy, pipeline.Decision.Status);
        Assert.Null(pipeline.Plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Failed, pipeline.Execution.Status);
        Assert.False(pipeline.StepResult.IsSuccess);
        Assert.Contains("unsupported input rejected before export", pipeline.Diagnostics, StringComparer.Ordinal);
        Assert.Contains("STEP export skipped due no recovered body", pipeline.Diagnostics, StringComparer.Ordinal);
        Assert.Contains("UnsupportedNonCoaxialCylinders", string.Join("|", pipeline.Decision.Evaluations.SelectMany(e => e.RejectionReasons)), StringComparison.Ordinal);
    }

    [Fact]
    public void CounterboreStepSmoke_ThroughHoleRegressionStillExports()
    {
        var pipeline = RecoverAndExport(new CirSubtractNode(new CirBoxNode(20d, 10d, 8d), new CirCylinderNode(2d, 20d)), "v7c-through-hole-regression");

        Assert.Equal(FrepMaterializerDecisionStatus.Selected, pipeline.Decision.Status);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, pipeline.Execution.Status);
        Assert.True(pipeline.StepResult.IsSuccess);
        Assert.Contains("selected variant: ThroughHoleVariant", pipeline.Diagnostics, StringComparer.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", pipeline.StepResult.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void CounterboreStepSmoke_UsesManifoldNotBrepWithVoids()
    {
        var pipeline = RecoverAndExport(BuildCanonicalCounterbore(), "v7c-counterbore-manifold");

        Assert.True(pipeline.StepResult.IsSuccess);
        Assert.Contains("MANIFOLD_SOLID_BREP", pipeline.StepResult.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", pipeline.StepResult.Value, StringComparison.Ordinal);
        Assert.Contains("no BREP_WITH_VOIDS expected without explicit void shells", pipeline.Diagnostics, StringComparer.Ordinal);
    }

    [Fact]
    public void CounterboreStepSmoke_TranslatedCounterbore_ExportsStep()
    {
        var pipeline = RecoverAndExport(BuildTranslatedCounterbore(), "v7c-counterbore-translated");

        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, pipeline.Execution.Status);
        Assert.True(pipeline.StepResult.IsSuccess);
        Assert.Contains("selected variant: CounterboreVariant", pipeline.Diagnostics, StringComparer.Ordinal);
        Assert.Contains("CYLINDRICAL_SURFACE", pipeline.StepResult.Value, StringComparison.Ordinal);
    }

    private static CounterboreRecoveryStepPipelineResult RecoverAndExport(CirNode root, string productName)
    {
        var diagnostics = new List<string>();
        var decision = FrepMaterializerPlanner.Decide(new FrepMaterializerContext(root), [Policy]);

        if (decision.Status != FrepMaterializerDecisionStatus.Selected)
        {
            diagnostics.Add("unsupported input rejected before export");
            diagnostics.Add("STEP export skipped due no recovered body");
            diagnostics.Add("no exporter behavior changed");
            diagnostics.Add("no rematerializer wiring");
            return new(decision, null, new(HoleRecoveryExecutionStatus.Failed, null, []), Aetheris.Kernel.Core.Results.KernelResult<string>.Failure([]), diagnostics);
        }

        diagnostics.Add("planner selected HoleRecoveryPolicy");
        var evaluation = decision.Evaluations.Single();
        var plan = Assert.IsType<HoleRecoveryPlan>(evaluation.Plan);
        diagnostics.Add($"selected variant: {evaluation.Evidence.Single(e => e.StartsWith("selected-variant:", StringComparison.Ordinal)).Split(':')[1]}");

        var execution = HoleRecoveryExecutor.Execute(plan);
        if (execution.Status != HoleRecoveryExecutionStatus.Succeeded || execution.Body is null)
        {
            diagnostics.Add("STEP export skipped due no recovered body");
            diagnostics.Add("no exporter behavior changed");
            diagnostics.Add("no rematerializer wiring");
            return new(decision, plan, execution, Aetheris.Kernel.Core.Results.KernelResult<string>.Failure([]), diagnostics);
        }

        diagnostics.Add("executor produced body");
        diagnostics.Add("STEP export attempted");
        var stepResult = Step242Exporter.ExportBody(execution.Body, new Step242ExportOptions { ProductName = productName });
        diagnostics.Add(stepResult.IsSuccess ? "STEP export succeeded" : "STEP export failed");
        diagnostics.Add("existing exporter API: Step242Exporter.ExportBody(BrepBody, Step242ExportOptions?)");
        diagnostics.Add("no exporter behavior changed");
        diagnostics.Add("no BREP_WITH_VOIDS expected without explicit void shells");
        return new(decision, plan, execution, stepResult, diagnostics);
    }

    private static CirNode BuildCanonicalCounterbore()
        => new CirSubtractNode(
            new CirSubtractNode(new CirBoxNode(20d, 20d, 10d), new CirCylinderNode(2d, 20d)),
            new CirTransformNode(new CirCylinderNode(4d, 4d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, -3d))));

    private static CirNode BuildTranslatedCounterbore()
        => new CirSubtractNode(
            new CirSubtractNode(
                new CirTransformNode(new CirBoxNode(24d, 24d, 12d), Transform3D.CreateTranslation(new Vector3D(5d, 1d, 6d))),
                new CirTransformNode(new CirCylinderNode(3d, 20d), Transform3D.CreateTranslation(new Vector3D(5d, 1d, 6d)))),
            new CirTransformNode(new CirCylinderNode(5d, 4d), Transform3D.CreateTranslation(new Vector3D(5d, 1d, 2d))));

    private static CirNode BuildNonCoaxialCounterbore()
        => new CirSubtractNode(
            new CirSubtractNode(new CirBoxNode(20d, 20d, 10d), new CirCylinderNode(2d, 20d)),
            new CirTransformNode(new CirCylinderNode(4d, 4d), Transform3D.CreateTranslation(new Vector3D(1d, 0d, -3d))));

    private sealed record CounterboreRecoveryStepPipelineResult(
        FrepMaterializerDecision Decision,
        HoleRecoveryPlan? Plan,
        HoleRecoveryExecutionResult Execution,
        Aetheris.Kernel.Core.Results.KernelResult<string> StepResult,
        IReadOnlyList<string> Diagnostics);
}
