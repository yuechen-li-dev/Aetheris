using System.Globalization;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentValidationRequiredFieldValidator
{
    public static KernelResult<bool> Validate(FirmamentParsedDocument parsedDocument)
    {
        ArgumentNullException.ThrowIfNull(parsedDocument);

        for (var index = 0; index < parsedDocument.Ops.Entries.Count; index++)
        {
            var entry = parsedDocument.Ops.Entries[index];
            if (entry.Family != FirmamentOpFamily.Validation)
            {
                continue;
            }

            var validationResult = entry.KnownKind switch
            {
                FirmamentKnownOpKind.ExpectExists => ValidateExpectExists(entry, index),
                FirmamentKnownOpKind.ExpectSelectable => ValidateExpectSelectable(entry, index),
                FirmamentKnownOpKind.ExpectManifold => ValidateExpectManifold(entry, index),
                _ => KernelResult<bool>.Success(true)
            };

            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateExpectExists(FirmamentParsedOpEntry entry, int opIndex)
    {
        if (!TryGetRequiredNonEmptyScalar(entry, "target", opIndex, out var missingOrTypeDiagnostic))
        {
            return KernelResult<bool>.Failure([missingOrTypeDiagnostic!]);
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateExpectManifold(FirmamentParsedOpEntry entry, int opIndex)
    {
        if (!TryGetRequiredNonEmptyScalar(entry, "target", opIndex, out var missingOrTypeDiagnostic))
        {
            return KernelResult<bool>.Failure([missingOrTypeDiagnostic!]);
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateExpectSelectable(FirmamentParsedOpEntry entry, int opIndex)
    {
        if (!TryGetRequiredNonEmptyScalar(entry, "target", opIndex, out var missingOrTypeDiagnostic))
        {
            return KernelResult<bool>.Failure([missingOrTypeDiagnostic!]);
        }

        if (!entry.RawFields.TryGetValue("count", out var countRaw) || string.IsNullOrWhiteSpace(countRaw))
        {
            return MissingField("count", opIndex, entry.OpName);
        }

        if (!TryParseNumeric(countRaw, out var countValue))
        {
            return InvalidFieldType("count", opIndex, entry.OpName, "expected a numeric scalar value");
        }

        if (Math.Abs(countValue - Math.Round(countValue)) > 0)
        {
            return InvalidFieldValue("count", opIndex, entry.OpName, "expected an integer-valued number greater than 0");
        }

        if (countValue <= 0)
        {
            return InvalidFieldValue("count", opIndex, entry.OpName, "expected an integer-valued number greater than 0");
        }

        return KernelResult<bool>.Success(true);
    }

    private static bool TryGetRequiredNonEmptyScalar(FirmamentParsedOpEntry entry, string fieldName, int opIndex, out KernelDiagnostic? diagnostic)
    {
        diagnostic = null;

        if (!entry.RawFields.TryGetValue(fieldName, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            diagnostic = MissingFieldDiagnostic(fieldName, opIndex, entry.OpName);
            return false;
        }

        if (LooksLikeStructuredValue(raw))
        {
            diagnostic = InvalidFieldTypeDiagnostic(fieldName, opIndex, entry.OpName, "expected a non-empty scalar/string-like value");
            return false;
        }

        return true;
    }

    private static bool LooksLikeStructuredValue(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static bool TryParseNumeric(string raw, out double value)
    {
        var trimmed = raw.Trim();
        if (LooksLikeStructuredValue(trimmed))
        {
            value = default;
            return false;
        }

        if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase))
        {
            value = default;
            return false;
        }

        if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            trimmed = trimmed[1..^1];
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static KernelResult<bool> MissingField(string fieldName, int opIndex, string opName) =>
        KernelResult<bool>.Failure([MissingFieldDiagnostic(fieldName, opIndex, opName)]);

    private static KernelDiagnostic MissingFieldDiagnostic(string fieldName, int opIndex, string opName) =>
        CreateDiagnostic(
            FirmamentDiagnosticCodes.ValidationMissingRequiredField,
            $"Validation op '{opName}' at index {opIndex} is missing required field '{fieldName}'.");

    private static KernelResult<bool> InvalidFieldType(string fieldName, int opIndex, string opName, string expectation) =>
        KernelResult<bool>.Failure([InvalidFieldTypeDiagnostic(fieldName, opIndex, opName, expectation)]);

    private static KernelDiagnostic InvalidFieldTypeDiagnostic(string fieldName, int opIndex, string opName, string expectation) =>
        CreateDiagnostic(
            FirmamentDiagnosticCodes.ValidationInvalidFieldTypeOrShape,
            $"Validation op '{opName}' at index {opIndex} has invalid field '{fieldName}'; {expectation}.");

    private static KernelResult<bool> InvalidFieldValue(string fieldName, int opIndex, string opName, string expectation) =>
        KernelResult<bool>.Failure(
            [CreateDiagnostic(
                FirmamentDiagnosticCodes.ValidationInvalidFieldValue,
                $"Validation op '{opName}' at index {opIndex} has invalid field '{fieldName}' value; {expectation}.")]);

    private static KernelDiagnostic CreateDiagnostic(FirmamentDiagnosticCode code, string message) =>
        new(
            KernelDiagnosticCode.ValidationFailed,
            KernelDiagnosticSeverity.Error,
            $"[{code.Value}] {message}",
            FirmamentDiagnosticConventions.Source);
}
