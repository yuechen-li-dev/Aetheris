using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.ParsedModel;
using System.Globalization;

namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentDocumentCoherenceValidator
{
    public static KernelResult<FirmamentParsedDocument> Validate(FirmamentParsedDocument parsedDocument)
    {
        ArgumentNullException.ThrowIfNull(parsedDocument);

        var featureIds = new HashSet<string>(StringComparer.Ordinal);
        var featureKindsById = new Dictionary<string, FirmamentKnownOpKind>(StringComparer.Ordinal);
        var featureEntriesById = new Dictionary<string, FirmamentParsedOpEntry>(StringComparer.Ordinal);
        var updatedEntries = new List<FirmamentParsedOpEntry>(parsedDocument.Ops.Entries.Count);

        for (var index = 0; index < parsedDocument.Ops.Entries.Count; index++)
        {
            var entry = parsedDocument.Ops.Entries[index];

            if (entry.Family == FirmamentOpFamily.Validation)
            {
                ValidateValidationTargetReference(entry, index, featureIds, featureKindsById, out var diagnostic, out var enrichedEntry);
                if (diagnostic is not null)
                {
                    return KernelResult<FirmamentParsedDocument>.Failure([diagnostic]);
                }

                updatedEntries.Add(enrichedEntry ?? entry);
                continue;
            }

            if (entry.Family == FirmamentOpFamily.Pattern)
            {
                var patternExpansion = ExpandPattern(entry, index, featureIds, featureKindsById, featureEntriesById);
                if (!patternExpansion.IsSuccess)
                {
                    return KernelResult<FirmamentParsedDocument>.Failure(patternExpansion.Diagnostics);
                }

                foreach (var expanded in patternExpansion.Value)
                {
                    updatedEntries.Add(expanded);

                    var expandedFeatureId = expanded.RawFields["id"];
                    if (!featureIds.Add(expandedFeatureId))
                    {
                        return KernelResult<FirmamentParsedDocument>.Failure([
                            CreateDiagnostic(
                                FirmamentDiagnosticCodes.ReferenceDuplicateFeatureId,
                                $"Pattern op '{entry.OpName}' at index {index} produced duplicate feature id '{expandedFeatureId}'.")
                        ]);
                    }

                    featureKindsById[expandedFeatureId] = expanded.KnownKind;
                    featureEntriesById[expandedFeatureId] = expanded;
                }

                continue;
            }

            ValidatePlacementSelectorReference(entry, index, featureIds, featureKindsById, out var placementDiagnostic);
            if (placementDiagnostic is not null)
            {
                return KernelResult<FirmamentParsedDocument>.Failure([placementDiagnostic]);
            }

            updatedEntries.Add(entry);

            var featureId = entry.RawFields["id"];
            if (entry.Family == FirmamentOpFamily.Boolean)
            {
                var referenceFieldName = GetReferenceFieldName(entry.KnownKind);
                var referenceId = entry.RawFields[referenceFieldName];
                if (!featureIds.Contains(referenceId))
                {
                    return KernelResult<FirmamentParsedDocument>.Failure([
                        CreateDiagnostic(
                            FirmamentDiagnosticCodes.ReferenceUnknownFeatureId,
                            $"Boolean op '{entry.OpName}' at index {index} references unknown feature id '{referenceId}' via field '{referenceFieldName}'.")
                    ]);
                }
            }

            if (!featureIds.Add(featureId))
            {
                return KernelResult<FirmamentParsedDocument>.Failure([
                    CreateDiagnostic(
                        FirmamentDiagnosticCodes.ReferenceDuplicateFeatureId,
                        $"Feature-producing op '{entry.OpName}' at index {index} reuses duplicate feature id '{featureId}'.")
                ]);
            }

            featureKindsById[featureId] = entry.KnownKind;
            featureEntriesById[featureId] = entry;
        }

        var updatedDocument = parsedDocument with
        {
            Ops = parsedDocument.Ops with { Entries = updatedEntries }
        };

        return KernelResult<FirmamentParsedDocument>.Success(updatedDocument);
    }

    private static KernelResult<IReadOnlyList<FirmamentParsedOpEntry>> ExpandPattern(
        FirmamentParsedOpEntry patternEntry,
        int opIndex,
        HashSet<string> featureIds,
        IReadOnlyDictionary<string, FirmamentKnownOpKind> featureKindsById,
        IReadOnlyDictionary<string, FirmamentParsedOpEntry> featureEntriesById)
    {
        var sourceId = patternEntry.RawFields["source"];
        if (!featureIds.Contains(sourceId) || !featureEntriesById.TryGetValue(sourceId, out var sourceEntry))
        {
            return KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Failure([
                CreateDiagnostic(
                    FirmamentDiagnosticCodes.ReferenceUnknownFeatureId,
                    $"Pattern op '{patternEntry.OpName}' at index {opIndex} references unknown feature id '{sourceId}' via field 'source'.")
            ]);
        }

        if (sourceEntry.Family is not (FirmamentOpFamily.Primitive or FirmamentOpFamily.Boolean))
        {
            return KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Failure([
                CreateDiagnostic(
                    FirmamentDiagnosticCodes.PatternInvalidFieldValue,
                    $"Pattern op '{patternEntry.OpName}' at index {opIndex} uses unsupported source feature id '{sourceId}' from op family '{sourceEntry.Family}'.")
            ]);
        }

        if (!int.TryParse(patternEntry.RawFields["count"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count <= 0)
        {
            return KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Failure([
                CreateDiagnostic(
                    FirmamentDiagnosticCodes.PatternInvalidFieldValue,
                    $"Pattern op '{patternEntry.OpName}' at index {opIndex} has invalid field 'count' value; expected an integer value greater than 0.")
            ]);
        }

        return patternEntry.KnownKind switch
        {
            FirmamentKnownOpKind.PatternLinear => ExpandLinearPattern(patternEntry, sourceEntry, opIndex, count),
            FirmamentKnownOpKind.PatternCircular => ExpandCircularPattern(patternEntry, sourceEntry, opIndex, count, featureIds, featureKindsById),
            _ => KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Success(Array.Empty<FirmamentParsedOpEntry>())
        };
    }

    private static KernelResult<IReadOnlyList<FirmamentParsedOpEntry>> ExpandLinearPattern(
        FirmamentParsedOpEntry patternEntry,
        FirmamentParsedOpEntry sourceEntry,
        int opIndex,
        int count)
    {
        if (!TryParseNumericVector(patternEntry.RawFields["step"], out var step) || step.Count != 3)
        {
            return KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Failure([
                CreateDiagnostic(
                    FirmamentDiagnosticCodes.PatternInvalidFieldTypeOrShape,
                    $"Pattern op '{patternEntry.OpName}' at index {opIndex} has invalid field 'step'; expected exactly 3 numeric components.")
            ]);
        }

        var expanded = new List<FirmamentParsedOpEntry>(count);
        var chainReferenceId = sourceEntry.RawFields["id"];
        for (var i = 1; i <= count; i++)
        {
            var instanceId = $"{sourceEntry.RawFields["id"]}__lin{i.ToString(CultureInfo.InvariantCulture)}";
            var cloned = CloneWithPlacementOffset(
                sourceEntry,
                instanceId,
                step[0] * i,
                step[1] * i,
                step[2] * i);
            cloned = ChainBooleanReferenceIfNeeded(cloned, chainReferenceId);
            chainReferenceId = cloned.RawFields["id"];
            expanded.Add(cloned);
        }

        return KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Success(expanded);
    }

    private static KernelResult<IReadOnlyList<FirmamentParsedOpEntry>> ExpandCircularPattern(
        FirmamentParsedOpEntry patternEntry,
        FirmamentParsedOpEntry sourceEntry,
        int opIndex,
        int count,
        HashSet<string> featureIds,
        IReadOnlyDictionary<string, FirmamentKnownOpKind> featureKindsById)
    {
        var axis = patternEntry.RawFields["axis"];
        if (!TryValidateSelectorReference(
                axis,
                featureIds,
                featureKindsById,
                $"Pattern op '{patternEntry.OpName}' at index {opIndex}",
                "axis",
                out _,
                out _,
                out _,
                out var axisDiagnostic))
        {
            return KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Failure([axisDiagnostic!]);
        }

        var hasSpan = patternEntry.RawFields.TryGetValue("angle_degrees", out var spanRaw) && !string.IsNullOrWhiteSpace(spanRaw);
        var hasStep = patternEntry.RawFields.TryGetValue("angle_step_degrees", out var stepRaw) && !string.IsNullOrWhiteSpace(stepRaw);
        if (!hasSpan && !hasStep)
        {
            return KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Failure([
                CreateDiagnostic(
                    FirmamentDiagnosticCodes.PatternMissingRequiredField,
                    $"Pattern op '{patternEntry.OpName}' at index {opIndex} is missing required field 'angle_degrees'.")
            ]);
        }

        if (hasSpan && hasStep)
        {
            return KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Failure([
                CreateDiagnostic(
                    FirmamentDiagnosticCodes.PatternInvalidFieldValue,
                    $"Pattern op '{patternEntry.OpName}' at index {opIndex} has invalid field 'angle_degrees' value; specify either 'angle_degrees' (span) or 'angle_step_degrees', not both.")
            ]);
        }

        if (!TryParseNumeric(hasStep ? stepRaw! : spanRaw!, out var angle))
        {
            return KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Failure([
                CreateDiagnostic(
                    FirmamentDiagnosticCodes.PatternInvalidFieldTypeOrShape,
                    $"Pattern op '{patternEntry.OpName}' at index {opIndex} has invalid field '{(hasStep ? "angle_step_degrees" : "angle_degrees")}'; expected a numeric scalar value.")
            ]);
        }

        var angularStep = hasStep ? angle : angle / count;
        var baseAngle = sourceEntry.Placement?.AngleDegrees ?? 0d;
        var expanded = new List<FirmamentParsedOpEntry>(count);
        var chainReferenceId = sourceEntry.RawFields["id"];
        for (var i = 1; i <= count; i++)
        {
            var instanceId = $"{sourceEntry.RawFields["id"]}__cir{i.ToString(CultureInfo.InvariantCulture)}";
            var instanceAngle = baseAngle + (angularStep * i);
            var cloned = CloneWithCircularPlacement(sourceEntry, instanceId, axis, instanceAngle);
            cloned = ChainBooleanReferenceIfNeeded(cloned, chainReferenceId);
            chainReferenceId = cloned.RawFields["id"];
            expanded.Add(cloned);
        }

        return KernelResult<IReadOnlyList<FirmamentParsedOpEntry>>.Success(expanded);
    }

    private static FirmamentParsedOpEntry CloneWithPlacementOffset(FirmamentParsedOpEntry sourceEntry, string instanceId, double dx, double dy, double dz)
    {
        var clonedFields = new Dictionary<string, string>(sourceEntry.RawFields, StringComparer.Ordinal)
        {
            ["id"] = instanceId
        };

        var sourcePlacement = sourceEntry.Placement;
        var baseOffset = sourcePlacement?.Offset ?? [0d, 0d, 0d];
        var placement = new FirmamentParsedPlacement(
            sourcePlacement?.On ?? new FirmamentParsedPlacementOriginAnchor(),
            [baseOffset[0] + dx, baseOffset[1] + dy, baseOffset[2] + dz],
            sourcePlacement?.OnFace,
            sourcePlacement?.CenteredOn,
            sourcePlacement?.AroundAxis,
            sourcePlacement?.RadialOffset,
            sourcePlacement?.AngleDegrees,
            sourcePlacement?.UnknownFields ?? Array.Empty<string>());

        return sourceEntry with
        {
            RawFields = clonedFields,
            Placement = placement
        };
    }

    private static FirmamentParsedOpEntry CloneWithCircularPlacement(FirmamentParsedOpEntry sourceEntry, string instanceId, string axisSelector, double angleDegrees)
    {
        var clonedFields = new Dictionary<string, string>(sourceEntry.RawFields, StringComparer.Ordinal)
        {
            ["id"] = instanceId
        };

        var sourcePlacement = sourceEntry.Placement;
        var placement = new FirmamentParsedPlacement(
            sourcePlacement?.On,
            sourcePlacement?.Offset ?? [0d, 0d, 0d],
            sourcePlacement?.OnFace,
            sourcePlacement?.CenteredOn,
            axisSelector,
            sourcePlacement?.RadialOffset ?? 0d,
            angleDegrees,
            sourcePlacement?.UnknownFields ?? Array.Empty<string>());

        return sourceEntry with
        {
            RawFields = clonedFields,
            Placement = placement
        };
    }

    private static FirmamentParsedOpEntry ChainBooleanReferenceIfNeeded(FirmamentParsedOpEntry entry, string nextPrimaryReference)
    {
        if (entry.Family != FirmamentOpFamily.Boolean)
        {
            return entry;
        }

        var primaryReferenceField = GetReferenceFieldName(entry.KnownKind);
        var fields = new Dictionary<string, string>(entry.RawFields, StringComparer.Ordinal)
        {
            [primaryReferenceField] = nextPrimaryReference
        };

        return entry with { RawFields = fields };
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
            using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return false;
            }

            var parsed = new List<double>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!TryParseNumeric(item.ToString(), out var value))
                {
                    return false;
                }

                parsed.Add(value);
            }

            values = parsed;
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
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

    private static void ValidateValidationTargetReference(
        FirmamentParsedOpEntry entry,
        int index,
        HashSet<string> featureIds,
        IReadOnlyDictionary<string, FirmamentKnownOpKind> featureKindsById,
        out KernelDiagnostic? diagnostic,
        out FirmamentParsedOpEntry? enrichedEntry)
    {
        diagnostic = null;
        enrichedEntry = null;

        if (entry.KnownKind is not (FirmamentKnownOpKind.ExpectExists or FirmamentKnownOpKind.ExpectSelectable or FirmamentKnownOpKind.ExpectManifold)
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

        if (!TryValidateSelectorReference(
                targetRaw,
                featureIds,
                featureKindsById,
                $"Validation op '{entry.OpName}' at index {index}",
                "target",
                out var rootFeatureId,
                out var portToken,
                out var rootFeatureKind,
                out diagnostic))
        {
            return;
        }

        if (!FirmamentSelectorContracts.TryGetPortContract(rootFeatureKind, portToken, out var contract))
        {
            return;
        }

        var classifiedFields = entry.ClassifiedFields is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(entry.ClassifiedFields, StringComparer.Ordinal);

        classifiedFields["selectorResultKind"] = contract.ResultKind.ToString();
        classifiedFields["selectorCardinality"] = contract.Cardinality.ToString();

        enrichedEntry = entry with { ClassifiedFields = classifiedFields };
    }

    private static void ValidatePlacementSelectorReference(
        FirmamentParsedOpEntry entry,
        int index,
        HashSet<string> featureIds,
        IReadOnlyDictionary<string, FirmamentKnownOpKind> featureKindsById,
        out KernelDiagnostic? diagnostic)
    {
        diagnostic = null;

        var selectorAnchor = entry.Placement?.On as FirmamentParsedPlacementSelectorAnchor;

        if (selectorAnchor is not null)
        {
            TryValidateSelectorReference(
                selectorAnchor.Selector,
                featureIds,
                featureKindsById,
                $"Primitive op '{entry.OpName}' at index {index}",
                "place.on",
                out _,
                out _,
                out _,
                out diagnostic);
            if (diagnostic is not null)
            {
                return;
            }
        }

        diagnostic = ValidateSemanticPlacementSelector(entry.Placement?.OnFace, featureIds, featureKindsById, entry, index, "place.on_face")
            ?? ValidateSemanticPlacementSelector(entry.Placement?.CenteredOn, featureIds, featureKindsById, entry, index, "place.centered_on")
            ?? ValidateSemanticPlacementSelector(entry.Placement?.AroundAxis, featureIds, featureKindsById, entry, index, "place.around_axis");
    }

    private static KernelDiagnostic? ValidateSemanticPlacementSelector(
        string? selector,
        HashSet<string> featureIds,
        IReadOnlyDictionary<string, FirmamentKnownOpKind> featureKindsById,
        FirmamentParsedOpEntry entry,
        int index,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return null;
        }

        TryValidateSelectorReference(
            selector,
            featureIds,
            featureKindsById,
            $"Primitive op '{entry.OpName}' at index {index}",
            fieldName,
            out _,
            out _,
            out _,
            out var diagnostic);
        return diagnostic;
    }

    private static bool TryValidateSelectorReference(
        string selector,
        HashSet<string> featureIds,
        IReadOnlyDictionary<string, FirmamentKnownOpKind> featureKindsById,
        string sourcePrefix,
        string fieldName,
        out string rootFeatureId,
        out string portToken,
        out FirmamentKnownOpKind rootFeatureKind,
        out KernelDiagnostic? diagnostic)
    {
        rootFeatureId = ExtractSelectorRoot(selector);
        portToken = ExtractSelectorPort(selector);
        rootFeatureKind = default;
        diagnostic = null;

        if (!featureIds.Contains(rootFeatureId))
        {
            diagnostic = CreateDiagnostic(
                FirmamentDiagnosticCodes.ValidationTargetUnknownSelectorRootFeatureId,
                $"{sourcePrefix} references unknown selector root feature id '{rootFeatureId}' via field '{fieldName}'.");
            return false;
        }

        if (!FirmamentValidationTargetClassifier.IsValidIdentifier(portToken))
        {
            diagnostic = CreateDiagnostic(
                FirmamentDiagnosticCodes.ValidationTargetInvalidSelectorPortToken,
                $"{sourcePrefix} has invalid selector port token '{portToken}' via field '{fieldName}'.");
            return false;
        }

        if (!featureKindsById.TryGetValue(rootFeatureId, out rootFeatureKind))
        {
            return true;
        }

        if (!FirmamentSelectorContracts.TryGetAllowedPorts(rootFeatureKind, out var allowedPorts))
        {
            return true;
        }

        if (allowedPorts.Contains(portToken))
        {
            return true;
        }

        diagnostic = CreateDiagnostic(
            FirmamentDiagnosticCodes.ValidationTargetSelectorPortNotAllowedForFeatureKind,
            $"{sourcePrefix} has selector port '{portToken}' not allowed for feature kind '{rootFeatureKind.ToString().ToLowerInvariant()}' on feature id '{rootFeatureId}' via field '{fieldName}'.");
        return false;
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
