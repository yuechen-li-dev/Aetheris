using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public enum BooleanOperation
{
    Union,
    Subtract,
    Intersect,
}

public sealed record BooleanRequest(
    BrepBody Left,
    BrepBody Right,
    BooleanOperation Operation,
    ToleranceContext? Tolerance = null);

public sealed record BooleanAnalysis(
    BooleanOperation Operation,
    bool IsSameBodyInstance,
    string? ShortcutReason);

public sealed record BooleanIntersectionData(
    BooleanAnalysis Analysis,
    bool IsComputed,
    int CandidatePairCount);

public sealed record BooleanClassificationData(
    BooleanIntersectionData Intersections,
    bool IsComputed,
    int FragmentCount);

public sealed record BooleanRebuildData(
    BooleanClassificationData Classification,
    BrepBody? RebuiltBody,
    IReadOnlyList<KernelDiagnostic> Diagnostics);

/// <summary>
/// M12 boolean pipeline scaffold.
/// Only same-instance Union/Intersect shortcuts are supported in this milestone.
/// All other requests return deterministic NotImplemented diagnostics.
/// </summary>
public static class BrepBoolean
{
    public static KernelResult<BrepBody> Union(BrepBody left, BrepBody right)
        => Execute(new BooleanRequest(left, right, BooleanOperation.Union));

    public static KernelResult<BrepBody> Subtract(BrepBody left, BrepBody right)
        => Execute(new BooleanRequest(left, right, BooleanOperation.Subtract));

    public static KernelResult<BrepBody> Intersect(BrepBody left, BrepBody right)
        => Execute(new BooleanRequest(left, right, BooleanOperation.Intersect));

    public static KernelResult<BrepBody> Execute(BooleanRequest? request)
    {
        var validation = ValidateInputs(request);
        if (!validation.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(validation.Diagnostics);
        }

        var analysis = AnalyzeInputs(validation.Value);
        var intersections = ComputeIntersections(analysis);
        var classification = ClassifyFragments(intersections);
        var rebuild = RebuildResult(validation.Value, classification);
        if (rebuild.Diagnostics.Any(d => d.Severity == KernelDiagnosticSeverity.Error))
        {
            return KernelResult<BrepBody>.Failure(rebuild.Diagnostics);
        }

        if (rebuild.RebuiltBody is null)
        {
            return KernelResult<BrepBody>.Failure([
                CreateInternalError("Pipeline completed without a rebuilt body.", source: "BrepBoolean.RebuildResult"),
            ]);
        }

        return ValidateOutput(rebuild.RebuiltBody, rebuild.Diagnostics);
    }

    private static KernelResult<BooleanRequest> ValidateInputs(BooleanRequest? request)
    {
        if (request is null)
        {
            return KernelResult<BooleanRequest>.Failure([
                CreateInvalidArgument("Boolean request must be provided.", source: "BrepBoolean.ValidateInputs"),
            ]);
        }

        var diagnostics = new List<KernelDiagnostic>();
        if (request.Left is null)
        {
            diagnostics.Add(CreateInvalidArgument($"Boolean {request.Operation}: left body must be provided.", source: "BrepBoolean.ValidateInputs"));
        }

        if (request.Right is null)
        {
            diagnostics.Add(CreateInvalidArgument($"Boolean {request.Operation}: right body must be provided.", source: "BrepBoolean.ValidateInputs"));
        }

        return diagnostics.Count > 0
            ? KernelResult<BooleanRequest>.Failure(diagnostics)
            : KernelResult<BooleanRequest>.Success(request);
    }

    private static BooleanAnalysis AnalyzeInputs(BooleanRequest request)
    {
        var isSameBodyInstance = ReferenceEquals(request.Left, request.Right);
        var shortcutReason = isSameBodyInstance
            ? $"Boolean {request.Operation}: same-body shortcut candidate."
            : null;

        return new BooleanAnalysis(request.Operation, isSameBodyInstance, shortcutReason);
    }

    private static BooleanIntersectionData ComputeIntersections(BooleanAnalysis analysis)
    {
        var candidatePairCount = analysis.IsSameBodyInstance ? 1 : 0;
        return new BooleanIntersectionData(analysis, IsComputed: true, candidatePairCount);
    }

    private static BooleanClassificationData ClassifyFragments(BooleanIntersectionData intersections)
    {
        var fragmentCount = intersections.Analysis.IsSameBodyInstance ? 1 : 0;
        return new BooleanClassificationData(intersections, IsComputed: true, fragmentCount);
    }

    private static BooleanRebuildData RebuildResult(BooleanRequest request, BooleanClassificationData classification)
    {
        var operation = request.Operation;
        if (classification.Intersections.Analysis.IsSameBodyInstance)
        {
            return operation switch
            {
                BooleanOperation.Union or BooleanOperation.Intersect => new BooleanRebuildData(
                    classification,
                    RebuiltBody: request.Left,
                    Diagnostics: Array.Empty<KernelDiagnostic>()),
                BooleanOperation.Subtract => new BooleanRebuildData(
                    classification,
                    RebuiltBody: null,
                    Diagnostics:
                    [
                        CreateNotImplemented(
                            $"Boolean {operation}: same-body subtraction requires an empty-body representation that is not available in M12.",
                            source: "BrepBoolean.RebuildResult"),
                    ]),
                _ => new BooleanRebuildData(
                    classification,
                    RebuiltBody: null,
                    Diagnostics:
                    [
                        CreateInternalError($"Boolean {operation}: unexpected operation.", source: "BrepBoolean.RebuildResult"),
                    ]),
            };
        }

        return new BooleanRebuildData(
            classification,
            RebuiltBody: null,
            Diagnostics:
            [
                CreateNotImplemented(
                    $"Boolean {operation}: general B-rep boolean intersection/rebuild is not implemented in M12.",
                    source: "BrepBoolean.RebuildResult"),
            ]);
    }

    private static KernelResult<BrepBody> ValidateOutput(BrepBody body, IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        if (!validation.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(validation.Diagnostics);
        }

        var mergedDiagnostics = diagnostics.Count == 0
            ? validation.Diagnostics
            : [.. diagnostics, .. validation.Diagnostics];

        return KernelResult<BrepBody>.Success(body, mergedDiagnostics);
    }

    private static KernelDiagnostic CreateInvalidArgument(string message, string source)
        => new(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message, source);

    private static KernelDiagnostic CreateNotImplemented(string message, string source)
        => new(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source);

    private static KernelDiagnostic CreateInternalError(string message, string source)
        => new(KernelDiagnosticCode.InternalError, KernelDiagnosticSeverity.Error, message, source);
}
