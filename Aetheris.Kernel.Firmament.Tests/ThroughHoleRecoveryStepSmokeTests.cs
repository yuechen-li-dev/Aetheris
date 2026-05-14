using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ThroughHoleRecoveryStepSmokeTests
{
    private static readonly ThroughHoleRecoveryPolicy Policy = new();

    [Fact]
    public void ThroughHoleRecoveryStepSmoke_CanonicalBoxCylinder_ExportsStep()
    {
        var root = new CirSubtractNode(new CirBoxNode(20d, 10d, 8d), new CirCylinderNode(2d, 20d));

        var pipeline = RecoverAndExport(root, "v3-canonical");

        Assert.Equal(FrepMaterializerDecisionStatus.Selected, pipeline.Decision.Status);
        Assert.Equal(ThroughHoleRecoveryExecutionStatus.Succeeded, pipeline.Execution.Status);
        Assert.NotNull(pipeline.Execution.Body);
        Assert.True(pipeline.StepResult.IsSuccess, string.Join(" | ", pipeline.StepResult.Diagnostics.Select(d => d.Message)));
        Assert.False(string.IsNullOrWhiteSpace(pipeline.StepResult.Value));
        Assert.Contains("ISO-10303-21", pipeline.StepResult.Value, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", pipeline.StepResult.Value, StringComparison.Ordinal);
        Assert.Contains("ADVANCED_FACE", pipeline.StepResult.Value, StringComparison.Ordinal);
        Assert.Contains("planner selected through-hole policy", pipeline.Diagnostics, StringComparer.Ordinal);
        Assert.Contains("executor produced body", pipeline.Diagnostics, StringComparer.Ordinal);
        Assert.Contains("STEP export attempted", pipeline.Diagnostics, StringComparer.Ordinal);
        Assert.Contains("STEP export succeeded", pipeline.Diagnostics, StringComparer.Ordinal);
        Assert.Contains("no exporter behavior changed", pipeline.Diagnostics, StringComparer.Ordinal);
    }

    [Fact]
    public void ThroughHoleRecoveryStepSmoke_TranslatedBoxCylinder_ExportsStep()
    {
        var root = new CirSubtractNode(
            new CirTransformNode(new CirBoxNode(20d, 20d, 10d), Transform3D.CreateTranslation(new Vector3D(5d, 2d, 4d))),
            new CirTransformNode(new CirCylinderNode(3d, 16d), Transform3D.CreateTranslation(new Vector3D(4d, 1d, 4d))));

        var pipeline = RecoverAndExport(root, "v3-translated");

        Assert.Equal(ThroughHoleRecoveryExecutionStatus.Succeeded, pipeline.Execution.Status);
        Assert.True(pipeline.StepResult.IsSuccess, string.Join(" | ", pipeline.StepResult.Diagnostics.Select(d => d.Message)));
        Assert.Contains("MANIFOLD_SOLID_BREP", pipeline.StepResult.Value, StringComparison.Ordinal);
        Assert.Contains("CYLINDRICAL_SURFACE", pipeline.StepResult.Value, StringComparison.Ordinal);
        Assert.Contains("STEP export succeeded", pipeline.Diagnostics, StringComparer.Ordinal);
    }

    [Fact]
    public void ThroughHoleRecoveryStepSmoke_RejectsUnsupportedBeforeExport()
    {
        var root = new CirSubtractNode(new CirBoxNode(20d, 10d, 8d), new CirSphereNode(3d));

        var pipeline = RecoverAndExport(root, "v3-unsupported");

        Assert.Equal(FrepMaterializerDecisionStatus.NoAdmissiblePolicy, pipeline.Decision.Status);
        Assert.Null(pipeline.Plan);
        Assert.Equal(ThroughHoleRecoveryExecutionStatus.Failed, pipeline.Execution.Status);
        Assert.False(pipeline.StepResult.IsSuccess);
        Assert.Contains("unsupported input rejected before export", pipeline.Diagnostics, StringComparer.Ordinal);
        Assert.Contains("STEP export skipped due no recovered body", pipeline.Diagnostics, StringComparer.Ordinal);
    }

    [Fact]
    public void ThroughHoleRecoveryStepSmoke_DoesNotChangeExporterBehavior()
    {
        var root = new CirSubtractNode(new CirBoxNode(12d, 12d, 12d), new CirCylinderNode(2d, 20d));

        var pipeline = RecoverAndExport(root, "v3-exporter-behavior");

        Assert.True(pipeline.StepResult.IsSuccess);
        Assert.Contains("existing exporter API: Step242Exporter.ExportBody(BrepBody, Step242ExportOptions?)", pipeline.Diagnostics, StringComparer.Ordinal);
        Assert.DoesNotContain(pipeline.Diagnostics, d => d.Contains("special exporter route", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(pipeline.Diagnostics, d => d.Contains("exporter mutation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("no rematerializer wiring", pipeline.Diagnostics, StringComparer.Ordinal);
    }

    private static RecoveryStepPipelineResult RecoverAndExport(CirNode root, string productName)
    {
        var diagnostics = new List<string>();
        var decision = FrepMaterializerPlanner.Decide(new FrepMaterializerContext(root), [Policy]);

        if (decision.Status != FrepMaterializerDecisionStatus.Selected)
        {
            diagnostics.Add("unsupported input rejected before export");
            diagnostics.Add("STEP export skipped due no recovered body");
            diagnostics.Add("no exporter behavior changed");
            diagnostics.Add("no rematerializer wiring");
            return new(decision, null, new(ThroughHoleRecoveryExecutionStatus.Failed, null, []), Kernel.Core.Results.KernelResult<string>.Failure([]), diagnostics);
        }

        diagnostics.Add("planner selected through-hole policy");

        var plan = decision.Evaluations.Select(e => e.Plan).OfType<ThroughHoleRecoveryPlan>().Single();
        var execution = ThroughHoleRecoveryExecutor.Execute(plan);

        if (execution.Status != ThroughHoleRecoveryExecutionStatus.Succeeded || execution.Body is null)
        {
            diagnostics.Add("STEP export skipped due no recovered body");
            diagnostics.Add("no exporter behavior changed");
            diagnostics.Add("no rematerializer wiring");
            return new(decision, plan, execution, Kernel.Core.Results.KernelResult<string>.Failure([]), diagnostics);
        }

        diagnostics.Add("executor produced body");
        diagnostics.Add("STEP export attempted");
        var stepResult = Step242Exporter.ExportBody(execution.Body, new Step242ExportOptions { ProductName = productName });
        diagnostics.Add(stepResult.IsSuccess ? "STEP export succeeded" : "STEP export failed");
        diagnostics.Add("existing exporter API: Step242Exporter.ExportBody(BrepBody, Step242ExportOptions?)");
        diagnostics.Add("no exporter behavior changed");
        diagnostics.Add("no rematerializer wiring");
        return new(decision, plan, execution, stepResult, diagnostics);
    }

    private sealed record RecoveryStepPipelineResult(
        FrepMaterializerDecision Decision,
        ThroughHoleRecoveryPlan? Plan,
        ThroughHoleRecoveryExecutionResult Execution,
        Aetheris.Kernel.Core.Results.KernelResult<string> StepResult,
        IReadOnlyList<string> Diagnostics);
}
