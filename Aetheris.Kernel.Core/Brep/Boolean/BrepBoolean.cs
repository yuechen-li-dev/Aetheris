using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

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
    string? ShortcutReason,
    BooleanExecutionClass ExecutionClass,
    SafeBooleanComposition? LeftSafeComposition,
    AxisAlignedBoxExtents? LeftBox,
    AxisAlignedBoxExtents? RightBox,
    AnalyticSurface? RightAnalyticSurface,
    string? UnsupportedReason,
    SupportedPrismaticSubtractTool? RightPrismaticTool = null);

public enum BooleanExecutionClass
{
    PlanarOnly,
    PlanarWithAnalyticHole,
    UnsupportedGeneralCase,
}

public sealed record BooleanCaseClassification(
    BooleanExecutionClass ExecutionClass,
    SafeBooleanComposition? LeftSafeComposition,
    AxisAlignedBoxExtents? LeftBox,
    AxisAlignedBoxExtents? RightBox,
    AnalyticSurface? RightAnalyticSurface,
    string? UnsupportedReason,
    BooleanDiagnostic? Diagnostic = null,
    SupportedPrismaticSubtractTool? RightPrismaticTool = null);

public sealed record BooleanIntersectionData(
    BooleanAnalysis Analysis,
    bool IsComputed,
    int CandidatePairCount,
    AxisAlignedBoxExtents? OverlapBox,
    bool IsTouchingOnly);

public sealed record BooleanClassificationData(
    BooleanIntersectionData Intersections,
    bool IsComputed,
    int FragmentCount,
    AxisAlignedBoxExtents? SingleBoxResult,
    SafeBooleanComposition? SafeCompositionResult,
    string? UnsupportedReason,
    IReadOnlyList<AxisAlignedBoxExtents>? OrthogonalUnionCells = null,
    BooleanDiagnostic? Diagnostic = null,
    SupportedPrismaticSubtractTool? PrismaticThroughCutTool = null);

public sealed record BooleanRebuildData(
    BooleanClassificationData Classification,
    BrepBody? RebuiltBody,
    IReadOnlyList<KernelDiagnostic> Diagnostics);

internal enum BooleanTopLevelCase
{
    UnionWithExistingOrthogonalRoot,
    BoxBoxPlanarOnly,
    SubtractAnalyticHole,
    SubtractIntersectRecognizedSafeRoot,
    SubtractCylinderRootKeyway,
    UnsupportedFromRecognizedSafeComposition,
    UnsupportedGeneral,
}

internal enum BooleanSubtractIntersectFamily
{
    SubtractIntersectOrthogonalCellsExistingRoot,
    SubtractPrismaticThroughCutOnRecognizedRoot,
    SubtractBoxSingleBoxResult,
    SubtractBoxPocketOnRecognizedRoot,
    IntersectSupportedBoundedCase,
    UnsupportedFromRecognizedSafeRoot,
    UnsupportedGeneral,
}

internal sealed record BooleanCaseContext(
    BooleanOperation Operation,
    ToleranceContext Tolerance,
    bool LeftRecognizedBox,
    bool RightRecognizedBox,
    bool LeftHasSafeComposition,
    bool RightRecognizedAnalytic,
    SafeBooleanComposition? LeftSafeComposition,
    AxisAlignedBoxExtents? LeftBox,
    AxisAlignedBoxExtents? RightBox,
    SupportedPrismaticSubtractTool? RightPrismaticTool,
    AnalyticSurface? RightAnalyticSurface,
    bool CanClassifyBoundedOrthogonalUnionWithExistingRoot,
    SafeBooleanComposition? AdditiveComposition,
    string? AdditiveUnsupportedReason,
    bool IsCylinderRootCandidate,
    bool IsCylinderRootKeywaySupported,
    string? CylinderRootKeywayUnsupportedReason);

internal sealed record OrthogonalUnionClassificationContext(
    AxisAlignedBoxExtents Left,
    AxisAlignedBoxExtents Right,
    ToleranceContext Tolerance,
    int SharedAxisSpanCount,
    bool HasPositiveVolumeOverlap,
    bool HasPositiveAreaFaceContact,
    IReadOnlyList<AxisAlignedBoxExtents> CandidateCells,
    bool IsConnectedCellUnion);

internal sealed record BooleanSubtractIntersectContext(
    BooleanOperation Operation,
    ToleranceContext Tolerance,
    SafeBooleanComposition LeftSafeComposition,
    AxisAlignedBoxExtents LeftRootBox,
    AxisAlignedBoxExtents? RightBox,
    SupportedPrismaticSubtractTool? RightPrismaticTool,
    bool LeftHasOccupiedCells,
    bool HasPositiveVolumeOverlap,
    bool IsTouchingOnly,
    bool RightContainsLeft,
    bool CanSubtractToSingleBox,
    bool CanSubtractToOrthogonalPocket,
    string? SubtractPocketUnsupportedReason);

/// <summary>
/// bounded boolean pipeline with narrow real support for axis-aligned box/box cases that resolve to a single box
/// plus the M10k box-minus-Z-aligned-through-cylinder subset that rebuilds to a single box-with-hole solid.
/// Unsupported and non-solid cases return deterministic NotImplemented diagnostics.
/// </summary>
public static class BrepBoolean
{
    internal const string BoxBoxUnionOrthogonalCellsStrictCandidate = "box_box_union_orthogonal_cells_strict";
    internal const string BoxBoxUnionFaceContactCellsCandidate = "box_box_union_face_contact_cells";
    private static readonly JudgmentEngine<BooleanCaseContext> BooleanCaseJudgmentEngine = new();
    private static readonly JudgmentEngine<BooleanSubtractIntersectContext> BooleanSubtractIntersectJudgmentEngine = new();
    private static readonly JudgmentEngine<OrthogonalUnionClassificationContext> OrthogonalUnionJudgmentEngine = new();
    private static readonly IReadOnlyList<JudgmentCandidate<BooleanCaseContext>> BooleanCaseCandidates = BuildBooleanCaseCandidates();
    private static readonly IReadOnlyList<JudgmentCandidate<BooleanSubtractIntersectContext>> BooleanSubtractIntersectCandidates = BuildBooleanSubtractIntersectCandidates();
    private static readonly IReadOnlyList<JudgmentCandidate<OrthogonalUnionClassificationContext>> OrthogonalUnionCandidates = BuildOrthogonalUnionCandidates();

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
        var intersections = ComputeIntersections(validation.Value, analysis);
        var classification = ClassifyFragments(validation.Value, intersections);
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

        var tolerance = request.Tolerance ?? ToleranceContext.Default;
        var classification = ClassifyBooleanCase(request.Left, request.Right, request.Operation, tolerance);

        return new BooleanAnalysis(
            request.Operation,
            isSameBodyInstance,
            shortcutReason,
            classification.ExecutionClass,
            classification.LeftSafeComposition,
            classification.LeftBox,
            classification.RightBox,
            classification.RightAnalyticSurface,
            classification.UnsupportedReason,
            classification.RightPrismaticTool);
    }

    public static BooleanCaseClassification ClassifyBooleanCase(BrepBody leftBody, BrepBody rightBody, BooleanOperation operation, ToleranceContext? tolerance = null)
    {
        var context = BuildBooleanCaseContext(leftBody, rightBody, operation, tolerance);
        var candidateResult = BooleanCaseJudgmentEngine.Evaluate(context, BooleanCaseCandidates);
        if (!candidateResult.IsSuccess)
        {
            return CreateUnsupportedGeneralClassification(context);
        }

        return RouteBooleanCaseSelection(context, candidateResult.Selection!.Value.Candidate.Name, candidateResult);
    }

    private static BooleanCaseContext BuildBooleanCaseContext(BrepBody leftBody, BrepBody rightBody, BooleanOperation operation, ToleranceContext? tolerance)
    {
        var resolvedTolerance = tolerance ?? ToleranceContext.Default;
        var leftRecognized = BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(leftBody, resolvedTolerance, out var leftBox, out _);
        var rightRecognized = BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(rightBody, resolvedTolerance, out var rightBox, out _);
        var rightRecognizedPrismaticTool = BrepBooleanPrismaticToolRecognition.TryRecognize(rightBody, resolvedTolerance, out var rightPrismaticTool, out _);
        if (rightRecognized)
        {
            rightRecognizedPrismaticTool = false;
            rightPrismaticTool = null!;
        }
        var rightAnalyticRecognized = BrepBooleanAnalyticSurfaceRecognition.TryRecognizeAnalyticSurface(rightBody, resolvedTolerance, out var analyticSurface, out _);

        var leftSafeCompositionRecognizedFromBody = leftBody.SafeBooleanComposition is not null;
        var leftSafeComposition = leftSafeCompositionRecognizedFromBody
            ? leftBody.SafeBooleanComposition!
            : null;
        var leftHasSafeComposition = leftSafeCompositionRecognizedFromBody;
        if (operation == BooleanOperation.Subtract
            && !leftRecognized
            && !leftHasSafeComposition
            && BrepBooleanSafeComposition.TryRecognize(leftBody, resolvedTolerance, out var recognizedRootComposition, out _))
        {
            leftSafeComposition = recognizedRootComposition;
            leftHasSafeComposition = true;
        }

        var canClassifyBoundedOrthogonalUnionWithExistingRoot = false;
        SafeBooleanComposition? additiveComposition = null;
        string? additiveUnsupportedReason = null;
        if (operation == BooleanOperation.Union
            && !leftRecognized
            && leftHasSafeComposition
            && rightRecognized
            && leftSafeComposition is not null)
        {
            canClassifyBoundedOrthogonalUnionWithExistingRoot = TryClassifyBoundedOrthogonalUnionWithExistingRoot(
                leftSafeComposition,
                rightBox,
                resolvedTolerance,
                out additiveComposition,
                out additiveUnsupportedReason);
        }

        var isCylinderRootCandidate = operation == BooleanOperation.Subtract
            && leftHasSafeComposition
            && leftSafeComposition is not null
            && leftSafeComposition.RootDescriptor.Kind == SafeBooleanRootKind.Cylinder
            && rightRecognized;
        var isCylinderRootKeywaySupported = false;
        string? cylinderRootKeywayUnsupportedReason = null;
        if (isCylinderRootCandidate)
        {
            isCylinderRootKeywaySupported = TryClassifyBoundedCylinderRootKeyway(
                leftSafeComposition!.RootDescriptor,
                rightBox,
                resolvedTolerance,
                out cylinderRootKeywayUnsupportedReason);
        }

        return new BooleanCaseContext(
            operation,
            resolvedTolerance,
            leftRecognized,
            rightRecognized,
            leftHasSafeComposition,
            rightAnalyticRecognized,
            leftSafeComposition,
            leftRecognized ? leftBox : null,
            rightRecognized ? rightBox : null,
            rightRecognizedPrismaticTool ? rightPrismaticTool : null,
            rightAnalyticRecognized ? analyticSurface : null,
            canClassifyBoundedOrthogonalUnionWithExistingRoot,
            additiveComposition,
            additiveUnsupportedReason,
            isCylinderRootCandidate,
            isCylinderRootKeywaySupported,
            cylinderRootKeywayUnsupportedReason);
    }

    private static IReadOnlyList<JudgmentCandidate<BooleanCaseContext>> BuildBooleanCaseCandidates()
        =>
        [
            new JudgmentCandidate<BooleanCaseContext>(
                Name: BooleanTopLevelCase.UnionWithExistingOrthogonalRoot.ToString(),
                IsAdmissible: context => context.CanClassifyBoundedOrthogonalUnionWithExistingRoot
                    && context.AdditiveComposition is not null,
                Score: _ => 500d,
                RejectionReason: context => context.AdditiveUnsupportedReason ?? "Union-with-existing-root bounded orthogonal additive candidate did not satisfy admissibility constraints."),
            new JudgmentCandidate<BooleanCaseContext>(
                Name: BooleanTopLevelCase.BoxBoxPlanarOnly.ToString(),
                IsAdmissible: context => context.LeftRecognizedBox && context.RightRecognizedBox,
                Score: _ => 400d,
                RejectionReason: _ => "Planar box-box candidate requires both operands to be recognized axis-aligned boxes."),
            new JudgmentCandidate<BooleanCaseContext>(
                Name: BooleanTopLevelCase.SubtractAnalyticHole.ToString(),
                IsAdmissible: context => BooleanGuards.RequireOperation(context.Operation, BooleanOperation.Subtract, out _)
                    && (context.LeftRecognizedBox || context.LeftHasSafeComposition)
                    && context.RightRecognizedAnalytic,
                Score: _ => 300d,
                RejectionReason: context => !BooleanGuards.RequireOperation(context.Operation, BooleanOperation.Subtract, out var operationReason)
                    ? $"Analytic-hole candidate {operationReason}"
                    : "Analytic-hole candidate requires a recognized left root (box/safe-composition) and recognized right analytic tool."),
            new JudgmentCandidate<BooleanCaseContext>(
                Name: BooleanTopLevelCase.SubtractIntersectRecognizedSafeRoot.ToString(),
                IsAdmissible: context =>
                    BooleanGuards.RequireOperation(context.Operation, BooleanOperation.Subtract, BooleanOperation.Intersect, out _)
                    && ((context.LeftHasSafeComposition
                            && context.LeftSafeComposition is not null
                            && context.LeftSafeComposition.RootDescriptor.Kind == SafeBooleanRootKind.Box)
                        || context.LeftRecognizedBox)
                    && (context.RightRecognizedBox
                        || (context.Operation == BooleanOperation.Subtract && context.RightPrismaticTool is not null)),
                Score: _ => 250d,
                RejectionReason: context =>
                    $"Recognized-safe-root subtract/intersect shell requires subtract/intersect, a recognized safe box root, and a recognized bounded right operand (box or subtract-prismatic tool) (op={context.Operation}, hasSafe={context.LeftHasSafeComposition}, rightBox={context.RightRecognizedBox}, rightPrismatic={(context.RightPrismaticTool is not null)})."),
            new JudgmentCandidate<BooleanCaseContext>(
                Name: BooleanTopLevelCase.SubtractCylinderRootKeyway.ToString(),
                IsAdmissible: context => context.IsCylinderRootCandidate,
                Score: _ => 200d,
                RejectionReason: context => context.CylinderRootKeywayUnsupportedReason
                    ?? "Cylinder-root keyway candidate requires subtract, a cylinder safe root, and a recognized box tool."),
            new JudgmentCandidate<BooleanCaseContext>(
                Name: BooleanTopLevelCase.UnsupportedFromRecognizedSafeComposition.ToString(),
                IsAdmissible: context => context.LeftHasSafeComposition,
                Score: _ => 100d,
                RejectionReason: _ => "Recognized safe-composition fallback is only available when the left operand carries a safe composition."),
            new JudgmentCandidate<BooleanCaseContext>(
                Name: BooleanTopLevelCase.UnsupportedGeneral.ToString(),
                IsAdmissible: _ => true,
                Score: _ => 0d)
        ];

    private static BooleanCaseClassification RouteBooleanCaseSelection(
        BooleanCaseContext context,
        string selectedCandidateName,
        JudgmentResult<BooleanCaseContext> candidateResult)
    {
        var selectedCase = Enum.Parse<BooleanTopLevelCase>(selectedCandidateName, ignoreCase: false);
        return selectedCase switch
        {
            BooleanTopLevelCase.UnionWithExistingOrthogonalRoot => new BooleanCaseClassification(
                BooleanExecutionClass.PlanarOnly,
                context.AdditiveComposition,
                context.AdditiveComposition!.OuterBox,
                context.RightBox,
                null,
                null),
            BooleanTopLevelCase.BoxBoxPlanarOnly => new BooleanCaseClassification(
                BooleanExecutionClass.PlanarOnly,
                context.LeftHasSafeComposition ? context.LeftSafeComposition : null,
                context.LeftBox,
                context.RightBox,
                null,
                null),
            BooleanTopLevelCase.SubtractAnalyticHole => new BooleanCaseClassification(
                BooleanExecutionClass.PlanarWithAnalyticHole,
                context.LeftHasSafeComposition
                    ? context.LeftSafeComposition
                    : new SafeBooleanComposition(context.LeftBox!.Value, []),
                context.LeftRecognizedBox ? context.LeftBox : context.LeftSafeComposition!.OuterBox,
                null,
                context.RightAnalyticSurface,
                null),
            BooleanTopLevelCase.SubtractIntersectRecognizedSafeRoot => RouteRecognizedSafeRootSubtractIntersectCase(context),
            BooleanTopLevelCase.SubtractCylinderRootKeyway => context.IsCylinderRootKeywaySupported
                ? new BooleanCaseClassification(
                    BooleanExecutionClass.UnsupportedGeneralCase,
                    context.LeftSafeComposition,
                    context.LeftSafeComposition!.OuterBox,
                    context.RightBox,
                    null,
                    null)
                : new BooleanCaseClassification(
                    BooleanExecutionClass.UnsupportedGeneralCase,
                    context.LeftSafeComposition,
                    context.LeftSafeComposition!.OuterBox,
                    context.RightBox,
                    null,
                    context.CylinderRootKeywayUnsupportedReason ?? "Boolean Subtract: cylinder-root subtract with box tool is outside the bounded keyway family."),
            BooleanTopLevelCase.UnsupportedFromRecognizedSafeComposition => new BooleanCaseClassification(
                BooleanExecutionClass.UnsupportedGeneralCase,
                context.LeftSafeComposition,
                context.LeftRecognizedBox ? context.LeftBox : context.LeftSafeComposition!.OuterBox,
                context.RightBox,
                context.RightAnalyticSurface,
                BuildUnsupportedSafeCompositionReason(context, candidateResult),
                RightPrismaticTool: context.RightPrismaticTool),
            _ => CreateUnsupportedGeneralClassification(context),
        };
    }

    private static BooleanCaseClassification RouteRecognizedSafeRootSubtractIntersectCase(BooleanCaseContext context)
    {
        var subtractIntersectContext = BuildBooleanSubtractIntersectContext(context);
        var selection = BooleanSubtractIntersectJudgmentEngine.Evaluate(subtractIntersectContext, BooleanSubtractIntersectCandidates);
        if (!selection.IsSuccess)
        {
            return new BooleanCaseClassification(
                BooleanExecutionClass.UnsupportedGeneralCase,
                context.LeftSafeComposition,
                subtractIntersectContext.LeftRootBox,
                subtractIntersectContext.RightBox,
                null,
                BuildRecognizedSafeRootSubtractIntersectUnsupportedReason(subtractIntersectContext, selection),
                RightPrismaticTool: subtractIntersectContext.RightPrismaticTool);
        }

        var family = Enum.Parse<BooleanSubtractIntersectFamily>(selection.Selection!.Value.Candidate.Name, ignoreCase: false);
        return family switch
        {
            BooleanSubtractIntersectFamily.SubtractIntersectOrthogonalCellsExistingRoot
            or BooleanSubtractIntersectFamily.SubtractPrismaticThroughCutOnRecognizedRoot
            or BooleanSubtractIntersectFamily.SubtractBoxSingleBoxResult
            or BooleanSubtractIntersectFamily.SubtractBoxPocketOnRecognizedRoot
            or BooleanSubtractIntersectFamily.IntersectSupportedBoundedCase
                => new BooleanCaseClassification(
                    BooleanExecutionClass.PlanarOnly,
                    subtractIntersectContext.LeftSafeComposition,
                    subtractIntersectContext.LeftRootBox,
                    subtractIntersectContext.RightBox,
                    null,
                    null,
                    RightPrismaticTool: subtractIntersectContext.RightPrismaticTool),
            _ => new BooleanCaseClassification(
                BooleanExecutionClass.UnsupportedGeneralCase,
                subtractIntersectContext.LeftSafeComposition,
                subtractIntersectContext.LeftRootBox,
                subtractIntersectContext.RightBox,
                null,
                BuildRecognizedSafeRootSubtractIntersectUnsupportedReason(subtractIntersectContext, selection),
                RightPrismaticTool: subtractIntersectContext.RightPrismaticTool),
        };
    }

    private static BooleanSubtractIntersectContext BuildBooleanSubtractIntersectContext(BooleanCaseContext context)
    {
        var leftComposition = context.LeftSafeComposition
            ?? new SafeBooleanComposition(context.LeftBox!.Value, [], SafeBooleanRootDescriptor.FromBox(context.LeftBox.Value));
        var leftRootBox = leftComposition.RootDescriptor.Box;
        var rightBox = context.RightBox;
        var rightPrismaticTool = context.RightPrismaticTool;
        var rightBounds = rightBox ?? rightPrismaticTool?.Bounds;
        if (rightBounds is null)
        {
            rightBounds = leftComposition.RootDescriptor.Box;
        }
        var overlap = AxisAlignedBoxExtents.Intersection(leftRootBox, rightBounds.Value);
        var hasPositiveVolumeOverlap = overlap is not null && overlap.Value.HasPositiveVolume(context.Tolerance);
        var isTouchingOnly = overlap is not null && !hasPositiveVolumeOverlap;
        var rightContainsLeft = rightBounds.Value.Contains(leftRootBox, context.Tolerance);

        var canSubtractToSingleBox = context.Operation == BooleanOperation.Subtract
            && rightBox is not null
            && TrySubtractToSingleBox(leftRootBox, rightBox.Value, context.Tolerance, out _);
        string? subtractPocketUnsupportedReason = null;
        var canSubtractToOrthogonalPocket = context.Operation == BooleanOperation.Subtract
            && rightBox is not null
            && TryClassifyBoundedOrthogonalPocketSubtract(leftRootBox, rightBox.Value, context.Tolerance, out _, out subtractPocketUnsupportedReason);

        return new BooleanSubtractIntersectContext(
            context.Operation,
            context.Tolerance,
            leftComposition,
            leftRootBox,
            rightBox,
            rightPrismaticTool,
            leftComposition.OccupiedCells is { Count: > 0 },
            hasPositiveVolumeOverlap,
            isTouchingOnly,
            rightContainsLeft,
            canSubtractToSingleBox,
            canSubtractToOrthogonalPocket,
            subtractPocketUnsupportedReason);
    }

    private static IReadOnlyList<JudgmentCandidate<BooleanSubtractIntersectContext>> BuildBooleanSubtractIntersectCandidates()
        =>
        [
            new JudgmentCandidate<BooleanSubtractIntersectContext>(
                Name: BooleanSubtractIntersectFamily.SubtractIntersectOrthogonalCellsExistingRoot.ToString(),
                IsAdmissible: context => context.LeftHasOccupiedCells
                    && BooleanGuards.RequireOperation(context.Operation, BooleanOperation.Subtract, BooleanOperation.Intersect, out _),
                Score: _ => 450d,
                RejectionReason: context => context.LeftHasOccupiedCells
                    ? !BooleanGuards.RequireOperation(context.Operation, BooleanOperation.Subtract, BooleanOperation.Intersect, out var operationReason)
                        ? $"Orthogonal occupied-cell subtract/intersect candidate {operationReason}"
                        : "Orthogonal occupied-cell subtract/intersect candidate requires subtract or intersect."
                    : "Orthogonal occupied-cell subtract/intersect candidate requires a recognized additive safe root with occupied cells."),
            new JudgmentCandidate<BooleanSubtractIntersectContext>(
                Name: BooleanSubtractIntersectFamily.SubtractPrismaticThroughCutOnRecognizedRoot.ToString(),
                IsAdmissible: context => BooleanGuards.RequireOperation(context.Operation, BooleanOperation.Subtract, out _)
                    && context.RightPrismaticTool is not null,
                Score: _ => 425d,
                RejectionReason: context => !BooleanGuards.RequireOperation(context.Operation, BooleanOperation.Subtract, out var operationReason)
                    ? $"Prismatic subtract candidate {operationReason}"
                    : "Prismatic subtract candidate requires a recognized bounded prismatic subtract tool."),
            new JudgmentCandidate<BooleanSubtractIntersectContext>(
                Name: BooleanSubtractIntersectFamily.SubtractBoxSingleBoxResult.ToString(),
                IsAdmissible: context => BooleanGuards.RequireOperation(context.Operation, BooleanOperation.Subtract, out _)
                    && !context.IsTouchingOnly
                    && !context.RightContainsLeft
                    && (!context.HasPositiveVolumeOverlap || context.CanSubtractToSingleBox),
                Score: _ => 400d,
                RejectionReason: context => !BooleanGuards.RequireOperation(context.Operation, BooleanOperation.Subtract, out var operationReason)
                    ? $"Single-box subtract candidate {operationReason}"
                    : context.IsTouchingOnly
                        ? "Single-box subtract candidate rejects touching-only contact."
                        : context.RightContainsLeft
                            ? "Single-box subtract candidate rejects full tool containment of the root (empty result)."
                            : "Single-box subtract candidate requires either no overlap or a subtract result representable as one box."),
            new JudgmentCandidate<BooleanSubtractIntersectContext>(
                Name: BooleanSubtractIntersectFamily.SubtractBoxPocketOnRecognizedRoot.ToString(),
                IsAdmissible: context => BooleanGuards.RequireOperation(context.Operation, BooleanOperation.Subtract, out _)
                    && !context.IsTouchingOnly
                    && context.HasPositiveVolumeOverlap
                    && !context.RightContainsLeft
                    && !context.CanSubtractToSingleBox
                    && context.CanSubtractToOrthogonalPocket,
                Score: _ => 300d,
                RejectionReason: context => !BooleanGuards.RequireOperation(context.Operation, BooleanOperation.Subtract, out var operationReason)
                    ? $"Orthogonal-pocket subtract candidate {operationReason}"
                    : context.SubtractPocketUnsupportedReason
                        ?? "Orthogonal-pocket subtract candidate rejected because bounded pocket predicates were not satisfied."),
            new JudgmentCandidate<BooleanSubtractIntersectContext>(
                Name: BooleanSubtractIntersectFamily.IntersectSupportedBoundedCase.ToString(),
                IsAdmissible: context => BooleanGuards.RequireOperation(context.Operation, BooleanOperation.Intersect, out _),
                Score: _ => 200d,
                RejectionReason: context => BooleanGuards.RequireOperation(context.Operation, BooleanOperation.Intersect, out _)
                    ? "Intersect candidate selected; overlap/emptiness is classified downstream by bounded planar intersection rules."
                    : "Intersect candidate requires 'Intersect'."),
            new JudgmentCandidate<BooleanSubtractIntersectContext>(
                Name: BooleanSubtractIntersectFamily.UnsupportedFromRecognizedSafeRoot.ToString(),
                IsAdmissible: _ => true,
                Score: _ => 100d),
            new JudgmentCandidate<BooleanSubtractIntersectContext>(
                Name: BooleanSubtractIntersectFamily.UnsupportedGeneral.ToString(),
                IsAdmissible: _ => true,
                Score: _ => 0d)
        ];

    private static string BuildRecognizedSafeRootSubtractIntersectUnsupportedReason(
        in BooleanSubtractIntersectContext context,
        JudgmentResult<BooleanSubtractIntersectContext> selection)
    {
        var baseReason =
            $"Boolean {context.Operation}: recognized safe box-root subtract/intersect routing could not match a bounded top-level family.";
        if (context.Operation == BooleanOperation.Subtract
            && context.SubtractPocketUnsupportedReason is not null)
        {
            baseReason = $"{baseReason} {context.SubtractPocketUnsupportedReason}";
        }

        var nearest = selection.Rejections.FirstOrDefault(rejection =>
            rejection.CandidateName != BooleanSubtractIntersectFamily.UnsupportedFromRecognizedSafeRoot.ToString()
            && rejection.CandidateName != BooleanSubtractIntersectFamily.UnsupportedGeneral.ToString());
        return nearest.CandidateName is null
            ? baseReason
            : $"{baseReason} Nearest candidate '{nearest.CandidateName}' rejected: {nearest.Reason}";
    }

    private static BooleanCaseClassification CreateUnsupportedGeneralClassification(BooleanCaseContext context)
    {
        var reason = $"Boolean {context.Operation}: bounded boolean family only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).";

        return new BooleanCaseClassification(
            BooleanExecutionClass.UnsupportedGeneralCase,
            null,
            context.LeftBox,
            context.RightBox,
            context.RightAnalyticSurface,
            reason);
    }

    private static string BuildUnsupportedSafeCompositionReason(BooleanCaseContext context, JudgmentResult<BooleanCaseContext> candidateResult)
    {
        var baseReason =
            $"Boolean {context.Operation}: sequential safe composition only supports subtracting supported analytic holes from the current bounded safe root family.";
        if (context.AdditiveUnsupportedReason is not null)
        {
            return $"{baseReason} Closest bounded union candidate rejected: {context.AdditiveUnsupportedReason}";
        }

        var nearestRejection = candidateResult.Rejections.FirstOrDefault(rejection => rejection.CandidateName is not null && rejection.CandidateName != BooleanTopLevelCase.UnsupportedFromRecognizedSafeComposition.ToString());
        return nearestRejection.CandidateName is null
            ? baseReason
            : $"{baseReason} Nearest candidate '{nearestRejection.CandidateName}' rejected: {nearestRejection.Reason}";
    }

    private static BooleanIntersectionData ComputeIntersections(BooleanRequest request, BooleanAnalysis analysis)
    {
        var rightBounds = analysis.RightBox ?? analysis.RightPrismaticTool?.Bounds;
        if (analysis.LeftBox is null || (rightBounds is null && analysis.RightAnalyticSurface is null))
        {
            return new BooleanIntersectionData(analysis, IsComputed: true, CandidatePairCount: 0, OverlapBox: null, IsTouchingOnly: false);
        }

        if (rightBounds is null)
        {
            return new BooleanIntersectionData(analysis, IsComputed: true, CandidatePairCount: 1, OverlapBox: null, IsTouchingOnly: false);
        }

        var overlap = AxisAlignedBoxExtents.Intersection(analysis.LeftBox.Value, rightBounds.Value);
        var tolerance = request.Tolerance ?? ToleranceContext.Default;
        var isTouchingOnly = overlap is not null && !overlap.Value.HasPositiveVolume(tolerance);
        var overlapBox = overlap is not null && overlap.Value.HasPositiveVolume(tolerance)
            ? overlap
            : null;

        return new BooleanIntersectionData(analysis, IsComputed: true, CandidatePairCount: 1, overlapBox, isTouchingOnly);
    }

    private static BooleanClassificationData ClassifyFragments(BooleanRequest request, BooleanIntersectionData intersections)
    {
        switch (intersections.Analysis.ExecutionClass)
        {
            case BooleanExecutionClass.PlanarOnly:
                return ClassifyPlanarOnly(request, intersections);
            case BooleanExecutionClass.PlanarWithAnalyticHole:
                return ClassifyPlanarWithAnalyticHole(request, intersections);
            default:
                if (TryClassifyBoundedCylinderRootOpenSlot(request, intersections, out var openSlotClassification))
                {
                    return openSlotClassification;
                }

                return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: intersections.Analysis.UnsupportedReason);
        }
    }

    private static bool TryClassifyBoundedCylinderRootOpenSlot(BooleanRequest request, BooleanIntersectionData intersections, out BooleanClassificationData classification)
    {
        classification = default!;
        if (request.Operation != BooleanOperation.Subtract
            || intersections.Analysis.LeftSafeComposition is not SafeBooleanComposition leftComposition
            || intersections.Analysis.RightBox is not AxisAlignedBoxExtents toolBox
            || leftComposition.RootDescriptor.Kind != SafeBooleanRootKind.Cylinder)
        {
            return false;
        }

        var tolerance = request.Tolerance ?? ToleranceContext.Default;
        if (!TryClassifyBoundedCylinderRootKeyway(leftComposition.RootDescriptor, toolBox, tolerance, out _))
        {
            return false;
        }

        classification = new BooleanClassificationData(
            intersections,
            IsComputed: true,
            FragmentCount: 1,
            SingleBoxResult: null,
            SafeCompositionResult: leftComposition with
            {
                OpenSlots = [new SupportedCylinderOpenSlot(toolBox)],
            },
            UnsupportedReason: null);
        return true;
    }

    private static BooleanClassificationData ClassifyPlanarWithAnalyticHole(BooleanRequest request, BooleanIntersectionData intersections)
    {
        var leftComposition = intersections.Analysis.LeftSafeComposition
            ?? new SafeBooleanComposition(intersections.Analysis.LeftBox!.Value, []);
        var left = leftComposition.OuterBox;
        var tolerance = request.Tolerance ?? ToleranceContext.Default;
        var operation = request.Operation;

        if (intersections.Analysis.RightAnalyticSurface is not AnalyticSurface analyticSurface)
        {
            return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: $"Boolean {operation}: analytic-hole classification requires a recognized analytic surface.");
        }

        if (!BrepBooleanSafeCompositionGraphValidator.TryValidateNextSubtract(leftComposition, analyticSurface, tolerance, out var updatedComposition, out var diagnostic))
        {
            return new BooleanClassificationData(
                intersections,
                IsComputed: true,
                FragmentCount: 0,
                SingleBoxResult: null,
                SafeCompositionResult: null,
                UnsupportedReason: null,
                Diagnostic: diagnostic);
        }

        return new BooleanClassificationData(
            intersections,
            IsComputed: true,
            FragmentCount: 1,
            SingleBoxResult: null,
            SafeCompositionResult: updatedComposition,
            UnsupportedReason: null);
    }

    private static BooleanClassificationData ClassifyPlanarOnly(BooleanRequest request, BooleanIntersectionData intersections)
    {
        if (intersections.Analysis.UnsupportedReason is not null)
        {
            return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: intersections.Analysis.UnsupportedReason);
        }

        var left = intersections.Analysis.LeftBox!.Value;
        var tolerance = request.Tolerance ?? ToleranceContext.Default;
        var operation = request.Operation;
        string? prismaticUnsupportedReason = null;
        if (operation == BooleanOperation.Subtract
            && intersections.Analysis.LeftSafeComposition is { } leftCompositionForPrismatic
            && intersections.Analysis.RightPrismaticTool is { } prismaticTool
            && TryClassifyBoundedPrismaticThroughCutSubtract(leftCompositionForPrismatic, prismaticTool, tolerance, out _))
        {
            var throughVoids = BuildThroughVoidSetForPrismaticContinuation(leftCompositionForPrismatic, prismaticTool);
            return new BooleanClassificationData(
                intersections,
                IsComputed: true,
                FragmentCount: 1,
                SingleBoxResult: null,
                SafeCompositionResult: leftCompositionForPrismatic with
                {
                    ThroughVoids = throughVoids,
                },
                UnsupportedReason: null,
                PrismaticThroughCutTool: prismaticTool);
        }
        else if (operation == BooleanOperation.Subtract
            && intersections.Analysis.RightPrismaticTool is not null
            && intersections.Analysis.LeftSafeComposition is not null)
        {
            TryClassifyBoundedPrismaticThroughCutSubtract(intersections.Analysis.LeftSafeComposition, intersections.Analysis.RightPrismaticTool, tolerance, out prismaticUnsupportedReason);
        }

        if (operation == BooleanOperation.Subtract
            && intersections.Analysis.RightPrismaticTool is not null
            && prismaticUnsupportedReason is not null)
        {
            return new BooleanClassificationData(
                intersections,
                IsComputed: true,
                FragmentCount: 0,
                SingleBoxResult: null,
                SafeCompositionResult: null,
                UnsupportedReason: prismaticUnsupportedReason);
        }

        var right = intersections.Analysis.RightBox!.Value;
        string? occupiedUnsupportedReason = null;

        if ((operation == BooleanOperation.Subtract || operation == BooleanOperation.Intersect)
            && intersections.Analysis.LeftSafeComposition is { OccupiedCells.Count: > 0 } occupiedRootComposition
            && TryClassifyBoundedOrthogonalSubtractIntersectWithExistingRoot(
                occupiedRootComposition,
                right,
                operation,
                tolerance,
                out var occupiedResultComposition,
                out occupiedUnsupportedReason))
        {
            return new BooleanClassificationData(
                intersections,
                IsComputed: true,
                FragmentCount: 1,
                SingleBoxResult: null,
                SafeCompositionResult: occupiedResultComposition,
                UnsupportedReason: null,
                OrthogonalUnionCells: occupiedResultComposition.OccupiedCells);
        }

        if ((operation == BooleanOperation.Subtract || operation == BooleanOperation.Intersect)
            && intersections.Analysis.LeftSafeComposition is { OccupiedCells.Count: > 0 }
            && occupiedUnsupportedReason is not null)
        {
            return new BooleanClassificationData(
                intersections,
                IsComputed: true,
                FragmentCount: 0,
                SingleBoxResult: null,
                SafeCompositionResult: null,
                UnsupportedReason: occupiedUnsupportedReason);
        }

        if (operation == BooleanOperation.Intersect)
        {
            if (intersections.IsTouchingOnly)
            {
                return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: $"Boolean {operation}: touching-only intersection is non-solid and empty results are not representable in the bounded boolean family.");
            }

            if (intersections.OverlapBox is null)
            {
                return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: $"Boolean {operation}: empty intersection result is not representable in the bounded boolean family.");
            }

            return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 1, SingleBoxResult: intersections.OverlapBox, SafeCompositionResult: null, UnsupportedReason: null);
        }

        if (operation == BooleanOperation.Union)
        {
            string? additiveUnsupportedReason = null;
            if (intersections.Analysis.LeftSafeComposition is { OccupiedCells.Count: > 0 } leftComposition
                && TryClassifyBoundedOrthogonalUnionWithExistingRoot(leftComposition, right, tolerance, out var additiveComposition, out additiveUnsupportedReason))
            {
                return new BooleanClassificationData(
                    intersections,
                    IsComputed: true,
                    FragmentCount: 1,
                    SingleBoxResult: null,
                    SafeCompositionResult: additiveComposition,
                    UnsupportedReason: null,
                    OrthogonalUnionCells: additiveComposition.OccupiedCells);
            }

            if (intersections.Analysis.LeftSafeComposition is { OccupiedCells.Count: > 0 }
                && additiveUnsupportedReason is not null)
            {
                return new BooleanClassificationData(
                    intersections,
                    IsComputed: true,
                    FragmentCount: 0,
                    SingleBoxResult: null,
                    SafeCompositionResult: null,
                    UnsupportedReason: additiveUnsupportedReason);
            }

            if (left.ApproximatelyEquals(right, tolerance))
            {
                return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 1, SingleBoxResult: left, SafeCompositionResult: null, UnsupportedReason: null);
            }

            if (left.Contains(right, tolerance))
            {
                return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 1, SingleBoxResult: left, SafeCompositionResult: null, UnsupportedReason: null);
            }

            if (right.Contains(left, tolerance))
            {
                return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 1, SingleBoxResult: right, SafeCompositionResult: null, UnsupportedReason: null);
            }

            var bounds = AxisAlignedBoxExtents.Bounding(left, right);
            if (AxisAlignedBoxExtents.UnionIsSingleBox(left, right, bounds, tolerance))
            {
                return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 1, SingleBoxResult: bounds, SafeCompositionResult: null, UnsupportedReason: null);
            }

            if (AxisAlignedBoxExtents.Intersection(left, right) is null)
            {
                return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: $"Boolean {operation}: disjoint box union is multi-body and not supported in the bounded boolean family.");
            }

            if (TryClassifyBoundedOrthogonalUnion(left, right, tolerance, out var unionCells, out var unsupportedReason, out _))
            {
                var unionBounds = AxisAlignedBoxExtents.Bounding(left, right);
                return new BooleanClassificationData(
                    intersections,
                    IsComputed: true,
                    FragmentCount: 1,
                    SingleBoxResult: null,
                    SafeCompositionResult: new SafeBooleanComposition(
                        unionBounds,
                        [],
                        SafeBooleanRootDescriptor.FromBox(unionBounds),
                        unionCells),
                    UnsupportedReason: null,
                    OrthogonalUnionCells: unionCells);
            }

            return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: unsupportedReason ?? $"Boolean {operation}: box union is outside the bounded connected orthogonal additive family for F1.");
        }

        if (intersections.IsTouchingOnly)
        {
            return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: "Boolean Subtract: bounded orthogonal pocket family rejects tangent/zero-thickness subtract contact.");
        }

        if (!left.OverlapsWithPositiveVolume(right, tolerance))
        {
            return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 1, SingleBoxResult: left, SafeCompositionResult: null, UnsupportedReason: null);
        }

        if (right.Contains(left, tolerance))
        {
            return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: $"Boolean {operation}: subtraction fully removes the left box and empty results are not representable in the bounded boolean family.");
        }

        if (TrySubtractToSingleBox(left, right, tolerance, out var singleBox))
        {
            return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 1, SingleBoxResult: singleBox, SafeCompositionResult: null, UnsupportedReason: null);
        }

        if (TryClassifyBoundedOrthogonalPocketSubtract(left, right, tolerance, out var pocketCells, out var pocketUnsupportedReason))
        {
            return new BooleanClassificationData(
                intersections,
                IsComputed: true,
                FragmentCount: 1,
                SingleBoxResult: null,
                SafeCompositionResult: new SafeBooleanComposition(
                    left,
                    [],
                    SafeBooleanRootDescriptor.FromBox(left),
                    pocketCells),
                UnsupportedReason: null,
                OrthogonalUnionCells: pocketCells);
        }

        if (pocketUnsupportedReason is not null)
        {
            return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: pocketUnsupportedReason);
        }

        return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: $"Boolean {operation}: box subtraction result is not representable as a single box in the bounded boolean family.");
    }

    private static BooleanRebuildData RebuildResult(BooleanRequest request, BooleanClassificationData classification)
    {
        var operation = request.Operation;

        if (classification.Diagnostic is not null)
        {
            return new BooleanRebuildData(
                classification,
                RebuiltBody: null,
                Diagnostics:
                [
                    classification.Diagnostic.ToKernelDiagnostic(),
                ]);
        }

        if (classification.OrthogonalUnionCells is not null)
        {
            var rebuiltOrthogonalUnion = BrepBooleanOrthogonalUnionBuilder.BuildFromCells(classification.OrthogonalUnionCells);
            if (!rebuiltOrthogonalUnion.IsSuccess)
            {
                return new BooleanRebuildData(classification, RebuiltBody: null, Diagnostics: rebuiltOrthogonalUnion.Diagnostics);
            }

            var rebuiltBody = classification.SafeCompositionResult is null
                ? rebuiltOrthogonalUnion.Value
                : CopyWithSafeComposition(rebuiltOrthogonalUnion.Value, classification.SafeCompositionResult);
            return new BooleanRebuildData(classification, rebuiltBody, rebuiltOrthogonalUnion.Diagnostics);
        }

        if (classification.PrismaticThroughCutTool is { } prismaticTool
            && classification.Intersections.Analysis.LeftSafeComposition is { } leftComposition)
        {
            if (leftComposition.Holes.Count == 1)
            {
                var rebuiltMixed = BrepBooleanBoxMixedThroughVoidBuilder.Build(leftComposition, prismaticTool, request.Tolerance ?? ToleranceContext.Default);
                return rebuiltMixed.IsSuccess
                    ? new BooleanRebuildData(classification, rebuiltMixed.Value, rebuiltMixed.Diagnostics)
                    : new BooleanRebuildData(classification, RebuiltBody: null, Diagnostics: rebuiltMixed.Diagnostics);
            }

            var rebuiltPrismSubtract = BrepBooleanBoxPrismThroughCutBuilder.Build(leftComposition.RootDescriptor.Box, prismaticTool.Footprint, request.Tolerance ?? ToleranceContext.Default);
            if (!rebuiltPrismSubtract.IsSuccess)
            {
                return new BooleanRebuildData(classification, RebuiltBody: null, Diagnostics: rebuiltPrismSubtract.Diagnostics);
            }

            var rebuiltBody = classification.SafeCompositionResult is null
                ? rebuiltPrismSubtract.Value
                : CopyWithSafeComposition(rebuiltPrismSubtract.Value, classification.SafeCompositionResult);
            return new BooleanRebuildData(classification, rebuiltBody, rebuiltPrismSubtract.Diagnostics);
        }

        if (classification.SafeCompositionResult is not null)
        {
            var tolerance = request.Tolerance ?? ToleranceContext.Default;
            var rebuiltThroughHole = BrepBooleanBoxCylinderHoleBuilder.BuildComposition(classification.SafeCompositionResult, tolerance);
            return rebuiltThroughHole.IsSuccess
                ? new BooleanRebuildData(classification, rebuiltThroughHole.Value, rebuiltThroughHole.Diagnostics)
                : new BooleanRebuildData(classification, RebuiltBody: null, Diagnostics: rebuiltThroughHole.Diagnostics);
        }

        if (classification.UnsupportedReason is not null)
        {
            return new BooleanRebuildData(
                classification,
                RebuiltBody: null,
                Diagnostics:
                [
                    CreateNotImplemented(classification.UnsupportedReason, source: "BrepBoolean.RebuildResult"),
                ]);
        }

        if (classification.SingleBoxResult is null)
        {
            return new BooleanRebuildData(
                classification,
                RebuiltBody: null,
                Diagnostics:
                [
                    CreateInternalError($"Boolean {operation}: classification omitted both result and unsupported reason.", source: "BrepBoolean.RebuildResult"),
                ]);
        }

        var rebuilt = BrepBooleanBoxRecognition.CreateBoxFromExtents(classification.SingleBoxResult.Value);
        return rebuilt.IsSuccess
            ? new BooleanRebuildData(classification, rebuilt.Value, Array.Empty<KernelDiagnostic>())
            : new BooleanRebuildData(classification, RebuiltBody: null, Diagnostics: rebuilt.Diagnostics);
    }

    private static bool TrySubtractToSingleBox(AxisAlignedBoxExtents left, AxisAlignedBoxExtents right, ToleranceContext tolerance, out AxisAlignedBoxExtents singleBox)
    {
        singleBox = default;
        var overlap = AxisAlignedBoxExtents.Intersection(left, right);
        if (overlap is null || !overlap.Value.HasPositiveVolume(tolerance))
        {
            return false;
        }

        var o = overlap.Value;

        if (ToleranceMath.GreaterThanOrAlmostEqual(o.MinX, left.MinX, tolerance) && ToleranceMath.AlmostEqual(o.MaxX, left.MaxX, tolerance) &&
            ToleranceMath.AlmostEqual(o.MinY, left.MinY, tolerance) && ToleranceMath.AlmostEqual(o.MaxY, left.MaxY, tolerance) &&
            ToleranceMath.AlmostEqual(o.MinZ, left.MinZ, tolerance) && ToleranceMath.AlmostEqual(o.MaxZ, left.MaxZ, tolerance))
        {
            singleBox = new AxisAlignedBoxExtents(left.MinX, o.MinX, left.MinY, left.MaxY, left.MinZ, left.MaxZ);
            return singleBox.HasPositiveVolume(tolerance);
        }

        if (ToleranceMath.LessThanOrAlmostEqual(o.MaxX, left.MaxX, tolerance) && ToleranceMath.AlmostEqual(o.MinX, left.MinX, tolerance) &&
            ToleranceMath.AlmostEqual(o.MinY, left.MinY, tolerance) && ToleranceMath.AlmostEqual(o.MaxY, left.MaxY, tolerance) &&
            ToleranceMath.AlmostEqual(o.MinZ, left.MinZ, tolerance) && ToleranceMath.AlmostEqual(o.MaxZ, left.MaxZ, tolerance))
        {
            singleBox = new AxisAlignedBoxExtents(o.MaxX, left.MaxX, left.MinY, left.MaxY, left.MinZ, left.MaxZ);
            return singleBox.HasPositiveVolume(tolerance);
        }

        if (ToleranceMath.GreaterThanOrAlmostEqual(o.MinY, left.MinY, tolerance) && ToleranceMath.AlmostEqual(o.MaxY, left.MaxY, tolerance) &&
            ToleranceMath.AlmostEqual(o.MinX, left.MinX, tolerance) && ToleranceMath.AlmostEqual(o.MaxX, left.MaxX, tolerance) &&
            ToleranceMath.AlmostEqual(o.MinZ, left.MinZ, tolerance) && ToleranceMath.AlmostEqual(o.MaxZ, left.MaxZ, tolerance))
        {
            singleBox = new AxisAlignedBoxExtents(left.MinX, left.MaxX, left.MinY, o.MinY, left.MinZ, left.MaxZ);
            return singleBox.HasPositiveVolume(tolerance);
        }

        if (ToleranceMath.LessThanOrAlmostEqual(o.MaxY, left.MaxY, tolerance) && ToleranceMath.AlmostEqual(o.MinY, left.MinY, tolerance) &&
            ToleranceMath.AlmostEqual(o.MinX, left.MinX, tolerance) && ToleranceMath.AlmostEqual(o.MaxX, left.MaxX, tolerance) &&
            ToleranceMath.AlmostEqual(o.MinZ, left.MinZ, tolerance) && ToleranceMath.AlmostEqual(o.MaxZ, left.MaxZ, tolerance))
        {
            singleBox = new AxisAlignedBoxExtents(left.MinX, left.MaxX, o.MaxY, left.MaxY, left.MinZ, left.MaxZ);
            return singleBox.HasPositiveVolume(tolerance);
        }

        if (ToleranceMath.GreaterThanOrAlmostEqual(o.MinZ, left.MinZ, tolerance) && ToleranceMath.AlmostEqual(o.MaxZ, left.MaxZ, tolerance) &&
            ToleranceMath.AlmostEqual(o.MinX, left.MinX, tolerance) && ToleranceMath.AlmostEqual(o.MaxX, left.MaxX, tolerance) &&
            ToleranceMath.AlmostEqual(o.MinY, left.MinY, tolerance) && ToleranceMath.AlmostEqual(o.MaxY, left.MaxY, tolerance))
        {
            singleBox = new AxisAlignedBoxExtents(left.MinX, left.MaxX, left.MinY, left.MaxY, left.MinZ, o.MinZ);
            return singleBox.HasPositiveVolume(tolerance);
        }

        if (ToleranceMath.LessThanOrAlmostEqual(o.MaxZ, left.MaxZ, tolerance) && ToleranceMath.AlmostEqual(o.MinZ, left.MinZ, tolerance) &&
            ToleranceMath.AlmostEqual(o.MinX, left.MinX, tolerance) && ToleranceMath.AlmostEqual(o.MaxX, left.MaxX, tolerance) &&
            ToleranceMath.AlmostEqual(o.MinY, left.MinY, tolerance) && ToleranceMath.AlmostEqual(o.MaxY, left.MaxY, tolerance))
        {
            singleBox = new AxisAlignedBoxExtents(left.MinX, left.MaxX, left.MinY, left.MaxY, o.MaxZ, left.MaxZ);
            return singleBox.HasPositiveVolume(tolerance);
        }

        return false;
    }

    private static bool TryClassifyBoundedOrthogonalUnion(
        AxisAlignedBoxExtents left,
        AxisAlignedBoxExtents right,
        ToleranceContext tolerance,
        out IReadOnlyList<AxisAlignedBoxExtents> occupiedCells,
        out string? unsupportedReason,
        out string? selectedCandidateName)
    {
        occupiedCells = Array.Empty<AxisAlignedBoxExtents>();
        unsupportedReason = null;
        selectedCandidateName = null;

        var intersection = AxisAlignedBoxExtents.Intersection(left, right);
        if (intersection is null)
        {
            unsupportedReason = "Boolean Union: disjoint box union is multi-body and not supported in the bounded boolean family.";
            return false;
        }

        var connectedByVolume = intersection.Value.HasPositiveVolume(tolerance);
        var connectedByFace = IsPositiveAreaFaceContact(left, right, tolerance);
        if (!connectedByVolume && !connectedByFace)
        {
            unsupportedReason = "Boolean Union: boxes do not intersect or touch with positive-area face contact; edge-only/point-only unions are non-manifold.";
            return false;
        }

        if (!TryBuildBoundedOrthogonalUnionCells(left, right, tolerance, out var cells))
        {
            unsupportedReason = "Boolean Union: bounded orthogonal reconstruction produced no occupied cells.";
            return false;
        }

        var sharedAxisSpanCount = CountSharedFullAxisSpans(left, right, tolerance);
        var isConnectedCellUnion = TryValidateOrthogonalCellConnectivity(cells, tolerance);
        var candidateContext = new OrthogonalUnionClassificationContext(
            left,
            right,
            tolerance,
            sharedAxisSpanCount,
            connectedByVolume,
            connectedByFace,
            cells,
            isConnectedCellUnion);

        var classification = OrthogonalUnionJudgmentEngine.Evaluate(candidateContext, OrthogonalUnionCandidates);
        if (!classification.IsSuccess)
        {
            var closestRejection = classification.Rejections.FirstOrDefault();
            unsupportedReason = closestRejection.CandidateName is null
                ? "Boolean Union: union not reconstructible via orthogonal cell decomposition."
                : $"Boolean Union: candidate '{closestRejection.CandidateName}' rejected: {closestRejection.Reason}";
            return false;
        }

        selectedCandidateName = classification.Selection!.Value.Candidate.Name;
        occupiedCells = cells;
        return true;
    }

    internal static string? ClassifyBoundedOrthogonalUnionCandidateForTesting(
        AxisAlignedBoxExtents left,
        AxisAlignedBoxExtents right,
        ToleranceContext tolerance)
    {
        return TryClassifyBoundedOrthogonalUnion(left, right, tolerance, out _, out _, out var selectedCandidateName)
            ? selectedCandidateName
            : null;
    }

    private static bool TryClassifyBoundedOrthogonalUnionWithExistingRoot(
        SafeBooleanComposition leftComposition,
        AxisAlignedBoxExtents right,
        ToleranceContext tolerance,
        out SafeBooleanComposition additiveComposition,
        out string? unsupportedReason)
    {
        additiveComposition = leftComposition;
        unsupportedReason = null;

        var leftCells = leftComposition.OccupiedCells;
        if (leftCells is null || leftCells.Count == 0)
        {
            unsupportedReason = "Boolean Union: additive root does not carry bounded orthogonal occupancy cells for chained union recognition.";
            return false;
        }

        var combined = new List<AxisAlignedBoxExtents>(leftCells.Count + 1);
        combined.AddRange(leftCells);
        combined.Add(right);

        var distinctX = combined.SelectMany(cell => new[] { cell.MinX, cell.MaxX }).Distinct().OrderBy(v => v).ToArray();
        var distinctY = combined.SelectMany(cell => new[] { cell.MinY, cell.MaxY }).Distinct().OrderBy(v => v).ToArray();
        var distinctZ = combined.SelectMany(cell => new[] { cell.MinZ, cell.MaxZ }).Distinct().OrderBy(v => v).ToArray();
        var refinedCells = new List<AxisAlignedBoxExtents>();

        for (var ix = 0; ix < distinctX.Length - 1; ix++)
        {
            for (var iy = 0; iy < distinctY.Length - 1; iy++)
            {
                for (var iz = 0; iz < distinctZ.Length - 1; iz++)
                {
                    var candidate = new AxisAlignedBoxExtents(
                        distinctX[ix],
                        distinctX[ix + 1],
                        distinctY[iy],
                        distinctY[iy + 1],
                        distinctZ[iz],
                        distinctZ[iz + 1]);
                    if (!candidate.HasPositiveVolume(tolerance))
                    {
                        continue;
                    }

                    if (combined.Any(cell => cell.Contains(candidate, tolerance)))
                    {
                        refinedCells.Add(candidate);
                    }
                }
            }
        }

        if (refinedCells.Count == 0)
        {
            unsupportedReason = "Boolean Union: additive-root refinement produced no occupied orthogonal cells.";
            return false;
        }

        var bounds = AxisAlignedBoxExtents.Bounding(refinedCells[0], refinedCells[0]);
        for (var i = 1; i < refinedCells.Count; i++)
        {
            bounds = AxisAlignedBoxExtents.Bounding(bounds, refinedCells[i]);
        }

        if (!TryValidateOrthogonalCellConnectivity(refinedCells, tolerance))
        {
            unsupportedReason = "Boolean Union: additive-root chained union is disjoint or only edge-connected after bounded orthogonal refinement.";
            return false;
        }

        additiveComposition = new SafeBooleanComposition(
            bounds,
            leftComposition.Holes,
            SafeBooleanRootDescriptor.FromBox(bounds),
            refinedCells);
        return true;
    }

    private static bool TryClassifyBoundedOrthogonalSubtractIntersectWithExistingRoot(
        SafeBooleanComposition leftComposition,
        AxisAlignedBoxExtents right,
        BooleanOperation operation,
        ToleranceContext tolerance,
        out SafeBooleanComposition composition,
        out string? unsupportedReason)
    {
        composition = leftComposition;
        unsupportedReason = null;
        if (leftComposition.OccupiedCells is not { Count: > 0 } sourceCells)
        {
            unsupportedReason = $"Boolean {operation}: recognized occupied-cell root is required for bounded subtract/intersect refinement.";
            return false;
        }

        List<AxisAlignedBoxExtents> outputCells = [];
        foreach (var cell in sourceCells)
        {
            if (operation == BooleanOperation.Intersect)
            {
                var intersection = AxisAlignedBoxExtents.Intersection(cell, right);
                if (intersection is { } overlapCell && overlapCell.HasPositiveVolume(tolerance))
                {
                    outputCells.Add(overlapCell);
                }

                continue;
            }

            foreach (var fragment in SubtractBoxFromCell(cell, right, tolerance))
            {
                outputCells.Add(fragment);
            }
        }

        if (outputCells.Count == 0)
        {
            unsupportedReason = operation == BooleanOperation.Intersect
                ? $"Boolean {operation}: empty intersection result is not representable in the bounded boolean family."
                : $"Boolean {operation}: subtraction fully removes the left body and empty results are not representable in the bounded boolean family.";
            return false;
        }

        var bounds = ComputeBoundingBox(outputCells);
        composition = leftComposition with
        {
            OuterBox = bounds,
            OccupiedCells = outputCells,
            Root = leftComposition.RootDescriptor.Kind == SafeBooleanRootKind.Box
                ? SafeBooleanRootDescriptor.FromBox(bounds)
                : leftComposition.Root,
        };
        return true;
    }

    private static IEnumerable<AxisAlignedBoxExtents> SubtractBoxFromCell(
        AxisAlignedBoxExtents cell,
        AxisAlignedBoxExtents tool,
        ToleranceContext tolerance)
    {
        var overlap = AxisAlignedBoxExtents.Intersection(cell, tool);
        if (overlap is null || !overlap.Value.HasPositiveVolume(tolerance))
        {
            yield return cell;
            yield break;
        }

        if (tool.Contains(cell, tolerance))
        {
            yield break;
        }

        var cut = overlap.Value;
        var xSpans = new (double Min, double Max)[]
        {
            (cell.MinX, cut.MinX),
            (cut.MinX, cut.MaxX),
            (cut.MaxX, cell.MaxX),
        };
        var ySpans = new (double Min, double Max)[]
        {
            (cell.MinY, cut.MinY),
            (cut.MinY, cut.MaxY),
            (cut.MaxY, cell.MaxY),
        };
        var zSpans = new (double Min, double Max)[]
        {
            (cell.MinZ, cut.MinZ),
            (cut.MinZ, cut.MaxZ),
            (cut.MaxZ, cell.MaxZ),
        };

        for (var xi = 0; xi < xSpans.Length; xi++)
        {
            var xSpan = xSpans[xi];
            if (xSpan.Max - xSpan.Min <= tolerance.Linear)
            {
                continue;
            }

            for (var yi = 0; yi < ySpans.Length; yi++)
            {
                var ySpan = ySpans[yi];
                if (ySpan.Max - ySpan.Min <= tolerance.Linear)
                {
                    continue;
                }

                for (var zi = 0; zi < zSpans.Length; zi++)
                {
                    if (xi == 1 && yi == 1 && zi == 1)
                    {
                        continue;
                    }

                    var zSpan = zSpans[zi];
                    if (zSpan.Max - zSpan.Min <= tolerance.Linear)
                    {
                        continue;
                    }

                    yield return new AxisAlignedBoxExtents(
                        xSpan.Min,
                        xSpan.Max,
                        ySpan.Min,
                        ySpan.Max,
                        zSpan.Min,
                        zSpan.Max);
                }
            }
        }
    }

    private static AxisAlignedBoxExtents ComputeBoundingBox(IReadOnlyList<AxisAlignedBoxExtents> cells)
    {
        var bounds = cells[0];
        for (var i = 1; i < cells.Count; i++)
        {
            bounds = AxisAlignedBoxExtents.Bounding(bounds, cells[i]);
        }

        return bounds;
    }

    private static bool TryValidateOrthogonalCellConnectivity(IReadOnlyList<AxisAlignedBoxExtents> cells, ToleranceContext tolerance)
    {
        if (cells.Count == 0)
        {
            return false;
        }

        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        visited.Add(0);
        queue.Enqueue(0);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            for (var i = 0; i < cells.Count; i++)
            {
                if (visited.Contains(i) || !CellsShareVolumeOrFace(cells[current], cells[i], tolerance))
                {
                    continue;
                }

                visited.Add(i);
                queue.Enqueue(i);
            }
        }

        return visited.Count == cells.Count;
    }

    private static IReadOnlyList<JudgmentCandidate<OrthogonalUnionClassificationContext>> BuildOrthogonalUnionCandidates()
        =>
        [
            new JudgmentCandidate<OrthogonalUnionClassificationContext>(
                Name: BoxBoxUnionOrthogonalCellsStrictCandidate,
                IsAdmissible: context => context.SharedAxisSpanCount > 0
                    && (context.HasPositiveVolumeOverlap || context.HasPositiveAreaFaceContact)
                    && context.CandidateCells.Count > 0
                    && context.IsConnectedCellUnion,
                Score: context => 200d + context.SharedAxisSpanCount,
                RejectionReason: context => context.SharedAxisSpanCount == 0
                    ? "strict candidate requires shared full axis span on at least one primary axis."
                    : !context.IsConnectedCellUnion
                        ? "union not reconstructible via orthogonal cell decomposition (cells are disconnected or only edge-connected)."
                        : "strict orthogonal union candidate predicates were not satisfied."),
            new JudgmentCandidate<OrthogonalUnionClassificationContext>(
                Name: BoxBoxUnionFaceContactCellsCandidate,
                IsAdmissible: context => (context.HasPositiveVolumeOverlap || context.HasPositiveAreaFaceContact)
                    && context.CandidateCells.Count > 0
                    && context.IsConnectedCellUnion,
                Score: context => context.HasPositiveVolumeOverlap ? 125d : 100d,
                RejectionReason: context =>
                {
                    if (!context.HasPositiveVolumeOverlap && !context.HasPositiveAreaFaceContact)
                    {
                        return "boxes do not intersect or touch with positive-area face contact.";
                    }

                    if (context.CandidateCells.Count == 0)
                    {
                        return "union not reconstructible via orthogonal cell decomposition (no occupied cells).";
                    }

                    if (!context.IsConnectedCellUnion)
                    {
                        return "ambiguous multi-region union not supported: bounded orthogonal cells are disconnected or only edge-connected.";
                    }

                    return "face-contact orthogonal cell candidate predicates were not satisfied.";
                }),
        ];

    private static int CountSharedFullAxisSpans(AxisAlignedBoxExtents left, AxisAlignedBoxExtents right, ToleranceContext tolerance)
    {
        var sharedAxisSpanCount = 0;
        if (ToleranceMath.AlmostEqual(left.MinX, right.MinX, tolerance) && ToleranceMath.AlmostEqual(left.MaxX, right.MaxX, tolerance))
        {
            sharedAxisSpanCount++;
        }

        if (ToleranceMath.AlmostEqual(left.MinY, right.MinY, tolerance) && ToleranceMath.AlmostEqual(left.MaxY, right.MaxY, tolerance))
        {
            sharedAxisSpanCount++;
        }

        if (ToleranceMath.AlmostEqual(left.MinZ, right.MinZ, tolerance) && ToleranceMath.AlmostEqual(left.MaxZ, right.MaxZ, tolerance))
        {
            sharedAxisSpanCount++;
        }

        return sharedAxisSpanCount;
    }

    private static bool TryBuildBoundedOrthogonalUnionCells(
        AxisAlignedBoxExtents left,
        AxisAlignedBoxExtents right,
        ToleranceContext tolerance,
        out IReadOnlyList<AxisAlignedBoxExtents> cells)
    {
        cells = Array.Empty<AxisAlignedBoxExtents>();
        var splitX = new[] { left.MinX, left.MaxX, right.MinX, right.MaxX }.Distinct().OrderBy(v => v).ToArray();
        var splitY = new[] { left.MinY, left.MaxY, right.MinY, right.MaxY }.Distinct().OrderBy(v => v).ToArray();
        var splitZ = new[] { left.MinZ, left.MaxZ, right.MinZ, right.MaxZ }.Distinct().OrderBy(v => v).ToArray();
        var reconstructedCells = new List<AxisAlignedBoxExtents>();
        for (var ix = 0; ix < splitX.Length - 1; ix++)
        {
            for (var iy = 0; iy < splitY.Length - 1; iy++)
            {
                for (var iz = 0; iz < splitZ.Length - 1; iz++)
                {
                    var candidate = new AxisAlignedBoxExtents(splitX[ix], splitX[ix + 1], splitY[iy], splitY[iy + 1], splitZ[iz], splitZ[iz + 1]);
                    if (!candidate.HasPositiveVolume(tolerance))
                    {
                        continue;
                    }

                    if (left.Contains(candidate, tolerance) || right.Contains(candidate, tolerance))
                    {
                        reconstructedCells.Add(candidate);
                    }
                }
            }
        }

        cells = reconstructedCells;
        return reconstructedCells.Count > 0;
    }

    private static bool TryClassifyBoundedCylinderRootKeyway(
        in SafeBooleanRootDescriptor rootDescriptor,
        AxisAlignedBoxExtents toolBox,
        ToleranceContext tolerance,
        out string? unsupportedReason)
    {
        unsupportedReason = null;
        if (rootDescriptor.Kind != SafeBooleanRootKind.Cylinder || rootDescriptor.Cylinder is not RecognizedCylinder rootCylinder)
        {
            unsupportedReason = "Boolean Subtract: bounded keyway family requires a recognized cylinder root.";
            return false;
        }

        if (!BrepBooleanCylinderRecognition.ValidateAxisAlignedZ(rootCylinder, tolerance))
        {
            unsupportedReason = "Boolean Subtract: bounded keyway family requires a world-Z aligned cylinder root.";
            return false;
        }

        var rootCenterX = (rootCylinder.MinCenter.X + rootCylinder.MaxCenter.X) * 0.5d;
        var rootCenterY = (rootCylinder.MinCenter.Y + rootCylinder.MaxCenter.Y) * 0.5d;
        var rootMinZ = System.Math.Min(rootCylinder.MinCenter.Z, rootCylinder.MaxCenter.Z);
        var rootMaxZ = System.Math.Max(rootCylinder.MinCenter.Z, rootCylinder.MaxCenter.Z);

        if (toolBox.MinZ > (rootMinZ + tolerance.Linear) || toolBox.MaxZ < (rootMaxZ - tolerance.Linear))
        {
            unsupportedReason = "Boolean Subtract: bounded keyway family requires a through slot that spans both cylinder end caps along world-Z.";
            return false;
        }

        var hasInteriorPenetration = DistanceFromPointToRectangle(rootCenterX, rootCenterY, toolBox) < (rootCylinder.Radius - tolerance.Linear);
        if (!hasInteriorPenetration)
        {
            unsupportedReason = "Boolean Subtract: bounded keyway family requires the rectangular tool to penetrate inside the shaft radius.";
            return false;
        }

        if ((toolBox.MaxY - toolBox.MinY) <= (2d * tolerance.Linear))
        {
            unsupportedReason = "Boolean Subtract: bounded keyway family requires strictly positive keyway side-wall span (non-degenerate slot width).";
            return false;
        }

        var dyMin = toolBox.MinY - rootCenterY;
        var dyMax = toolBox.MaxY - rootCenterY;
        if (System.Math.Abs(dyMin) >= (rootCylinder.Radius - tolerance.Linear)
            || System.Math.Abs(dyMax) >= (rootCylinder.Radius - tolerance.Linear))
        {
            unsupportedReason = "Boolean Subtract: bounded keyway family requires side walls to intersect the cylindrical wall with positive span (tangent side walls are unsupported).";
            return false;
        }

        var reachesFarSideWall = ContainsPoint(toolBox.MinX, toolBox.MaxX, rootCenterX - rootCylinder.Radius, tolerance)
            && ContainsPoint(toolBox.MinY, toolBox.MaxY, rootCenterY, tolerance);
        if (reachesFarSideWall)
        {
            unsupportedReason = "Boolean Subtract: bounded keyway family requires a single exterior mouth and cannot cut through the far-side cylindrical wall.";
            return false;
        }

        var positiveWallAtMinY = (rootCenterX + System.Math.Sqrt((rootCylinder.Radius * rootCylinder.Radius) - (dyMin * dyMin))) - toolBox.MinX;
        var positiveWallAtMaxY = (rootCenterX + System.Math.Sqrt((rootCylinder.Radius * rootCylinder.Radius) - (dyMax * dyMax))) - toolBox.MinX;
        if (positiveWallAtMinY <= (2d * tolerance.Linear) || positiveWallAtMaxY <= (2d * tolerance.Linear))
        {
            unsupportedReason = "Boolean Subtract: bounded keyway family requires strictly positive wall thickness between the slot floor and cylindrical wall.";
            return false;
        }

        return true;
    }

    private static bool ContainsPoint(double min, double max, double value, ToleranceContext tolerance)
        => value >= (min - tolerance.Linear) && value <= (max + tolerance.Linear);

    private static double DistanceFromPointToRectangle(double px, double py, AxisAlignedBoxExtents rectangle)
    {
        var dx = px < rectangle.MinX ? rectangle.MinX - px : px > rectangle.MaxX ? px - rectangle.MaxX : 0d;
        var dy = py < rectangle.MinY ? rectangle.MinY - py : py > rectangle.MaxY ? py - rectangle.MaxY : 0d;
        return System.Math.Sqrt((dx * dx) + (dy * dy));
    }


    private static bool CellsShareVolumeOrFace(AxisAlignedBoxExtents a, AxisAlignedBoxExtents b, ToleranceContext tolerance)
    {
        var overlap = AxisAlignedBoxExtents.Intersection(a, b);
        if (overlap is not null && overlap.Value.HasPositiveVolume(tolerance))
        {
            return true;
        }

        return IsPositiveAreaFaceContact(a, b, tolerance);
    }

    private static bool IsPositiveAreaFaceContact(AxisAlignedBoxExtents left, AxisAlignedBoxExtents right, ToleranceContext tolerance)
    {
        var xFaceTouch = (ToleranceMath.AlmostEqual(left.MaxX, right.MinX, tolerance) || ToleranceMath.AlmostEqual(right.MaxX, left.MinX, tolerance))
            && PositiveOverlap(left.MinY, left.MaxY, right.MinY, right.MaxY, tolerance)
            && PositiveOverlap(left.MinZ, left.MaxZ, right.MinZ, right.MaxZ, tolerance);
        var yFaceTouch = (ToleranceMath.AlmostEqual(left.MaxY, right.MinY, tolerance) || ToleranceMath.AlmostEqual(right.MaxY, left.MinY, tolerance))
            && PositiveOverlap(left.MinX, left.MaxX, right.MinX, right.MaxX, tolerance)
            && PositiveOverlap(left.MinZ, left.MaxZ, right.MinZ, right.MaxZ, tolerance);
        var zFaceTouch = (ToleranceMath.AlmostEqual(left.MaxZ, right.MinZ, tolerance) || ToleranceMath.AlmostEqual(right.MaxZ, left.MinZ, tolerance))
            && PositiveOverlap(left.MinX, left.MaxX, right.MinX, right.MaxX, tolerance)
            && PositiveOverlap(left.MinY, left.MaxY, right.MinY, right.MaxY, tolerance);
        return xFaceTouch || yFaceTouch || zFaceTouch;
    }

    private static bool TryClassifyBoundedOrthogonalPocketSubtract(
        AxisAlignedBoxExtents left,
        AxisAlignedBoxExtents right,
        ToleranceContext tolerance,
        out IReadOnlyList<AxisAlignedBoxExtents> occupiedCells,
        out string? unsupportedReason)
    {
        occupiedCells = Array.Empty<AxisAlignedBoxExtents>();
        unsupportedReason = null;

        var overlap = AxisAlignedBoxExtents.Intersection(left, right);
        if (overlap is null || !overlap.Value.HasPositiveVolume(tolerance))
        {
            unsupportedReason = "Boolean Subtract: orthogonal pocket family requires positive-volume overlap between the root and subtract tool.";
            return false;
        }

        var removed = overlap.Value;
        var touchesMinX = ToleranceMath.AlmostEqual(removed.MinX, left.MinX, tolerance);
        var touchesMaxX = ToleranceMath.AlmostEqual(removed.MaxX, left.MaxX, tolerance);
        var touchesMinY = ToleranceMath.AlmostEqual(removed.MinY, left.MinY, tolerance);
        var touchesMaxY = ToleranceMath.AlmostEqual(removed.MaxY, left.MaxY, tolerance);
        var touchesMinZ = ToleranceMath.AlmostEqual(removed.MinZ, left.MinZ, tolerance);
        var touchesMaxZ = ToleranceMath.AlmostEqual(removed.MaxZ, left.MaxZ, tolerance);
        var mouthTouchCount = (touchesMinX ? 1 : 0) + (touchesMaxX ? 1 : 0) + (touchesMinY ? 1 : 0) + (touchesMaxY ? 1 : 0) + (touchesMinZ ? 1 : 0) + (touchesMaxZ ? 1 : 0);

        if (mouthTouchCount == 0)
        {
            unsupportedReason = "Boolean Subtract: bounded orthogonal pocket family requires the subtract box to open to exactly one exterior root face; fully enclosed cavities remain deferred.";
            return false;
        }

        if (mouthTouchCount > 1)
        {
            unsupportedReason = "Boolean Subtract: bounded orthogonal pocket family requires a single pocket mouth; through-slots and multi-face openings remain deferred.";
            return false;
        }

        var sideWallPositiveX = removed.MinX - left.MinX;
        var sideWallNegativeX = left.MaxX - removed.MaxX;
        var sideWallPositiveY = removed.MinY - left.MinY;
        var sideWallNegativeY = left.MaxY - removed.MaxY;
        var sideWallPositiveZ = removed.MinZ - left.MinZ;
        var sideWallNegativeZ = left.MaxZ - removed.MaxZ;

        if ((touchesMinX || touchesMaxX) && (!HasStrictThickness(sideWallPositiveY)
            || !HasStrictThickness(sideWallNegativeY)
            || !HasStrictThickness(sideWallPositiveZ)
            || !HasStrictThickness(sideWallNegativeZ)))
        {
            unsupportedReason = "Boolean Subtract: bounded orthogonal pocket family requires strictly positive wall/floor thickness; tangent or zero-thickness side walls are unsupported.";
            return false;
        }

        if ((touchesMinY || touchesMaxY) && (!HasStrictThickness(sideWallPositiveX)
            || !HasStrictThickness(sideWallNegativeX)
            || !HasStrictThickness(sideWallPositiveZ)
            || !HasStrictThickness(sideWallNegativeZ)))
        {
            unsupportedReason = "Boolean Subtract: bounded orthogonal pocket family requires strictly positive wall/floor thickness; tangent or zero-thickness side walls are unsupported.";
            return false;
        }

        if ((touchesMinZ || touchesMaxZ) && (!HasStrictThickness(sideWallPositiveX)
            || !HasStrictThickness(sideWallNegativeX)
            || !HasStrictThickness(sideWallPositiveY)
            || !HasStrictThickness(sideWallNegativeY)))
        {
            unsupportedReason = "Boolean Subtract: bounded orthogonal pocket family requires strictly positive wall/floor thickness; tangent or zero-thickness side walls are unsupported.";
            return false;
        }

        var splitX = new[] { left.MinX, removed.MinX, removed.MaxX, left.MaxX }.Distinct().OrderBy(v => v).ToArray();
        var splitY = new[] { left.MinY, removed.MinY, removed.MaxY, left.MaxY }.Distinct().OrderBy(v => v).ToArray();
        var splitZ = new[] { left.MinZ, removed.MinZ, removed.MaxZ, left.MaxZ }.Distinct().OrderBy(v => v).ToArray();
        var cells = new List<AxisAlignedBoxExtents>();

        for (var ix = 0; ix < splitX.Length - 1; ix++)
        {
            for (var iy = 0; iy < splitY.Length - 1; iy++)
            {
                for (var iz = 0; iz < splitZ.Length - 1; iz++)
                {
                    var candidate = new AxisAlignedBoxExtents(
                        splitX[ix],
                        splitX[ix + 1],
                        splitY[iy],
                        splitY[iy + 1],
                        splitZ[iz],
                        splitZ[iz + 1]);

                    if (!candidate.HasPositiveVolume(tolerance))
                    {
                        continue;
                    }

                    if (!left.Contains(candidate, tolerance))
                    {
                        continue;
                    }

                    if (removed.Contains(candidate, tolerance))
                    {
                        continue;
                    }

                    cells.Add(candidate);
                }
            }
        }

        if (cells.Count == 0)
        {
            unsupportedReason = "Boolean Subtract: bounded orthogonal pocket reconstruction produced no occupied cells.";
            return false;
        }

        if (!TryValidateOrthogonalCellConnectivity(cells, tolerance))
        {
            unsupportedReason = "Boolean Subtract: bounded orthogonal pocket result would be disconnected or only edge-connected.";
            return false;
        }

        occupiedCells = cells;
        return true;

        bool HasStrictThickness(double thickness)
            => thickness > (2d * tolerance.Linear);
    }

    private static bool PositiveOverlap(double minA, double maxA, double minB, double maxB, ToleranceContext tolerance)
        => (System.Math.Min(maxA, maxB) - System.Math.Max(minA, minB)) > tolerance.Linear;

    private static bool TryClassifyBoundedPrismaticThroughCutSubtract(
        SafeBooleanComposition leftComposition,
        SupportedPrismaticSubtractTool prismTool,
        ToleranceContext tolerance,
        out string? unsupportedReason)
    {
        unsupportedReason = null;
        if (leftComposition.RootDescriptor.Kind != SafeBooleanRootKind.Box)
        {
            unsupportedReason = "Boolean Subtract: bounded prismatic subtract family requires a recognized safe box root.";
            return false;
        }

        var hasPriorSubtractHistory = leftComposition.Holes.Count > 0 || (leftComposition.OpenSlots?.Count ?? 0) > 0;
        if (hasPriorSubtractHistory)
        {
            if (!SupportsBoundedAnalyticHistoryForPrismaticContinuation(leftComposition, tolerance, out var historyReason))
            {
                unsupportedReason = historyReason;
                return false;
            }
        }

        var root = leftComposition.RootDescriptor.Box;
        if (!hasPriorSubtractHistory
            && (prismTool.Bounds.MinZ > root.MinZ + tolerance.Linear || prismTool.Bounds.MaxZ < root.MaxZ - tolerance.Linear))
        {
            unsupportedReason = "Boolean Subtract: bounded prismatic subtract family requires a through-cut tool that spans the full box root height.";
            return false;
        }

        foreach (var point in prismTool.Footprint)
        {
            var insideX = point.X > root.MinX + tolerance.Linear && point.X < root.MaxX - tolerance.Linear;
            var insideY = point.Y > root.MinY + tolerance.Linear && point.Y < root.MaxY - tolerance.Linear;
            if (!insideX || !insideY)
            {
                unsupportedReason = "Boolean Subtract: bounded prismatic subtract family requires footprint vertices strictly inside the box-root XY bounds.";
                return false;
            }
        }

        return true;
    }

    private static SupportedThroughVoidSet BuildThroughVoidSetForPrismaticContinuation(
        SafeBooleanComposition composition,
        SupportedPrismaticSubtractTool prismaticTool)
    {
        var analyticVoids = composition.Holes
            .Where(hole => hole.SpanKind == SupportedBooleanHoleSpanKind.Through
                && hole.Surface.Kind is (AnalyticSurfaceKind.Cylinder or AnalyticSurfaceKind.Cone))
            .ToArray();
        var prismaticVoids = new[]
        {
            new SupportedPrismaticThroughVoid(prismaticTool.Bounds, prismaticTool.Footprint),
        };
        return new SupportedThroughVoidSet(analyticVoids, prismaticVoids);
    }

    private static bool SupportsBoundedAnalyticHistoryForPrismaticContinuation(
        SafeBooleanComposition composition,
        ToleranceContext tolerance,
        out string unsupportedReason)
    {
        unsupportedReason = "Boolean Subtract: bounded prismatic subtract continuation currently requires prior subtract history to stay inside supported analytic-hole/open-slot families.";
        if (composition.OpenSlots is { Count: > 0 })
        {
            return false;
        }

        foreach (var hole in composition.Holes)
        {
            if (hole.SpanKind != SupportedBooleanHoleSpanKind.Through)
            {
                return false;
            }

            if (hole.Surface.Kind is not (AnalyticSurfaceKind.Cylinder or AnalyticSurfaceKind.Cone))
            {
                return false;
            }

            var axis = hole.Axis.ToVector();
            if (!ToleranceMath.AlmostZero(axis.X, tolerance)
                || !ToleranceMath.AlmostZero(axis.Y, tolerance)
                || !ToleranceMath.AlmostEqual(System.Math.Abs(axis.Z), 1d, tolerance))
            {
                return false;
            }
        }

        unsupportedReason = string.Empty;
        return true;
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

    private static BrepBody CopyWithSafeComposition(BrepBody source, SafeBooleanComposition composition)
        => new(source.Topology, source.Geometry, source.Bindings, GetVertexPoints(source), composition, source.ShellRepresentation);

    private static IReadOnlyDictionary<VertexId, Point3D> GetVertexPoints(BrepBody source)
    {
        var points = new Dictionary<VertexId, Point3D>();
        foreach (var vertex in source.Topology.Vertices)
        {
            if (source.TryGetVertexPoint(vertex.Id, out var point))
            {
                points[vertex.Id] = point;
            }
        }

        return points;
    }
}
