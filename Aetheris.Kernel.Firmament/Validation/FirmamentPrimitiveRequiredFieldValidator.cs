using System.Globalization;
using System.Text.Json;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentPrimitiveRequiredFieldValidator
{
    public static KernelResult<bool> Validate(FirmamentParsedDocument parsedDocument)
    {
        ArgumentNullException.ThrowIfNull(parsedDocument);

        for (var index = 0; index < parsedDocument.Ops.Entries.Count; index++)
        {
            var entry = parsedDocument.Ops.Entries[index];
            if (entry.Family != FirmamentOpFamily.Primitive)
            {
                continue;
            }

            var validationResult = entry.KnownKind switch
            {
                FirmamentKnownOpKind.Box => ValidateBox(entry, index),
                FirmamentKnownOpKind.Cylinder => ValidateCylinder(entry, index),
                FirmamentKnownOpKind.Sphere => ValidateSphere(entry, index),
                _ => KernelResult<bool>.Success(true)
            };

            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateBox(FirmamentParsedOpEntry entry, int opIndex)
    {
        if (!TryGetRequiredNonEmptyScalar(entry, "id", opIndex, out var missingOrTypeDiagnostic))
        {
            return KernelResult<bool>.Failure([missingOrTypeDiagnostic!]);
        }

        if (!entry.RawFields.TryGetValue("size", out var sizeRaw) || string.IsNullOrWhiteSpace(sizeRaw))
        {
            return MissingField("size", opIndex, entry.OpName);
        }

        if (!TryParseVector(sizeRaw, out var components))
        {
            return InvalidFieldType("size", opIndex, entry.OpName, "expected a 3-element numeric vector-like value");
        }

        if (components.Count != 3)
        {
            return InvalidFieldType("size", opIndex, entry.OpName, "expected exactly 3 numeric components");
        }

        for (var i = 0; i < components.Count; i++)
        {
            if (components[i] <= 0)
            {
                return InvalidFieldValue("size", opIndex, entry.OpName, "all components must be greater than 0");
            }
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateCylinder(FirmamentParsedOpEntry entry, int opIndex)
    {
        if (!TryGetRequiredNonEmptyScalar(entry, "id", opIndex, out var missingOrTypeDiagnostic))
        {
            return KernelResult<bool>.Failure([missingOrTypeDiagnostic!]);
        }

        var radiusResult = ValidatePositiveNumericField(entry, "radius", opIndex);
        if (!radiusResult.IsSuccess)
        {
            return radiusResult;
        }

        var heightResult = ValidatePositiveNumericField(entry, "height", opIndex);
        if (!heightResult.IsSuccess)
        {
            return heightResult;
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateSphere(FirmamentParsedOpEntry entry, int opIndex)
    {
        if (!TryGetRequiredNonEmptyScalar(entry, "id", opIndex, out var missingOrTypeDiagnostic))
        {
            return KernelResult<bool>.Failure([missingOrTypeDiagnostic!]);
        }

        var radiusResult = ValidatePositiveNumericField(entry, "radius", opIndex);
        if (!radiusResult.IsSuccess)
        {
            return radiusResult;
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

    private static KernelResult<bool> ValidatePositiveNumericField(FirmamentParsedOpEntry entry, string fieldName, int opIndex)
    {
        if (!entry.RawFields.TryGetValue(fieldName, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return MissingField(fieldName, opIndex, entry.OpName);
        }

        if (!TryParseNumeric(raw, out var value))
        {
            return InvalidFieldType(fieldName, opIndex, entry.OpName, "expected a numeric scalar value");
        }

        if (value <= 0)
        {
            return InvalidFieldValue(fieldName, opIndex, entry.OpName, "expected a numeric value greater than 0");
        }

        return KernelResult<bool>.Success(true);
    }

    private static bool LooksLikeStructuredValue(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static bool TryParseNumeric(string raw, out double value)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            trimmed = trimmed[1..^1];
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseVector(string raw, out List<double> components)
    {
        components = [];

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Number)
                {
                    return false;
                }

                components.Add(element.GetDouble());
            }

            return true;
        }
        catch (JsonException)
        {
            var trimmed = raw.Trim();
            if (!trimmed.StartsWith("[", StringComparison.Ordinal) || !trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                return false;
            }

            var content = trimmed[1..^1];
            var parts = content.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (!TryParseNumeric(part, out var value))
                {
                    return false;
                }

                components.Add(value);
            }

            return true;
        }
    }

    private static KernelResult<bool> MissingField(string fieldName, int opIndex, string opName) =>
        KernelResult<bool>.Failure([MissingFieldDiagnostic(fieldName, opIndex, opName)]);

    private static KernelDiagnostic MissingFieldDiagnostic(string fieldName, int opIndex, string opName) =>
        CreateDiagnostic(
            FirmamentDiagnosticCodes.PrimitiveMissingRequiredField,
            $"Primitive op '{opName}' at index {opIndex} is missing required field '{fieldName}'.");

    private static KernelResult<bool> InvalidFieldType(string fieldName, int opIndex, string opName, string expectation) =>
        KernelResult<bool>.Failure([InvalidFieldTypeDiagnostic(fieldName, opIndex, opName, expectation)]);

    private static KernelDiagnostic InvalidFieldTypeDiagnostic(string fieldName, int opIndex, string opName, string expectation) =>
        CreateDiagnostic(
            FirmamentDiagnosticCodes.PrimitiveInvalidFieldTypeOrShape,
            $"Primitive op '{opName}' at index {opIndex} has invalid field '{fieldName}'; {expectation}.");

    private static KernelResult<bool> InvalidFieldValue(string fieldName, int opIndex, string opName, string expectation) =>
        KernelResult<bool>.Failure(
            [CreateDiagnostic(
                FirmamentDiagnosticCodes.PrimitiveInvalidFieldValue,
                $"Primitive op '{opName}' at index {opIndex} has invalid field '{fieldName}' value; {expectation}.")]);

    private static KernelDiagnostic CreateDiagnostic(FirmamentDiagnosticCode code, string message) =>
        new(
            KernelDiagnosticCode.ValidationFailed,
            KernelDiagnosticSeverity.Error,
            $"[{code.Value}] {message}",
            FirmamentDiagnosticConventions.Source);
}
