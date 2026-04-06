using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Firmament.ParsedModel;
using Aetheris.Kernel.Firmament.Validation;

namespace Aetheris.Kernel.Firmament;

public static class FirmamentStepExporter
{
    public const string LastExecutedGeometricBodyPolicy = "last-executed-geometric-body";
    public const string LastExecutedGeometricBodySelectionReason = "Select the last successfully executed primitive or boolean body in source order; validation ops are never export bodies.";

    public static KernelResult<FirmamentStepExportResult> Export(FirmamentCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var compiler = new FirmamentCompiler();
        var compileResult = compiler.Compile(request);
        if (!compileResult.Compilation.IsSuccess)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(compileResult.Compilation.Diagnostics);
        }

        return Export(compileResult.Compilation.Value);
    }

    public static KernelResult<FirmamentStepExportResult> Export(FirmamentCompilationArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var selectionResult = SelectExportBody(artifact.PrimitiveExecutionResult);
        if (!selectionResult.IsSuccess)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(selectionResult.Diagnostics);
        }

        var semanticPmi = DeriveSemanticPmi(artifact.ParsedDocument, artifact.PrimitiveLoweringPlan, artifact.PrimitiveExecutionResult, selectionResult.Value);
        var stepResult = Step242Exporter.ExportBody(selectionResult.Value.Body, semanticPmi);
        if (!stepResult.IsSuccess)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(stepResult.Diagnostics);
        }

        return KernelResult<FirmamentStepExportResult>.Success(
            new FirmamentStepExportResult(
                stepResult.Value,
                selectionResult.Value.FeatureId,
                selectionResult.Value.OpIndex,
                selectionResult.Value.BodyCategory,
                selectionResult.Value.FeatureKind));
    }

    private static KernelResult<ExportBodySelection> SelectExportBody(FirmamentPrimitiveExecutionResult? primitiveExecutionResult)
    {
        if (primitiveExecutionResult is null)
        {
            return Failure("Firmament STEP export requires a completed primitive execution result.");
        }

        ExportBodySelection? selected = null;

        foreach (var primitive in primitiveExecutionResult.ExecutedPrimitives)
        {
            selected = SelectLater(
                selected,
                new ExportBodySelection(
                    primitive.OpIndex,
                    primitive.FeatureId,
                    ExportBodyCategoryPrimitive,
                    primitive.Kind.ToString().ToLowerInvariant(),
                    primitive.Body));
        }

        foreach (var boolean in primitiveExecutionResult.ExecutedBooleans)
        {
            selected = SelectLater(
                selected,
                new ExportBodySelection(
                    boolean.OpIndex,
                    boolean.FeatureId,
                    ExportBodyCategoryBoolean,
                    boolean.Kind.ToString().ToLowerInvariant(),
                    boolean.Body));
        }

        return selected is null
            ? Failure("Firmament STEP export requires at least one executed primitive or boolean body.")
            : KernelResult<ExportBodySelection>.Success(selected);
    }

    private static ExportBodySelection SelectLater(ExportBodySelection? current, ExportBodySelection candidate)
    {
        if (current is null)
        {
            return candidate;
        }

        return candidate.OpIndex > current.OpIndex ? candidate : current;
    }

    private static KernelResult<ExportBodySelection> Failure(string message) =>
        KernelResult<ExportBodySelection>.Failure(
        [
            new KernelDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error,
                message,
                FirmamentDiagnosticConventions.Source)
        ]);

    private const string ExportBodyCategoryPrimitive = "primitive";
    private const string ExportBodyCategoryBoolean = "boolean";

    private static IReadOnlyList<Step242SemanticPmi> DeriveSemanticPmi(
        FirmamentParsedDocument? parsedDocument,
        FirmamentPrimitiveLoweringPlan? loweringPlan,
        FirmamentPrimitiveExecutionResult? executionResult,
        ExportBodySelection selection)
    {
        var explicitPmi = DeriveExplicitPmi(parsedDocument, executionResult);
        if (explicitPmi.Count > 0)
        {
            return explicitPmi;
        }

        if (loweringPlan is null
            || !string.Equals(selection.BodyCategory, ExportBodyCategoryBoolean, StringComparison.Ordinal)
            || !string.Equals(selection.FeatureKind, "subtract", StringComparison.Ordinal))
        {
            return [];
        }

        var booleansByFeatureId = loweringPlan.Booleans.ToDictionary(boolean => boolean.FeatureId, StringComparer.Ordinal);
        var currentFeatureId = selection.FeatureId;
        var derived = new List<(int OpIndex, Step242SemanticPmiHole Item)>();

        while (booleansByFeatureId.TryGetValue(currentFeatureId, out var boolean)
            && boolean.Kind == FirmamentLoweredBooleanKind.Subtract)
        {
            if (string.Equals(boolean.Tool.OpName, "cylinder", StringComparison.Ordinal)
                && boolean.Tool.RawFields.TryGetValue("radius", out var radiusRaw)
                && !string.IsNullOrWhiteSpace(radiusRaw))
            {
                var radius = FirmamentPrimitiveToolParsing.ParseScalar(radiusRaw);
                derived.Add((boolean.OpIndex, new Step242SemanticPmiHole(boolean.FeatureId, radius * 2d, null, "through_or_blind_cylindrical", null, null)));
            }

            currentFeatureId = boolean.PrimaryReferenceFeatureId;
        }

        return derived
            .OrderBy(item => item.OpIndex)
            .Select(item => item.Item)
            .ToArray();
    }

    private static IReadOnlyList<Step242SemanticPmi> DeriveExplicitPmi(
        FirmamentParsedDocument? parsedDocument,
        FirmamentPrimitiveExecutionResult? executionResult)
    {
        if (parsedDocument?.Pmi is null || parsedDocument.Pmi.Entries.Count == 0 || executionResult is null)
        {
            return [];
        }

        var featureBodies = BuildFeatureBodyMap(executionResult);
        var resolved = new List<Step242SemanticPmi>();

        foreach (var entry in parsedDocument.Pmi.Entries)
        {
            if (!entry.RawFields.TryGetValue("target", out var targetRaw))
            {
                continue;
            }

            var featureId = targetRaw.Contains('.', StringComparison.Ordinal)
                ? targetRaw[..targetRaw.IndexOf('.', StringComparison.Ordinal)]
                : targetRaw;

            switch (entry.Kind)
            {
                case FirmamentParsedPmiKind.Hole:
                    if (entry.RawFields.TryGetValue("diameter", out var diameterRaw)
                        && double.TryParse(diameterRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var diameter))
                    {
                        double? depth = null;
                        if (entry.RawFields.TryGetValue("depth", out var depthRaw)
                            && double.TryParse(depthRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedDepth))
                        {
                            depth = parsedDepth;
                        }

                        resolved.Add(new Step242SemanticPmiHole(
                            featureId,
                            diameter,
                            depth,
                            entry.RawFields.TryGetValue("hole_family", out var familyRaw) ? familyRaw : null,
                            entry.RawFields.TryGetValue("tol_plus", out var tolPlusRaw) && double.TryParse(tolPlusRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tolPlus) ? tolPlus : null,
                            entry.RawFields.TryGetValue("tol_minus", out var tolMinusRaw) && double.TryParse(tolMinusRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tolMinus) ? tolMinus : null));
                    }

                    break;
                case FirmamentParsedPmiKind.Datum:
                    if (entry.RawFields.TryGetValue("datum_kind", out var datumKind)
                        && entry.RawFields.TryGetValue("label", out var label))
                    {
                        resolved.Add(new Step242SemanticPmiDatum(featureId, datumKind, label, targetRaw));
                    }

                    break;
                case FirmamentParsedPmiKind.Note:
                    if (entry.RawFields.TryGetValue("text", out var text))
                    {
                        if (FirmamentValidationTargetClassifier.TryClassify(targetRaw, out var targetShape)
                            && targetShape == FirmamentValidationTargetShape.SelectorShaped)
                        {
                            if (FirmamentSelectorResolver.TryResolve(targetRaw, featureBodies, FirmamentSelectorResultKind.Face, out var selectorResolution)
                                && selectorResolution.Count == 0)
                            {
                                continue;
                            }
                        }

                        resolved.Add(new Step242SemanticPmiNote(featureId, targetRaw, text));
                    }

                    break;
            }
        }

        return resolved;
    }

    private static IReadOnlyDictionary<string, BrepBody> BuildFeatureBodyMap(FirmamentPrimitiveExecutionResult executionResult)
    {
        var map = new Dictionary<string, BrepBody>(StringComparer.Ordinal);
        foreach (var primitive in executionResult.ExecutedPrimitives)
        {
            map[primitive.FeatureId] = primitive.Body;
        }

        foreach (var boolean in executionResult.ExecutedBooleans)
        {
            map[boolean.FeatureId] = boolean.Body;
        }

        return map;
    }

    private sealed record ExportBodySelection(
        int OpIndex,
        string FeatureId,
        string BodyCategory,
        string FeatureKind,
        BrepBody Body);
}
