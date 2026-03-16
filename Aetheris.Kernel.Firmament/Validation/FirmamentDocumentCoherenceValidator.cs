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
        var featureKindsById = new Dictionary<string, FirmamentKnownOpKind>(StringComparer.Ordinal);

        for (var index = 0; index < parsedDocument.Ops.Entries.Count; index++)
        {
            var entry = parsedDocument.Ops.Entries[index];

            if (entry.Family == FirmamentOpFamily.Validation)
            {
                ValidateValidationTargetReference(entry, index, featureIds, featureKindsById, out var diagnostic);
                if (diagnostic is not null)
                {
                    return KernelResult<bool>.Failure([diagnostic]);
                }

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

            featureKindsById[featureId] = entry.KnownKind;
        }

        return KernelResult<bool>.Success(true);
    }

    private static void ValidateValidationTargetReference(
        FirmamentParsedOpEntry entry,
        int index,
        HashSet<string> featureIds,
        IReadOnlyDictionary<string, FirmamentKnownOpKind> featureKindsById,
        out KernelDiagnostic? diagnostic)
    {
        diagnostic = null;

        if (entry.KnownKind is not (FirmamentKnownOpKind.ExpectExists or FirmamentKnownOpKind.ExpectSelectable)
            || !entry.RawFields.TryGetValue("target", out var targetRaw))
        {
            return;
        }

        var targetShape = entry.ClassifiedFields is not null && entry.ClassifiedFields.TryGetValue("targetShape", out var shape)
            ? shape
            : null;

        if (string.Equals(targetShape, FirmamentValidationTargetShape.FeatureId.ToString(), StringComparison.Ordinal))
        {
            if (!featureIds.Contains(targetRaw))
            {
                diagnostic = CreateDiagnostic(
                    FirmamentDiagnosticCodes.ValidationTargetUnknownFeatureId,
                    $"Validation op '{entry.OpName}' at index {index} references unknown feature id '{targetRaw}' via field 'target'.");
            }

            return;
        }

        if (!string.Equals(targetShape, FirmamentValidationTargetShape.SelectorShaped.ToString(), StringComparison.Ordinal))
        {
            return;
        }

        var rootFeatureId = ExtractSelectorRoot(targetRaw);
        if (!featureIds.Contains(rootFeatureId))
        {
            diagnostic = CreateDiagnostic(
                FirmamentDiagnosticCodes.ValidationTargetUnknownSelectorRootFeatureId,
                $"Validation op '{entry.OpName}' at index {index} references unknown selector root feature id '{rootFeatureId}' via field 'target'.");
            return;
        }

        var portToken = ExtractSelectorPort(targetRaw);
        if (!FirmamentValidationTargetClassifier.IsValidIdentifier(portToken))
        {
            diagnostic = CreateDiagnostic(
                FirmamentDiagnosticCodes.ValidationTargetInvalidSelectorPortToken,
                $"Validation op '{entry.OpName}' at index {index} has invalid selector port token '{portToken}' via field 'target'.");
            return;
        }

        if (!featureKindsById.TryGetValue(rootFeatureId, out var rootFeatureKind))
        {
            return;
        }

        if (!FirmamentPrimitiveSelectorContracts.TryGetAllowedPorts(rootFeatureKind, out var allowedPorts))
        {
            return;
        }

        if (!allowedPorts.Contains(portToken))
        {
            diagnostic = CreateDiagnostic(
                FirmamentDiagnosticCodes.ValidationTargetSelectorPortNotAllowedForFeatureKind,
                $"Validation op '{entry.OpName}' at index {index} has selector port '{portToken}' not allowed for feature kind '{rootFeatureKind.ToString().ToLowerInvariant()}' on feature id '{rootFeatureId}' via field 'target'.");
        }
    }

    private static string ExtractSelectorRoot(string target)
    {
        var separatorIndex = target.IndexOf('.', StringComparison.Ordinal);
        return separatorIndex > 0
            ? target[..separatorIndex]
            : target;
    }

    private static string ExtractSelectorPort(string target)
    {
        var separatorIndex = target.IndexOf('.', StringComparison.Ordinal);
        return separatorIndex >= 0 && separatorIndex < target.Length - 1
            ? target[(separatorIndex + 1)..]
            : string.Empty;
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
