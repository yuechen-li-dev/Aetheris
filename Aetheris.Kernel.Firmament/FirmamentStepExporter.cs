using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Pmi;
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

        var pmiModel = DerivePmiModel(artifact.ParsedDocument, artifact.PrimitiveLoweringPlan, artifact.PrimitiveExecutionResult, selectionResult.Value);
        var semanticPmi = PmiStep242Adapter.ToStep242SemanticPmi(pmiModel.Model, pmiModel.PassthroughNotes);
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

    private static DerivedPmiPayload DerivePmiModel(
        FirmamentParsedDocument? parsedDocument,
        FirmamentPrimitiveLoweringPlan? loweringPlan,
        FirmamentPrimitiveExecutionResult? executionResult,
        ExportBodySelection selection)
    {
        var explicitPmi = DeriveExplicitPmi(parsedDocument, executionResult);
        if (explicitPmi.Model.DatumFeatures.Count > 0
            || explicitPmi.Model.Dimensions.Count > 0
            || explicitPmi.PassthroughNotes.Count > 0)
        {
            return explicitPmi;
        }

        if (loweringPlan is null
            || !string.Equals(selection.BodyCategory, ExportBodyCategoryBoolean, StringComparison.Ordinal)
            || !string.Equals(selection.FeatureKind, "subtract", StringComparison.Ordinal))
        {
            return new DerivedPmiPayload(PmiModel.Empty(selection.FeatureId), []);
        }

        var model = BuildLegacyAutoHolePmiModel(loweringPlan, selection.FeatureId);

        return new DerivedPmiPayload(model, []);
    }


    internal static PmiModel BuildLegacyAutoHolePmiModel(FirmamentPrimitiveLoweringPlan loweringPlan, string selectionFeatureId)
    {
        ArgumentNullException.ThrowIfNull(loweringPlan);

        var booleansByFeatureId = loweringPlan.Booleans.ToDictionary(boolean => boolean.FeatureId, StringComparer.Ordinal);
        var currentFeatureId = selectionFeatureId;
        var derived = new List<(int OpIndex, PmiDimension Item)>();

        while (booleansByFeatureId.TryGetValue(currentFeatureId, out var boolean)
            && boolean.Kind == FirmamentLoweredBooleanKind.Subtract)
        {
            if (string.Equals(boolean.Tool.OpName, "cylinder", StringComparison.Ordinal)
                && boolean.Tool.RawFields.TryGetValue("radius", out var radiusRaw)
                && !string.IsNullOrWhiteSpace(radiusRaw))
            {
                var radius = FirmamentPrimitiveToolParsing.ParseScalar(radiusRaw);
                derived.Add((
                    boolean.OpIndex,
                    new PmiDimension(
                        $"diameter:{boolean.FeatureId}",
                        PmiDimensionKind.Diameter,
                        new PmiCylindricalFeatureReference(boolean.FeatureId, "through_or_blind_cylindrical"),
                        null,
                        radius * 2d,
                        SourceTag: "legacy-auto-hole-demo")));
            }

            currentFeatureId = boolean.PrimaryReferenceFeatureId;
        }

        var model = PmiModel.Empty(selectionFeatureId);
        foreach (var dimension in derived.OrderBy(item => item.OpIndex).Select(item => item.Item))
        {
            model = model.AddDimension(dimension);
        }

        return model;
    }

    private static DerivedPmiPayload DeriveExplicitPmi(
        FirmamentParsedDocument? parsedDocument,
        FirmamentPrimitiveExecutionResult? executionResult)
    {
        if (parsedDocument?.Pmi is null || parsedDocument.Pmi.Entries.Count == 0 || executionResult is null)
        {
            return new DerivedPmiPayload(PmiModel.Empty("unspecified"), []);
        }

        var featureBodies = BuildFeatureBodyMap(executionResult);
        var bodyFeatureId = executionResult.ExecutedBooleans.OrderByDescending(b => b.OpIndex).Select(b => b.FeatureId).FirstOrDefault()
            ?? executionResult.ExecutedPrimitives.OrderByDescending(p => p.OpIndex).Select(p => p.FeatureId).FirstOrDefault()
            ?? "unspecified";
        var model = PmiModel.Empty(bodyFeatureId);
        var passthroughNotes = new List<Step242SemanticPmiNote>();

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

                        model = model.AddDimension(new PmiDimension(
                            $"diameter:{featureId}",
                            PmiDimensionKind.Diameter,
                            new PmiCylindricalFeatureReference(featureId, entry.RawFields.TryGetValue("hole_family", out var familyRaw) ? familyRaw : null),
                            null,
                            diameter,
                            SourceTag: depth.HasValue ? "explicit-hole-depth-provided" : "explicit-hole"));
                    }

                    break;
                case FirmamentParsedPmiKind.Datum:
                    if (entry.RawFields.TryGetValue("datum_kind", out var datumKind)
                        && entry.RawFields.TryGetValue("label", out var label))
                    {
                        if (string.Equals(datumKind, "plane", StringComparison.Ordinal)
                            && targetRaw.Contains(".", StringComparison.Ordinal))
                        {
                            model = model.AddDatum(new PmiDatumFeature(
                                $"datum:{label}",
                                label,
                                PmiDatumFeatureKind.Planar,
                                new PmiPlanarFaceReference(featureId, targetRaw)));
                        }
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

                        passthroughNotes.Add(new Step242SemanticPmiNote(featureId, targetRaw, text));
                    }

                    break;
            }
        }

        return new DerivedPmiPayload(model, passthroughNotes);
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

    private sealed record DerivedPmiPayload(PmiModel Model, IReadOnlyList<Step242SemanticPmiNote> PassthroughNotes);

    private sealed record ExportBodySelection(
        int OpIndex,
        string FeatureId,
        string BodyCategory,
        string FeatureKind,
        BrepBody Body);
}
