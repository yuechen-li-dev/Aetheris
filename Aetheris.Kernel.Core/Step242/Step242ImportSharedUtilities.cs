using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Step242;

internal static class Step242ImportSharedUtilities
{
    internal static KernelResult<BrepBody> ExecuteWithGuardrail(Func<KernelResult<BrepBody>> import)
    {
        try
        {
            return import();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.InvalidArgument,
                    KernelDiagnosticSeverity.Error,
                    $"Importer rejected parseable STEP input: {ex.Message}",
                    "Importer.Guardrail")
            ]);
        }
    }

    internal static KernelResult<Step242ParsedEntity> RequireSingleEntityByName(
        Step242ParsedDocument document,
        string entityName,
        string missingMessage,
        string missingSource,
        string multipleMessage,
        string multipleSource)
    {
        var candidates = document.Entities
            .Where(e => string.Equals(e.Name, entityName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            return NotImplementedFailure<Step242ParsedEntity>(missingMessage, missingSource);
        }

        if (candidates.Count > 1)
        {
            return NotImplementedFailure<Step242ParsedEntity>(multipleMessage, multipleSource);
        }

        return KernelResult<Step242ParsedEntity>.Success(candidates[0]);
    }

    internal static KernelResult<T> NotImplementedFailure<T>(string message, string source)
        => KernelResult<T>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source)]);

    internal static KernelResult<T> ValidationFailure<T>(string message, string source)
        => KernelResult<T>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, source)]);
}
