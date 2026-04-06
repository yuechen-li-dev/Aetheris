using System.Globalization;
using System.Linq;
using System.Text.Json;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.Execution;
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

        if (!TryGetWithFields(withRaw, out var withFields, out var isMappingLike))
        {
            return InvalidFieldTypeOrShape("with", opIndex, entry.OpName, "expected an object-like mapping");
        }

        if (!isMappingLike || !withFields.TryGetValue("op", out var withOpRaw) || string.IsNullOrWhiteSpace(withOpRaw) || LooksLikeStructuredValue(withOpRaw))
        {
            return InvalidFieldTypeOrShape("with.op", opIndex, entry.OpName, "expected a non-empty scalar/string-like value");
        }

        var toolValidationResult = ValidateNestedPrimitiveToolFields(entry, opIndex, withOpRaw, withFields);
        if (!toolValidationResult.IsSuccess)
        {
            return toolValidationResult;
        }

        return ValidatePlacement(entry, opIndex);
    }

    private static KernelResult<bool> ValidateNestedPrimitiveToolFields(FirmamentParsedOpEntry entry, int opIndex, string withOpRaw, IReadOnlyDictionary<string, string> withFields)
    {
        if (string.Equals(withOpRaw, "box", StringComparison.Ordinal))
        {
            return ValidateBoxTool(entry, opIndex, withFields);
        }

        if (string.Equals(withOpRaw, "cylinder", StringComparison.Ordinal))
        {
            return ValidateCylinderTool(entry, opIndex, withFields);
        }

        if (string.Equals(withOpRaw, "sphere", StringComparison.Ordinal))
        {
            return ValidateSphereTool(entry, opIndex, withFields);
        }

        if (string.Equals(withOpRaw, "cone", StringComparison.Ordinal))
        {
            return ValidateConeTool(entry, opIndex, withFields);
        }

        if (string.Equals(withOpRaw, "torus", StringComparison.Ordinal))
        {
            return ValidateTorusTool(entry, opIndex, withFields);
        }

        if (FirmamentPrismFamilyTools.TryGetDescriptor(withOpRaw, out var prismTool))
        {
            return ValidatePrismTool(entry, opIndex, withFields, prismTool);
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateBoxTool(FirmamentParsedOpEntry entry, int opIndex, IReadOnlyDictionary<string, string> withFields)
    {
        if (!withFields.TryGetValue("size", out var sizeRaw) || string.IsNullOrWhiteSpace(sizeRaw))
        {
            return MissingField("with.size", opIndex, entry.OpName);
        }

        if (!TryParseVector(sizeRaw, out var components))
        {
            return InvalidFieldTypeOrShape("with.size", opIndex, entry.OpName, "expected a 3-element numeric vector-like value");
        }

        if (components.Count != 3)
        {
            return InvalidFieldTypeOrShape("with.size", opIndex, entry.OpName, "expected exactly 3 numeric components");
        }

        for (var i = 0; i < components.Count; i++)
        {
            if (components[i] <= 0)
            {
                return InvalidFieldValue("with.size", opIndex, entry.OpName, "all components must be greater than 0");
            }
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateCylinderTool(FirmamentParsedOpEntry entry, int opIndex, IReadOnlyDictionary<string, string> withFields)
    {
        var radiusResult = ValidatePositiveNumericWithField(entry, opIndex, withFields, "radius");
        if (!radiusResult.IsSuccess)
        {
            return radiusResult;
        }

        return ValidatePositiveNumericWithField(entry, opIndex, withFields, "height");
    }

    private static KernelResult<bool> ValidateSphereTool(FirmamentParsedOpEntry entry, int opIndex, IReadOnlyDictionary<string, string> withFields)
        => ValidatePositiveNumericWithField(entry, opIndex, withFields, "radius");

    private static KernelResult<bool> ValidateConeTool(FirmamentParsedOpEntry entry, int opIndex, IReadOnlyDictionary<string, string> withFields)
    {
        var bottomRadiusResult = ValidateNonNegativeNumericWithField(entry, opIndex, withFields, "bottom_radius");
        if (!bottomRadiusResult.IsSuccess)
        {
            return bottomRadiusResult;
        }

        var topRadiusResult = ValidateNonNegativeNumericWithField(entry, opIndex, withFields, "top_radius");
        if (!topRadiusResult.IsSuccess)
        {
            return topRadiusResult;
        }

        var heightResult = ValidatePositiveNumericWithField(entry, opIndex, withFields, "height");
        if (!heightResult.IsSuccess)
        {
            return heightResult;
        }

        _ = TryParseNumeric(withFields["bottom_radius"], out var bottomRadius);
        _ = TryParseNumeric(withFields["top_radius"], out var topRadius);
        if (bottomRadius <= 1e-12d && topRadius <= 1e-12d)
        {
            return InvalidFieldValue("with.top_radius", opIndex, entry.OpName, "expected at least one of 'with.bottom_radius' or 'with.top_radius' to be greater than 0");
        }

        if (bottomRadius > 1e-12d && topRadius > 1e-12d && double.Abs(bottomRadius - topRadius) <= 1e-12d)
        {
            return InvalidFieldValue("with.top_radius", opIndex, entry.OpName, "expected a numeric value different from 'with.bottom_radius' when both cone radii are greater than 0");
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateTorusTool(FirmamentParsedOpEntry entry, int opIndex, IReadOnlyDictionary<string, string> withFields)
    {
        var majorRadiusResult = ValidatePositiveNumericWithField(entry, opIndex, withFields, "major_radius");
        if (!majorRadiusResult.IsSuccess)
        {
            return majorRadiusResult;
        }

        var minorRadiusResult = ValidatePositiveNumericWithField(entry, opIndex, withFields, "minor_radius");
        if (!minorRadiusResult.IsSuccess)
        {
            return minorRadiusResult;
        }

        _ = TryParseNumeric(withFields["major_radius"], out var majorRadius);
        _ = TryParseNumeric(withFields["minor_radius"], out var minorRadius);
        if (majorRadius <= minorRadius)
        {
            return InvalidFieldValue("with.major_radius", opIndex, entry.OpName, "expected a numeric value greater than 'with.minor_radius'");
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidatePrismTool(
        FirmamentParsedOpEntry entry,
        int opIndex,
        IReadOnlyDictionary<string, string> withFields,
        FirmamentPrismToolDescriptor prismTool)
    {
        foreach (var requiredField in prismTool.RequiredFields)
        {
            var requiredResult = ValidatePositiveNumericWithField(entry, opIndex, withFields, requiredField);
            if (!requiredResult.IsSuccess)
            {
                return requiredResult;
            }
        }

        if (prismTool.Kind == FirmamentPrismToolKind.StraightSlot)
        {
            _ = TryParseNumeric(withFields["length"], out var length);
            _ = TryParseNumeric(withFields["width"], out var width);
            if (length <= width)
            {
                return InvalidFieldValue("with.length", opIndex, entry.OpName, "expected a numeric value greater than 'with.width' for straight_slot");
            }
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidatePositiveNumericWithField(FirmamentParsedOpEntry entry, int opIndex, IReadOnlyDictionary<string, string> withFields, string fieldName)
    {
        var qualifiedFieldName = $"with.{fieldName}";
        if (!withFields.TryGetValue(fieldName, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return MissingField(qualifiedFieldName, opIndex, entry.OpName);
        }

        if (!TryParseNumeric(raw, out var value))
        {
            return InvalidFieldTypeOrShape(qualifiedFieldName, opIndex, entry.OpName, "expected a numeric scalar value");
        }

        if (value <= 0)
        {
            return InvalidFieldValue(qualifiedFieldName, opIndex, entry.OpName, "expected a numeric value greater than 0");
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateNonNegativeNumericWithField(FirmamentParsedOpEntry entry, int opIndex, IReadOnlyDictionary<string, string> withFields, string fieldName)
    {
        var qualifiedFieldName = $"with.{fieldName}";
        if (!withFields.TryGetValue(fieldName, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return MissingField(qualifiedFieldName, opIndex, entry.OpName);
        }

        if (!TryParseNumeric(raw, out var value))
        {
            return InvalidFieldTypeOrShape(qualifiedFieldName, opIndex, entry.OpName, "expected a numeric scalar value");
        }

        if (value < 0)
        {
            return InvalidFieldValue(qualifiedFieldName, opIndex, entry.OpName, "expected a numeric value greater than or equal to 0");
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

    private static bool TryGetWithFields(string raw, out Dictionary<string, string> fields, out bool isMappingLike)
    {
        fields = new Dictionary<string, string>(StringComparer.Ordinal);
        isMappingLike = false;

        if (TryParseJsonObject(raw, out var objectElement))
        {
            isMappingLike = true;
            foreach (var property in objectElement.EnumerateObject())
            {
                fields[property.Name] = property.Value.ToString();
            }

            return true;
        }

        if (TryParseToonInlineObject(raw, out fields))
        {
            isMappingLike = true;
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

        var parts = SplitTopLevelCommaSeparated(inner);
        foreach (var part in parts)
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

    private static IReadOnlyList<string> SplitTopLevelCommaSeparated(string inner)
    {
        var parts = new List<string>();
        var start = 0;
        var squareDepth = 0;
        var curlyDepth = 0;

        for (var i = 0; i < inner.Length; i++)
        {
            var ch = inner[i];
            switch (ch)
            {
                case '[':
                    squareDepth++;
                    break;
                case ']':
                    squareDepth = Math.Max(0, squareDepth - 1);
                    break;
                case '{':
                    curlyDepth++;
                    break;
                case '}':
                    curlyDepth = Math.Max(0, curlyDepth - 1);
                    break;
                case ',':
                    if (squareDepth == 0 && curlyDepth == 0)
                    {
                        var segment = inner[start..i].Trim();
                        if (segment.Length > 0)
                        {
                            parts.Add(segment);
                        }

                        start = i + 1;
                    }

                    break;
            }
        }

        var last = inner[start..].Trim();
        if (last.Length > 0)
        {
            parts.Add(last);
        }

        return parts;
    }

    private static string NormalizeArrayFieldName(string fieldName)
    {
        var bracketIndex = fieldName.IndexOf('[', StringComparison.Ordinal);
        return bracketIndex > 0 && fieldName.EndsWith(']')
            ? fieldName[..bracketIndex]
            : fieldName;
    }

    private static bool LooksLikeStructuredValue(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
    }


    private static KernelResult<bool> ValidatePlacement(FirmamentParsedOpEntry entry, int opIndex)
    {
        if (!entry.RawFields.ContainsKey("place"))
        {
            return KernelResult<bool>.Success(true);
        }

        if (entry.Placement is null)
        {
            return KernelResult<bool>.Failure([
                CreateDiagnostic(
                    FirmamentDiagnosticCodes.PlacementMissingRequiredField,
                    $"Boolean op '{entry.OpName}' at index {opIndex} has invalid field 'place'; expected object-like placement block.")]);
        }

        if (entry.Placement.UnknownFields.Count > 0)
        {
            return KernelResult<bool>.Failure([
                CreateDiagnostic(
                    FirmamentDiagnosticCodes.PlacementUnsupportedSemanticField,
                    $"Boolean op '{entry.OpName}' at index {opIndex} uses unsupported placement semantic field(s): {string.Join(", ", entry.Placement.UnknownFields.Select(f => $"'{f}'"))}.")]);
        }

        var usesSemantic = !string.IsNullOrWhiteSpace(entry.Placement.OnFace)
                           || !string.IsNullOrWhiteSpace(entry.Placement.CenteredOn)
                           || !string.IsNullOrWhiteSpace(entry.Placement.AroundAxis)
                           || entry.Placement.RadialOffset is not null
                           || entry.Placement.AngleDegrees is not null;

        if (!usesSemantic && entry.Placement.On is null)
        {
            return KernelResult<bool>.Failure([
                CreateDiagnostic(
                    FirmamentDiagnosticCodes.PlacementMissingRequiredField,
                    $"Boolean op '{entry.OpName}' at index {opIndex} is missing required field 'place.on'.")]);
        }

        if (!usesSemantic)
        {
            entry.RawFields.TryGetValue("place", out var placeRaw);
            var hasOffset = false;
            _ = TryReadPlacementFieldPresence(placeRaw ?? string.Empty, out _, out hasOffset);
            if (!hasOffset)
            {
                return KernelResult<bool>.Failure([
                    CreateDiagnostic(
                        FirmamentDiagnosticCodes.PlacementMissingRequiredField,
                        $"Boolean op '{entry.OpName}' at index {opIndex} is missing required field 'place.offset'.")]);
            }
        }

        if (entry.Placement.On is FirmamentParsedPlacementSelectorAnchor selectorAnchor)
        {
            if (!FirmamentValidationTargetClassifier.TryClassify(selectorAnchor.Selector, out var shape)
                || shape != FirmamentValidationTargetShape.SelectorShaped)
            {
                return KernelResult<bool>.Failure([
                    CreateDiagnostic(
                        FirmamentDiagnosticCodes.PlacementInvalidAnchorShape,
                        $"Boolean op '{entry.OpName}' at index {opIndex} has invalid placement anchor in field 'place.on'; expected 'origin' or selector-shaped token 'feature.port'.")]);
            }
        }
        else if (entry.Placement.On is not null && entry.Placement.On is not FirmamentParsedPlacementOriginAnchor)
        {
            return KernelResult<bool>.Failure([
                CreateDiagnostic(
                    FirmamentDiagnosticCodes.PlacementInvalidAnchorShape,
                    $"Boolean op '{entry.OpName}' at index {opIndex} has invalid placement anchor in field 'place.on'; expected 'origin' or selector-shaped token 'feature.port'.")]);
        }

        var semanticSelectorError = ValidateSemanticSelectorField(entry.Placement.OnFace, "place.on_face", entry, opIndex)
                                    ?? ValidateSemanticSelectorField(entry.Placement.CenteredOn, "place.centered_on", entry, opIndex)
                                    ?? ValidateSemanticSelectorField(entry.Placement.AroundAxis, "place.around_axis", entry, opIndex);
        if (semanticSelectorError is not null)
        {
            return KernelResult<bool>.Failure([semanticSelectorError]);
        }

        if (entry.Placement.RadialOffset is not null && string.IsNullOrWhiteSpace(entry.Placement.AroundAxis))
        {
            return KernelResult<bool>.Failure([
                CreateDiagnostic(
                    FirmamentDiagnosticCodes.PlacementInvalidSemanticCombination,
                    $"Boolean op '{entry.OpName}' at index {opIndex} has invalid placement semantics; 'place.radial_offset' requires 'place.around_axis'.")]);
        }

        if (entry.RawFields.TryGetValue("place", out var placeRawForOffset)
            && TryReadOffsetRawTokens(placeRawForOffset, out var offsetTokens)
            && offsetTokens.Count == 3
            && offsetTokens.Any(token => !TryParseNumeric(token, out _)))
        {
            return KernelResult<bool>.Failure([
                CreateDiagnostic(
                    FirmamentDiagnosticCodes.PlacementInvalidOffsetValue,
                    $"Boolean op '{entry.OpName}' at index {opIndex} has invalid field 'place.offset' value; all components must be numeric.")]);
        }

        if (entry.Placement.Offset.Count > 0 && entry.Placement.Offset.Count != 3)
        {
            return KernelResult<bool>.Failure([
                CreateDiagnostic(
                    FirmamentDiagnosticCodes.PlacementInvalidOffsetShape,
                    $"Boolean op '{entry.OpName}' at index {opIndex} has invalid field 'place.offset'; expected exactly 3 numeric components.")]);
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelDiagnostic? ValidateSemanticSelectorField(string? selector, string fieldName, FirmamentParsedOpEntry entry, int opIndex)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return null;
        }

        if (!FirmamentValidationTargetClassifier.TryClassify(selector, out var shape)
            || shape != FirmamentValidationTargetShape.SelectorShaped)
        {
            return CreateDiagnostic(
                FirmamentDiagnosticCodes.PlacementInvalidAnchorShape,
                $"Boolean op '{entry.OpName}' at index {opIndex} has invalid placement selector in field '{fieldName}'; expected selector-shaped token 'feature.port'.");
        }

        return null;
    }

    private static bool TryReadPlacementFieldPresence(string placeRaw, out bool hasOn, out bool hasOffset)
    {
        hasOn = false;
        hasOffset = false;

        var trimmed = placeRaw.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return false;
        }

        hasOn = trimmed.Contains("on:", StringComparison.Ordinal);
        hasOffset = trimmed.Contains("offset:", StringComparison.Ordinal)
            || trimmed.Contains("offset[", StringComparison.Ordinal);
        return true;
    }

    private static bool TryReadOffsetRawTokens(string placeRaw, out List<string> tokens)
    {
        tokens = [];
        var trimmed = placeRaw.Trim();

        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            var offsetKeyIndex = trimmed.IndexOf("offset", StringComparison.Ordinal);
            if (offsetKeyIndex < 0)
            {
                return false;
            }

            var offsetSeparator = trimmed.IndexOf(':', offsetKeyIndex);
            if (offsetSeparator < 0)
            {
                return false;
            }

            var bracketStart = trimmed.IndexOf('[', offsetSeparator + 1);
            var bracketEnd = trimmed.IndexOf(']', bracketStart + 1);
            if (bracketStart < 0 || bracketEnd <= bracketStart)
            {
                return false;
            }

            var content = trimmed[(bracketStart + 1)..bracketEnd];
            tokens = content.Split(',', StringSplitOptions.TrimEntries).ToList();
            return true;
        }

        return false;
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
        var trimmed = raw.Trim();
        try
        {
            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                foreach (var element in document.RootElement.EnumerateArray())
                {
                    if (!TryParseNumeric(element.ToString(), out var value))
                    {
                        return false;
                    }

                    components.Add(value);
                }

                return true;
            }

            if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                var inner = trimmed[1..^1].Trim();
                if (inner.Length == 0)
                {
                    return false;
                }

                var separator = inner.IndexOf(':');
                if (separator <= 0)
                {
                    return false;
                }

                var valueRaw = inner[(separator + 1)..].Trim();
                if (!valueRaw.StartsWith("[", StringComparison.Ordinal))
                {
                    return false;
                }

                return TryParseVector(valueRaw, out components);
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
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

    private static KernelResult<bool> InvalidFieldValue(string fieldName, int opIndex, string opName, string expectation) =>
        KernelResult<bool>.Failure([InvalidFieldValueDiagnostic(fieldName, opIndex, opName, expectation)]);

    private static KernelDiagnostic InvalidFieldValueDiagnostic(string fieldName, int opIndex, string opName, string expectation) =>
        CreateDiagnostic(
            FirmamentDiagnosticCodes.BooleanInvalidFieldValue,
            $"Boolean op '{opName}' at index {opIndex} has invalid field '{fieldName}'; {expectation}.");

    private static KernelDiagnostic CreateDiagnostic(FirmamentDiagnosticCode code, string message) =>
        new(
            KernelDiagnosticCode.ValidationFailed,
            KernelDiagnosticSeverity.Error,
            $"[{code.Value}] {message}",
            FirmamentDiagnosticConventions.Source);
}
