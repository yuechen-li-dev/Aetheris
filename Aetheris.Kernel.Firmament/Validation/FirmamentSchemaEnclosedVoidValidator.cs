using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Analysis;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.CompiledModel;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentSchemaEnclosedVoidValidator
{
    public static KernelResult<bool> Validate(FirmamentPrimitiveExecutionResult primitiveExecutionResult, FirmamentCompiledSchema? compiledSchema)
    {
        ArgumentNullException.ThrowIfNull(primitiveExecutionResult);

        if (AllowsEnclosedVoids(compiledSchema))
        {
            return KernelResult<bool>.Success(true);
        }

        var finalBody = GetFinalExecutedBody(primitiveExecutionResult);
        if (finalBody is null)
        {
            return KernelResult<bool>.Success(true);
        }

        var (featureId, body) = finalBody.Value;
        var enclosedVoidFacts = BrepEnclosedVoidAnalyzer.Analyze(body);
        if (enclosedVoidFacts.HasEnclosedVoids)
        {
            return KernelResult<bool>.Failure([new KernelDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error,
                $"[{FirmamentDiagnosticCodes.SchemaEnclosedVoidsNotAllowed.Value}] Feature '{featureId}' contains {enclosedVoidFacts.EnclosedVoidCount} fully enclosed internal void(s) (shell ids: {FormatShellIds(enclosedVoidFacts)}), which are not allowed for process '{ResolveProcessName(compiledSchema)}'.",
                FirmamentDiagnosticConventions.Source)]);
        }
        return KernelResult<bool>.Success(true);
    }

    private static (string FeatureId, BrepBody Body)? GetFinalExecutedBody(FirmamentPrimitiveExecutionResult primitiveExecutionResult)
    {
        var last = primitiveExecutionResult.ExecutedPrimitives
            .Select(executed => (executed.OpIndex, executed.FeatureId, executed.Body))
            .Concat(primitiveExecutionResult.ExecutedBooleans.Select(executed => (executed.OpIndex, executed.FeatureId, executed.Body)))
            .OrderBy(entry => entry.OpIndex)
            .ThenBy(entry => entry.FeatureId, StringComparer.Ordinal)
            .LastOrDefault();

        return last == default ? null : (last.FeatureId, last.Body);
    }

    private static bool AllowsEnclosedVoids(FirmamentCompiledSchema? compiledSchema)
        => compiledSchema?.Process == FirmamentCompiledSchemaProcess.Additive;

    private static string ResolveProcessName(FirmamentCompiledSchema? compiledSchema)
        => compiledSchema is null
            ? "default"
            : compiledSchema.Process switch
            {
                FirmamentCompiledSchemaProcess.Cnc => "cnc",
                FirmamentCompiledSchemaProcess.InjectionMolded => "injection_molded",
                FirmamentCompiledSchemaProcess.Additive => "additive",
                _ => "default"
            };

    private static string FormatShellIds(BrepEnclosedVoidFacts enclosedVoidFacts)
        => string.Join(", ", enclosedVoidFacts.EnclosedVoidShellIds.Select(shellId => shellId.Value));
}
