using System.Text.Json;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentBooleanRequiredFieldValidator
{
    public static KernelResult<bool> Validate(FirmamentParsedDocument parsedDocument)
    {
        ArgumentNullException.ThrowIfNull(parsedDocument);

        for (var index = 0; index < parsedDocument.Ops.Entries.Count; index++)
        {
            var entry = parsedDocument.Ops.Entries[index];
            if (entry.Family != FirmamentOpFamily.Boolean)
            {
                continue;
            }

            var validationResult = entry.KnownKind switch
            {
                FirmamentKnownOpKind.Add => ValidateBooleanEntry(entry, index, targetFieldName: "to"),
                FirmamentKnownOpKind.Subtract => ValidateBooleanEntry(entry, index, targetFieldName: "from"),
                FirmamentKnownOpKind.Intersect => ValidateBooleanEntry(entry, index, targetFieldName: "left"),
                _ => KernelResult<bool>.Success(true)
            };

            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateBooleanEntry(FirmamentParsedOpEntry entry, int opIndex, string targetFieldName)
    {
        if (!TryGetRequiredNonEmptyScalar(entry, "id", opIndex, out var missingOrTypeDiagnostic))
        {
            return KernelResult<bool>.Failure([missingOrTypeDiagnostic!]);
        }

        if (!TryGetRequiredNonEmptyScalar(entry, targetFieldName, opIndex, out missingOrTypeDiagnostic))
        {
            return KernelResult<bool>.Failure([missingOrTypeDiagnostic!]);
        }

        if (!entry.RawFields.TryGetValue("with", out var withRaw) || string.IsNullOrWhiteSpace(withRaw))
        {
            return MissingField("with", opIndex, entry.OpName);
        }

        if (!TryGetWithOpRaw(withRaw, out var withOpRaw, out var isMappingLike))
        {
            return InvalidFieldTypeOrShape("with", opIndex, entry.OpName, "expected an object-like mapping");
        }

        if (!isMappingLike || string.IsNullOrWhiteSpace(withOpRaw) || LooksLikeStructuredValue(withOpRaw))
        {
            return InvalidFieldTypeOrShape("with.op", opIndex, entry.OpName, "expected a non-empty scalar/string-like value");
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
            diagnostic = InvalidFieldTypeOrShapeDiagnostic(fieldName, opIndex, entry.OpName, "expected a non-empty scalar/string-like value");
            return false;
        }

        return true;
    }

    private static bool TryGetWithOpRaw(string raw, out string withOpRaw, out bool isMappingLike)
    {
        withOpRaw = string.Empty;
        isMappingLike = false;

        if (TryParseJsonObject(raw, out var objectElement))
        {
            isMappingLike = true;
            if (!objectElement.TryGetProperty("op", out var nestedOpElement))
            {
                return true;
            }

            withOpRaw = nestedOpElement.ToString();
            return true;
        }

        if (TryParseToonInlineObject(raw, out var fields))
        {
            isMappingLike = true;
            if (!fields.TryGetValue("op", out withOpRaw))
            {
                withOpRaw = string.Empty;
            }

            return true;
        }

        return false;
    }

    private static bool TryParseJsonObject(string raw, out JsonElement objectElement)
    {
        objectElement = default;

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            objectElement = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseToonInlineObject(string raw, out Dictionary<string, string> fields)
    {
        fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return false;
        }

        var inner = trimmed[1..^1].Trim();
        if (inner.Length == 0)
        {
            return true;
        }

        var parts = inner.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var separator = part.IndexOf(':');
            if (separator <= 0)
            {
                return false;
            }

            var name = part[..separator].Trim();
            var value = part[(separator + 1)..].Trim();
            if (name.Length == 0)
            {
                return false;
            }

            fields[name] = value;
        }

        return true;
    }

    private static bool LooksLikeStructuredValue(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static KernelResult<bool> MissingField(string fieldName, int opIndex, string opName) =>
        KernelResult<bool>.Failure([MissingFieldDiagnostic(fieldName, opIndex, opName)]);

    private static KernelDiagnostic MissingFieldDiagnostic(string fieldName, int opIndex, string opName) =>
        CreateDiagnostic(
            FirmamentDiagnosticCodes.BooleanMissingRequiredField,
            $"Boolean op '{opName}' at index {opIndex} is missing required field '{fieldName}'.");

    private static KernelResult<bool> InvalidFieldTypeOrShape(string fieldName, int opIndex, string opName, string expectation) =>
        KernelResult<bool>.Failure([InvalidFieldTypeOrShapeDiagnostic(fieldName, opIndex, opName, expectation)]);

    private static KernelDiagnostic InvalidFieldTypeOrShapeDiagnostic(string fieldName, int opIndex, string opName, string expectation) =>
        CreateDiagnostic(
            FirmamentDiagnosticCodes.BooleanInvalidFieldTypeOrShape,
            $"Boolean op '{opName}' at index {opIndex} has invalid field '{fieldName}'; {expectation}.");

    private static KernelDiagnostic CreateDiagnostic(FirmamentDiagnosticCode code, string message) =>
        new(
            KernelDiagnosticCode.ValidationFailed,
            KernelDiagnosticSeverity.Error,
            $"[{code.Value}] {message}",
            FirmamentDiagnosticConventions.Source);
}
