using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public static class BrepBooleanSafeCompositionGraphValidator
{
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

        if (!TryCreateSupportedHole(composition.OuterBox, surface, tolerance, out var nextHole, out diagnostic, nextFeatureId))
        {
            return false;
        }

        foreach (var existingHole in composition.Holes)
        {
            var deltaX = existingHole.CenterX - nextHole.CenterX;
            var deltaY = existingHole.CenterY - nextHole.CenterY;
            var centerDistance = System.Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            var requiredDistance = existingHole.MaxBoundaryRadius + nextHole.MaxBoundaryRadius;

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

    private static bool TryCreateSupportedHole(
        AxisAlignedBoxExtents outerBox,
        AnalyticSurface surface,
        ToleranceContext tolerance,
        out SupportedBooleanHole hole,
        out BooleanDiagnostic? diagnostic,
        string? featureId)
    {
        switch (surface.Kind)
        {
            case AnalyticSurfaceKind.Cylinder when surface.Cylinder is RecognizedCylinder cylinder:
                if (!BrepBooleanCylinderRecognition.ValidateThroughHole(outerBox, cylinder, tolerance, out diagnostic, featureId))
                {
                    hole = default;
                    return false;
                }

                hole = new SupportedBooleanHole(
                    featureId,
                    surface,
                    cylinder.MinCenter.X,
                    cylinder.MinCenter.Y,
                    cylinder.Radius,
                    cylinder.Radius);
                return true;

            case AnalyticSurfaceKind.Cone when surface.Cone is RecognizedCone cone:
                if (!BrepBooleanCylinderRecognition.ValidateThroughHole(outerBox, cone, tolerance, out diagnostic, featureId))
                {
                    hole = default;
                    return false;
                }

                var bottomAxisParameter = AxisParameterAtZ(cone, outerBox.MinZ, tolerance);
                var topAxisParameter = AxisParameterAtZ(cone, outerBox.MaxZ, tolerance);
                var boundaryBottomCenter = cone.PointAtAxisParameter(bottomAxisParameter);
                hole = new SupportedBooleanHole(
                    featureId,
                    surface,
                    boundaryBottomCenter.X,
                    boundaryBottomCenter.Y,
                    cone.RadiusAtAxisParameter(bottomAxisParameter),
                    cone.RadiusAtAxisParameter(topAxisParameter));
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

    private static double AxisParameterAtZ(in RecognizedCone cone, double z, ToleranceContext tolerance)
    {
        var axisZ = cone.Axis.ToVector().Z;
        if (ToleranceMath.AlmostEqual(axisZ, 0d, tolerance))
        {
            throw new InvalidOperationException("AxisParameterAtZ requires a cone axis aligned with Z.");
        }

        return (z - cone.AxisOrigin.Z) / axisZ;
    }

    private static string FormatFeatureRef(string? featureId)
        => string.IsNullOrWhiteSpace(featureId) ? "<unknown>" : $"'{featureId}'";
}
