using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentDocumentCoherenceValidator
{
    public static KernelResult<bool> Validate(FirmamentParsedDocument parsedDocument)
    {
        ArgumentNullException.ThrowIfNull(parsedDocument);

        var featureIds = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < parsedDocument.Ops.Entries.Count; index++)
        {
            var entry = parsedDocument.Ops.Entries[index];

            if (entry.Family == FirmamentOpFamily.Validation)
            {
                continue;
            }

            var featureId = entry.RawFields["id"];
            if (entry.Family == FirmamentOpFamily.Boolean)
            {
                var referenceFieldName = GetReferenceFieldName(entry.KnownKind);
                var referenceId = entry.RawFields[referenceFieldName];
                if (!featureIds.Contains(referenceId))
                {
                    return KernelResult<bool>.Failure([
                        CreateDiagnostic(
                            FirmamentDiagnosticCodes.ReferenceUnknownFeatureId,
                            $"Boolean op '{entry.OpName}' at index {index} references unknown feature id '{referenceId}' via field '{referenceFieldName}'.")
                    ]);
                }
            }

            if (!featureIds.Add(featureId))
            {
                return KernelResult<bool>.Failure([
                    CreateDiagnostic(
                        FirmamentDiagnosticCodes.ReferenceDuplicateFeatureId,
                        $"Feature-producing op '{entry.OpName}' at index {index} reuses duplicate feature id '{featureId}'.")
                ]);
            }
        }

        return KernelResult<bool>.Success(true);
    }

    private static string GetReferenceFieldName(FirmamentKnownOpKind kind) =>
        kind switch
        {
            FirmamentKnownOpKind.Add => "to",
            FirmamentKnownOpKind.Subtract => "from",
            FirmamentKnownOpKind.Intersect => "left",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Boolean op kind must map to a feature reference field.")
        };

    private static KernelDiagnostic CreateDiagnostic(FirmamentDiagnosticCode code, string message) =>
        new(
            KernelDiagnosticCode.ValidationFailed,
            KernelDiagnosticSeverity.Error,
            $"[{code.Value}] {message}",
            FirmamentDiagnosticConventions.Source);
}
