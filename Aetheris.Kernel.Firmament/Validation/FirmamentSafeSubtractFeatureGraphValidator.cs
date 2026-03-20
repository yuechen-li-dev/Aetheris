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
                    $"Boolean op '{boolean.FeatureId}' at index {boolean.OpIndex} violates safe subtract feature-graph ordering: composed hole chains can only continue with 'subtract', but got '{boolean.Kind.ToString().ToLowerInvariant()}'.");
            }

            if (!usesSupportedSafeHoleTool)
            {
                return Failure(
                    $"Boolean op '{boolean.FeatureId}' at index {boolean.OpIndex} uses unsupported follow-on tool kind '{boolean.Tool.OpName}' after safe subtract composition began; only nested 'cylinder' or 'cone' subtract tools are supported.");
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
                $"Boolean op '{boolean.FeatureId}' at index {boolean.OpIndex} violates safe subtract feature-graph ordering: nested 'cylinder'/'cone' through-hole composition can only start from a box or continue from a previously validated safe subtract result, but '{boolean.PrimaryReferenceFeatureId}' is outside that supported family.");
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
            return Failure($"Boolean op '{boolean.FeatureId}' at index {boolean.OpIndex} could not resolve left feature '{boolean.PrimaryReferenceFeatureId}' for feature-graph validation.");
        }

        var resolvedTolerance = tolerance ?? ToleranceContext.Default;
        if (!BrepBooleanSafeComposition.TryRecognize(leftBody, resolvedTolerance, out var composition, out _))
        {
            return Failure(
                $"Boolean op '{boolean.FeatureId}' at index {boolean.OpIndex} violates safe subtract feature-graph ordering: left feature '{boolean.PrimaryReferenceFeatureId}' is not a recognized base box or previously validated safe subtract composition.");
        }

        var toolBodyResult = FirmamentBooleanToolBodyFactory.CreateBody(boolean.Tool);
        if (!toolBodyResult.IsSuccess)
        {
            return KernelResult<FirmamentSafeSubtractFeatureGraphValidation>.Failure(toolBodyResult.Diagnostics);
        }

        if (!BrepBooleanAnalyticSurfaceRecognition.TryRecognizeAnalyticSurface(toolBodyResult.Value, resolvedTolerance, out var analyticSurface, out var reason))
        {
            return Failure(
                $"Boolean op '{boolean.FeatureId}' at index {boolean.OpIndex} could not recognize nested tool op '{boolean.Tool.OpName}' as an analytic hole candidate for safe subtract feature-graph validation ({reason}).");
        }

        if (!BrepBooleanSafeCompositionGraphValidator.TryValidateNextSubtract(
            composition,
            analyticSurface,
            resolvedTolerance,
            out _,
            out var diagnostic))
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

    private static KernelResult<FirmamentSafeSubtractFeatureGraphValidation> Failure(string message)
        => KernelResult<FirmamentSafeSubtractFeatureGraphValidation>.Failure(
        [
            new KernelDiagnostic(
                KernelDiagnosticCode.NotImplemented,
                KernelDiagnosticSeverity.Error,
                message,
                Source: "firmament.feature-graph")
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
