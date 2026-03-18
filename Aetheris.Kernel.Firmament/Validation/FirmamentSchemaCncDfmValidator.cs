using System.Globalization;
using System.Text.Json;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.CompiledModel;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentSchemaCncDfmValidator
{
    public static KernelResult<bool> Validate(FirmamentParsedDocument parsedDocument, FirmamentCompiledSchema? compiledSchema)
    {
        ArgumentNullException.ThrowIfNull(parsedDocument);

        if (compiledSchema?.Process != FirmamentCompiledSchemaProcess.Cnc
            || compiledSchema.Payload is not FirmamentCompiledCncSchema cncSchema)
        {
            return KernelResult<bool>.Success(true);
        }

        for (var index = 0; index < parsedDocument.Ops.Entries.Count; index++)
        {
            var entry = parsedDocument.Ops.Entries[index];
            if (entry.KnownKind != FirmamentKnownOpKind.Subtract)
            {
                continue;
            }

            if (!entry.RawFields.TryGetValue("with", out var withRaw)
                || string.IsNullOrWhiteSpace(withRaw)
                || !TryGetWithFields(withRaw, out var withFields)
                || !withFields.TryGetValue("op", out var withOpRaw)
                || !string.Equals(withOpRaw, "cylinder", StringComparison.Ordinal))
            {
                continue;
            }

            if (!withFields.TryGetValue("radius", out var radiusRaw)
                || !double.TryParse(radiusRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var radius))
            {
                continue;
            }

            if (radius >= cncSchema.MinimumToolRadius)
            {
                continue;
            }

            var featureId = entry.RawFields.TryGetValue("id", out var rawId) && !string.IsNullOrWhiteSpace(rawId)
                ? rawId
                : $"op_index_{index}";

            return KernelResult<bool>.Failure([
                CreateDiagnostic(
                    FirmamentDiagnosticCodes.SchemaCncMinimumToolRadiusViolated,
                    $"CNC minimum_tool_radius {FormatNumeric(cncSchema.MinimumToolRadius)} exceeds subtract tool radius {FormatNumeric(radius)} for feature '{featureId}'.")]);
        }

        return KernelResult<bool>.Success(true);
    }

    private static string FormatNumeric(double value) =>
        value.ToString("0.0###############", CultureInfo.InvariantCulture);

    private static bool TryGetWithFields(string raw, out Dictionary<string, string> fields)
    {
        fields = new Dictionary<string, string>(StringComparer.Ordinal);

        if (TryParseJsonObject(raw, out var objectElement))
        {
            foreach (var property in objectElement.EnumerateObject())
            {
                fields[property.Name] = property.Value.ToString();
            }

            return true;
        }

        return TryParseToonInlineObject(raw, out fields);
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

        foreach (var part in SplitTopLevelCommaSeparated(inner))
        {
            var separator = part.IndexOf(':');
            if (separator <= 0)
            {
                return false;
            }

            var name = NormalizeArrayFieldName(part[..separator].Trim());
            var value = part[(separator + 1)..].Trim();
            if (name.Length == 0)
            {
                return false;
            }

            fields[name] = value;
        }

        return true;
    }

    private static List<string> SplitTopLevelCommaSeparated(string input)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            switch (ch)
            {
                case '{':
                case '[':
                    depth++;
                    break;
                case '}':
                case ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    parts.Add(input[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }

        parts.Add(input[start..].Trim());
        return parts;
    }

    private static string NormalizeArrayFieldName(string fieldName)
    {
        var bracketIndex = fieldName.IndexOf('[', StringComparison.Ordinal);
        return bracketIndex >= 0 ? fieldName[..bracketIndex] : fieldName;
    }

    private static KernelDiagnostic CreateDiagnostic(FirmamentDiagnosticCode code, string message) =>
        new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, $"[{code.Value}] {message}", FirmamentDiagnosticConventions.Source);
}
