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

        var firstTop = ResolveTop(first);
        var firstBottom = ResolveBottom(first);
        var secondTop = ResolveTop(second);
        var secondBottom = ResolveBottom(second);

        var firstTopEntry = IsTopEntry(first, outerBox, tolerance);
        var secondTopEntry = IsTopEntry(second, outerBox, tolerance);
        var firstBottomEntry = IsBottomEntry(first, outerBox, tolerance);
        var secondBottomEntry = IsBottomEntry(second, outerBox, tolerance);
        var hasTopEntry = firstTopEntry || secondTopEntry;
        var hasBottomEntry = firstBottomEntry || secondBottomEntry;
        var hasTopBlindEntry = IsTopBlindEntry(first, outerBox, tolerance) || IsTopBlindEntry(second, outerBox, tolerance);
        var hasBottomBlindEntry = IsBottomBlindEntry(first, outerBox, tolerance) || IsBottomBlindEntry(second, outerBox, tolerance);

        if (!hasTopEntry && !hasBottomEntry)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded contained coaxial subtract-stack support requires at least one segment to open on a box entry face.");
            return false;
        }

        if (hasTopBlindEntry && hasBottomBlindEntry)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded contained coaxial subtract-stack support requires all segments in a two-cylinder stack to share one entry side.");
            return false;
        }

        var entryFromTop = hasTopEntry;
        var entryIsFirst = firstTopEntry || firstBottomEntry;
        var entryHole = entryIsFirst ? first : second;
        var deepHole = entryIsFirst ? second : first;

        if (entryHole.SpanKind == SupportedBooleanHoleSpanKind.Contained)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded contained coaxial subtract-stack support requires the entry segment to intersect the chosen entry face; fully contained-first stacks remain deferred.");
            return false;
        }

        var entryShoulder = entryFromTop ? ResolveBottom(entryHole) : ResolveTop(entryHole);
        var deepTop = ResolveTop(deepHole);
        var deepBottom = ResolveBottom(deepHole);

        if (entryFromTop)
        {
            if (deepTop < (outerBox.MaxZ - tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded contained coaxial subtract-stack support requires the deeper segment to open on the same top entry face.");
                return false;
            }

            if (deepBottom >= (entryShoulder - tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded contained coaxial subtract-stack support requires the deeper segment to extend below the shoulder plane.");
                return false;
            }
        }
        else
        {
            if (deepBottom > (outerBox.MinZ + tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded contained coaxial subtract-stack support requires the deeper segment to open on the same bottom entry face.");
                return false;
            }

            if (deepTop <= (entryShoulder + tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded contained coaxial subtract-stack support requires the deeper segment to extend above the shoulder plane.");
                return false;
            }
        }

        if (entryHole.BottomRadius <= (deepHole.BottomRadius + tolerance.Linear))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateTangentContactDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded contained coaxial subtract-stack support requires a strictly larger entry-segment radius than the deeper segment radius to avoid degenerate shoulder geometry.");
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

    private static bool IsTopBlindEntry(in SupportedBooleanHole hole, AxisAlignedBoxExtents outerBox, ToleranceContext tolerance)
        => hole.SpanKind == SupportedBooleanHoleSpanKind.BlindFromTop
           || (hole.SpanKind == SupportedBooleanHoleSpanKind.Contained && IsTopEntry(hole, outerBox, tolerance));

    private static bool IsBottomBlindEntry(in SupportedBooleanHole hole, AxisAlignedBoxExtents outerBox, ToleranceContext tolerance)
        => hole.SpanKind == SupportedBooleanHoleSpanKind.BlindFromBottom
           || (hole.SpanKind == SupportedBooleanHoleSpanKind.Contained && IsBottomEntry(hole, outerBox, tolerance));

    private static bool IsWorldZAligned(Direction3D axis, ToleranceContext tolerance)
    {
        var axisVector = axis.ToVector();
        return ToleranceMath.AlmostZero(axisVector.X, tolerance)
            && ToleranceMath.AlmostZero(axisVector.Y, tolerance)
            && ToleranceMath.AlmostEqual(System.Math.Abs(axisVector.Z), 1d, tolerance);
    }
}
