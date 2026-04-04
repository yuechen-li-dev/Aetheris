using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Geometry;

namespace Aetheris.Kernel.Core.Brep.Boolean;

internal readonly record struct SteppedCoaxialHoleProfile(
    SupportedBooleanHole EntryHole,
    SupportedBooleanHole DeepHole,
    bool EntryFromTop,
    double ShoulderZ);

internal static class BrepBooleanSteppedHoleFamily
{
    public static bool TryClassifyPair(
        AxisAlignedBoxExtents outerBox,
        in SupportedBooleanHole first,
        in SupportedBooleanHole second,
        ToleranceContext tolerance,
        out SteppedCoaxialHoleProfile profile,
        out BooleanDiagnostic? diagnostic,
        string? featureId = null)
    {
        profile = default;
        diagnostic = null;

        if (first.Surface.Kind != AnalyticSurfaceKind.Cylinder || second.Surface.Kind != AnalyticSurfaceKind.Cylinder)
        {
            return false;
        }

        if (!IsWorldZAligned(first.Axis, tolerance) || !IsWorldZAligned(second.Axis, tolerance))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateAxisNotAlignedDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded stepped-hole support requires world-Z aligned cylinder tools.");
            return false;
        }

        var deltaX = first.CenterX - second.CenterX;
        var deltaY = first.CenterY - second.CenterY;
        if (System.Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) > tolerance.Linear)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateAxisNotAlignedDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded stepped-hole support requires coaxial cylinders with matching XY axis centers.");
            return false;
        }

        var firstTopEntry = first.SpanKind == SupportedBooleanHoleSpanKind.BlindFromTop;
        var secondTopEntry = second.SpanKind == SupportedBooleanHoleSpanKind.BlindFromTop;
        var firstBottomEntry = first.SpanKind == SupportedBooleanHoleSpanKind.BlindFromBottom;
        var secondBottomEntry = second.SpanKind == SupportedBooleanHoleSpanKind.BlindFromBottom;
        var hasTopBlind = firstTopEntry || secondTopEntry;
        var hasBottomBlind = firstBottomEntry || secondBottomEntry;
        if (hasTopBlind && hasBottomBlind)
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded stepped-hole support requires all blind segments in the stack to share one entry side.");
            return false;
        }

        if (!hasTopBlind && !hasBottomBlind)
        {
            return false;
        }

        var entryFromTop = hasTopBlind;
        var entryHole = first.IsBlind ? first : second;
        var deepHole = first.IsBlind ? second : first;
        var shoulderZ = entryHole.EndCenter.Z;

        if (entryFromTop)
        {
            var deepTop = System.Math.Max(deepHole.StartCenter.Z, deepHole.EndCenter.Z);
            var deepBottom = System.Math.Min(deepHole.StartCenter.Z, deepHole.EndCenter.Z);
            if (deepTop < (outerBox.MaxZ - tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded top-entry stepped holes require the deeper coaxial segment to open on the same top entry face.");
                return false;
            }

            if (deepBottom >= (shoulderZ - tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded top-entry stepped holes require the deeper segment to extend below the shoulder plane.");
                return false;
            }
        }
        else
        {
            var deepTop = System.Math.Max(deepHole.StartCenter.Z, deepHole.EndCenter.Z);
            var deepBottom = System.Math.Min(deepHole.StartCenter.Z, deepHole.EndCenter.Z);
            if (deepBottom > (outerBox.MinZ + tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded bottom-entry stepped holes require the deeper coaxial segment to open on the same bottom entry face.");
                return false;
            }

            if (deepTop <= (shoulderZ + tolerance.Linear))
            {
                diagnostic = BrepBooleanCylinderRecognition.CreateNotFullySpanningDiagnostic(
                    BooleanOperation.Subtract.ToString(),
                    featureId,
                    "bounded bottom-entry stepped holes require the deeper segment to extend above the shoulder plane.");
                return false;
            }
        }

        if (entryHole.BottomRadius <= (deepHole.BottomRadius + tolerance.Linear))
        {
            diagnostic = BrepBooleanCylinderRecognition.CreateTangentContactDiagnostic(
                BooleanOperation.Subtract.ToString(),
                featureId,
                "bounded stepped-hole support requires a strictly larger entry-pocket radius than the deeper coaxial segment radius to avoid tangent/degenerate shoulder geometry.");
            return false;
        }

        profile = new SteppedCoaxialHoleProfile(entryHole, deepHole, entryFromTop, shoulderZ);
        return true;
    }

    private static bool IsWorldZAligned(Direction3D axis, ToleranceContext tolerance)
    {
        var axisVector = axis.ToVector();
        return ToleranceMath.AlmostZero(axisVector.X, tolerance)
            && ToleranceMath.AlmostZero(axisVector.Y, tolerance)
            && ToleranceMath.AlmostEqual(System.Math.Abs(axisVector.Z), 1d, tolerance);
    }
}
