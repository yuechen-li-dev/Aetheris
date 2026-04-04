using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
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
    string? ShortcutReason,
    BooleanExecutionClass ExecutionClass,
    SafeBooleanComposition? LeftSafeComposition,
    AxisAlignedBoxExtents? LeftBox,
    AxisAlignedBoxExtents? RightBox,
    AnalyticSurface? RightAnalyticSurface,
    string? UnsupportedReason);

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
    BooleanDiagnostic? Diagnostic = null);

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
    BooleanDiagnostic? Diagnostic = null);

public sealed record BooleanRebuildData(
    BooleanClassificationData Classification,
    BrepBody? RebuiltBody,
    IReadOnlyList<KernelDiagnostic> Diagnostics);

/// <summary>
/// M13 boolean pipeline with narrow real support for axis-aligned box/box cases that resolve to a single box
/// plus the M10k box-minus-Z-aligned-through-cylinder subset that rebuilds to a single box-with-hole solid.
/// Unsupported and non-solid cases return deterministic NotImplemented diagnostics.
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
            classification.UnsupportedReason);
    }

    public static BooleanCaseClassification ClassifyBooleanCase(BrepBody leftBody, BrepBody rightBody, BooleanOperation operation, ToleranceContext? tolerance = null)
    {
        var resolvedTolerance = tolerance ?? ToleranceContext.Default;
        var leftSafeCompositionRecognized = leftBody.SafeBooleanComposition is not null;
        var leftSafeComposition = leftSafeCompositionRecognized
            ? leftBody.SafeBooleanComposition!
            : null;
        var leftRecognized = BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(leftBody, resolvedTolerance, out var leftBox, out _);
        var rightRecognized = BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(rightBody, resolvedTolerance, out var rightBox, out _);
        var rightAnalyticRecognized = BrepBooleanAnalyticSurfaceRecognition.TryRecognizeAnalyticSurface(rightBody, resolvedTolerance, out var analyticSurface, out _);

        if (leftRecognized && rightRecognized)
        {
            return new BooleanCaseClassification(BooleanExecutionClass.PlanarOnly, leftSafeComposition, leftBox, rightBox, null, null);
        }

        if (operation == BooleanOperation.Subtract && !leftRecognized && !leftSafeCompositionRecognized
            && BrepBooleanSafeComposition.TryRecognize(leftBody, resolvedTolerance, out var recognizedRootComposition, out _))
        {
            leftSafeCompositionRecognized = true;
            leftSafeComposition = recognizedRootComposition;
        }

        if (operation == BooleanOperation.Subtract && (leftRecognized || leftSafeCompositionRecognized) && rightAnalyticRecognized)
        {
            return new BooleanCaseClassification(
                BooleanExecutionClass.PlanarWithAnalyticHole,
                leftSafeCompositionRecognized ? leftSafeComposition : new SafeBooleanComposition(leftBox, []),
                leftRecognized ? leftBox : leftSafeComposition!.OuterBox,
                null,
                analyticSurface,
                null);
        }

        if (leftSafeCompositionRecognized)
        {
            return new BooleanCaseClassification(
                BooleanExecutionClass.UnsupportedGeneralCase,
                leftSafeComposition,
                leftRecognized ? leftBox : leftSafeComposition!.OuterBox,
                rightRecognized ? rightBox : null,
                rightAnalyticRecognized ? analyticSurface : null,
                $"Boolean {operation}: sequential safe composition only supports subtracting supported analytic holes from the current bounded safe root family.");
        }

        return new BooleanCaseClassification(
            BooleanExecutionClass.UnsupportedGeneralCase,
            null,
            leftRecognized ? leftBox : null,
            rightRecognized ? rightBox : null,
            rightAnalyticRecognized ? analyticSurface : null,
            $"Boolean {operation}: M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).");
    }

    private static BooleanIntersectionData ComputeIntersections(BooleanRequest request, BooleanAnalysis analysis)
    {
        if (analysis.LeftBox is null || (analysis.RightBox is null && analysis.RightAnalyticSurface is null))
        {
            return new BooleanIntersectionData(analysis, IsComputed: true, CandidatePairCount: 0, OverlapBox: null, IsTouchingOnly: false);
        }

        if (analysis.RightBox is null)
        {
            return new BooleanIntersectionData(analysis, IsComputed: true, CandidatePairCount: 1, OverlapBox: null, IsTouchingOnly: false);
        }

        var overlap = AxisAlignedBoxExtents.Intersection(analysis.LeftBox.Value, analysis.RightBox.Value);
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
                return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: intersections.Analysis.UnsupportedReason);
        }
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
        var right = intersections.Analysis.RightBox!.Value;

        if (operation == BooleanOperation.Intersect)
        {
            if (intersections.IsTouchingOnly)
            {
                return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: $"Boolean {operation}: touching-only intersection is non-solid and empty results are not representable in M13.");
            }

            if (intersections.OverlapBox is null)
            {
                return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: $"Boolean {operation}: empty intersection result is not representable in M13.");
            }

            return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 1, SingleBoxResult: intersections.OverlapBox, SafeCompositionResult: null, UnsupportedReason: null);
        }

        if (operation == BooleanOperation.Union)
        {
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
                return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: $"Boolean {operation}: disjoint box union is multi-body and not supported in M13.");
            }

            if (TryClassifyBoundedOrthogonalUnion(left, right, tolerance, out var unionCells, out var unsupportedReason))
            {
                return new BooleanClassificationData(
                    intersections,
                    IsComputed: true,
                    FragmentCount: 1,
                    SingleBoxResult: null,
                    SafeCompositionResult: null,
                    UnsupportedReason: null,
                    OrthogonalUnionCells: unionCells);
            }

            return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: unsupportedReason ?? $"Boolean {operation}: box union is outside the bounded connected orthogonal additive family for F1.");
        }

        if (!left.OverlapsWithPositiveVolume(right, tolerance))
        {
            return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 1, SingleBoxResult: left, SafeCompositionResult: null, UnsupportedReason: null);
        }

        if (right.Contains(left, tolerance))
        {
            return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: $"Boolean {operation}: subtraction fully removes the left box and empty results are not representable in M13.");
        }

        if (TrySubtractToSingleBox(left, right, tolerance, out var singleBox))
        {
            return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 1, SingleBoxResult: singleBox, SafeCompositionResult: null, UnsupportedReason: null);
        }

        return new BooleanClassificationData(intersections, IsComputed: true, FragmentCount: 0, SingleBoxResult: null, SafeCompositionResult: null, UnsupportedReason: $"Boolean {operation}: box subtraction result is not representable as a single box in M13.");
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

        if (classification.SafeCompositionResult is not null)
        {
            var tolerance = request.Tolerance ?? ToleranceContext.Default;
            var rebuiltThroughHole = BrepBooleanBoxCylinderHoleBuilder.BuildComposition(classification.SafeCompositionResult, tolerance);
            return rebuiltThroughHole.IsSuccess
                ? new BooleanRebuildData(classification, rebuiltThroughHole.Value, rebuiltThroughHole.Diagnostics)
                : new BooleanRebuildData(classification, RebuiltBody: null, Diagnostics: rebuiltThroughHole.Diagnostics);
        }

        if (classification.OrthogonalUnionCells is not null)
        {
            var rebuiltOrthogonalUnion = BrepBooleanOrthogonalUnionBuilder.BuildFromCells(classification.OrthogonalUnionCells);
            return rebuiltOrthogonalUnion.IsSuccess
                ? new BooleanRebuildData(classification, rebuiltOrthogonalUnion.Value, rebuiltOrthogonalUnion.Diagnostics)
                : new BooleanRebuildData(classification, RebuiltBody: null, Diagnostics: rebuiltOrthogonalUnion.Diagnostics);
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
        out string? unsupportedReason)
    {
        occupiedCells = Array.Empty<AxisAlignedBoxExtents>();
        unsupportedReason = null;

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

        if (sharedAxisSpanCount == 0)
        {
            unsupportedReason = "Boolean Union: box union is connected but outside the bounded F1 additive family because the operands do not share a full span on any primary axis.";
            return false;
        }

        var intersection = AxisAlignedBoxExtents.Intersection(left, right);
        if (intersection is null)
        {
            unsupportedReason = "Boolean Union: disjoint box union is multi-body and not supported in M13.";
            return false;
        }

        var connectedByVolume = intersection.Value.HasPositiveVolume(tolerance);
        var connectedByFace = IsPositiveAreaFaceContact(left, right, tolerance);
        if (!connectedByVolume && !connectedByFace)
        {
            unsupportedReason = "Boolean Union: edge-only or point-only contact is non-manifold and outside the bounded F1 additive family.";
            return false;
        }

        var splitX = new[] { left.MinX, left.MaxX, right.MinX, right.MaxX }.Distinct().OrderBy(v => v).ToArray();
        var splitY = new[] { left.MinY, left.MaxY, right.MinY, right.MaxY }.Distinct().OrderBy(v => v).ToArray();
        var splitZ = new[] { left.MinZ, left.MaxZ, right.MinZ, right.MaxZ }.Distinct().OrderBy(v => v).ToArray();
        var cells = new List<AxisAlignedBoxExtents>();
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
                        cells.Add(candidate);
                    }
                }
            }
        }

        if (cells.Count == 0)
        {
            unsupportedReason = "Boolean Union: bounded orthogonal reconstruction produced no occupied cells.";
            return false;
        }

        occupiedCells = cells;
        return true;
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

    private static bool PositiveOverlap(double minA, double maxA, double minB, double maxB, ToleranceContext tolerance)
        => (System.Math.Min(maxA, maxB) - System.Math.Max(minA, minB)) > tolerance.Linear;

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
