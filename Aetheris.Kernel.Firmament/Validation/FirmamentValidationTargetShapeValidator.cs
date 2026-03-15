using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentValidationTargetShapeValidator
{
    public static KernelResult<FirmamentParsedDocument> Validate(FirmamentParsedDocument parsedDocument)
    {
        ArgumentNullException.ThrowIfNull(parsedDocument);

        var updatedEntries = new List<FirmamentParsedOpEntry>(parsedDocument.Ops.Entries.Count);

        for (var index = 0; index < parsedDocument.Ops.Entries.Count; index++)
        {
            var entry = parsedDocument.Ops.Entries[index];
            if (entry.KnownKind is not (FirmamentKnownOpKind.ExpectExists or FirmamentKnownOpKind.ExpectSelectable))
            {
                updatedEntries.Add(entry);
                continue;
            }

            var targetRaw = entry.RawFields.TryGetValue("target", out var rawTarget) ? rawTarget : string.Empty;
            if (!FirmamentValidationTargetClassifier.TryClassify(targetRaw, out var targetShape))
            {
                return KernelResult<FirmamentParsedDocument>.Failure([
                    CreateDiagnostic(
                        FirmamentDiagnosticCodes.ValidationInvalidTargetShape,
                        $"Validation op '{entry.OpName}' at index {index} has malformed target '{targetRaw}'. Expected 'feature_id' or 'feature_id.port_name'.")
                ]);
            }

            var classifiedFields = entry.ClassifiedFields is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(entry.ClassifiedFields, StringComparer.Ordinal);

            classifiedFields["targetShape"] = targetShape.ToString();
            updatedEntries.Add(entry with { ClassifiedFields = classifiedFields });
        }

        var updatedDocument = parsedDocument with
        {
            Ops = parsedDocument.Ops with { Entries = updatedEntries }
        };

        return KernelResult<FirmamentParsedDocument>.Success(updatedDocument);
    }

    private static KernelDiagnostic CreateDiagnostic(FirmamentDiagnosticCode code, string message) =>
        new(
            KernelDiagnosticCode.ValidationFailed,
            KernelDiagnosticSeverity.Error,
            $"[{code.Value}] {message}",
            FirmamentDiagnosticConventions.Source);
}
