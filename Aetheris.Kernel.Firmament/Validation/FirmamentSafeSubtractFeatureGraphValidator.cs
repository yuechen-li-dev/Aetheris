using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentSafeSubtractFeatureGraphValidator
{
    public static KernelResult<FirmamentSafeSubtractFeatureGraphValidation> ValidateNextBoolean(
        FirmamentLoweredBoolean boolean,
        IReadOnlyDictionary<string, FirmamentSafeSubtractFeatureGraphState> statesByFeatureId,
        IReadOnlyDictionary<string, BrepBody> executionBodiesByFeatureId,
        ToleranceContext? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(boolean);
        ArgumentNullException.ThrowIfNull(statesByFeatureId);
        ArgumentNullException.ThrowIfNull(executionBodiesByFeatureId);

        var sourceState = statesByFeatureId.TryGetValue(boolean.PrimaryReferenceFeatureId, out var knownState)
            ? knownState
            : FirmamentSafeSubtractFeatureGraphState.Other;
        var usesSupportedSafeHoleTool = IsSupportedSafeHoleTool(boolean.Tool);

        if (sourceState == FirmamentSafeSubtractFeatureGraphState.SafeSubtractComposition)
        {
            if (boolean.Kind != FirmamentLoweredBooleanKind.Subtract)
            {
                return Failure(
                    KernelDiagnosticCode.ValidationFailed,
                    $"Boolean feature '{boolean.FeatureId}' ({boolean.Kind.ToString().ToLowerInvariant()}) cannot continue the safe subtract chain rooted at '{boolean.PrimaryReferenceFeatureId}' because only follow-on subtract operations are supported after a safe-hole composition has started.",
                    "firmament.feature-graph.invalid-composition-order");
            }

            if (!usesSupportedSafeHoleTool)
            {
                return Failure(
                    KernelDiagnosticCode.ValidationFailed,
                    $"Boolean feature '{boolean.FeatureId}' (subtract) uses unsupported follow-on tool kind '{boolean.Tool.OpName}' after safe subtract composition began on '{boolean.PrimaryReferenceFeatureId}'. Only nested cylinder or cone through-hole tools are supported in that chain.",
                    "firmament.feature-graph.unsupported-follow-on-kind");
            }

            return ValidateSupportedSafeSubtract(boolean, executionBodiesByFeatureId, tolerance);
        }

        if (sourceState == FirmamentSafeSubtractFeatureGraphState.BoxRoot)
        {
            if (boolean.Kind == FirmamentLoweredBooleanKind.Subtract && usesSupportedSafeHoleTool)
            {
                return ValidateSupportedSafeSubtract(boolean, executionBodiesByFeatureId, tolerance);
            }

            return KernelResult<FirmamentSafeSubtractFeatureGraphValidation>.Success(
                new FirmamentSafeSubtractFeatureGraphValidation(
                    WasValidated: false,
                    ResultState: FirmamentSafeSubtractFeatureGraphState.Other));
        }

        if (boolean.Kind == FirmamentLoweredBooleanKind.Subtract && usesSupportedSafeHoleTool)
        {
            return Failure(
                KernelDiagnosticCode.ValidationFailed,
                $"Boolean feature '{boolean.FeatureId}' (subtract) cannot re-enter the safe subtract family from '{boolean.PrimaryReferenceFeatureId}'. Safe-hole composition may start only from a box root or continue from a previously validated safe subtract result.",
                "firmament.feature-graph.invalid-composition-order");
        }

        return KernelResult<FirmamentSafeSubtractFeatureGraphValidation>.Success(
            new FirmamentSafeSubtractFeatureGraphValidation(
                WasValidated: false,
                ResultState: FirmamentSafeSubtractFeatureGraphState.Other));
    }

    private static KernelResult<FirmamentSafeSubtractFeatureGraphValidation> ValidateSupportedSafeSubtract(
        FirmamentLoweredBoolean boolean,
        IReadOnlyDictionary<string, BrepBody> executionBodiesByFeatureId,
        ToleranceContext? tolerance)
    {
        if (!executionBodiesByFeatureId.TryGetValue(boolean.PrimaryReferenceFeatureId, out var leftBody))
        {
            return Failure(KernelDiagnosticCode.ValidationFailed, $"Boolean feature '{boolean.FeatureId}' ({boolean.Kind.ToString().ToLowerInvariant()}) could not resolve left feature '{boolean.PrimaryReferenceFeatureId}' for safe subtract feature-graph validation.", "firmament.feature-graph.unresolved-left");
        }

        var resolvedTolerance = tolerance ?? ToleranceContext.Default;
        if (!BrepBooleanSafeComposition.TryRecognize(leftBody, resolvedTolerance, out var composition, out _))
        {
            return Failure(
                KernelDiagnosticCode.ValidationFailed,
                $"Boolean feature '{boolean.FeatureId}' (subtract) cannot use '{boolean.PrimaryReferenceFeatureId}' as a safe subtract input because that earlier result is not a recognized base box or previously validated safe-hole composition.",
                "firmament.feature-graph.invalid-composition-order");
        }

        var toolBodyResult = FirmamentBooleanToolBodyFactory.CreateBody(boolean.Tool);
        if (!toolBodyResult.IsSuccess)
        {
            return KernelResult<FirmamentSafeSubtractFeatureGraphValidation>.Failure(toolBodyResult.Diagnostics);
        }

        if (!BrepBooleanAnalyticSurfaceRecognition.TryRecognizeAnalyticSurface(toolBodyResult.Value, resolvedTolerance, out var analyticSurface, out var reason))
        {
            return Failure(
                KernelDiagnosticCode.ValidationFailed,
                $"Boolean feature '{boolean.FeatureId}' (subtract) could not recognize nested tool op '{boolean.Tool.OpName}' as an analytic through-hole candidate for safe subtract validation ({reason}).",
                "firmament.feature-graph.unrecognized-analytic-hole");
        }

        if (!BrepBooleanSafeCompositionGraphValidator.TryValidateNextSubtract(
            composition,
            analyticSurface,
            resolvedTolerance,
            out _,
            out var diagnostic,
            boolean.FeatureId))
        {
            return KernelResult<FirmamentSafeSubtractFeatureGraphValidation>.Failure([diagnostic!.ToKernelDiagnostic()]);
        }

        return KernelResult<FirmamentSafeSubtractFeatureGraphValidation>.Success(
            new FirmamentSafeSubtractFeatureGraphValidation(
                WasValidated: true,
                ResultState: FirmamentSafeSubtractFeatureGraphState.SafeSubtractComposition));
    }

    private static bool IsSupportedSafeHoleTool(FirmamentLoweredToolOp tool)
        => string.Equals(tool.OpName, "cylinder", StringComparison.Ordinal)
           || string.Equals(tool.OpName, "cone", StringComparison.Ordinal);

    private static KernelResult<FirmamentSafeSubtractFeatureGraphValidation> Failure(KernelDiagnosticCode code, string message, string source)
        => KernelResult<FirmamentSafeSubtractFeatureGraphValidation>.Failure(
        [
            new KernelDiagnostic(
                code,
                KernelDiagnosticSeverity.Error,
                message,
                Source: source)
        ]);
}

internal sealed record FirmamentSafeSubtractFeatureGraphValidation(
    bool WasValidated,
    FirmamentSafeSubtractFeatureGraphState ResultState);

internal enum FirmamentSafeSubtractFeatureGraphState
{
    BoxRoot,
    SafeSubtractComposition,
    Other
}
