using System.Globalization;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.EdgeFinishing;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Analysis;
using Aetheris.Kernel.Firmament.Connectors;
using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Firmament.Validation;
using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class FirmamentPrimitiveExecutor
{
    public static KernelResult<FirmamentPrimitiveExecutionResult> Execute(FirmamentPrimitiveLoweringPlan loweringPlan)
    {
        ArgumentNullException.ThrowIfNull(loweringPlan);

        var executedPrimitives = new List<FirmamentExecutedPrimitive>(loweringPlan.Primitives.Count);
        var executedBooleans = new List<FirmamentExecutedBoolean>(loweringPlan.Booleans.Count);
        var publishedBodiesByFeatureId = new Dictionary<string, BrepBody>(StringComparer.Ordinal);
        var booleanExecutionBodiesByFeatureId = new Dictionary<string, BrepBody>(StringComparer.Ordinal);
        var featureGraphStates = new Dictionary<string, FirmamentSafeSubtractFeatureGraphState>(StringComparer.Ordinal);
        var resolvedPlacementByOpIndex = new Dictionary<int, NativeGeometryResolvedPlacement>();

        var tolerance = ToleranceContext.Default;

        foreach (var primitive in loweringPlan.Primitives.OrderBy(p => p.OpIndex))
        {
            var bodyResult = ExecutePrimitive(primitive, publishedBodiesByFeatureId);
            if (!bodyResult.IsSuccess)
            {
                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(bodyResult.Diagnostics);
            }

            executedPrimitives.Add(new FirmamentExecutedPrimitive(primitive.OpIndex, primitive.FeatureId, primitive.Kind, bodyResult.Value.Published));
            resolvedPlacementByOpIndex[primitive.OpIndex] = BuildResolvedPlacement(primitive.Placement, bodyResult.Value.PlacementTranslation);
            publishedBodiesByFeatureId[primitive.FeatureId] = bodyResult.Value.Published;
            booleanExecutionBodiesByFeatureId[primitive.FeatureId] = bodyResult.Value.LegacyForBoolean;
            featureGraphStates[primitive.FeatureId] = primitive.Kind switch
            {
                FirmamentLoweredPrimitiveKind.Box => FirmamentSafeSubtractFeatureGraphState.BoxRoot,
                FirmamentLoweredPrimitiveKind.Cylinder => FirmamentSafeSubtractFeatureGraphState.CylinderRoot,
                _ => FirmamentSafeSubtractFeatureGraphState.Other
            };
        }

        foreach (var boolean in loweringPlan.Booleans.OrderBy(b => b.OpIndex))
        {
            if (!booleanExecutionBodiesByFeatureId.TryGetValue(boolean.PrimaryReferenceFeatureId, out var baseBody))
            {
                continue;
            }

            if (boolean.Kind == FirmamentLoweredBooleanKind.Draft)
            {
                var draftResult = ExecuteBoundedDraftOnRecognizedOrthogonalRoot(boolean, baseBody, featureGraphStates);
                if (!draftResult.IsSuccess)
                {
                    return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(
                    [
                        CreateBooleanExecutionFailureDiagnostic(boolean),
                        .. WithBooleanContext(boolean, draftResult.Diagnostics)
                    ]);
                }

                var draftedBody = draftResult.Value;
                executedBooleans.Add(new FirmamentExecutedBoolean(boolean.OpIndex, boolean.FeatureId, boolean.Kind, draftedBody));
                publishedBodiesByFeatureId[boolean.FeatureId] = draftedBody;
                booleanExecutionBodiesByFeatureId[boolean.FeatureId] = draftedBody;
                featureGraphStates[boolean.FeatureId] = FirmamentSafeSubtractFeatureGraphState.Other;
                resolvedPlacementByOpIndex[boolean.OpIndex] = BuildResolvedPlacement(boolean.Placement, Vector3D.Zero);
                continue;
            }

            if (boolean.Kind == FirmamentLoweredBooleanKind.Chamfer)
            {
                var chamferResult = ExecuteBoundedChamferOnRecognizedOrthogonalRoot(boolean, baseBody, featureGraphStates);
                if (!chamferResult.IsSuccess)
                {
                    return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(
                    [
                        CreateBooleanExecutionFailureDiagnostic(boolean),
                        .. WithBooleanContext(boolean, chamferResult.Diagnostics)
                    ]);
                }

                var chamferedBody = chamferResult.Value;
                executedBooleans.Add(new FirmamentExecutedBoolean(boolean.OpIndex, boolean.FeatureId, boolean.Kind, chamferedBody));
                publishedBodiesByFeatureId[boolean.FeatureId] = chamferedBody;
                booleanExecutionBodiesByFeatureId[boolean.FeatureId] = chamferedBody;
                featureGraphStates[boolean.FeatureId] = FirmamentSafeSubtractFeatureGraphState.Other;
                resolvedPlacementByOpIndex[boolean.OpIndex] = BuildResolvedPlacement(boolean.Placement, Vector3D.Zero);
                continue;
            }

            if (boolean.Kind == FirmamentLoweredBooleanKind.Fillet)
            {
                var filletResult = ExecuteBoundedManufacturingFilletOnRecognizedOrthogonalRoot(boolean, baseBody, featureGraphStates);
                if (!filletResult.IsSuccess)
                {
                    return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(
                    [
                        CreateBooleanExecutionFailureDiagnostic(boolean),
                        .. WithBooleanContext(boolean, filletResult.Diagnostics)
                    ]);
                }

                var filletedBody = filletResult.Value;
                executedBooleans.Add(new FirmamentExecutedBoolean(boolean.OpIndex, boolean.FeatureId, boolean.Kind, filletedBody));
                publishedBodiesByFeatureId[boolean.FeatureId] = filletedBody;
                booleanExecutionBodiesByFeatureId[boolean.FeatureId] = filletedBody;
                featureGraphStates[boolean.FeatureId] = FirmamentSafeSubtractFeatureGraphState.Other;
                resolvedPlacementByOpIndex[boolean.OpIndex] = BuildResolvedPlacement(boolean.Placement, Vector3D.Zero);
                continue;
            }

            if (IsPatternGeneratedFeature(boolean.FeatureId))
            {
                var patternToolResult = FirmamentBooleanToolBodyFactory.CreateBody(boolean.Tool);
                if (!patternToolResult.IsSuccess)
                {
                    return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(patternToolResult.Diagnostics);
                }

                var patternPlacementResult = FirmamentPlacementResolver.ResolvePlacementTranslation(boolean, publishedBodiesByFeatureId);
                if (!patternPlacementResult.IsSuccess)
                {
                    return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(patternPlacementResult.Diagnostics);
                }

                var placedToolBody = TranslateBody(patternToolResult.Value, patternPlacementResult.Value);
                var patternGraphValidation = FirmamentSafeSubtractFeatureGraphValidator.ValidateNextBoolean(
                    boolean,
                    featureGraphStates,
                    booleanExecutionBodiesByFeatureId,
                    resolvedToolBody: placedToolBody);
                if (!patternGraphValidation.IsSuccess)
                {
                    return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(
                    [
                        CreateBooleanExecutionFailureDiagnostic(boolean),
                        .. WithBooleanContext(boolean, patternGraphValidation.Diagnostics)
                    ]);
                }

                var patternBooleanResult = ExecuteBoolean(boolean.Kind, baseBody, placedToolBody);
                if (!patternBooleanResult.IsSuccess)
                {
                    return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(
                    [
                        CreateBooleanExecutionFailureDiagnostic(boolean),
                        .. WithBooleanContext(boolean, patternBooleanResult.Diagnostics)
                    ]);
                }

                var patternBooleanBody = patternBooleanResult.Value;
                executedBooleans.Add(new FirmamentExecutedBoolean(boolean.OpIndex, boolean.FeatureId, boolean.Kind, patternBooleanBody));
                publishedBodiesByFeatureId[boolean.FeatureId] = patternBooleanBody;
                booleanExecutionBodiesByFeatureId[boolean.FeatureId] = patternBooleanBody;
                featureGraphStates[boolean.FeatureId] = DetermineBooleanFeatureGraphState(
                    boolean,
                    patternGraphValidation.Value.ResultState,
                    patternBooleanBody,
                    tolerance);
                resolvedPlacementByOpIndex[boolean.OpIndex] = BuildResolvedPlacement(boolean.Placement, patternPlacementResult.Value);
                continue;
            }

            var toolResult = FirmamentBooleanToolBodyFactory.CreateBody(boolean.Tool);
            if (!toolResult.IsSuccess)
            {
                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(toolResult.Diagnostics);
            }

            var featureGraphValidation = FirmamentSafeSubtractFeatureGraphValidator.ValidateNextBoolean(
                boolean,
                featureGraphStates,
                booleanExecutionBodiesByFeatureId,
                resolvedToolBody: ResolveValidationToolBody(boolean, baseBody, toolResult.Value, publishedBodiesByFeatureId));
            if (!featureGraphValidation.IsSuccess)
            {
                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(
                [
                    CreateBooleanExecutionFailureDiagnostic(boolean),
                    .. WithBooleanContext(boolean, featureGraphValidation.Diagnostics)
                ]);
            }

            var canUsePublishedFrameAdditiveExecution = boolean.Kind == FirmamentLoweredBooleanKind.Add
                && boolean.Placement is not null;
            var usedPublishedFrameAdditiveExecution = false;
            var useSemanticToolPlacement = !canUsePublishedFrameAdditiveExecution
                && ShouldUseSemanticToolPlacement(boolean, baseBody, toolResult.Value);
            BrepBody? toolBodyUsedForBoolean = null;
            Vector3D deferredPlacementTranslation = Vector3D.Zero;

            KernelResult<BrepBody> booleanResult;
            if (canUsePublishedFrameAdditiveExecution)
            {
                if (!publishedBodiesByFeatureId.TryGetValue(boolean.PrimaryReferenceFeatureId, out var publishedBaseBody))
                {
                    continue;
                }

                var placementResult = FirmamentPlacementResolver.ResolvePlacementTranslation(boolean, publishedBodiesByFeatureId);
                if (!placementResult.IsSuccess)
                {
                    return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(placementResult.Diagnostics);
                }

                deferredPlacementTranslation = placementResult.Value;
                var translatedToolBody = TranslateBody(ApplyDefaultToolLocalFrame(boolean.Tool, toolResult.Value), placementResult.Value);
                toolBodyUsedForBoolean = translatedToolBody;
                booleanResult = ExecuteBoolean(boolean.Kind, publishedBaseBody, translatedToolBody);

                if (booleanResult.IsSuccess)
                {
                    usedPublishedFrameAdditiveExecution = true;
                }
                else
                {
                    toolBodyUsedForBoolean = toolResult.Value;
                    booleanResult = ExecuteBoolean(boolean.Kind, baseBody, toolResult.Value);
                }
            }
            else if (useSemanticToolPlacement)
            {
                var placementResult = FirmamentPlacementResolver.ResolvePlacementTranslation(boolean, publishedBodiesByFeatureId);
                if (!placementResult.IsSuccess)
                {
                    return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(placementResult.Diagnostics);
                }

                deferredPlacementTranslation = placementResult.Value;
                var translatedToolBody = PlaceSemanticToolBody(boolean, toolResult.Value, placementResult.Value);
                toolBodyUsedForBoolean = translatedToolBody;
                booleanResult = ExecuteBoolean(boolean.Kind, baseBody, translatedToolBody);
            }
            else
            {
                toolBodyUsedForBoolean = toolResult.Value;
                booleanResult = ExecuteBoolean(boolean.Kind, baseBody, toolResult.Value);
            }

            if (!booleanResult.IsSuccess)
            {
                var fallbackReplayLog = BuildReplayLog(loweringPlan, resolvedPlacementByOpIndex);
                var fallForward = TryBuildCirOnlyFallback(loweringPlan, fallbackReplayLog, boolean, booleanResult.Diagnostics);
                if (fallForward.IsSuccess)
                {
                    return KernelResult<FirmamentPrimitiveExecutionResult>.Success(
                        new FirmamentPrimitiveExecutionResult(executedPrimitives, executedBooleans, fallForward.Value));
                }

                var failureDiagnostics = new List<KernelDiagnostic>
                {
                    CreateBooleanExecutionFailureDiagnostic(boolean)
                };
                failureDiagnostics.AddRange(WithBooleanContext(boolean, booleanResult.Diagnostics));
                failureDiagnostics.AddRange(fallForward.Diagnostics);
                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(failureDiagnostics);
            }

            var placedBooleanBody = booleanResult.Value;
            if (!useSemanticToolPlacement && !usedPublishedFrameAdditiveExecution)
            {
                var placementResult = FirmamentPlacementResolver.ResolvePlacementTranslation(boolean, publishedBodiesByFeatureId);
                if (!placementResult.IsSuccess)
                {
                    return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(placementResult.Diagnostics);
                }

                deferredPlacementTranslation = placementResult.Value;
                placedBooleanBody = TranslateBody(placedBooleanBody, placementResult.Value);
            }

            var semanticSafeComposition = TryBuildSemanticSafeComposition(boolean, baseBody, toolBodyUsedForBoolean, deferredPlacementTranslation);
            executedBooleans.Add(new FirmamentExecutedBoolean(boolean.OpIndex, boolean.FeatureId, boolean.Kind, placedBooleanBody, semanticSafeComposition));
            publishedBodiesByFeatureId[boolean.FeatureId] = placedBooleanBody;
            booleanExecutionBodiesByFeatureId[boolean.FeatureId] = placedBooleanBody;
            featureGraphStates[boolean.FeatureId] = DetermineBooleanFeatureGraphState(
                boolean,
                featureGraphValidation.Value.ResultState,
                placedBooleanBody,
                tolerance);
            resolvedPlacementByOpIndex[boolean.OpIndex] = BuildResolvedPlacement(boolean.Placement, deferredPlacementTranslation);
        }

        var replayLog = BuildReplayLog(loweringPlan, resolvedPlacementByOpIndex);
        BrepBody? materializedBody = null;
        if (executedBooleans.Count > 0)
        {
            materializedBody = executedBooleans.OrderBy(b => b.OpIndex).Last().Body;
        }
        else if (executedPrimitives.Count > 0)
        {
            materializedBody = executedPrimitives.OrderBy(p => p.OpIndex).Last().Body;
        }

        var nativeState = new NativeGeometryState(
            NativeGeometryExecutionMode.BRepActive,
            NativeGeometryMaterializationAuthority.BRepAuthoritative,
            materializedBody,
            null,
            replayLog,
            [],
            BuildCirMirrorState(loweringPlan));

        return KernelResult<FirmamentPrimitiveExecutionResult>.Success(new FirmamentPrimitiveExecutionResult(executedPrimitives, executedBooleans, nativeState));
    }




    private static NativeGeometryCirMirrorState BuildCirMirrorState(FirmamentPrimitiveLoweringPlan loweringPlan)
    {
        var lower = FirmamentCirLowerer.Lower(loweringPlan);
        if (!lower.IsSuccess)
        {
            return new NativeGeometryCirMirrorState(
                CirMirrorStatus.Unsupported,
                null,
                lower.Diagnostics.Select(d => new NativeGeometryCirMirrorDiagnostics(d.Message, null, d.Source)).ToArray());
        }

        var analysis = CirNativeAnalysisService.AnalyzeNode(lower.Value.Root, denseResolution: 28);
        if (!analysis.Success || analysis.Bounds is null || analysis.Volume is null)
        {
            var diagnostics = analysis.Diagnostics
                .Select(d => new NativeGeometryCirMirrorDiagnostics(d.Message, d.OpIndex, d.FeatureId))
                .ToList();
            if (!analysis.Success && diagnostics.Count == 0)
            {
                diagnostics.Add(new NativeGeometryCirMirrorDiagnostics("CIR mirror analysis failed.", null, null));
            }

            return new NativeGeometryCirMirrorState(CirMirrorStatus.Failed, null, diagnostics);
        }

        var summary = new NativeGeometryCirMirrorSummary(
            analysis.Bounds.Min,
            analysis.Bounds.Max,
            analysis.Volume.EstimatedVolume,
            analysis.Volume.Approximate,
            analysis.Volume.Resolution);

        return new NativeGeometryCirMirrorState(CirMirrorStatus.Available, summary, []);
    }

    private static KernelResult<NativeGeometryState> TryBuildCirOnlyFallback(
        FirmamentPrimitiveLoweringPlan loweringPlan,
        NativeGeometryReplayLog replayLog,
        FirmamentLoweredBoolean failedBoolean,
        IReadOnlyList<KernelDiagnostic> brepDiagnostics)
    {
        if (ClassifyFallForwardEligibility(failedBoolean, brepDiagnostics) != NativeGeometryTransitionReasonCategory.MaterializationUnsupported)
        {
            return KernelResult<NativeGeometryState>.Failure([]);
        }

        var cirLower = FirmamentCirLowerer.Lower(loweringPlan);
        if (!cirLower.IsSuccess)
        {
            var loweredDiagnostics = cirLower.Diagnostics.Select(d => d with
            {
                Message = $"CIR fallback lowering failed after unsupported BRep materialization for '{failedBoolean.FeatureId}': {d.Message}"
            }).ToArray();
            return KernelResult<NativeGeometryState>.Failure(loweredDiagnostics);
        }

        var nativeState = new NativeGeometryState(
            NativeGeometryExecutionMode.CirOnly,
            NativeGeometryMaterializationAuthority.CirIntentOnly,
            null,
            failedBoolean.FeatureId,
            replayLog,
            [
                new NativeGeometryTransitionEvent(
                    NativeGeometryExecutionMode.BRepActive,
                    NativeGeometryExecutionMode.CirOnly,
                    failedBoolean.FeatureId,
                    failedBoolean.OpIndex,
                    NativeGeometryTransitionReasonCategory.MaterializationUnsupported,
                    $"BRep materialization unsupported for boolean '{failedBoolean.FeatureId}'; transitioned to CirOnly intent execution.")
            ],
            BuildCirMirrorState(loweringPlan));

        return KernelResult<NativeGeometryState>.Success(nativeState);
    }

    private static NativeGeometryTransitionReasonCategory ClassifyFallForwardEligibility(FirmamentLoweredBoolean failedBoolean, IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        var isBoundedCandidate = failedBoolean.Kind == FirmamentLoweredBooleanKind.Subtract
            && string.Equals(failedBoolean.Tool.OpName, "sphere", StringComparison.Ordinal)
            && diagnostics.Any(d => d.Code == KernelDiagnosticCode.NotImplemented
                && string.Equals(d.Source, "BrepBoolean.AnalyticHole.TangentContact", StringComparison.Ordinal));
        if (isBoundedCandidate)
        {
            return NativeGeometryTransitionReasonCategory.MaterializationUnsupported;
        }

        if (diagnostics.Any(d => d.Code == KernelDiagnosticCode.ValidationFailed))
        {
            return NativeGeometryTransitionReasonCategory.InvalidIntent;
        }

        return NativeGeometryTransitionReasonCategory.AnalyzerUncertainty;
    }
    private static IReadOnlyList<KernelDiagnostic> WithBooleanContext(FirmamentLoweredBoolean boolean, IReadOnlyList<KernelDiagnostic> diagnostics)
        => diagnostics.Select(diagnostic =>
        {
            if (diagnostic.Message.Contains($"Boolean feature '{boolean.FeatureId}'", StringComparison.Ordinal))
            {
                return diagnostic;
            }

            return diagnostic with
            {
                Message = $"Boolean feature '{boolean.FeatureId}' ({boolean.Kind.ToString().ToLowerInvariant()}): {diagnostic.Message}"
            };
        }).ToArray();

    private static KernelDiagnostic CreateBooleanExecutionFailureDiagnostic(FirmamentLoweredBoolean boolean)
        => new(
            KernelDiagnosticCode.NotImplemented,
            KernelDiagnosticSeverity.Error,
            $"Requested boolean feature '{boolean.FeatureId}' ({boolean.Kind.ToString().ToLowerInvariant()}) could not be executed.",
            Source: "firmament");


    private static NativeGeometryReplayLog BuildReplayLog(FirmamentPrimitiveLoweringPlan loweringPlan, IReadOnlyDictionary<int, NativeGeometryResolvedPlacement> resolvedPlacementByOpIndex)
    {
        var replayOps = new List<NativeGeometryReplayOperation>(loweringPlan.Primitives.Count + loweringPlan.Booleans.Count);

        replayOps.AddRange(loweringPlan.Primitives
            .OrderBy(p => p.OpIndex)
            .Select(p => new NativeGeometryReplayOperation(
                p.OpIndex,
                p.FeatureId,
                $"primitive:{p.Kind.ToString().ToLowerInvariant()}",
                null,
                null,
                null,
                SummarizePlacement(p.Placement),
                ResolveReplayPlacement(p.OpIndex, p.Placement, resolvedPlacementByOpIndex),
                null)));

        replayOps.AddRange(loweringPlan.Booleans
            .OrderBy(b => b.OpIndex)
            .Select(b => new NativeGeometryReplayOperation(
                b.OpIndex,
                b.FeatureId,
                $"boolean:{b.Kind.ToString().ToLowerInvariant()}",
                b.PrimaryReferenceFeatureId,
                b.Tool.OpName,
                b.Tool.RawFields.TryGetValue("id", out var toolId) ? toolId : null,
                SummarizePlacement(b.Placement),
                ResolveReplayPlacement(b.OpIndex, b.Placement, resolvedPlacementByOpIndex),
                b.Tool.RawValue)));

        return new NativeGeometryReplayLog(replayOps.OrderBy(o => o.OpIndex).ToArray());
    }

    private static string? SummarizePlacement(FirmamentLoweredPlacement? placement)
    {
        if (placement is null)
        {
            return null;
        }

        var anchor = placement.On switch
        {
            FirmamentLoweredPlacementOriginAnchor => "origin",
            FirmamentLoweredPlacementSelectorAnchor selector => selector.Selector,
            _ => "unknown"
        };

        var offset = placement.Offset.Count == 3
            ? $"[{placement.Offset[0]},{placement.Offset[1]},{placement.Offset[2]}]"
            : "[]";

        return $"on={anchor};offset={offset};semantic={placement.UsesSemanticAnchor}";
    }

    private static NativeGeometryResolvedPlacement ResolveReplayPlacement(
        int opIndex,
        FirmamentLoweredPlacement? placement,
        IReadOnlyDictionary<int, NativeGeometryResolvedPlacement> resolvedPlacementByOpIndex)
    {
        if (resolvedPlacementByOpIndex.TryGetValue(opIndex, out var resolved))
        {
            return resolved;
        }

        return BuildResolvedPlacement(placement, Vector3D.Zero);
    }

    private static NativeGeometryResolvedPlacement BuildResolvedPlacement(FirmamentLoweredPlacement? placement, Vector3D translation)
    {
        if (placement is null)
        {
            return new NativeGeometryResolvedPlacement(NativeGeometryPlacementKind.None, null, null, Vector3D.Zero, Vector3D.Zero, true, null);
        }

        var offset = placement.Offset.Count == 3
            ? new Vector3D(placement.Offset[0], placement.Offset[1], placement.Offset[2])
            : Vector3D.Zero;

        if (!string.IsNullOrWhiteSpace(placement.AroundAxis))
        {
            return new NativeGeometryResolvedPlacement(NativeGeometryPlacementKind.AroundAxis, null, null, offset, translation, false, "Around-axis placement replay facts are deferred in CIR-F2.5.");
        }

        if (placement.On is FirmamentLoweredPlacementSelectorAnchor selectorAnchor)
        {
            var i = selectorAnchor.Selector.IndexOf('.', StringComparison.Ordinal);
            if (i <= 0 || i >= selectorAnchor.Selector.Length - 1)
            {
                return new NativeGeometryResolvedPlacement(NativeGeometryPlacementKind.Unsupported, null, null, offset, translation, false, $"Unsupported placement selector '{selectorAnchor.Selector}' for replay facts.");
            }

            return new NativeGeometryResolvedPlacement(
                NativeGeometryPlacementKind.OnFace,
                selectorAnchor.Selector[..i],
                selectorAnchor.Selector[(i + 1)..],
                offset,
                translation,
                true,
                null);
        }

        return new NativeGeometryResolvedPlacement(NativeGeometryPlacementKind.Offset, null, null, offset, translation, true, null);
    }

    private static bool IsPatternGeneratedFeature(string featureId)
        => featureId.Contains("__lin", StringComparison.Ordinal)
           || featureId.Contains("__cir", StringComparison.Ordinal)
           || featureId.Contains("__mir_", StringComparison.Ordinal);

    private static SafeBooleanComposition? TryBuildSemanticSafeComposition(
        FirmamentLoweredBoolean boolean,
        BrepBody baseBody,
        BrepBody? toolBodyUsedForBoolean,
        Vector3D deferredPlacementTranslation)
    {
        if (boolean.Kind != FirmamentLoweredBooleanKind.Subtract || toolBodyUsedForBoolean is null)
        {
            return null;
        }

        if (!BrepBooleanSafeComposition.TryRecognize(baseBody, ToleranceContext.Default, out var composition, out _)
            || !BrepBooleanAnalyticSurfaceRecognition.TryRecognizeAnalyticSurface(toolBodyUsedForBoolean, ToleranceContext.Default, out var analyticSurface, out _)
            || !BrepBooleanSafeCompositionGraphValidator.TryValidateNextSubtract(
                composition,
                analyticSurface,
                ToleranceContext.Default,
                out var updated,
                out _,
                boolean.FeatureId))
        {
            return null;
        }

        return deferredPlacementTranslation == Vector3D.Zero
            ? updated
            : updated.Translate(deferredPlacementTranslation);
    }

    private static KernelResult<FirmamentExecutedPrimitiveBodies> ExecutePrimitive(FirmamentLoweredPrimitive primitive, IReadOnlyDictionary<string, BrepBody> publishedBodies)
    {
        var legacyResult = ExecuteLegacyPrimitive(primitive);
        if (!legacyResult.IsSuccess)
        {
            return KernelResult<FirmamentExecutedPrimitiveBodies>.Failure(legacyResult.Diagnostics);
        }

        var defaultFrameBody = ApplyDefaultLocalFrame(primitive, legacyResult.Value);
        var placementResult = FirmamentPlacementResolver.ResolvePlacementTranslation(primitive, publishedBodies);
        if (!placementResult.IsSuccess)
        {
            return KernelResult<FirmamentExecutedPrimitiveBodies>.Failure(placementResult.Diagnostics);
        }

        var publishedBody = TranslateBody(defaultFrameBody, placementResult.Value);
        return KernelResult<FirmamentExecutedPrimitiveBodies>.Success(new FirmamentExecutedPrimitiveBodies(publishedBody, legacyResult.Value, placementResult.Value));
    }

    private static BrepBody ApplyDefaultLocalFrame(FirmamentLoweredPrimitive primitive, BrepBody body)
    {
        return primitive.Kind switch
        {
            FirmamentLoweredPrimitiveKind.Box => TranslateBody(body, new Vector3D(0d, 0d, ((FirmamentLoweredBoxParameters)primitive.Parameters).SizeZ * 0.5d)),
            FirmamentLoweredPrimitiveKind.Cylinder => TranslateBody(body, new Vector3D(0d, 0d, ((FirmamentLoweredCylinderParameters)primitive.Parameters).Height * 0.5d)),
            FirmamentLoweredPrimitiveKind.Cone => TranslateBody(body, new Vector3D(0d, 0d, ((FirmamentLoweredConeParameters)primitive.Parameters).Height * 0.5d)),
            FirmamentLoweredPrimitiveKind.TriangularPrism => TranslateBody(body, new Vector3D(0d, 0d, ((FirmamentLoweredTriangularPrismParameters)primitive.Parameters).Height * 0.5d)),
            FirmamentLoweredPrimitiveKind.HexagonalPrism => TranslateBody(body, new Vector3D(0d, 0d, ((FirmamentLoweredHexagonalPrismParameters)primitive.Parameters).Height * 0.5d)),
            FirmamentLoweredPrimitiveKind.StraightSlot => TranslateBody(body, new Vector3D(0d, 0d, ((FirmamentLoweredStraightSlotParameters)primitive.Parameters).Height * 0.5d)),
            FirmamentLoweredPrimitiveKind.RoundedCornerBox => TranslateBody(body, new Vector3D(0d, 0d, ((FirmamentLoweredRoundedCornerBoxParameters)primitive.Parameters).Height * 0.5d)),
            FirmamentLoweredPrimitiveKind.SlotCut => TranslateBody(body, new Vector3D(0d, 0d, ((FirmamentLoweredSlotCutParameters)primitive.Parameters).Height * 0.5d)),
            _ => body
        };
    }

    private static KernelResult<BrepBody> ExecuteLegacyPrimitive(FirmamentLoweredPrimitive primitive)
    {
        return primitive.Kind switch
        {
            FirmamentLoweredPrimitiveKind.Box => BrepPrimitives.CreateBox(((FirmamentLoweredBoxParameters)primitive.Parameters).SizeX, ((FirmamentLoweredBoxParameters)primitive.Parameters).SizeY, ((FirmamentLoweredBoxParameters)primitive.Parameters).SizeZ),
            FirmamentLoweredPrimitiveKind.Cylinder => BrepPrimitives.CreateCylinder(((FirmamentLoweredCylinderParameters)primitive.Parameters).Radius, ((FirmamentLoweredCylinderParameters)primitive.Parameters).Height),
            FirmamentLoweredPrimitiveKind.Cone => ExecuteCone((FirmamentLoweredConeParameters)primitive.Parameters),
            FirmamentLoweredPrimitiveKind.Torus => BrepPrimitives.CreateTorus(((FirmamentLoweredTorusParameters)primitive.Parameters).MajorRadius, ((FirmamentLoweredTorusParameters)primitive.Parameters).MinorRadius),
            FirmamentLoweredPrimitiveKind.Sphere => BrepPrimitives.CreateSphere(((FirmamentLoweredSphereParameters)primitive.Parameters).Radius),
            FirmamentLoweredPrimitiveKind.TriangularPrism => BrepPrimitives.CreateTriangularPrism(
                ((FirmamentLoweredTriangularPrismParameters)primitive.Parameters).BaseWidth,
                ((FirmamentLoweredTriangularPrismParameters)primitive.Parameters).BaseDepth,
                ((FirmamentLoweredTriangularPrismParameters)primitive.Parameters).Height),
            FirmamentLoweredPrimitiveKind.HexagonalPrism => BrepPrimitives.CreateHexagonalPrism(
                ((FirmamentLoweredHexagonalPrismParameters)primitive.Parameters).AcrossFlats,
                ((FirmamentLoweredHexagonalPrismParameters)primitive.Parameters).Height),
            FirmamentLoweredPrimitiveKind.StraightSlot => BrepPrimitives.CreateStraightSlot(
                ((FirmamentLoweredStraightSlotParameters)primitive.Parameters).Length,
                ((FirmamentLoweredStraightSlotParameters)primitive.Parameters).Width,
                ((FirmamentLoweredStraightSlotParameters)primitive.Parameters).Height),
            FirmamentLoweredPrimitiveKind.RoundedCornerBox => StandardLibraryPrimitives.CreateRoundedCornerBox(
                ((FirmamentLoweredRoundedCornerBoxParameters)primitive.Parameters).Width,
                ((FirmamentLoweredRoundedCornerBoxParameters)primitive.Parameters).Depth,
                ((FirmamentLoweredRoundedCornerBoxParameters)primitive.Parameters).Height,
                ((FirmamentLoweredRoundedCornerBoxParameters)primitive.Parameters).CornerRadius),
            FirmamentLoweredPrimitiveKind.SlotCut => StandardLibraryPrimitives.CreateSlotCut(
                ((FirmamentLoweredSlotCutParameters)primitive.Parameters).Length,
                ((FirmamentLoweredSlotCutParameters)primitive.Parameters).Width,
                ((FirmamentLoweredSlotCutParameters)primitive.Parameters).Height,
                ((FirmamentLoweredSlotCutParameters)primitive.Parameters).CornerRadius),
            FirmamentLoweredPrimitiveKind.LibraryPart => FirmamentPartLibraryConnector.ResolvePart(
                ((FirmamentLoweredLibraryPartParameters)primitive.Parameters).PartReference),
            _ => KernelResult<BrepBody>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, $"Primitive execution for kind '{primitive.Kind}' is not implemented.")])
        };
    }

    internal static KernelResult<BrepBody> ExecuteCone(FirmamentLoweredConeParameters parameters)
    {
        var frame = new ExtrudeFrame3D(
            origin: Point3D.Origin,
            normal: Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            uAxis: Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var axis = new RevolveAxis3D(Point3D.Origin, new Vector3D(0d, 0d, 1d));

        var coneResult = BrepRevolve.Create(
            [
                new ProfilePoint2D(parameters.BottomRadius, 0d),
                new ProfilePoint2D(parameters.TopRadius, parameters.Height)
            ],
            frame,
            axis);

        if (!coneResult.IsSuccess)
        {
            return coneResult;
        }

        return KernelResult<BrepBody>.Success(TranslateBody(coneResult.Value, new Vector3D(0d, 0d, -parameters.Height * 0.5d)));
    }

    private static KernelResult<BrepBody> ExecuteBoolean(FirmamentLoweredBooleanKind kind, BrepBody left, BrepBody right) =>
        kind switch
        {
            FirmamentLoweredBooleanKind.Add => BrepBoolean.Union(left, right),
            FirmamentLoweredBooleanKind.Subtract => BrepBoolean.Subtract(left, right),
            FirmamentLoweredBooleanKind.Intersect => BrepBoolean.Intersect(left, right),
            _ => KernelResult<BrepBody>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, $"Boolean execution for kind '{kind}' is not implemented.")])
        };

    private static FirmamentSafeSubtractFeatureGraphState DetermineBooleanFeatureGraphState(
        FirmamentLoweredBoolean boolean,
        FirmamentSafeSubtractFeatureGraphState validatedResultState,
        BrepBody resultBody,
        ToleranceContext tolerance)
    {
        if (validatedResultState == FirmamentSafeSubtractFeatureGraphState.SafeSubtractComposition)
        {
            return validatedResultState;
        }

        if (boolean.Kind == FirmamentLoweredBooleanKind.Add)
        {
            return BrepBooleanSafeComposition.TryRecognize(resultBody, tolerance, out _, out _)
                ? FirmamentSafeSubtractFeatureGraphState.BoundedOrthogonalAdditiveSafeRoot
                : FirmamentSafeSubtractFeatureGraphState.BoundedOrthogonalAdditiveOutsideSafeRoot;
        }

        if (boolean.Kind == FirmamentLoweredBooleanKind.Subtract
            && validatedResultState == FirmamentSafeSubtractFeatureGraphState.Other
            && BrepBooleanSafeComposition.TryRecognize(resultBody, tolerance, out _, out _))
        {
            return FirmamentSafeSubtractFeatureGraphState.SafeSubtractComposition;
        }

        return validatedResultState;
    }

    private static BrepBody ApplyDefaultToolLocalFrame(FirmamentLoweredToolOp tool, BrepBody body)
    {
        if (string.Equals(tool.OpName, "box", StringComparison.Ordinal)
            && tool.RawFields.TryGetValue("size", out var boxSizeRaw)
            && !string.IsNullOrWhiteSpace(boxSizeRaw))
        {
            var box = FirmamentPrimitiveToolParsing.ParseBox(boxSizeRaw);
            return TranslateBody(body, new Vector3D(0d, 0d, box.SizeZ * 0.5d));
        }

        if (string.Equals(tool.OpName, "cylinder", StringComparison.Ordinal)
            && tool.RawFields.TryGetValue("height", out var cylinderHeightRaw)
            && !string.IsNullOrWhiteSpace(cylinderHeightRaw))
        {
            var height = FirmamentPrimitiveToolParsing.ParseScalar(cylinderHeightRaw);
            return TranslateBody(body, new Vector3D(0d, 0d, height * 0.5d));
        }

        if (string.Equals(tool.OpName, "cone", StringComparison.Ordinal)
            && tool.RawFields.TryGetValue("height", out var coneHeightRaw)
            && !string.IsNullOrWhiteSpace(coneHeightRaw))
        {
            var height = FirmamentPrimitiveToolParsing.ParseScalar(coneHeightRaw);
            return TranslateBody(body, new Vector3D(0d, 0d, height * 0.5d));
        }

        if (FirmamentPrismFamilyTools.IsPrismTool(tool.OpName))
        {
            return TranslateBody(body, FirmamentPrismFamilyTools.ResolveDefaultFrameTranslation(tool));
        }

        return body;
    }

    private static bool ShouldUseSemanticToolPlacement(FirmamentLoweredBoolean boolean, BrepBody baseBody, BrepBody toolBody)
    {
        if (boolean.Kind != FirmamentLoweredBooleanKind.Subtract)
        {
            return false;
        }

        if (boolean.Placement is null)
        {
            return false;
        }

        if (string.Equals(boolean.Tool.OpName, "box", StringComparison.Ordinal))
        {
            return BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(baseBody, ToleranceContext.Default, out _, out _)
                && BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(toolBody, ToleranceContext.Default, out _, out _);
        }

        var isCylinderTool = string.Equals(boolean.Tool.OpName, "cylinder", StringComparison.Ordinal);
        var isConeTool = string.Equals(boolean.Tool.OpName, "cone", StringComparison.Ordinal);
        var isSphereTool = string.Equals(boolean.Tool.OpName, "sphere", StringComparison.Ordinal);
        if (!isCylinderTool && !isConeTool && !isSphereTool)
        {
            return false;
        }

        if (isSphereTool)
        {
            return boolean.Placement.UsesSemanticAnchor
                && BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(baseBody, ToleranceContext.Default, out _, out _);
        }

        if (isConeTool)
        {
            // Cone tools require real placed-body validation/execution for bounded blind-entry families
            // (e.g., countersink entry cones); validating unplaced cones collapses them into contained tools.
            return true;
        }

        if (baseBody.SafeBooleanComposition is { Holes.Count: > 0 })
        {
            return true;
        }

        if (boolean.Placement.UsesSemanticAnchor
            && BrepBooleanCylinderRecognition.TryRecognizeCylinder(baseBody, ToleranceContext.Default, out _, out _))
        {
            return true;
        }

        if (!boolean.Placement.UsesSemanticAnchor)
        {
            return false;
        }

        if (!BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(baseBody, ToleranceContext.Default, out var rootBox, out _)
            || !BrepBooleanCylinderRecognition.TryRecognizeCylinder(toolBody, ToleranceContext.Default, out var cylinder, out _))
        {
            return false;
        }

        var boundaryAtMinZ = rootBox.MinZ - cylinder.AxisOrigin.Z;
        var boundaryAtMaxZ = rootBox.MaxZ - cylinder.AxisOrigin.Z;
        var coversMin = boundaryAtMinZ >= cylinder.MinAxisParameter - ToleranceContext.Default.Linear
                        && boundaryAtMinZ <= cylinder.MaxAxisParameter + ToleranceContext.Default.Linear;
        var coversMax = boundaryAtMaxZ >= cylinder.MinAxisParameter - ToleranceContext.Default.Linear
                        && boundaryAtMaxZ <= cylinder.MaxAxisParameter + ToleranceContext.Default.Linear;

        return !coversMin && !coversMax;
    }

    private static BrepBody? ResolveValidationToolBody(
        FirmamentLoweredBoolean boolean,
        BrepBody baseBody,
        BrepBody toolBody,
        IReadOnlyDictionary<string, BrepBody> publishedBodiesByFeatureId)
    {
        if (!ShouldUseSemanticToolPlacement(boolean, baseBody, toolBody))
        {
            return null;
        }

        var placementResult = FirmamentPlacementResolver.ResolvePlacementTranslation(boolean, publishedBodiesByFeatureId);
        if (!placementResult.IsSuccess)
        {
            return null;
        }

        return PlaceSemanticToolBody(boolean, toolBody, placementResult.Value);
    }

    private static BrepBody PlaceSemanticToolBody(FirmamentLoweredBoolean boolean, BrepBody toolBody, Vector3D placementTranslation)
    {
        var shouldApplyDefaultToolFrame = string.IsNullOrWhiteSpace(boolean.Placement?.AroundAxis);
        var localFrameBody = shouldApplyDefaultToolFrame
            ? ApplyDefaultToolLocalFrame(boolean.Tool, toolBody)
            : toolBody;
        return TranslateBody(localFrameBody, placementTranslation);
    }

    private static KernelResult<BrepBody> ExecuteBoundedPrismSubtractOnBoxRoot(
        FirmamentLoweredBoolean boolean,
        FirmamentPrismToolDescriptor prismTool,
        BrepBody baseBody,
        IReadOnlyDictionary<string, FirmamentSafeSubtractFeatureGraphState> featureGraphStates)
    {
        if (boolean.Kind != FirmamentLoweredBooleanKind.Subtract)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.NotImplemented,
                    KernelDiagnosticSeverity.Error,
                    $"Bounded prism-family boolean support accepts subtract only; got '{boolean.Kind.ToString().ToLowerInvariant()}' for tool op '{prismTool.OpName}'.",
                    Source: "firmament.prism-bounded-subtract")
            ]);
        }

        if (!featureGraphStates.TryGetValue(boolean.PrimaryReferenceFeatureId, out var sourceState)
            || sourceState != FirmamentSafeSubtractFeatureGraphState.BoxRoot)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    $"Bounded prism-family subtract requires a direct box-root source feature; '{boolean.PrimaryReferenceFeatureId}' is not a supported box root input for tool op '{prismTool.OpName}'.",
                    Source: "firmament.prism-bounded-subtract")
            ]);
        }

        if (boolean.Placement is not null)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    $"Bounded prism-family subtract on box-root supports default tool placement only; placement directives are not supported for tool op '{prismTool.OpName}'.",
                    Source: "firmament.prism-bounded-subtract")
            ]);
        }

        if (!BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(baseBody, ToleranceContext.Default, out var rootBox, out var reason))
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.NotImplemented,
                    KernelDiagnosticSeverity.Error,
                    $"Bounded prism-family subtract requires an axis-aligned box root from BrepPrimitives.CreateBox(...); failed to recognize source body ({reason}).",
                    Source: "firmament.prism-bounded-subtract")
            ]);
        }

        var toolHeight = prismTool.ResolveHeight(boolean.Tool);
        var rootHeight = rootBox.MaxZ - rootBox.MinZ;
        if (toolHeight < rootHeight - ToleranceContext.Default.Linear)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    $"Bounded prism-family subtract on box-root requires a through-cut tool that spans the full root height ({rootHeight.ToString("G", CultureInfo.InvariantCulture)}). Got height '{toolHeight.ToString("G", CultureInfo.InvariantCulture)}' for tool op '{prismTool.OpName}'.",
                    Source: "firmament.prism-bounded-subtract")
            ]);
        }

        var footprint = FirmamentPrismFamilyTools.ResolveFootprint(prismTool, boolean.Tool)
            .Select(point => (X: point.X, Y: point.Y))
            .ToArray();

        foreach (var point in footprint)
        {
            var insideX = point.X > rootBox.MinX + ToleranceContext.Default.Linear && point.X < rootBox.MaxX - ToleranceContext.Default.Linear;
            var insideY = point.Y > rootBox.MinY + ToleranceContext.Default.Linear && point.Y < rootBox.MaxY - ToleranceContext.Default.Linear;
            if (!insideX || !insideY)
            {
                return KernelResult<BrepBody>.Failure([
                    new KernelDiagnostic(
                        KernelDiagnosticCode.ValidationFailed,
                        KernelDiagnosticSeverity.Error,
                        $"Bounded prism-family subtract on box-root requires the prism footprint to remain strictly inside the source box bounds; tool op '{prismTool.OpName}' is out of bounds.",
                        Source: "firmament.prism-bounded-subtract")
                ]);
            }
        }

        return BrepBooleanBoxPrismThroughCutBuilder.Build(rootBox, footprint, ToleranceContext.Default);
    }

    private static KernelResult<BrepBody> ExecuteBoundedDraftOnRecognizedOrthogonalRoot(
        FirmamentLoweredBoolean boolean,
        BrepBody baseBody,
        IReadOnlyDictionary<string, FirmamentSafeSubtractFeatureGraphState> featureGraphStates)
    {
        if (!featureGraphStates.TryGetValue(boolean.PrimaryReferenceFeatureId, out var sourceState)
            || sourceState is not (FirmamentSafeSubtractFeatureGraphState.BoxRoot
                or FirmamentSafeSubtractFeatureGraphState.BoundedOrthogonalAdditiveSafeRoot
                or FirmamentSafeSubtractFeatureGraphState.BoundedOrthogonalAdditiveOutsideSafeRoot))
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    $"Bounded draft requires a box-root or recognized orthogonal additive root input; '{boolean.PrimaryReferenceFeatureId}' is not eligible.",
                    Source: "firmament.draft-bounded")
            ]);
        }

        if (!BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(baseBody, ToleranceContext.Default, out var box, out var reason))
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    $"Bounded draft requires an axis-aligned box-like source body; failed to recognize source body ({reason}).",
                    Source: "firmament.draft-bounded")
            ]);
        }

        if (!boolean.Tool.RawFields.TryGetValue("pull", out var pullRaw)
            || !string.Equals(UnquoteScalar(pullRaw), "+z", StringComparison.Ordinal))
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    "Bounded draft supports principal pull direction '+z' only.",
                    Source: "firmament.draft-bounded")
            ]);
        }

        if (!boolean.Tool.RawFields.TryGetValue("angle", out var angleRaw)
            || !FirmamentPrimitiveToolParsing.TryParseScalar(angleRaw, out var angleDegrees))
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    "Bounded draft execution expected validated 'angle' scalar.",
                    Source: "firmament.draft-bounded")
            ]);
        }

        if (!TryParseDraftFaces(boolean.Tool.RawFields, out var faces, out var faceError))
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    $"Bounded draft faces are invalid: {faceError}.",
                    Source: "firmament.draft-bounded")
            ]);
        }

        return BrepBoundedDraft.DraftAxisAlignedBoxSideFaces(box, angleDegrees, faces);
    }

    private static KernelResult<BrepBody> ExecuteBoundedChamferOnRecognizedOrthogonalRoot(
        FirmamentLoweredBoolean boolean,
        BrepBody baseBody,
        IReadOnlyDictionary<string, FirmamentSafeSubtractFeatureGraphState> featureGraphStates)
    {
        if (!featureGraphStates.TryGetValue(boolean.PrimaryReferenceFeatureId, out var sourceState))
        {
            return KernelResult<BrepBody>.Failure(
            [
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    $"Bounded chamfer requires a resolvable source body state; '{boolean.PrimaryReferenceFeatureId}' is not eligible.",
                    Source: "firmament.chamfer-bounded")
            ]);
        }

        if (!boolean.Tool.RawFields.TryGetValue("distance", out var distanceRaw)
            || !double.TryParse(distanceRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var distance)
            || !double.IsFinite(distance))
        {
            return KernelResult<BrepBody>.Failure(
            [
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    "Bounded chamfer execution expected validated 'distance' scalar.",
                    Source: "firmament.chamfer-bounded")
            ]);
        }

        if (!BrepBoundedEdgeFinishingToolParser.TryParseChamferSelection(boolean.Tool.RawFields, out var edge, out var incidentEdgePair, out var corner, out var edgeError))
        {
            return KernelResult<BrepBody>.Failure(
            [
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    $"Bounded chamfer edges are invalid: {edgeError}.",
                    Source: "firmament.chamfer-bounded")
            ]);
        }

        if (edge.HasValue)
        {
            if (edge.Value.IsInternalConcaveToken())
            {
                if (sourceState is not (FirmamentSafeSubtractFeatureGraphState.BoundedOrthogonalAdditiveSafeRoot
                    or FirmamentSafeSubtractFeatureGraphState.BoundedOrthogonalAdditiveOutsideSafeRoot
                    or FirmamentSafeSubtractFeatureGraphState.SafeSubtractComposition))
                {
                    return KernelResult<BrepBody>.Failure(
                    [
                        new KernelDiagnostic(
                            KernelDiagnosticCode.ValidationFailed,
                            KernelDiagnosticSeverity.Error,
                            $"Bounded concave chamfer edge mode requires a recognized occupied-cell additive or safe-subtract composition root input; '{boolean.PrimaryReferenceFeatureId}' is not eligible.",
                            Source: "firmament.chamfer-bounded")
                    ]);
                }

                return BrepBoundedChamfer.ChamferTrustedPolyhedralSingleInternalConcaveEdge(baseBody, edge.Value, distance);
            }

            if (sourceState is not (FirmamentSafeSubtractFeatureGraphState.BoxRoot or FirmamentSafeSubtractFeatureGraphState.BoundedOrthogonalAdditiveSafeRoot))
            {
                return KernelResult<BrepBody>.Failure(
                [
                    new KernelDiagnostic(
                        KernelDiagnosticCode.ValidationFailed,
                        KernelDiagnosticSeverity.Error,
                        $"Bounded chamfer edge mode requires a box-root or recognized orthogonal additive root input; '{boolean.PrimaryReferenceFeatureId}' is not eligible.",
                        Source: "firmament.chamfer-bounded")
                ]);
            }

            if (!BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(baseBody, ToleranceContext.Default, out var box, out var reason))
            {
                return KernelResult<BrepBody>.Failure(
                [
                    new KernelDiagnostic(
                        KernelDiagnosticCode.ValidationFailed,
                        KernelDiagnosticSeverity.Error,
                        $"Bounded chamfer edge mode requires an axis-aligned box-like source body; failed to recognize source body ({reason}).",
                        Source: "firmament.chamfer-bounded")
                ]);
            }

            return BrepBoundedChamfer.ChamferAxisAlignedBoxVerticalEdge(box, edge.Value, distance);
        }

        if (incidentEdgePair.HasValue)
        {
            return BrepBoundedChamfer.ChamferTrustedPolyhedralIncidentEdgePair(baseBody, incidentEdgePair.Value, distance);
        }

        if (corner.HasValue)
        {
            if (BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(baseBody, ToleranceContext.Default, out var box, out _))
            {
                return BrepBoundedChamfer.ChamferAxisAlignedBoxSingleCorner(box, corner.Value, distance);
            }

            return BrepBoundedChamfer.ChamferTrustedPolyhedralSingleCorner(baseBody, corner.Value, distance);
        }

        return KernelResult<BrepBody>.Failure(
        [
            new KernelDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error,
                "Bounded chamfer expected either one edge or one corner selection token.",
                Source: "firmament.chamfer-bounded")
        ]);
    }

    private static KernelResult<BrepBody> ExecuteBoundedManufacturingFilletOnRecognizedOrthogonalRoot(
        FirmamentLoweredBoolean boolean,
        BrepBody baseBody,
        IReadOnlyDictionary<string, FirmamentSafeSubtractFeatureGraphState> featureGraphStates)
    {
        if (!featureGraphStates.TryGetValue(boolean.PrimaryReferenceFeatureId, out var sourceState)
            || sourceState is not (FirmamentSafeSubtractFeatureGraphState.BoundedOrthogonalAdditiveSafeRoot
                or FirmamentSafeSubtractFeatureGraphState.BoundedOrthogonalAdditiveOutsideSafeRoot
                or FirmamentSafeSubtractFeatureGraphState.SafeSubtractComposition))
        {
            if (baseBody.SafeBooleanComposition is not null)
            {
                sourceState = FirmamentSafeSubtractFeatureGraphState.SafeSubtractComposition;
            }
            else
            {
                return KernelResult<BrepBody>.Failure(
                [
                    new KernelDiagnostic(
                        KernelDiagnosticCode.ValidationFailed,
                        KernelDiagnosticSeverity.Error,
                        $"Bounded M5b fillet requires a recognized orthogonal additive or safe subtract root input; '{boolean.PrimaryReferenceFeatureId}' is not eligible.",
                        Source: "firmament.fillet-bounded")
                ]);
            }
        }

        if (sourceState is not (FirmamentSafeSubtractFeatureGraphState.BoundedOrthogonalAdditiveSafeRoot
            or FirmamentSafeSubtractFeatureGraphState.BoundedOrthogonalAdditiveOutsideSafeRoot
            or FirmamentSafeSubtractFeatureGraphState.SafeSubtractComposition))
        {
            return KernelResult<BrepBody>.Failure(
            [
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    $"Bounded M5b fillet requires a recognized orthogonal additive or safe subtract root input; '{boolean.PrimaryReferenceFeatureId}' is not eligible.",
                    Source: "firmament.fillet-bounded")
            ]);
        }

        if (!boolean.Tool.RawFields.TryGetValue("radius", out var radiusRaw)
            || !double.TryParse(radiusRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var radius)
            || !double.IsFinite(radius))
        {
            return KernelResult<BrepBody>.Failure(
            [
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    "Bounded M5b fillet execution expected validated 'radius' scalar.",
                    Source: "firmament.fillet-bounded")
            ]);
        }

        if (!BrepBoundedEdgeFinishingToolParser.TryParseFilletEdges(boolean.Tool.RawFields, out var edges, out var edgeError))
        {
            return KernelResult<BrepBody>.Failure(
            [
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    $"Bounded fillet edges are invalid: {edgeError}.",
                    Source: "firmament.fillet-bounded")
            ]);
        }

        var composition = baseBody.SafeBooleanComposition;
        if (composition is null)
        {
            if (!BrepBooleanSafeComposition.TryRecognize(baseBody, ToleranceContext.Default, out var recognized, out var reason))
            {
                return KernelResult<BrepBody>.Failure(
                [
                    new KernelDiagnostic(
                        KernelDiagnosticCode.ValidationFailed,
                        KernelDiagnosticSeverity.Error,
                        $"Bounded M5b fillet requires source safe-composition recognition for explicit internal concave edge preflight; recognition failed ({reason}).",
                        Source: "firmament.fillet-bounded")
                ]);
            }

            composition = recognized;
        }

        var preflight = BrepBoundedManufacturingFilletPreflight.ResolveInternalConcaveVerticalEdges(
            composition,
            edges,
            radius);
        if (!preflight.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(preflight.Diagnostics);
        }

        var selection = preflight.Value;
        return BrepBoundedFillet.FilletTrustedPolyhedralSingleInternalConcaveEdge(baseBody, selection, radius);
    }

    private static bool TryParseDraftFaces(
        IReadOnlyDictionary<string, string> fields,
        out BrepBoundedDraftFaces faces,
        out string error)
    {
        faces = BrepBoundedDraftFaces.None;
        error = string.Empty;

        if (!fields.TryGetValue("faces", out var facesRaw))
        {
            error = "missing 'faces' field";
            return false;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(facesRaw);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                error = "expected array";
                return false;
            }

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.ValueKind != System.Text.Json.JsonValueKind.String)
                {
                    error = "all face entries must be strings";
                    return false;
                }

                var token = element.GetString();
                faces |= token switch
                {
                    "x_min" => BrepBoundedDraftFaces.XMin,
                    "x_max" => BrepBoundedDraftFaces.XMax,
                    "y_min" => BrepBoundedDraftFaces.YMin,
                    "y_max" => BrepBoundedDraftFaces.YMax,
                    _ => BrepBoundedDraftFaces.None
                };
            }
        }
        catch (System.Text.Json.JsonException)
        {
            var trimmed = facesRaw.Trim();
            if (!trimmed.StartsWith("[", StringComparison.Ordinal) || !trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                error = "faces must be valid array syntax";
                return false;
            }

            foreach (var token in trimmed[1..^1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var parsedToken = token.Trim().Trim('"');
                faces |= parsedToken switch
                {
                    "x_min" => BrepBoundedDraftFaces.XMin,
                    "x_max" => BrepBoundedDraftFaces.XMax,
                    "y_min" => BrepBoundedDraftFaces.YMin,
                    "y_max" => BrepBoundedDraftFaces.YMax,
                    _ => BrepBoundedDraftFaces.None
                };
            }
        }

        if (faces == BrepBoundedDraftFaces.None)
        {
            error = "no supported side faces were selected";
            return false;
        }

        return true;
    }

    private static string UnquoteScalar(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"')
            ? trimmed[1..^1]
            : trimmed;
    }

    private static BrepBody TranslateBody(BrepBody body, Vector3D translation)
        => FirmamentPrimitiveExecutionTranslation.TranslateBody(body, translation);
}

internal sealed record FirmamentExecutedPrimitiveBodies(BrepBody Published, BrepBody LegacyForBoolean, Vector3D PlacementTranslation);

internal static class FirmamentPrimitiveToolParsing
{
    public static FirmamentLoweredBoxParameters ParseBox(string sizeRaw)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(sizeRaw);
        var elements = doc.RootElement.EnumerateArray().ToArray();
        return new FirmamentLoweredBoxParameters(ParseScalar(elements[0].ToString()), ParseScalar(elements[1].ToString()), ParseScalar(elements[2].ToString()));
    }

    public static double ParseScalar(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            trimmed = trimmed[1..^1];
        }

        return double.Parse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static bool TryParseScalar(string raw, out double value)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            trimmed = trimmed[1..^1];
        }

        return double.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
