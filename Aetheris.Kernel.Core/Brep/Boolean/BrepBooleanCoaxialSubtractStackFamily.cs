using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

internal readonly record struct CoaxialCylinderSubtractStackProfile(
    SupportedBooleanHole EntryHole,
    SupportedBooleanHole DeepHole,
    bool EntryFromTop,
    double ShoulderZ);

internal static class BrepBooleanCoaxialSubtractStackFamily
{
    public static bool TryClassifyPair(
        AxisAlignedBoxExtents outerBox,
        in SupportedBooleanHole first,
        in SupportedBooleanHole second,
        ToleranceContext tolerance,
        out CoaxialCylinderSubtractStackProfile profile,
        out BooleanDiagnostic? diagnostic,
        string? featureId = null)
    {
        profile = default;
        diagnostic = null;

        if (first.Surface.Kind != AnalyticSurfaceKind.Cylinder || second.Surface.Kind != AnalyticSurfaceKind.Cylinder)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateUnsupportedAnalyticSurfaceKindDiagnostic(BooleanOperation.Subtract.ToString(), AnalyticSurfaceKind.Cylinder, featureId);
            return false;
        }

        if (!IsWorldZAligned(first.Axis, tolerance) || !IsWorldZAligned(second.Axis, tolerance))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateAxisNotAlignedDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded contained coaxial subtract-stack support requires world-Z aligned cylinder tools.");
            return false;
        }

        var deltaX = first.CenterX - second.CenterX;
        var deltaY = first.CenterY - second.CenterY;
        if (System.Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) > tolerance.Linear)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateAxisNotAlignedDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded contained coaxial subtract-stack support requires coaxial cylinders with matching XY axis centers.");
            return false;
        }

        var firstTopEntry = IsTopEntry(first, outerBox, tolerance);
        var secondTopEntry = IsTopEntry(second, outerBox, tolerance);
        var firstBottomEntry = IsBottomEntry(first, outerBox, tolerance);
        var secondBottomEntry = IsBottomEntry(second, outerBox, tolerance);
        var hasTopEntry = firstTopEntry || secondTopEntry;
        var hasBottomEntry = firstBottomEntry || secondBottomEntry;

        if (!hasTopEntry && !hasBottomEntry)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded contained coaxial subtract-stack support requires at least one segment to open on a box entry face.");
            return false;
        }

        var firstTopOnlyEntry = firstTopEntry && !firstBottomEntry;
        var secondTopOnlyEntry = secondTopEntry && !secondBottomEntry;
        var firstBottomOnlyEntry = firstBottomEntry && !firstTopEntry;
        var secondBottomOnlyEntry = secondBottomEntry && !secondTopEntry;
        var hasTopOnlyEntry = firstTopOnlyEntry || secondTopOnlyEntry;
        var hasBottomOnlyEntry = firstBottomOnlyEntry || secondBottomOnlyEntry;

        if (hasTopOnlyEntry && hasBottomOnlyEntry)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded contained coaxial subtract-stack support requires all segments in a two-cylinder stack to share one entry side.");
            return false;
        }

        var entryFromTop = hasTopOnlyEntry;
        var firstIsEntryCandidate = entryFromTop ? firstTopOnlyEntry : firstBottomOnlyEntry;
        var secondIsEntryCandidate = entryFromTop ? secondTopOnlyEntry : secondBottomOnlyEntry;
        var fallbackContainedEntry = false;
        if (!firstIsEntryCandidate && !secondIsEntryCandidate)
        {
            var firstContained = first.SpanKind == SupportedBooleanHoleSpanKind.Contained;
            var secondContained = second.SpanKind == SupportedBooleanHoleSpanKind.Contained;
            if (firstContained == secondContained)
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded contained coaxial subtract-stack support requires one shallow segment to open on exactly one box entry face.");
                return false;
            }

            fallbackContainedEntry = true;
        }

        var entryIsFirst = fallbackContainedEntry
            ? first.SpanKind == SupportedBooleanHoleSpanKind.Contained
            : firstIsEntryCandidate && (!secondIsEntryCandidate || first.BottomRadius >= second.BottomRadius);
        var entryHole = entryIsFirst ? first : second;
        var deepHole = entryIsFirst ? second : first;

        if (fallbackContainedEntry)
        {
            entryFromTop = IsTopEntry(deepHole, outerBox, tolerance)
                ? true
                : IsBottomEntry(deepHole, outerBox, tolerance)
                    ? false
                    : (outerBox.MaxZ - ResolveTop(entryHole)) <= (ResolveBottom(entryHole) - outerBox.MinZ);
        }

        if (!fallbackContainedEntry && !IntersectsEntryFace(entryHole, entryFromTop, outerBox, tolerance))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded contained coaxial subtract-stack support requires the entry segment to intersect the chosen entry face; fully contained-first stacks remain deferred.");
            return false;
        }

        if (fallbackContainedEntry && !IntersectsEntryFace(deepHole, entryFromTop, outerBox, tolerance))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded contained coaxial subtract-stack support requires the deeper segment to intersect the selected entry face when the entry segment is fully contained.");
            return false;
        }

        var entryShoulder = entryFromTop ? ResolveBottom(entryHole) : ResolveTop(entryHole);
        var deepTop = ResolveTop(deepHole);
        var deepBottom = ResolveBottom(deepHole);

        if (entryFromTop)
        {
            if (deepTop < (entryShoulder - tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded contained coaxial subtract-stack support requires the deeper segment to reach the shoulder plane; unsupported deeper continuation shape.");
                return false;
            }

            if (deepBottom >= (entryShoulder - tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded contained coaxial subtract-stack support requires the deeper segment to continue below the shoulder plane.");
                return false;
            }
        }
        else
        {
            if (deepBottom > (entryShoulder + tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded contained coaxial subtract-stack support requires the deeper segment to reach the shoulder plane; unsupported deeper continuation shape.");
                return false;
            }

            if (deepTop <= (entryShoulder + tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded contained coaxial subtract-stack support requires the deeper segment to continue above the shoulder plane.");
                return false;
            }
        }

        if (entryHole.BottomRadius <= (deepHole.BottomRadius + tolerance.Linear))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateTangentContactDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded contained coaxial subtract-stack support requires a strictly larger entry-segment radius than the deeper segment radius; invalid counterbore radius ordering or degenerate shoulder geometry.");
            return false;
        }

        profile = new CoaxialCylinderSubtractStackProfile(entryHole, deepHole, entryFromTop, entryShoulder);
        return true;
    }

    private static double ResolveTop(in SupportedBooleanHole hole) => System.Math.Max(hole.StartCenter.Z, hole.EndCenter.Z);

    private static double ResolveBottom(in SupportedBooleanHole hole) => System.Math.Min(hole.StartCenter.Z, hole.EndCenter.Z);

    private static bool IsTopEntry(in SupportedBooleanHole hole, AxisAlignedBoxExtents outerBox, ToleranceContext tolerance)
        => ResolveTop(hole) >= (outerBox.MaxZ - tolerance.Linear);

    private static bool IsBottomEntry(in SupportedBooleanHole hole, AxisAlignedBoxExtents outerBox, ToleranceContext tolerance)
        => ResolveBottom(hole) <= (outerBox.MinZ + tolerance.Linear);

    private static bool IntersectsEntryFace(in SupportedBooleanHole hole, bool entryFromTop, AxisAlignedBoxExtents outerBox, ToleranceContext tolerance)
        => entryFromTop
            ? IsTopEntry(hole, outerBox, tolerance)
            : IsBottomEntry(hole, outerBox, tolerance);

    private static bool IsWorldZAligned(Direction3D axis, ToleranceContext tolerance)
    {
        var axisVector = axis.ToVector();
        return ToleranceMath.AlmostZero(axisVector.X, tolerance)
            && ToleranceMath.AlmostZero(axisVector.Y, tolerance)
            && ToleranceMath.AlmostEqual(System.Math.Abs(axisVector.Z), 1d, tolerance);
    }
}
