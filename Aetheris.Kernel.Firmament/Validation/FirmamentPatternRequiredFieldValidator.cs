using System.Globalization;
using System.Text.Json;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentPatternRequiredFieldValidator
{
    public static KernelResult<bool> Validate(FirmamentParsedDocument parsedDocument)
    {
        ArgumentNullException.ThrowIfNull(parsedDocument);

        for (var index = 0; index < parsedDocument.Ops.Entries.Count; index++)
        {
            var entry = parsedDocument.Ops.Entries[index];
            if (entry.Family != FirmamentOpFamily.Pattern)
            {
                continue;
            }

            var result = entry.KnownKind switch
            {
                FirmamentKnownOpKind.PatternLinear => ValidateLinear(entry, index),
                FirmamentKnownOpKind.PatternCircular => ValidateCircular(entry, index),
                _ => KernelResult<bool>.Success(true)
            };

            if (!result.IsSuccess)
            {
                return result;
            }
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateLinear(FirmamentParsedOpEntry entry, int opIndex)
    {
        if (!TryGetRequiredScalar(entry, "source", opIndex, out var sourceDiagnostic))
        {
            return KernelResult<bool>.Failure([sourceDiagnostic!]);
        }

        var countResult = ValidatePositiveIntegerField(entry, "count", opIndex);
        if (!countResult.IsSuccess)
        {
            return countResult;
        }

        if (!entry.RawFields.TryGetValue("step", out var stepRaw) || string.IsNullOrWhiteSpace(stepRaw))
        {
            return MissingField("step", opIndex, entry.OpName);
        }

        if (!TryParseNumericVector(stepRaw, out var components) || components.Count != 3)
        {
            return InvalidFieldTypeOrShape("step", opIndex, entry.OpName, "expected exactly 3 numeric components");
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateCircular(FirmamentParsedOpEntry entry, int opIndex)
    {
        if (!TryGetRequiredScalar(entry, "source", opIndex, out var sourceDiagnostic))
        {
            return KernelResult<bool>.Failure([sourceDiagnostic!]);
        }

        var countResult = ValidatePositiveIntegerField(entry, "count", opIndex);
        if (!countResult.IsSuccess)
        {
            return countResult;
        }

        if (!TryGetRequiredScalar(entry, "axis", opIndex, out var axisDiagnostic))
        {
            return KernelResult<bool>.Failure([axisDiagnostic!]);
        }

        var hasSpan = entry.RawFields.TryGetValue("angle_degrees", out var spanRaw) && !string.IsNullOrWhiteSpace(spanRaw);
        var hasStep = entry.RawFields.TryGetValue("angle_step_degrees", out var stepRaw) && !string.IsNullOrWhiteSpace(stepRaw);

        if (!hasSpan && !hasStep)
        {
            return MissingField("angle_degrees", opIndex, entry.OpName);
        }

        if (hasSpan && hasStep)
        {
            return InvalidFieldValue("angle_degrees", opIndex, entry.OpName, "specify either 'angle_degrees' (span) or 'angle_step_degrees', not both");
        }

        if (hasSpan && !TryParseNumeric(spanRaw!, out _))
        {
            return InvalidFieldTypeOrShape("angle_degrees", opIndex, entry.OpName, "expected a numeric scalar value");
        }

        if (hasStep && !TryParseNumeric(stepRaw!, out _))
        {
            return InvalidFieldTypeOrShape("angle_step_degrees", opIndex, entry.OpName, "expected a numeric scalar value");
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidatePositiveIntegerField(FirmamentParsedOpEntry entry, string fieldName, int opIndex)
    {
        if (!entry.RawFields.TryGetValue(fieldName, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return MissingField(fieldName, opIndex, entry.OpName);
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return InvalidFieldTypeOrShape(fieldName, opIndex, entry.OpName, "expected an integer scalar value");
        }

        if (value <= 0)
        {
            return InvalidFieldValue(fieldName, opIndex, entry.OpName, "expected an integer value greater than 0");
        }

        return KernelResult<bool>.Success(true);
    }

    private static bool TryGetRequiredScalar(FirmamentParsedOpEntry entry, string fieldName, int opIndex, out KernelDiagnostic? diagnostic)
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

    private static bool LooksLikeStructuredValue(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed.StartsWith("{", StringComparison.Ordinal)
               || trimmed.StartsWith("[", StringComparison.Ordinal)
               || trimmed.EndsWith(":", StringComparison.Ordinal);
    }

    private static bool TryParseNumericVector(string raw, out IReadOnlyList<double> values)
    {
        values = Array.Empty<double>();
        var trimmed = raw.Trim();

        if (!trimmed.StartsWith("[", StringComparison.Ordinal) || !trimmed.EndsWith("]", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var parsed = new List<double>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var token = item.ToString();
                if (!TryParseNumeric(token, out var value))
                {
                    return false;
                }

                parsed.Add(value);
            }

            values = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseNumeric(string raw, out double value)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length >= 2)
        {
            trimmed = trimmed[1..^1];
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static KernelResult<bool> MissingField(string fieldName, int opIndex, string opName) =>
        KernelResult<bool>.Failure([MissingFieldDiagnostic(fieldName, opIndex, opName)]);

    private static KernelResult<bool> InvalidFieldTypeOrShape(string fieldName, int opIndex, string opName, string expectation) =>
        KernelResult<bool>.Failure([InvalidFieldTypeOrShapeDiagnostic(fieldName, opIndex, opName, expectation)]);

    private static KernelResult<bool> InvalidFieldValue(string fieldName, int opIndex, string opName, string expectation) =>
        KernelResult<bool>.Failure([
            CreateDiagnostic(
                FirmamentDiagnosticCodes.PatternInvalidFieldValue,
                $"Pattern op '{opName}' at index {opIndex} has invalid field '{fieldName}' value; {expectation}.")]);

    private static KernelDiagnostic MissingFieldDiagnostic(string fieldName, int opIndex, string opName) =>
        CreateDiagnostic(
            FirmamentDiagnosticCodes.PatternMissingRequiredField,
            $"Pattern op '{opName}' at index {opIndex} is missing required field '{fieldName}'.");

    private static KernelDiagnostic InvalidFieldTypeOrShapeDiagnostic(string fieldName, int opIndex, string opName, string expectation) =>
        CreateDiagnostic(
            FirmamentDiagnosticCodes.PatternInvalidFieldTypeOrShape,
            $"Pattern op '{opName}' at index {opIndex} has invalid field '{fieldName}'; {expectation}.");

    private static KernelDiagnostic CreateDiagnostic(FirmamentDiagnosticCode code, string message) =>
        new(
            KernelDiagnosticCode.ValidationFailed,
            KernelDiagnosticSeverity.Error,
            $"[{code.Value}] {message}",
            Source: FirmamentDiagnosticConventions.Source);
}
