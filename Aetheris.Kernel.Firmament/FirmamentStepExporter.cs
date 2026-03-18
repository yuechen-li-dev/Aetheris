using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament;

public static class FirmamentStepExporter
{
    public const string LastExecutedGeometricBodyPolicy = "last-executed-geometric-body";

    public static KernelResult<FirmamentStepExportResult> Export(FirmamentCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var compiler = new FirmamentCompiler();
        var compileResult = compiler.Compile(request);
        if (!compileResult.Compilation.IsSuccess)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(compileResult.Compilation.Diagnostics);
        }

        return Export(compileResult.Compilation.Value);
    }

    public static KernelResult<FirmamentStepExportResult> Export(FirmamentCompilationArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var selectionResult = SelectExportBody(artifact.PrimitiveExecutionResult);
        if (!selectionResult.IsSuccess)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(selectionResult.Diagnostics);
        }

        var stepResult = Step242Exporter.ExportBody(selectionResult.Value.Body);
        if (!stepResult.IsSuccess)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(stepResult.Diagnostics);
        }

        return KernelResult<FirmamentStepExportResult>.Success(
            new FirmamentStepExportResult(
                stepResult.Value,
                selectionResult.Value.FeatureId,
                selectionResult.Value.OpIndex));
    }

    private static KernelResult<ExportBodySelection> SelectExportBody(FirmamentPrimitiveExecutionResult? primitiveExecutionResult)
    {
        if (primitiveExecutionResult is null)
        {
            return Failure("Firmament STEP export requires a completed primitive execution result.");
        }

        ExportBodySelection? selected = null;

        foreach (var primitive in primitiveExecutionResult.ExecutedPrimitives)
        {
            selected = SelectLater(
                selected,
                new ExportBodySelection(primitive.OpIndex, primitive.FeatureId, primitive.Body));
        }

        foreach (var boolean in primitiveExecutionResult.ExecutedBooleans)
        {
            selected = SelectLater(
                selected,
                new ExportBodySelection(boolean.OpIndex, boolean.FeatureId, boolean.Body));
        }

        return selected is null
            ? Failure("Firmament STEP export requires at least one executed primitive or boolean body.")
            : KernelResult<ExportBodySelection>.Success(selected);
    }

    private static ExportBodySelection SelectLater(ExportBodySelection? current, ExportBodySelection candidate)
    {
        if (current is null)
        {
            return candidate;
        }

        return candidate.OpIndex > current.OpIndex ? candidate : current;
    }

    private static KernelResult<ExportBodySelection> Failure(string message) =>
        KernelResult<ExportBodySelection>.Failure(
        [
            new KernelDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error,
                message,
                FirmamentDiagnosticConventions.Source)
        ]);

    private sealed record ExportBodySelection(int OpIndex, string FeatureId, BrepBody Body);
}
