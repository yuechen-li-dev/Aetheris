using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Numerics;
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
    private const string AutoHolePmiSourceTag = "auto-hole-pmi";
    private static readonly JudgmentEngine<PmiAutoHoleContext> AutoHoleJudgmentEngine = new();
    private static readonly IReadOnlyList<JudgmentCandidate<PmiAutoHoleContext>> AutoHoleCandidates = BuildAutoHoleCandidates();

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
                selectionResult.Value.FeatureKind,
                DatumInspection: BuildDatumInspection(pmiModel.Model),
                DimensionInspection: BuildDimensionInspection(pmiModel.Model)));
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
        if (loweringPlan is null
            || executionResult is null
            || !string.Equals(selection.BodyCategory, ExportBodyCategoryBoolean, StringComparison.Ordinal)
            || !string.Equals(selection.FeatureKind, "subtract", StringComparison.Ordinal))
        {
            return explicitPmi;
        }

        var legacyModel = BuildLegacyAutoHolePmiModel(loweringPlan, executionResult, explicitPmi.Model, selection.FeatureId);
        var mergedModel = explicitPmi.Model;
        foreach (var legacyDimension in legacyModel.Dimensions)
        {
            mergedModel = mergedModel.AddDimension(legacyDimension);
        }

        return new DerivedPmiPayload(mergedModel, explicitPmi.PassthroughNotes);
    }

    internal static PmiModel BuildLegacyAutoHolePmiModel(
        FirmamentPrimitiveLoweringPlan loweringPlan,
        FirmamentPrimitiveExecutionResult executionResult,
        PmiModel explicitPmiModel,
        string selectionFeatureId)
    {
        ArgumentNullException.ThrowIfNull(loweringPlan);
        ArgumentNullException.ThrowIfNull(executionResult);
        ArgumentNullException.ThrowIfNull(explicitPmiModel);

        var booleansByFeatureId = loweringPlan.Booleans.ToDictionary(boolean => boolean.FeatureId, StringComparer.Ordinal);
        var executedBooleansByFeatureId = executionResult.ExecutedBooleans.ToDictionary(boolean => boolean.FeatureId, StringComparer.Ordinal);
        var opIndexByFeatureId = loweringPlan.Booleans.ToDictionary(boolean => boolean.FeatureId, boolean => boolean.OpIndex, StringComparer.Ordinal);
        var currentFeatureId = selectionFeatureId;
        var derived = new List<(int OpIndex, PmiDimension Item, string CandidateName)>();

        while (booleansByFeatureId.TryGetValue(currentFeatureId, out var boolean)
            && boolean.Kind == FirmamentLoweredBooleanKind.Subtract)
        {
            if (!executedBooleansByFeatureId.TryGetValue(boolean.FeatureId, out var executed)
                || (executed.SemanticSafeComposition ?? executed.Body.SafeBooleanComposition) is not { } composition)
            {
                currentFeatureId = boolean.PrimaryReferenceFeatureId;
                continue;
            }

            var hasExplicitDiameter = HasExplicitEquivalentDiameter(explicitPmiModel, boolean.FeatureId);
            var context = BuildAutoHoleContext(boolean, composition, opIndexByFeatureId, hasExplicitDiameter);
            var selection = AutoHoleJudgmentEngine.Evaluate(context, AutoHoleCandidates);
            if (selection.IsSuccess)
            {
                var candidateName = selection.Selection!.Value.Candidate.Name;
                foreach (var dimension in BuildAutoHoleDimensions(context, candidateName))
                {
                    derived.Add((boolean.OpIndex, dimension, candidateName));
                }
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

        for (var index = 0; index < parsedDocument.Pmi.Entries.Count; index++)
        {
            var entry = parsedDocument.Pmi.Entries[index];
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
                            $"explicit-diameter:{featureId}:{index}",
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
                            && TryBuildPlanarDatumReference(targetRaw, featureBodies, out var planarReference))
                        {
                            if (model.DatumFeatures.Any(existing => string.Equals(existing.Label, label, StringComparison.OrdinalIgnoreCase)))
                            {
                                continue;
                            }

                            model = model.CreatePlanarDatum(label, planarReference);
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
                case FirmamentParsedPmiKind.Dimension:
                    if (!entry.RawFields.TryGetValue("dimension_kind", out var dimensionKindRaw)
                        || !entry.RawFields.TryGetValue("value", out var valueRaw)
                        || !double.TryParse(valueRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
                    {
                        break;
                    }

                    if (string.Equals(dimensionKindRaw, "diameter", StringComparison.Ordinal))
                    {
                        model = model.AddDimension(new PmiDimension(
                            $"explicit-diameter:{featureId}:{index}",
                            PmiDimensionKind.Diameter,
                            new PmiCylindricalFeatureReference(featureId, "through_or_blind_cylindrical"),
                            null,
                            value,
                            SourceTag: "explicit-dimension"));
                    }
                    else if (string.Equals(dimensionKindRaw, "linear_distance_to_datum", StringComparison.Ordinal)
                        && entry.RawFields.TryGetValue("datum", out var datumLabel))
                    {
                        var datum = model.DatumFeatures.FirstOrDefault(existing => string.Equals(existing.Label, datumLabel, StringComparison.OrdinalIgnoreCase));
                        if (datum is null || datum.Kind != PmiDatumFeatureKind.Planar)
                        {
                            break;
                        }

                        model = model.AddDimension(new PmiDimension(
                            $"explicit-linear-distance:{featureId}-to-{datum.Label}:{index}",
                            PmiDimensionKind.LinearDistanceToDatum,
                            new PmiCylindricalFeatureReference(featureId, "through_or_blind_cylindrical"),
                            new PmiDatumReference(datum.DatumId),
                            value,
                            SourceTag: "explicit-dimension"));
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

    private static IReadOnlyList<FirmamentPmiInspectionDatum> BuildDatumInspection(PmiModel model)
        => model.DatumFeatures
            .Select(datum => datum.Target is PmiPlanarFaceReference planar
                ? new FirmamentPmiInspectionDatum(datum.Label, "planar", planar.Selector)
                : new FirmamentPmiInspectionDatum(datum.Label, datum.Kind.ToString().ToLowerInvariant(), datum.Target.ToString() ?? "unknown"))
            .ToArray();

    private static IReadOnlyList<FirmamentPmiInspectionDimension> BuildDimensionInspection(PmiModel model)
        => model.Dimensions.Select(dimension =>
        {
            var target = dimension.PrimaryReference switch
            {
                PmiCylindricalFeatureReference cylindrical => cylindrical.FeatureId,
                PmiPlanarFaceReference planar => planar.Selector,
                PmiDatumReference datum => datum.DatumId,
                _ => dimension.PrimaryReference.ToString() ?? "unknown"
            };

            var datumLabel = dimension.SecondaryReference is PmiDatumReference datumReference
                ? model.DatumFeatures.FirstOrDefault(existing => string.Equals(existing.DatumId, datumReference.DatumId, StringComparison.Ordinal))?.Label
                    ?? datumReference.DatumId
                : null;

            return new FirmamentPmiInspectionDimension(
                dimension.Kind.ToString(),
                target,
                datumLabel,
                dimension.NominalValue,
                dimension.SourceTag,
                ExtractCandidateName(dimension.SourceTag));
        }).ToArray();

    private static IReadOnlyList<JudgmentCandidate<PmiAutoHoleContext>> BuildAutoHoleCandidates()
        =>
        [
            new JudgmentCandidate<PmiAutoHoleContext>(
                Name: "counterbore_callout",
                IsAdmissible: context => context.IsSubtract
                    && context.IsFromRecognizedFeature
                    && !context.IsAmbiguous
                    && !context.HasExplicitPmi
                    && context.Recognition is { Kind: HoleFeatureKind.Counterbore },
                Score: _ => 300d,
                RejectionReason: BuildRejectionReason),
            new JudgmentCandidate<PmiAutoHoleContext>(
                Name: "countersink_callout",
                IsAdmissible: context => context.IsSubtract
                    && context.IsFromRecognizedFeature
                    && !context.IsAmbiguous
                    && !context.HasExplicitPmi
                    && context.Recognition is { Kind: HoleFeatureKind.Countersink },
                Score: _ => 250d,
                RejectionReason: BuildRejectionReason),
            new JudgmentCandidate<PmiAutoHoleContext>(
                Name: "simple_hole_callout",
                IsAdmissible: context => context.IsSubtract
                    && context.IsFromRecognizedFeature
                    && !context.IsAmbiguous
                    && !context.HasExplicitPmi
                    && context.Recognition is { Kind: HoleFeatureKind.SimpleHole },
                Score: _ => 200d,
                RejectionReason: BuildRejectionReason),
            new JudgmentCandidate<PmiAutoHoleContext>(
                Name: "reject_non_hole",
                IsAdmissible: _ => true,
                Score: _ => 0d)
        ];

    private static string BuildRejectionReason(PmiAutoHoleContext context)
        => context.RejectionReason
            ?? (context.HasExplicitPmi ? "explicit PMI already exists for this feature." : "hole-family predicates were not satisfied.");

    private static PmiAutoHoleContext BuildAutoHoleContext(
        FirmamentLoweredBoolean boolean,
        SafeBooleanComposition composition,
        IReadOnlyDictionary<string, int> opIndexByFeatureId,
        bool hasExplicitPmi)
    {
        var recognized = BrepBooleanHoleFeatureRecognition.TryRecognizeFeature(
            composition,
            boolean.FeatureId,
            opIndexByFeatureId,
            ToleranceContext.Default,
            out var recognition,
            out var isAmbiguous,
            out var reason);

        return new PmiAutoHoleContext(
            Operation: BooleanOperation.Subtract,
            IsSubtract: boolean.Kind == FirmamentLoweredBooleanKind.Subtract,
            IsFromRecognizedFeature: recognized,
            Recognition: recognized ? recognition : null,
            IsAmbiguous: isAmbiguous,
            HasExplicitPmi: hasExplicitPmi,
            RejectionReason: reason);
    }

    private static IEnumerable<PmiDimension> BuildAutoHoleDimensions(PmiAutoHoleContext context, string candidateName)
    {
        if (context.Recognition is not { } recognition)
        {
            yield break;
        }

        if (string.Equals(candidateName, "simple_hole_callout", StringComparison.Ordinal))
        {
            yield return CreateAutoDiameterDimension(recognition.FeatureId, "simple", recognition.PrimaryHole.BottomRadius * 2d, candidateName);
            yield break;
        }

        if (string.Equals(candidateName, "counterbore_callout", StringComparison.Ordinal)
            && recognition.SecondaryHole is { } secondary)
        {
            yield return CreateAutoDiameterDimension(recognition.FeatureId, "counterbore_entry", recognition.PrimaryHole.TopRadius * 2d, candidateName);
            yield return CreateAutoDiameterDimension(recognition.FeatureId, "counterbore_deep", secondary.BottomRadius * 2d, candidateName);
            yield break;
        }

        if (string.Equals(candidateName, "countersink_callout", StringComparison.Ordinal))
        {
            yield return CreateAutoDiameterDimension(recognition.FeatureId, "countersink", recognition.PrimaryHole.TopRadius * 2d, candidateName);
        }
    }

    private static PmiDimension CreateAutoDiameterDimension(string featureId, string semanticFamily, double diameter, string candidateName)
        => new(
            $"auto-diameter:{featureId}:{semanticFamily}",
            PmiDimensionKind.Diameter,
            new PmiCylindricalFeatureReference(featureId, semanticFamily),
            null,
            diameter,
            SourceTag: $"{AutoHolePmiSourceTag}:{candidateName}");

    private static bool HasExplicitEquivalentDiameter(PmiModel explicitPmiModel, string featureId)
        => explicitPmiModel.Dimensions.Any(dimension =>
            dimension.Kind == PmiDimensionKind.Diameter
            && dimension.PrimaryReference is PmiCylindricalFeatureReference cylinder
            && string.Equals(cylinder.FeatureId, featureId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(dimension.SourceTag)
            && dimension.SourceTag.StartsWith("explicit-", StringComparison.Ordinal));

    private static string? ExtractCandidateName(string? sourceTag)
    {
        if (string.IsNullOrWhiteSpace(sourceTag)
            || !sourceTag.StartsWith($"{AutoHolePmiSourceTag}:", StringComparison.Ordinal))
        {
            return null;
        }

        return sourceTag[(AutoHolePmiSourceTag.Length + 1)..];
    }

    private static bool TryBuildPlanarDatumReference(
        string targetRaw,
        IReadOnlyDictionary<string, BrepBody> featureBodies,
        out PmiPlanarFaceReference planarReference)
    {
        planarReference = null!;

        PmiPlanarFaceReference candidate;
        try
        {
            candidate = PmiPlanarFaceReference.FromSelector(targetRaw);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (!FirmamentSelectorResolver.TryResolve(targetRaw, featureBodies, FirmamentSelectorResultKind.Face, out var selectorResolution))
        {
            return false;
        }

        if (selectorResolution.Count != 1)
        {
            return false;
        }

        if (!featureBodies.TryGetValue(candidate.FeatureId, out var body))
        {
            return false;
        }

        var planarFaceCount = body.Topology.Faces.Count(face =>
            body.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface is not null
            && surface.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Plane);

        if (planarFaceCount <= 0)
        {
            return false;
        }

        if (targetRaw.EndsWith(".side_face", StringComparison.Ordinal) || targetRaw.EndsWith(".surface", StringComparison.Ordinal))
        {
            return false;
        }

        planarReference = candidate;
        return true;
    }

    private sealed record DerivedPmiPayload(PmiModel Model, IReadOnlyList<Step242SemanticPmiNote> PassthroughNotes);
    private readonly record struct PmiAutoHoleContext(
        BooleanOperation Operation,
        bool IsSubtract,
        bool IsFromRecognizedFeature,
        HoleFeatureRecognition? Recognition,
        bool IsAmbiguous,
        bool HasExplicitPmi,
        string? RejectionReason);

    private sealed record ExportBodySelection(
        int OpIndex,
        string FeatureId,
        string BodyCategory,
        string FeatureKind,
        BrepBody Body);
}
