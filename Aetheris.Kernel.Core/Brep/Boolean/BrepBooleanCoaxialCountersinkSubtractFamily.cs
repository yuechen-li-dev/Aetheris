using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

internal readonly record struct CoaxialCountersinkSubtractProfile(
    SupportedBooleanHole EntryCone,
    SupportedBooleanHole ContinuationCylinder,
    bool EntryFromTop,
    double TransitionZ,
    double EntryRadius,
    double ContinuationRadius,
    double ContinuationEndZ);

internal static class BrepBooleanCoaxialCountersinkSubtractFamily
{
    // M1 bounded countersink family:
    // - exactly one cone + one cylinder subtract in a two-segment coaxial stack
    // - both world-Z aligned and centered on the same XY axis
    // - cone provides single-sided entry at box top or bottom (not through-cone)
    // - cone transition radius must match continuation cylinder radius
    // - continuation may be through or blind but must continue inward past transition
    // - larger/smaller ordering is enforced: entry radius > continuation radius

    public static bool TryClassifyPair(
        AxisAlignedBoxExtents outerBox,
        in SupportedBooleanHole first,
        in SupportedBooleanHole second,
        ToleranceContext tolerance,
        out CoaxialCountersinkSubtractProfile profile,
        out BooleanDiagnostic? diagnostic,
        string? featureId = null)
    {
        profile = default;
        diagnostic = null;

        var firstIsCone = first.Surface.Kind == AnalyticSurfaceKind.Cone;
        var secondIsCone = second.Surface.Kind == AnalyticSurfaceKind.Cone;
        var firstIsCylinder = first.Surface.Kind == AnalyticSurfaceKind.Cylinder;
        var secondIsCylinder = second.Surface.Kind == AnalyticSurfaceKind.Cylinder;
        if ((!firstIsCone && !secondIsCone) || (!firstIsCylinder && !secondIsCylinder))
        {
            return false;
        }

        var entryCone = firstIsCone ? first : second;
        var continuationCylinder = firstIsCylinder ? first : second;

        if (!IsWorldZAligned(entryCone.Axis, tolerance) || !IsWorldZAligned(continuationCylinder.Axis, tolerance))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateAxisNotAlignedDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded countersink subtract-stack support requires world-Z aligned cone and cylinder tools.");
            return false;
        }

        var deltaX = entryCone.CenterX - continuationCylinder.CenterX;
        var deltaY = entryCone.CenterY - continuationCylinder.CenterY;
        if (System.Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) > tolerance.Linear)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateAxisNotAlignedDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded countersink subtract-stack support requires coaxial cone and cylinder tools with matching XY axis centers.");
            return false;
        }

        var coneTopEntry = IsTopEntry(entryCone, outerBox, tolerance);
        var coneBottomEntry = IsBottomEntry(entryCone, outerBox, tolerance);
        if (coneTopEntry == coneBottomEntry)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded countersink subtract-stack support requires one-sided cone entry opening on exactly one box entry face.");
            return false;
        }

        var entryFromTop = coneTopEntry;
        var entryZ = entryFromTop ? ResolveTop(entryCone) : ResolveBottom(entryCone);
        var transitionZ = entryFromTop ? ResolveBottom(entryCone) : ResolveTop(entryCone);
        var continuationTop = ResolveTop(continuationCylinder);
        var continuationBottom = ResolveBottom(continuationCylinder);

        if (entryFromTop)
        {
            if (continuationTop < (transitionZ - tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded countersink subtract-stack support requires the cylindrical continuation to reach the cone transition circle.");
                return false;
            }

            if (continuationBottom >= (transitionZ - tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded countersink subtract-stack support requires continuation below the cone transition plane.");
                return false;
            }
        }
        else
        {
            if (continuationBottom > (transitionZ + tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded countersink subtract-stack support requires the cylindrical continuation to reach the cone transition circle.");
                return false;
            }

            if (continuationTop <= (transitionZ + tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded countersink subtract-stack support requires continuation above the cone transition plane.");
                return false;
            }
        }

        var entryRadius = entryFromTop ? entryCone.BottomRadius : entryCone.TopRadius;
        var continuationRadius = continuationCylinder.BottomRadius;
        if (entryRadius <= (continuationRadius + tolerance.Linear))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateTangentContactDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded countersink subtract-stack support requires cone entry radius strictly larger than continuation-cylinder radius at the transition plane.");
            return false;
        }

        var coneTransitionRadius = entryFromTop ? entryCone.TopRadius : entryCone.BottomRadius;
        if (!ToleranceMath.AlmostEqual(coneTransitionRadius, continuationRadius, tolerance))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded countersink subtract-stack support requires cone transition radius to match continuation-cylinder radius.");
            return false;
        }

        if (!ValidateCircleInsideBoxFootprint(outerBox, entryCone.CenterX, entryCone.CenterY, entryRadius, tolerance, out diagnostic, featureId, "countersink entry"))
        {
            return false;
        }

        var continuationEndZ = continuationCylinder.SpanKind == SupportedBooleanHoleSpanKind.Through
            ? (entryFromTop ? outerBox.MinZ : outerBox.MaxZ)
            : continuationCylinder.EndCenter.Z;

        profile = new CoaxialCountersinkSubtractProfile(
            entryCone,
            continuationCylinder,
            entryFromTop,
            transitionZ,
            entryRadius,
            continuationRadius,
            continuationEndZ);
        return true;
    }

    private static bool ValidateCircleInsideBoxFootprint(
        AxisAlignedBoxExtents box,
        double centerX,
        double centerY,
        double radius,
        ToleranceContext tolerance,
        out BooleanDiagnostic? diagnostic,
        string? featureId,
        string section)
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

    private static double ResolveTop(in SupportedBooleanHole hole) => System.Math.Max(hole.StartCenter.Z, hole.EndCenter.Z);

    private static double ResolveBottom(in SupportedBooleanHole hole) => System.Math.Min(hole.StartCenter.Z, hole.EndCenter.Z);

    private static bool IsTopEntry(in SupportedBooleanHole hole, AxisAlignedBoxExtents outerBox, ToleranceContext tolerance)
        => ResolveTop(hole) >= (outerBox.MaxZ - tolerance.Linear);

    private static bool IsBottomEntry(in SupportedBooleanHole hole, AxisAlignedBoxExtents outerBox, ToleranceContext tolerance)
        => ResolveBottom(hole) <= (outerBox.MinZ + tolerance.Linear);

    private static bool IsWorldZAligned(Direction3D axis, ToleranceContext tolerance)
    {
        var axisVector = axis.ToVector();
        return ToleranceMath.AlmostZero(axisVector.X, tolerance)
            && ToleranceMath.AlmostZero(axisVector.Y, tolerance)
            && ToleranceMath.AlmostEqual(System.Math.Abs(axisVector.Z), 1d, tolerance);
    }
}
