using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public static class BrepBooleanSafeCompositionGraphValidator
{
    private static readonly IReadOnlyList<JudgmentCandidate<BooleanContinuationContext>> ContinuationCandidates = BuildContinuationCandidates();

    public static bool TryValidateNextSubtract(
        SafeBooleanComposition composition,
        AnalyticSurface surface,
        ToleranceContext tolerance,
        out SafeBooleanComposition updatedComposition,
        out BooleanDiagnostic? diagnostic,
        string? nextFeatureId = null)
    {
        updatedComposition = composition;
        diagnostic = null;
        var root = composition.RootDescriptor;

        if (root.Kind == SafeBooleanRootKind.Cylinder)
        {
            return TryValidateCylinderRootSubtract(
                composition,
                root,
                surface,
                tolerance,
                out updatedComposition,
                out diagnostic,
                nextFeatureId);
        }

        if (!TryCreateSupportedHole(composition, surface, tolerance, composition.Holes.Count > 0, out var nextHole, out diagnostic, nextFeatureId))
        {
            return false;
        }

        var supportsContinuedAdditiveBlindComposition = SupportsBoundedBlindContinuationOnRecognizedOrthogonalAdditiveRoot(composition, nextHole, tolerance);
        if (composition.Holes.Count > 0 && nextHole.IsBlind && !supportsContinuedAdditiveBlindComposition)
        {
            diagnostic = new BooleanDiagnostic(
                BooleanDiagnosticCode.UnsupportedBlindHoleComposition,
                BrepBooleanCylinderRecognition.CreateBooleanMessage(
                    BooleanOperation.Subtract.ToString(),
                    nextFeatureId,
                    "blind-hole continuation exceeds the bounded family; only recognized orthogonal additive roots with world-Z analytic-hole chains are supported for continued blind-hole composition."),
                "BrepBoolean.AnalyticHole.BlindContinuationOutsideBoundedFamily");
            return false;
        }

        if (composition.Holes.Count > 0
            && (!IsAxisAlignedWithWorldZ(nextHole.Axis, tolerance) || composition.Holes.Any(h => !IsAxisAlignedWithWorldZ(h.Axis, tolerance))))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                BooleanOperation.Subtract.ToString(),
                nextFeatureId,
                "currently keeps arbitrary-axis subtract support to single-feature rebuilds; composed safe-hole chains remain limited to world-Z family in this milestone.");
            return false;
        }

        foreach (var existingHole in composition.Holes)
        {
            var deltaX = existingHole.CenterX - nextHole.CenterX;
            var deltaY = existingHole.CenterY - nextHole.CenterY;
            var centerDistance = System.Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            var requiredDistance = existingHole.MaxBoundaryRadius + nextHole.MaxBoundaryRadius;

            if (composition.Holes.Count == 1
                && (existingHole.IsBlind || nextHole.IsBlind))
            {
                if (!TryValidateBlindContinuationCandidate(
                        composition,
                        existingHole,
                        nextHole,
                        tolerance,
                        nextFeatureId,
                        out var skipPairChecks,
                        out diagnostic))
                {
                    return false;
                }

                if (skipPairChecks)
                {
                    continue;
                }
            }

            if (ToleranceMath.AlmostEqual(centerDistance, requiredDistance, tolerance))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateTangentContactDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    nextFeatureId,
                    $"would be tangent to previously accepted hole {FormatFeatureRef(existingHole.FeatureId)}; tangent safe-hole composition is rejected to avoid zero-thickness geometry.");
                return false;
            }

            if (centerDistance < (requiredDistance - tolerance.Linear))
            {
                diagnostic = new BooleanDiagnostic(
                    BooleanDiagnosticCode.HoleInterference,
                    BrepBooleanCylinderRecognition.CreateBooleanMessage(
                        BooleanOperation.Subtract.ToString(),
                        nextFeatureId,
                        $"overlaps previously accepted hole {FormatFeatureRef(existingHole.FeatureId)}; overlapping safe-hole composition is not supported. Separate the hole centers or reduce one of the boundary radii."),
                    "BrepBoolean.AnalyticHole.HoleInterference");
                return false;
            }
        }

        updatedComposition = composition with
        {
            Holes = [.. composition.Holes, nextHole],
        };
        return true;
    }

    private static bool TryValidateCylinderRootSubtract(
        SafeBooleanComposition composition,
        SafeBooleanRootDescriptor root,
        AnalyticSurface surface,
        ToleranceContext tolerance,
        out SafeBooleanComposition updatedComposition,
        out BooleanDiagnostic? diagnostic,
        string? nextFeatureId)
    {
        updatedComposition = composition;
        diagnostic = null;

        if (surface.Kind != AnalyticSurfaceKind.Cylinder || surface.Cylinder is not RecognizedCylinder toolCylinder)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateUnsupportedAnalyticSurfaceKindDiagnostic(
                BooleanOperation.Subtract.ToString(),
                surface.Kind,
                nextFeatureId);
            return false;
        }

        if (root.Cylinder is not RecognizedCylinder rootCylinder)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateAxisNotAlignedDiagnostic(
                BooleanOperation.Subtract.ToString(),
                nextFeatureId,
                "cannot resolve recognized cylinder-root descriptor.");
            return false;
        }

        if (!BrepBooleanCylinderRecognition.ValidateAxisAlignedZ(rootCylinder, tolerance)
            || !BrepBooleanCylinderRecognition.ValidateAxisAlignedZ(toolCylinder, tolerance))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateAxisNotAlignedDiagnostic(
                BooleanOperation.Subtract.ToString(),
                nextFeatureId,
                "bounded cylinder-root safe subtract supports only world-Z aligned root and center-bore cylinders.");
            return false;
        }

        var rootCenter = new Point3D(
            (rootCylinder.MinCenter.X + rootCylinder.MaxCenter.X) * 0.5d,
            (rootCylinder.MinCenter.Y + rootCylinder.MaxCenter.Y) * 0.5d,
            (rootCylinder.MinCenter.Z + rootCylinder.MaxCenter.Z) * 0.5d);
        var toolCenter = new Point3D(
            (toolCylinder.MinCenter.X + toolCylinder.MaxCenter.X) * 0.5d,
            (toolCylinder.MinCenter.Y + toolCylinder.MaxCenter.Y) * 0.5d,
            (toolCylinder.MinCenter.Z + toolCylinder.MaxCenter.Z) * 0.5d);

        var rootMinZ = System.Math.Min(rootCylinder.MinCenter.Z, rootCylinder.MaxCenter.Z);
        var rootMaxZ = System.Math.Max(rootCylinder.MinCenter.Z, rootCylinder.MaxCenter.Z);
        var toolMinZ = System.Math.Min(toolCylinder.MinCenter.Z, toolCylinder.MaxCenter.Z);
        var toolMaxZ = System.Math.Max(toolCylinder.MinCenter.Z, toolCylinder.MaxCenter.Z);
        var spansRoot = toolMinZ <= (rootMinZ + tolerance.Linear) && toolMaxZ >= (rootMaxZ - tolerance.Linear);
        if (!spansRoot)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                BooleanOperation.Subtract.ToString(),
                nextFeatureId,
                "is outside the bounded cylinder-root safe subtract family; only through center bores that span both planar caps are supported.");
            return false;
        }

        var deltaX = toolCenter.X - rootCenter.X;
        var deltaY = toolCenter.Y - rootCenter.Y;
        var radialDistance = System.Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        var isCoaxialCenterBore = ToleranceMath.AlmostZero(radialDistance, tolerance);
        var nextHole = new SupportedBooleanHole(
            nextFeatureId,
            surface,
            toolCenter.X,
            toolCenter.Y,
            new Point3D(toolCenter.X, toolCenter.Y, rootMinZ),
            new Point3D(toolCenter.X, toolCenter.Y, rootMaxZ),
            toolCylinder.Axis,
            Direction3D.Create(new Vector3D(1d, 0d, 0d)),
            toolCylinder.Radius,
            toolCylinder.Radius,
            SupportedBooleanHoleSpanKind.Through,
            rootMinZ,
            rootMaxZ);

        var ringOuterDistance = radialDistance + toolCylinder.Radius;
        if (ToleranceMath.AlmostEqual(ringOuterDistance, rootCylinder.Radius, tolerance)
            || ringOuterDistance > (rootCylinder.Radius - tolerance.Linear))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateRadiusExceedsBoundaryDiagnostic(
                BooleanOperation.Subtract.ToString(),
                nextFeatureId,
                isCoaxialCenterBore
                    ? "must remain strictly smaller than the flange root radius in the bounded cylinder-root safe subtract family."
                    : "is outside the bounded cylinder-root safe subtract family; off-axis through-holes must remain strictly inside the outer cylindrical wall.");
            return false;
        }

        foreach (var existingHole in composition.Holes)
        {
            var existingDeltaX = existingHole.CenterX - nextHole.CenterX;
            var existingDeltaY = existingHole.CenterY - nextHole.CenterY;
            var centerDistance = System.Math.Sqrt((existingDeltaX * existingDeltaX) + (existingDeltaY * existingDeltaY));
            var requiredDistance = existingHole.MaxBoundaryRadius + nextHole.MaxBoundaryRadius;

            if (AreEquivalentCylinderRootThroughHoles(existingHole, nextHole, tolerance))
            {
                updatedComposition = composition;
                return true;
            }

            if (ToleranceMath.AlmostEqual(centerDistance, requiredDistance, tolerance))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateTangentContactDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    nextFeatureId,
                    $"would be tangent to previously accepted hole {FormatFeatureRef(existingHole.FeatureId)}; tangent safe-hole composition is rejected.");
                return false;
            }

            if (centerDistance < (requiredDistance - tolerance.Linear))
            {
                diagnostic = new BooleanDiagnostic(
                    BooleanDiagnosticCode.HoleInterference,
                    BrepBooleanCylinderRecognition.CreateBooleanMessage(
                        BooleanOperation.Subtract.ToString(),
                        nextFeatureId,
                        $"overlaps previously accepted hole {FormatFeatureRef(existingHole.FeatureId)}; overlapping cylinder-root hole chains are not supported."),
                    "BrepBoolean.AnalyticHole.HoleInterference");
                return false;
            }
        }

        updatedComposition = composition with
        {
            Holes = [.. composition.Holes, nextHole],
        };

        return true;
    }

    private static bool AreEquivalentCylinderRootThroughHoles(
        in SupportedBooleanHole existingHole,
        in SupportedBooleanHole nextHole,
        ToleranceContext tolerance)
    {
        if (existingHole.Surface.Kind != AnalyticSurfaceKind.Cylinder
            || nextHole.Surface.Kind != AnalyticSurfaceKind.Cylinder
            || existingHole.SpanKind != SupportedBooleanHoleSpanKind.Through
            || nextHole.SpanKind != SupportedBooleanHoleSpanKind.Through)
        {
            return false;
        }

        return ToleranceMath.AlmostEqual(existingHole.CenterX, nextHole.CenterX, tolerance)
            && ToleranceMath.AlmostEqual(existingHole.CenterY, nextHole.CenterY, tolerance)
            && ToleranceMath.AlmostEqual(existingHole.BottomRadius, nextHole.BottomRadius, tolerance)
            && ToleranceMath.AlmostEqual(existingHole.TopRadius, nextHole.TopRadius, tolerance)
            && ToleranceMath.AlmostEqual(existingHole.StartZ, nextHole.StartZ, tolerance)
            && ToleranceMath.AlmostEqual(existingHole.EndZ, nextHole.EndZ, tolerance);
    }

    private static bool TryCreateSupportedHole(
        SafeBooleanComposition composition,
        AnalyticSurface surface,
        ToleranceContext tolerance,
        bool hasExistingHoles,
        out SupportedBooleanHole hole,
        out BooleanDiagnostic? diagnostic,
        string? featureId)
    {
        var outerBox = composition.OuterBox;
        switch (surface.Kind)
        {
            case AnalyticSurfaceKind.Cylinder when surface.Cylinder is RecognizedCylinder cylinder:
                if (!BrepBooleanCylinderRecognition.TryValidateCylinderSubtractProfile(outerBox, cylinder, tolerance, out var cylinderProfile, out diagnostic, featureId))
                {
                    if (!ShouldTryContainedCylinderSegment(outerBox, cylinder, tolerance)
                        || !TryValidateContainedCylinderSubtractSegment(outerBox, cylinder, tolerance, out cylinderProfile, out diagnostic, featureId))
                    {
                        hole = default;
                        return false;
                    }
                }

                hole = new SupportedBooleanHole(
                    featureId,
                    surface,
                    cylinderProfile.CenterX,
                    cylinderProfile.CenterY,
                    cylinderProfile.StartCenter,
                    cylinderProfile.EndCenter,
                    cylinderProfile.Axis,
                    cylinderProfile.ReferenceAxis,
                    cylinderProfile.StartRadius,
                    cylinderProfile.EndRadius,
                    cylinderProfile.SpanKind,
                    cylinderProfile.StartZ,
                    cylinderProfile.EndZ);
                return true;

            case AnalyticSurfaceKind.Cone when surface.Cone is RecognizedCone cone:
                if (!BrepBooleanCylinderRecognition.TryValidateConeSubtractProfile(outerBox, cone, tolerance, out var coneProfile, out diagnostic, featureId))
                {
                    hole = default;
                    return false;
                }

                hole = new SupportedBooleanHole(
                    featureId,
                    surface,
                    coneProfile.CenterX,
                    coneProfile.CenterY,
                    coneProfile.StartCenter,
                    coneProfile.EndCenter,
                    coneProfile.Axis,
                    coneProfile.ReferenceAxis,
                    coneProfile.StartRadius,
                    coneProfile.EndRadius,
                    coneProfile.SpanKind,
                    coneProfile.StartZ,
                    coneProfile.EndZ);
                return true;

            case AnalyticSurfaceKind.Sphere when surface.Sphere is RecognizedSphere sphere:
                if (!ValidateContainedSphereCavity(outerBox, sphere, tolerance, out diagnostic, featureId))
                {
                    hole = default;
                    return false;
                }

                if (hasExistingHoles)
                {
                    diagnostic = BrepBooleanCylinderRecognition.CreateUnsupportedAnalyticSurfaceKindDiagnostic(
                        BooleanOperation.Subtract.ToString(),
                        AnalyticSurfaceKind.Sphere,
                        featureId);
                    hole = default;
                    return false;
                }

                hole = new SupportedBooleanHole(
                    featureId,
                    surface,
                    sphere.Center.X,
                    sphere.Center.Y,
                    sphere.Center,
                    sphere.Center,
                    Direction3D.Create(new Vector3D(0d, 0d, 1d)),
                    Direction3D.Create(new Vector3D(1d, 0d, 0d)),
                    sphere.Radius,
                    sphere.Radius,
                    SupportedBooleanHoleSpanKind.Through,
                    outerBox.MinZ,
                    outerBox.MaxZ);
                return true;

            case AnalyticSurfaceKind.Sphere:
            case AnalyticSurfaceKind.Torus:
            default:
                diagnostic = BrepBooleanCylinderRecognition.CreateUnsupportedAnalyticSurfaceKindDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    surface.Kind,
                    featureId);
                hole = default;
                return false;
        }
    }


    private static bool ShouldTryContainedCylinderSegment(AxisAlignedBoxExtents outerBox, in RecognizedCylinder cylinder, ToleranceContext tolerance)
    {
        if (!BrepBooleanCylinderRecognition.ValidateAxisAlignedZ(cylinder, tolerance))
        {
            return false;
        }

        var minCenter = cylinder.MinCenter;
        var maxCenter = cylinder.MaxCenter;
        var bottomZ = System.Math.Min(minCenter.Z, maxCenter.Z);
        var topZ = System.Math.Max(minCenter.Z, maxCenter.Z);
        return topZ < (outerBox.MaxZ - tolerance.Linear)
            && bottomZ > (outerBox.MinZ + tolerance.Linear);
    }

    private static bool TryValidateContainedCylinderSubtractSegment(
        AxisAlignedBoxExtents outerBox,
        in RecognizedCylinder cylinder,
        ToleranceContext tolerance,
        out SupportedSubtractProfile profile,
        out BooleanDiagnostic? diagnostic,
        string? featureId)
    {
        profile = default;
        diagnostic = null;

        if (!BrepBooleanCylinderRecognition.ValidateAxisAlignedZ(cylinder, tolerance))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateAxisNotAlignedDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded contained coaxial subtract-stack support requires world-Z aligned contained cylinder segments.");
            return false;
        }

        var minCenter = cylinder.MinCenter;
        var maxCenter = cylinder.MaxCenter;
        var bottomCenter = minCenter.Z <= maxCenter.Z ? minCenter : maxCenter;
        var topCenter = minCenter.Z > maxCenter.Z ? minCenter : maxCenter;

        if (topCenter.Z >= (outerBox.MaxZ - tolerance.Linear)
            || bottomCenter.Z <= (outerBox.MinZ + tolerance.Linear))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "is outside the bounded contained coaxial subtract-stack family; contained cylinder segments must remain strictly between box entry planes.");
            return false;
        }

        if (!ValidateCircleInsideBoxFootprint(outerBox, topCenter.X, topCenter.Y, cylinder.Radius, tolerance, out diagnostic, "top contained section", featureId)
            || !ValidateCircleInsideBoxFootprint(outerBox, bottomCenter.X, bottomCenter.Y, cylinder.Radius, tolerance, out diagnostic, "bottom contained section", featureId))
        {
            return false;
        }

        profile = new SupportedSubtractProfile(
            SupportedBooleanHoleSpanKind.Contained,
            (topCenter.X + bottomCenter.X) * 0.5d,
            (topCenter.Y + bottomCenter.Y) * 0.5d,
            topCenter,
            bottomCenter,
            cylinder.Axis,
            ResolveReferenceAxis(cylinder.Axis),
            topCenter.Z,
            bottomCenter.Z,
            cylinder.Radius,
            cylinder.Radius);
        return true;
    }

    private static Direction3D ResolveReferenceAxis(Direction3D axis)
    {
        var candidate = System.Math.Abs(axis.ToVector().X) > 0.5d
            ? Direction3D.Create(new Vector3D(0d, 1d, 0d))
            : Direction3D.Create(new Vector3D(1d, 0d, 0d));
        var projected = candidate.ToVector() - (axis.ToVector() * candidate.ToVector().Dot(axis.ToVector()));
        return projected.Length <= 1e-12d
            ? Direction3D.Create(new Vector3D(0d, 1d, 0d))
            : Direction3D.Create(projected);
    }

    private static bool ValidateCircleInsideBoxFootprint(
        AxisAlignedBoxExtents box,
        double centerX,
        double centerY,
        double radius,
        ToleranceContext tolerance,
        out BooleanDiagnostic? diagnostic,
        string section,
        string? featureId)
    {
        diagnostic = null;
        var tangent = ToleranceMath.AlmostEqual(centerX - radius, box.MinX, tolerance)
            || ToleranceMath.AlmostEqual(centerX + radius, box.MaxX, tolerance)
            || ToleranceMath.AlmostEqual(centerY - radius, box.MinY, tolerance)
            || ToleranceMath.AlmostEqual(centerY + radius, box.MaxY, tolerance);
        if (tangent)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateTangentContactDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                $"has tangent contact at the box side walls ({section}); tangent support is rejected to avoid zero-thickness boundary sections.");
            return false;
        }

        if (centerX - radius < (box.MinX - tolerance.Linear)
            || centerX + radius > (box.MaxX + tolerance.Linear)
            || centerY - radius < (box.MinY - tolerance.Linear)
            || centerY + radius > (box.MaxY + tolerance.Linear))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateRadiusExceedsBoundaryDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                $"extends outside the box side walls ({section}); supported subtract tools must remain inside the box footprint.");
            return false;
        }

        return true;
    }

    private static string FormatFeatureRef(string? featureId)
        => string.IsNullOrWhiteSpace(featureId) ? "<unknown>" : $"'{featureId}'";

    private static bool IsAxisAlignedWithWorldZ(Direction3D axis, ToleranceContext tolerance)
    {
        var v = axis.ToVector();
        return ToleranceMath.AlmostZero(v.X, tolerance)
               && ToleranceMath.AlmostZero(v.Y, tolerance)
               && ToleranceMath.AlmostEqual(System.Math.Abs(v.Z), 1d, tolerance);
    }

    private static bool IsCoaxialHolePair(in SupportedBooleanHole first, in SupportedBooleanHole second, ToleranceContext tolerance)
    {
        var deltaX = first.CenterX - second.CenterX;
        var deltaY = first.CenterY - second.CenterY;
        return ToleranceMath.AlmostZero(deltaX, tolerance)
            && ToleranceMath.AlmostZero(deltaY, tolerance);
    }

    private static bool SupportsBoundedIndependentHoleContinuationOnRecognizedOrthogonalAdditiveRoot(
        SafeBooleanComposition composition,
        in SupportedBooleanHole nextHole,
        ToleranceContext tolerance)
    {
        if (composition.OccupiedCells is null || composition.OccupiedCells.Count < 2)
        {
            return false;
        }

        if (!IsAxisAlignedWithWorldZ(nextHole.Axis, tolerance))
        {
            return false;
        }

        if (!IsSupportedIndependentHoleShape(nextHole.Surface.Kind))
        {
            return false;
        }

        foreach (var existingHole in composition.Holes)
        {
            if (!IsAxisAlignedWithWorldZ(existingHole.Axis, tolerance)
                || !IsSupportedIndependentHoleShape(existingHole.Surface.Kind))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSupportedIndependentHoleShape(AnalyticSurfaceKind kind)
        => kind is AnalyticSurfaceKind.Cylinder or AnalyticSurfaceKind.Cone;

    private static bool SupportsBoundedBlindContinuationOnRecognizedOrthogonalAdditiveRoot(
        SafeBooleanComposition composition,
        in SupportedBooleanHole nextHole,
        ToleranceContext tolerance)
    {
        if (composition.OccupiedCells is null || composition.OccupiedCells.Count < 2)
        {
            return false;
        }

        if (!IsAxisAlignedWithWorldZ(nextHole.Axis, tolerance))
        {
            return false;
        }

        if (composition.Holes.Any(existingHole => !IsAxisAlignedWithWorldZ(existingHole.Axis, tolerance)))
        {
            return false;
        }

        return nextHole.Surface.Kind is AnalyticSurfaceKind.Cylinder or AnalyticSurfaceKind.Cone;
    }

    private static bool TryValidateBlindContinuationCandidate(
        SafeBooleanComposition composition,
        in SupportedBooleanHole existingHole,
        in SupportedBooleanHole nextHole,
        ToleranceContext tolerance,
        string? featureId,
        out bool skipPairChecks,
        out BooleanDiagnostic? diagnostic)
    {
        skipPairChecks = false;
        var pairIsCoaxial = IsCoaxialHolePair(existingHole, nextHole, tolerance);
        var countersinkSupported = false;
        BooleanDiagnostic? countersinkDiagnostic = null;
        var stackSupported = false;
        BooleanDiagnostic? stackDiagnostic = null;

        if (pairIsCoaxial)
        {
            countersinkSupported = BrepBooleanCoaxialCountersinkSubtractFamily.TryClassifyPair(
                composition.OuterBox,
                existingHole,
                nextHole,
                tolerance,
                out _,
                out countersinkDiagnostic,
                featureId);
            stackSupported = BrepBooleanCoaxialSubtractStackFamily.TryClassifyPair(
                composition.OuterBox,
                existingHole,
                nextHole,
                tolerance,
                out _,
                out stackDiagnostic,
                featureId);
        }
        else
        {
            BrepBooleanCoaxialSubtractStackFamily.TryClassifyPair(
                composition.OuterBox,
                existingHole,
                nextHole,
                tolerance,
                out _,
                out stackDiagnostic,
                featureId);
        }

        var context = new BooleanContinuationContext(
            Operation: BooleanOperation.Subtract,
            HasRecognizedSafeCompositionRoot: true,
            HasRecognizedOrthogonalAdditiveRoot: composition.OccupiedCells is not null && composition.OccupiedCells.Count >= 2,
            ExistingHole: existingHole,
            NextHole: nextHole,
            PairIsCoaxial: pairIsCoaxial,
            IsCountersinkSupported: countersinkSupported,
            CountersinkDiagnostic: countersinkDiagnostic,
            IsSubtractStackSupported: stackSupported,
            SubtractStackDiagnostic: stackDiagnostic,
            SupportsIndependentContinuationOnAdditiveRoot: SupportsBoundedIndependentHoleContinuationOnRecognizedOrthogonalAdditiveRoot(composition, nextHole, tolerance));

        var result = new JudgmentEngine<BooleanContinuationContext>().Evaluate(context, ContinuationCandidates);
        if (!result.IsSuccess)
        {
            diagnostic = BuildUnsupportedBlindContinuationDiagnostic(
                context,
                featureId,
                result.Rejections);
            return false;
        }

        var selectedFamily = Enum.Parse<BooleanContinuationFamily>(result.Selection!.Value.Candidate.Name, ignoreCase: false);
        switch (selectedFamily)
        {
            case BooleanContinuationFamily.CoaxialCountersinkPair:
            case BooleanContinuationFamily.CoaxialSubtractStack:
            case BooleanContinuationFamily.IndependentHolesOnAdditiveRoot:
                skipPairChecks = true;
                diagnostic = null;
                return true;
            case BooleanContinuationFamily.UnsupportedContinuationFromRecognizedRoot:
            case BooleanContinuationFamily.UnsupportedGeneral:
                diagnostic = BuildUnsupportedBlindContinuationDiagnostic(
                    context,
                    featureId,
                    result.Rejections);
                return false;
            default:
                diagnostic = new BooleanDiagnostic(
                    BooleanDiagnosticCode.UnsupportedBlindHoleComposition,
                    BrepBooleanCylinderRecognition.CreateBooleanMessage(
                        BooleanOperation.Subtract.ToString(),
                        featureId,
                        $"blind-hole continuation selected unknown family '{selectedFamily}'."),
                    "BrepBoolean.AnalyticHole.BlindContinuationOutsideBoundedFamily");
                return false;
        }
    }

    private static IReadOnlyList<JudgmentCandidate<BooleanContinuationContext>> BuildContinuationCandidates()
        =>
        [
            new JudgmentCandidate<BooleanContinuationContext>(
                Name: BooleanContinuationFamily.CoaxialCountersinkPair.ToString(),
                IsAdmissible: context => context.PairIsCoaxial && context.IsCountersinkSupported,
                Score: _ => 500d,
                RejectionReason: context => context.PairIsCoaxial
                    ? context.CountersinkDiagnostic?.Message ?? "Coaxial countersink continuation predicates were not satisfied."
                    : "Coaxial countersink continuation requires a coaxial pair."),
            new JudgmentCandidate<BooleanContinuationContext>(
                Name: BooleanContinuationFamily.CoaxialSubtractStack.ToString(),
                IsAdmissible: context => context.PairIsCoaxial && context.IsSubtractStackSupported,
                Score: _ => 400d,
                RejectionReason: context => context.PairIsCoaxial
                    ? context.SubtractStackDiagnostic?.Message ?? "Coaxial subtract-stack continuation predicates were not satisfied."
                    : "Coaxial subtract-stack continuation requires a coaxial pair."),
            new JudgmentCandidate<BooleanContinuationContext>(
                Name: BooleanContinuationFamily.IndependentHolesOnAdditiveRoot.ToString(),
                IsAdmissible: context => !context.PairIsCoaxial && context.SupportsIndependentContinuationOnAdditiveRoot,
                Score: _ => 300d,
                RejectionReason: context => context.PairIsCoaxial
                    ? "Independent-hole continuation requires a non-coaxial pair."
                    : "Independent-hole continuation requires a recognized orthogonal additive root with world-Z cylinder/cone holes."),
            new JudgmentCandidate<BooleanContinuationContext>(
                Name: BooleanContinuationFamily.UnsupportedContinuationFromRecognizedRoot.ToString(),
                IsAdmissible: context => context.HasRecognizedSafeCompositionRoot,
                Score: _ => 100d,
                RejectionReason: context => context.HasRecognizedSafeCompositionRoot
                    ? "Supported continuation candidates were rejected for the recognized safe root."
                    : "Recognized-root unsupported continuation fallback requires safe composition metadata."),
            new JudgmentCandidate<BooleanContinuationContext>(
                Name: BooleanContinuationFamily.UnsupportedGeneral.ToString(),
                IsAdmissible: _ => true,
                Score: _ => 0d)
        ];

    private static BooleanDiagnostic BuildUnsupportedBlindContinuationDiagnostic(
        in BooleanContinuationContext context,
        string? featureId,
        IReadOnlyList<JudgmentRejection> rejections)
    {
        if (context.PairIsCoaxial)
        {
            if (context.CountersinkDiagnostic is not null)
            {
                return AppendNearestCandidateDetail(context.CountersinkDiagnostic, rejections);
            }

            if (context.SubtractStackDiagnostic is not null)
            {
                return AppendNearestCandidateDetail(context.SubtractStackDiagnostic, rejections);
            }
        }

        if (!context.PairIsCoaxial && context.SubtractStackDiagnostic is not null)
        {
            return AppendNearestCandidateDetail(context.SubtractStackDiagnostic, rejections);
        }

        const string baseReason = "independent multi-hole continuation exceeds the bounded family; non-coaxial blind-hole composition is only supported on recognized orthogonal additive roots with world-Z cylinder/cone holes.";
        var nearest = rejections.FirstOrDefault(rejection =>
            rejection.CandidateName != BooleanContinuationFamily.UnsupportedContinuationFromRecognizedRoot.ToString()
            && rejection.CandidateName != BooleanContinuationFamily.UnsupportedGeneral.ToString());
        var detail = nearest.CandidateName is null
            ? baseReason
            : $"{baseReason} Nearest candidate '{nearest.CandidateName}' rejected: {nearest.Reason}";

        return new BooleanDiagnostic(
            BooleanDiagnosticCode.UnsupportedBlindHoleComposition,
            BrepBooleanCylinderRecognition.CreateBooleanMessage(
                BooleanOperation.Subtract.ToString(),
                featureId,
                detail),
            "BrepBoolean.AnalyticHole.BlindContinuationOutsideBoundedFamily");
    }

    private static BooleanDiagnostic AppendNearestCandidateDetail(BooleanDiagnostic diagnostic, IReadOnlyList<JudgmentRejection> rejections)
    {
        var nearest = rejections.FirstOrDefault(rejection =>
            rejection.CandidateName != BooleanContinuationFamily.UnsupportedContinuationFromRecognizedRoot.ToString()
            && rejection.CandidateName != BooleanContinuationFamily.UnsupportedGeneral.ToString());
        if (nearest.CandidateName is null)
        {
            return diagnostic;
        }

        return diagnostic with
        {
            Message = $"{diagnostic.Message} Nearest candidate '{nearest.CandidateName}' rejected: {nearest.Reason}"
        };
    }

    private enum BooleanContinuationFamily
    {
        CoaxialCountersinkPair,
        CoaxialSubtractStack,
        IndependentHolesOnAdditiveRoot,
        UnsupportedContinuationFromRecognizedRoot,
        UnsupportedGeneral
    }

    private readonly record struct BooleanContinuationContext(
        BooleanOperation Operation,
        bool HasRecognizedSafeCompositionRoot,
        bool HasRecognizedOrthogonalAdditiveRoot,
        SupportedBooleanHole ExistingHole,
        SupportedBooleanHole NextHole,
        bool PairIsCoaxial,
        bool IsCountersinkSupported,
        BooleanDiagnostic? CountersinkDiagnostic,
        bool IsSubtractStackSupported,
        BooleanDiagnostic? SubtractStackDiagnostic,
        bool SupportsIndependentContinuationOnAdditiveRoot);

    private static bool ValidateContainedSphereCavity(
        AxisAlignedBoxExtents outerBox,
        in RecognizedSphere sphere,
        ToleranceContext tolerance,
        out BooleanDiagnostic? diagnostic,
        string? featureId)
    {
        diagnostic = null;

        var minX = sphere.Center.X - sphere.Radius;
        var maxX = sphere.Center.X + sphere.Radius;
        var minY = sphere.Center.Y - sphere.Radius;
        var maxY = sphere.Center.Y + sphere.Radius;
        var minZ = sphere.Center.Z - sphere.Radius;
        var maxZ = sphere.Center.Z + sphere.Radius;

        var tangentContact =
            ToleranceMath.AlmostEqual(minX, outerBox.MinX, tolerance)
            || ToleranceMath.AlmostEqual(maxX, outerBox.MaxX, tolerance)
            || ToleranceMath.AlmostEqual(minY, outerBox.MinY, tolerance)
            || ToleranceMath.AlmostEqual(maxY, outerBox.MaxY, tolerance)
            || ToleranceMath.AlmostEqual(minZ, outerBox.MinZ, tolerance)
            || ToleranceMath.AlmostEqual(maxZ, outerBox.MaxZ, tolerance);
        if (tangentContact)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateTangentContactDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "is tangent to a box boundary plane; tangent spherical cavities are rejected to avoid zero-thickness boundary contact.");
            return false;
        }

        if (minX < (outerBox.MinX - tolerance.Linear)
            || maxX > (outerBox.MaxX + tolerance.Linear)
            || minY < (outerBox.MinY - tolerance.Linear)
            || maxY > (outerBox.MaxY + tolerance.Linear)
            || minZ < (outerBox.MinZ - tolerance.Linear)
            || maxZ > (outerBox.MaxZ + tolerance.Linear))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateRadiusExceedsBoundaryDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "extends outside the box boundary; spherical cavity tools must remain strictly contained inside the box.");
            return false;
        }

        return true;
    }
}
