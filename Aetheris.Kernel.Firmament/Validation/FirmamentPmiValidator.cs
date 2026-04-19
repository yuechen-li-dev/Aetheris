using System.Globalization;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentPmiValidator
{
    public static KernelResult<bool> Validate(FirmamentParsedDocument parsedDocument)
    {
        ArgumentNullException.ThrowIfNull(parsedDocument);

        if (parsedDocument.Pmi is null)
        {
            return KernelResult<bool>.Success(true);
        }

        var opsByFeatureId = parsedDocument.Ops.Entries
            .Where(entry => entry.RawFields.TryGetValue("id", out _))
            .ToDictionary(entry => entry.RawFields["id"], StringComparer.Ordinal);
        var seenDatumLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var declaredDatumKindsByLabel = BuildDeclaredDatumKindsByLabel(parsedDocument.Pmi.Entries);

        for (var index = 0; index < parsedDocument.Pmi.Entries.Count; index++)
        {
            var entry = parsedDocument.Pmi.Entries[index];
            var result = entry.Kind switch
            {
                FirmamentParsedPmiKind.Hole => ValidateHole(entry, index, opsByFeatureId),
                FirmamentParsedPmiKind.Datum => ValidateDatum(entry, index, opsByFeatureId, seenDatumLabels),
                FirmamentParsedPmiKind.Note => ValidateNote(entry, index, opsByFeatureId),
                FirmamentParsedPmiKind.Dimension => ValidateDimension(entry, index, opsByFeatureId, declaredDatumKindsByLabel),
                _ => Fail($"PMI entry at index {index} has unsupported kind '{entry.KindRaw}'. Supported kinds: hole, datum, note, dimension.")
            };

            if (!result.IsSuccess)
            {
                return result;
            }
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateHole(FirmamentParsedPmiEntry entry, int index, IReadOnlyDictionary<string, FirmamentParsedOpEntry> opsByFeatureId)
    {
        var targetResult = RequireTarget(entry, index, allowSelectorShaped: false, opsByFeatureId);
        if (!targetResult.IsSuccess)
        {
            return KernelResult<bool>.Failure(targetResult.Diagnostics);
        }

        var targetFeature = targetResult.Value;
        if (!opsByFeatureId.TryGetValue(targetFeature, out var targetOp)
            || targetOp.KnownKind != FirmamentKnownOpKind.Subtract
            || !targetOp.RawFields.TryGetValue("with", out var toolRaw)
            || !IsCylinderToolShape(toolRaw))
        {
            return Fail($"PMI hole entry at index {index} requires target feature '{targetFeature}' to be a subtract boolean with a cylinder tool (bounded hole family).");
        }

        if (!entry.RawFields.TryGetValue("diameter", out var diameterRaw)
            || !double.TryParse(diameterRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var diameter)
            || diameter <= 0d)
        {
            return Fail($"PMI hole entry at index {index} must declare positive numeric 'diameter'.");
        }

        if (entry.RawFields.TryGetValue("depth", out var depthRaw)
            && (!double.TryParse(depthRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var depth) || depth <= 0d))
        {
            return Fail($"PMI hole entry at index {index} has invalid 'depth'; expected positive numeric value when supplied.");
        }

        if (entry.RawFields.TryGetValue("tol_plus", out var tolPlusRaw)
            && !double.TryParse(tolPlusRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return Fail($"PMI hole entry at index {index} has invalid 'tol_plus'; expected numeric value.");
        }

        if (entry.RawFields.TryGetValue("tol_minus", out var tolMinusRaw)
            && !double.TryParse(tolMinusRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return Fail($"PMI hole entry at index {index} has invalid 'tol_minus'; expected numeric value.");
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateDatum(
        FirmamentParsedPmiEntry entry,
        int index,
        IReadOnlyDictionary<string, FirmamentParsedOpEntry> opsByFeatureId,
        ISet<string> seenDatumLabels)
    {
        if (!entry.RawFields.TryGetValue("datum_kind", out var datumKind)
            || string.IsNullOrWhiteSpace(datumKind))
        {
            return Fail($"PMI datum entry at index {index} is missing required field 'datum_kind' (supported: plane, axis).");
        }

        if (!entry.RawFields.TryGetValue("label", out var label)
            || string.IsNullOrWhiteSpace(label))
        {
            return Fail($"PMI datum entry at index {index} is missing required field 'label'.");
        }

        if (!seenDatumLabels.Add(label))
        {
            return Fail($"PMI datum entry at index {index} reuses label '{label}'. Datum labels must be unique within a document.");
        }

        if (string.Equals(datumKind, "plane", StringComparison.Ordinal))
        {
            var selectorResult = RequireSelectorTarget(entry, index, opsByFeatureId);
            if (!selectorResult.IsSuccess)
            {
                return KernelResult<bool>.Failure(selectorResult.Diagnostics);
            }

            var rootKind = opsByFeatureId[selectorResult.Value.FeatureId].KnownKind;
            if (!FirmamentSelectorContracts.TryGetPortContract(rootKind, selectorResult.Value.Port, out var contract)
                || contract.ResultKind != FirmamentSelectorResultKind.Face
                || contract.Cardinality != FirmamentSelectorCardinality.One
                || !IsPlanarFacePort(rootKind, selectorResult.Value.Port))
            {
                return Fail($"PMI datum entry at index {index} requires a planar-face selector target for datum_kind 'plane'.");
            }

            return KernelResult<bool>.Success(true);
        }

        if (string.Equals(datumKind, "axis", StringComparison.Ordinal))
        {
            var featureResult = RequireTarget(entry, index, allowSelectorShaped: false, opsByFeatureId);
            if (!featureResult.IsSuccess)
            {
                return KernelResult<bool>.Failure(featureResult.Diagnostics);
            }

            var featureId = featureResult.Value;
            var targetOp = opsByFeatureId[featureId];
            var isSupportedAxisSource = targetOp.KnownKind == FirmamentKnownOpKind.Cylinder
                || (targetOp.KnownKind == FirmamentKnownOpKind.Subtract
                    && targetOp.RawFields.TryGetValue("with", out var toolRaw)
                    && IsCylinderToolShape(toolRaw));

            if (!isSupportedAxisSource)
            {
                return Fail($"PMI datum entry at index {index} requires datum_kind 'axis' target '{featureId}' to come from a cylindrical primitive/feature.");
            }

            return KernelResult<bool>.Success(true);
        }

        return Fail($"PMI datum entry at index {index} has unsupported datum_kind '{datumKind}'. Supported: plane, axis.");
    }

    private static KernelResult<bool> ValidateNote(FirmamentParsedPmiEntry entry, int index, IReadOnlyDictionary<string, FirmamentParsedOpEntry> opsByFeatureId)
    {
        if (!entry.RawFields.TryGetValue("text", out var text)
            || string.IsNullOrWhiteSpace(text))
        {
            return Fail($"PMI note entry at index {index} is missing required field 'text'.");
        }

        var targetResult = RequireTarget(entry, index, allowSelectorShaped: true, opsByFeatureId);
        if (!targetResult.IsSuccess)
        {
            return KernelResult<bool>.Failure(targetResult.Diagnostics);
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateDimension(
        FirmamentParsedPmiEntry entry,
        int index,
        IReadOnlyDictionary<string, FirmamentParsedOpEntry> opsByFeatureId,
        IReadOnlyDictionary<string, string> declaredDatumKindsByLabel)
    {
        if (!entry.RawFields.TryGetValue("dimension_kind", out var dimensionKind)
            || string.IsNullOrWhiteSpace(dimensionKind))
        {
            return Fail($"PMI dimension entry at index {index} is missing required field 'dimension_kind' (supported: diameter, linear_distance_to_datum).");
        }

        if (!entry.RawFields.TryGetValue("value", out var valueRaw)
            || !double.TryParse(valueRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var nominalValue)
            || nominalValue <= 0d)
        {
            return Fail($"PMI dimension entry at index {index} must declare positive numeric 'value'.");
        }

        var targetResult = RequireTarget(entry, index, allowSelectorShaped: false, opsByFeatureId);
        if (!targetResult.IsSuccess)
        {
            return KernelResult<bool>.Failure(targetResult.Diagnostics);
        }

        var targetFeature = targetResult.Value;
        var normalizedDimensionKind = dimensionKind.Trim();

        if (string.Equals(normalizedDimensionKind, "diameter", StringComparison.Ordinal))
        {
            if (!IsSupportedCylindricalDimensionTarget(targetFeature, opsByFeatureId))
            {
                return Fail($"PMI dimension entry at index {index} requires dimension_kind 'diameter' target '{targetFeature}' to be a cylindrical primitive/feature.");
            }

            return KernelResult<bool>.Success(true);
        }

        if (string.Equals(normalizedDimensionKind, "linear_distance_to_datum", StringComparison.Ordinal))
        {
            if (!IsSupportedCylindricalDimensionTarget(targetFeature, opsByFeatureId))
            {
                return Fail($"PMI dimension entry at index {index} requires dimension_kind 'linear_distance_to_datum' target '{targetFeature}' to be a cylindrical primitive/feature.");
            }

            if (!entry.RawFields.TryGetValue("datum", out var datumLabel)
                || string.IsNullOrWhiteSpace(datumLabel))
            {
                return Fail($"PMI dimension entry at index {index} with dimension_kind 'linear_distance_to_datum' is missing required field 'datum'.");
            }

            if (!declaredDatumKindsByLabel.TryGetValue(datumLabel, out var declaredDatumKind))
            {
                return Fail($"PMI dimension entry at index {index} references missing datum label '{datumLabel}'.");
            }

            if (!string.Equals(declaredDatumKind, "plane", StringComparison.Ordinal))
            {
                return Fail($"PMI dimension entry at index {index} requires datum '{datumLabel}' to be planar for dimension_kind 'linear_distance_to_datum'.");
            }

            return KernelResult<bool>.Success(true);
        }

        return Fail($"PMI dimension entry at index {index} has unsupported dimension_kind '{dimensionKind}'. Supported: diameter, linear_distance_to_datum.");
    }

    private static KernelResult<string> RequireTarget(FirmamentParsedPmiEntry entry, int index, bool allowSelectorShaped, IReadOnlyDictionary<string, FirmamentParsedOpEntry> opsByFeatureId)
    {
        if (!entry.RawFields.TryGetValue("target", out var targetRaw)
            || !FirmamentValidationTargetClassifier.TryClassify(targetRaw, out var targetShape))
        {
            return FailTarget($"PMI entry at index {index} has invalid or missing 'target'; expected feature id or selector-shaped target.");
        }

        if (!allowSelectorShaped && targetShape == FirmamentValidationTargetShape.SelectorShaped)
        {
            return FailTarget($"PMI entry at index {index} does not support selector-shaped target '{targetRaw}'.");
        }

        if (targetShape == FirmamentValidationTargetShape.FeatureId)
        {
            if (!opsByFeatureId.ContainsKey(targetRaw))
            {
                return FailTarget($"PMI entry at index {index} references unknown feature target '{targetRaw}'.");
            }

            return KernelResult<string>.Success(targetRaw);
        }

        var separatorIndex = targetRaw.IndexOf('.', StringComparison.Ordinal);
        var featureId = targetRaw[..separatorIndex];
        if (!opsByFeatureId.ContainsKey(featureId))
        {
            return FailTarget($"PMI entry at index {index} references unknown selector root feature '{featureId}'.");
        }

        return KernelResult<string>.Success(featureId);
    }

    private static KernelResult<(string FeatureId, string Port)> RequireSelectorTarget(FirmamentParsedPmiEntry entry, int index, IReadOnlyDictionary<string, FirmamentParsedOpEntry> opsByFeatureId)
    {
        if (!entry.RawFields.TryGetValue("target", out var targetRaw)
            || !FirmamentValidationTargetClassifier.TryClassify(targetRaw, out var targetShape)
            || targetShape != FirmamentValidationTargetShape.SelectorShaped)
        {
            return KernelResult<(string FeatureId, string Port)>.Failure([
                CreateDiagnostic($"PMI datum entry at index {index} requires selector target shaped as 'feature.port'.")
            ]);
        }

        var separatorIndex = targetRaw.IndexOf('.', StringComparison.Ordinal);
        var featureId = targetRaw[..separatorIndex];
        var port = targetRaw[(separatorIndex + 1)..];
        if (!opsByFeatureId.ContainsKey(featureId))
        {
            return KernelResult<(string FeatureId, string Port)>.Failure([
                CreateDiagnostic($"PMI datum entry at index {index} references unknown selector root feature '{featureId}'.")
            ]);
        }

        return KernelResult<(string FeatureId, string Port)>.Success((featureId, port));
    }

    private static KernelResult<bool> Fail(string message) =>
        KernelResult<bool>.Failure([CreateDiagnostic(message)]);

    private static KernelResult<string> FailTarget(string message) =>
        KernelResult<string>.Failure([CreateDiagnostic(message)]);

    private static KernelDiagnostic CreateDiagnostic(string message) =>
        new(
            KernelDiagnosticCode.ValidationFailed,
            KernelDiagnosticSeverity.Error,
            $"[{FirmamentDiagnosticCodes.ValidationInvalidTargetShape.Value}] {message}",
            FirmamentDiagnosticConventions.Source);

    private static bool IsCylinderToolShape(string toolRaw)
    {
        var normalized = new string(toolRaw.Where(character => !char.IsWhiteSpace(character)).ToArray());
        return normalized.Contains("op:cylinder", StringComparison.Ordinal);
    }

    private static bool IsPlanarFacePort(FirmamentKnownOpKind featureKind, string port)
        => featureKind switch
        {
            FirmamentKnownOpKind.Box
                or FirmamentKnownOpKind.TriangularPrism
                or FirmamentKnownOpKind.HexagonalPrism
                or FirmamentKnownOpKind.StraightSlot
                or FirmamentKnownOpKind.RoundedCornerBox
                or FirmamentKnownOpKind.LibraryPart
                or FirmamentKnownOpKind.Add
                or FirmamentKnownOpKind.Subtract
                or FirmamentKnownOpKind.Intersect
                or FirmamentKnownOpKind.Draft
                or FirmamentKnownOpKind.Chamfer
                or FirmamentKnownOpKind.Fillet
                => string.Equals(port, "top_face", StringComparison.Ordinal)
                || string.Equals(port, "bottom_face", StringComparison.Ordinal),
            FirmamentKnownOpKind.Cylinder or FirmamentKnownOpKind.Cone
                => string.Equals(port, "top_face", StringComparison.Ordinal)
                || string.Equals(port, "bottom_face", StringComparison.Ordinal),
            _ => false
        };

    private static bool IsSupportedCylindricalDimensionTarget(string featureId, IReadOnlyDictionary<string, FirmamentParsedOpEntry> opsByFeatureId)
    {
        if (!opsByFeatureId.TryGetValue(featureId, out var targetOp))
        {
            return false;
        }

        return targetOp.KnownKind == FirmamentKnownOpKind.Cylinder
            || (targetOp.KnownKind == FirmamentKnownOpKind.Subtract
                && targetOp.RawFields.TryGetValue("with", out var toolRaw)
                && IsCylinderToolShape(toolRaw));
    }

    private static IReadOnlyDictionary<string, string> BuildDeclaredDatumKindsByLabel(IReadOnlyList<FirmamentParsedPmiEntry> entries)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.Where(entry => entry.Kind == FirmamentParsedPmiKind.Datum))
        {
            if (entry.RawFields.TryGetValue("label", out var label)
                && !string.IsNullOrWhiteSpace(label)
                && entry.RawFields.TryGetValue("datum_kind", out var datumKind)
                && !string.IsNullOrWhiteSpace(datumKind)
                && !map.ContainsKey(label))
            {
                map[label] = datumKind;
            }
        }

        return map;
    }
}
