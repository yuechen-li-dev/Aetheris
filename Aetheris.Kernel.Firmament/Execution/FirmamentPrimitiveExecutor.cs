using System.Globalization;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Firmament.Validation;

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

        var tolerance = ToleranceContext.Default;

        foreach (var primitive in loweringPlan.Primitives.OrderBy(p => p.OpIndex))
        {
            var bodyResult = ExecutePrimitive(primitive, publishedBodiesByFeatureId);
            if (!bodyResult.IsSuccess)
            {
                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(bodyResult.Diagnostics);
            }

            executedPrimitives.Add(new FirmamentExecutedPrimitive(primitive.OpIndex, primitive.FeatureId, primitive.Kind, bodyResult.Value.Published));
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
                continue;
            }

            var toolResult = FirmamentBooleanToolBodyFactory.CreateBody(boolean.Tool);
            if (!toolResult.IsSuccess)
            {
                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(toolResult.Diagnostics);
            }

            if (FirmamentPrismFamilyTools.TryGetDescriptor(boolean.Tool.OpName, out var prismTool))
            {
                var prismResult = ExecuteBoundedPrismSubtractOnBoxRoot(
                    boolean,
                    prismTool,
                    baseBody,
                    featureGraphStates);
                if (!prismResult.IsSuccess)
                {
                    return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(
                    [
                        CreateBooleanExecutionFailureDiagnostic(boolean),
                        .. WithBooleanContext(boolean, prismResult.Diagnostics)
                    ]);
                }

                var prismBooleanBody = prismResult.Value;
                executedBooleans.Add(new FirmamentExecutedBoolean(boolean.OpIndex, boolean.FeatureId, boolean.Kind, prismBooleanBody));
                publishedBodiesByFeatureId[boolean.FeatureId] = prismBooleanBody;
                booleanExecutionBodiesByFeatureId[boolean.FeatureId] = prismBooleanBody;
                featureGraphStates[boolean.FeatureId] = FirmamentSafeSubtractFeatureGraphState.Other;
                continue;
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

                var translatedToolBody = TranslateBody(ApplyDefaultToolLocalFrame(boolean.Tool, toolResult.Value), placementResult.Value);
                booleanResult = ExecuteBoolean(boolean.Kind, publishedBaseBody, translatedToolBody);

                if (booleanResult.IsSuccess)
                {
                    usedPublishedFrameAdditiveExecution = true;
                }
                else
                {
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

                var translatedToolBody = PlaceSemanticToolBody(boolean, toolResult.Value, placementResult.Value);
                booleanResult = ExecuteBoolean(boolean.Kind, baseBody, translatedToolBody);
            }
            else
            {
                booleanResult = ExecuteBoolean(boolean.Kind, baseBody, toolResult.Value);
            }

            if (!booleanResult.IsSuccess)
            {
                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(
                [
                    CreateBooleanExecutionFailureDiagnostic(boolean),
                    .. WithBooleanContext(boolean, booleanResult.Diagnostics)
                ]);
            }

            var placedBooleanBody = booleanResult.Value;
            if (!useSemanticToolPlacement && !usedPublishedFrameAdditiveExecution)
            {
                var placementResult = FirmamentPlacementResolver.ResolvePlacementTranslation(boolean, publishedBodiesByFeatureId);
                if (!placementResult.IsSuccess)
                {
                    return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(placementResult.Diagnostics);
                }

                placedBooleanBody = TranslateBody(placedBooleanBody, placementResult.Value);
            }

            executedBooleans.Add(new FirmamentExecutedBoolean(boolean.OpIndex, boolean.FeatureId, boolean.Kind, placedBooleanBody));
            publishedBodiesByFeatureId[boolean.FeatureId] = placedBooleanBody;
            booleanExecutionBodiesByFeatureId[boolean.FeatureId] = placedBooleanBody;
            featureGraphStates[boolean.FeatureId] = DetermineBooleanFeatureGraphState(
                boolean,
                featureGraphValidation.Value.ResultState,
                placedBooleanBody,
                tolerance);
        }

        return KernelResult<FirmamentPrimitiveExecutionResult>.Success(new FirmamentPrimitiveExecutionResult(executedPrimitives, executedBooleans));
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

    private static bool IsPatternGeneratedFeature(string featureId)
        => featureId.Contains("__lin", StringComparison.Ordinal)
           || featureId.Contains("__cir", StringComparison.Ordinal)
           || featureId.Contains("__mir_", StringComparison.Ordinal);

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
        return KernelResult<FirmamentExecutedPrimitiveBodies>.Success(new FirmamentExecutedPrimitiveBodies(publishedBody, legacyResult.Value));
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
        if (!isCylinderTool && !isConeTool)
        {
            return false;
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

        if (string.IsNullOrWhiteSpace(boolean.Placement.OnFace))
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

        if (!BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(baseBody, ToleranceContext.Default, out _, out var reason))
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.NotImplemented,
                    KernelDiagnosticSeverity.Error,
                    $"Bounded prism-family subtract requires an axis-aligned box root from BrepPrimitives.CreateBox(...); failed to recognize source body ({reason}).",
                    Source: "firmament.prism-bounded-subtract")
            ]);
        }
        
        return KernelResult<BrepBody>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.NotImplemented,
                KernelDiagnosticSeverity.Error,
                $"Bounded prism-family subtract on box-root is validated, but execution is currently blocked: core M13 boolean rebuild supports box/box and analytic-hole families only; polyhedral prism tools ('{prismTool.OpName}') are not rebuildable yet.",
                Source: "firmament.prism-bounded-subtract")
        ]);
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
        if (!featureGraphStates.TryGetValue(boolean.PrimaryReferenceFeatureId, out var sourceState)
            || sourceState is not (FirmamentSafeSubtractFeatureGraphState.BoxRoot or FirmamentSafeSubtractFeatureGraphState.BoundedOrthogonalAdditiveSafeRoot))
        {
            return KernelResult<BrepBody>.Failure(
            [
                new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    $"Bounded chamfer requires a box-root or recognized orthogonal additive root input; '{boolean.PrimaryReferenceFeatureId}' is not eligible.",
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
                    $"Bounded chamfer requires an axis-aligned box-like source body; failed to recognize source body ({reason}).",
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

        if (!TryParseChamferEdge(boolean.Tool.RawFields, out var edge, out var edgeError))
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

        return BrepBoundedChamfer.ChamferAxisAlignedBoxVerticalEdge(box, edge, distance);
    }

    private static bool TryParseChamferEdge(
        IReadOnlyDictionary<string, string> fields,
        out BrepBoundedChamferEdge edge,
        out string error)
    {
        edge = BrepBoundedChamferEdge.XMinYMin;
        error = string.Empty;

        if (!fields.TryGetValue("edges", out var edgesRaw) || string.IsNullOrWhiteSpace(edgesRaw))
        {
            error = "missing required edges list";
            return false;
        }

        var trimmed = edgesRaw.Trim();
        if (!trimmed.StartsWith("[", StringComparison.Ordinal) || !trimmed.EndsWith("]", StringComparison.Ordinal))
        {
            error = "expected array-like edges value";
            return false;
        }

        var content = trimmed[1..^1];
        var tokens = content.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim().Trim('"'))
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();
        if (tokens.Length != 1)
        {
            error = "bounded M5a supports exactly one explicit edge token";
            return false;
        }

        edge = tokens[0] switch
        {
            "x_min_y_min" => BrepBoundedChamferEdge.XMinYMin,
            "x_min_y_max" => BrepBoundedChamferEdge.XMinYMax,
            "x_max_y_min" => BrepBoundedChamferEdge.XMaxYMin,
            "x_max_y_max" => BrepBoundedChamferEdge.XMaxYMax,
            _ => BrepBoundedChamferEdge.XMinYMin
        };
        if (tokens[0] is not ("x_min_y_min" or "x_min_y_max" or "x_max_y_min" or "x_max_y_max"))
        {
            error = "supported tokens are x_min_y_min, x_min_y_max, x_max_y_min, x_max_y_max";
            return false;
        }

        return true;
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
    {
        if (translation == Vector3D.Zero) return body;

        var translatedGeometry = new BrepGeometryStore();
        foreach (var curveEntry in body.Geometry.Curves)
        {
            translatedGeometry.AddCurve(curveEntry.Key, curveEntry.Value.Kind switch
            {
                CurveGeometryKind.Line3 => CurveGeometry.FromLine(new Line3Curve(curveEntry.Value.Line3!.Value.Origin + translation, curveEntry.Value.Line3.Value.Direction)),
                CurveGeometryKind.Circle3 => CurveGeometry.FromCircle(new Circle3Curve(curveEntry.Value.Circle3!.Value.Center + translation, curveEntry.Value.Circle3.Value.Normal, curveEntry.Value.Circle3.Value.Radius, curveEntry.Value.Circle3.Value.XAxis)),
                _ => curveEntry.Value
            });
        }

        foreach (var surfaceEntry in body.Geometry.Surfaces)
        {
            translatedGeometry.AddSurface(surfaceEntry.Key, surfaceEntry.Value.Kind switch
            {
                SurfaceGeometryKind.Plane => SurfaceGeometry.FromPlane(new PlaneSurface(surfaceEntry.Value.Plane!.Value.Origin + translation, surfaceEntry.Value.Plane.Value.Normal, surfaceEntry.Value.Plane.Value.UAxis)),
                SurfaceGeometryKind.Cylinder => SurfaceGeometry.FromCylinder(new CylinderSurface(surfaceEntry.Value.Cylinder!.Value.Origin + translation, surfaceEntry.Value.Cylinder.Value.Axis, surfaceEntry.Value.Cylinder.Value.Radius, surfaceEntry.Value.Cylinder.Value.XAxis)),
                SurfaceGeometryKind.Cone => SurfaceGeometry.FromCone(new ConeSurface(surfaceEntry.Value.Cone!.Value.PlacementOrigin + translation, surfaceEntry.Value.Cone.Value.Axis, surfaceEntry.Value.Cone.Value.PlacementRadius, surfaceEntry.Value.Cone.Value.SemiAngleRadians, surfaceEntry.Value.Cone.Value.ReferenceAxis)),
                SurfaceGeometryKind.Torus => SurfaceGeometry.FromTorus(new TorusSurface(surfaceEntry.Value.Torus!.Value.Center + translation, surfaceEntry.Value.Torus.Value.Axis, surfaceEntry.Value.Torus.Value.MajorRadius, surfaceEntry.Value.Torus.Value.MinorRadius, surfaceEntry.Value.Torus.Value.XAxis)),
                SurfaceGeometryKind.Sphere => SurfaceGeometry.FromSphere(new SphereSurface(surfaceEntry.Value.Sphere!.Value.Center + translation, surfaceEntry.Value.Sphere.Value.Axis, surfaceEntry.Value.Sphere.Value.Radius, surfaceEntry.Value.Sphere.Value.XAxis)),
                _ => surfaceEntry.Value
            });
        }

        var vertexPoints = new Dictionary<VertexId, Point3D>();
        foreach (var vertex in body.Topology.Vertices)
        {
            if (FirmamentPlacementResolver.TryGetVertexPoint(body, vertex.Id, out var point))
            {
                vertexPoints[vertex.Id] = point + translation;
            }
        }

        return new BrepBody(body.Topology, translatedGeometry, body.Bindings, vertexPoints, body.SafeBooleanComposition?.Translate(translation));
    }
}

internal sealed record FirmamentExecutedPrimitiveBodies(BrepBody Published, BrepBody LegacyForBoolean);

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
