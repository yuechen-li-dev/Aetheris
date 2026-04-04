using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Geometry;
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

        if (!TryCreateSupportedHole(composition.OuterBox, surface, tolerance, composition.Holes.Count > 0, out var nextHole, out diagnostic, nextFeatureId))
        {
            return false;
        }

        if (composition.Holes.Count > 0 && nextHole.IsBlind)
        {
            diagnostic = new BooleanDiagnostic(
                BooleanDiagnosticCode.UnsupportedBlindHoleComposition,
                BrepBooleanCylinderRecognition.CreateBooleanMessage(
                    BooleanOperation.Subtract.ToString(),
                    nextFeatureId,
                    "cannot append a blind analytic hole to an existing safe subtract composition in B1; blind-hole support is currently limited to single-feature subtracts."),
                "BrepBoolean.AnalyticHole.UnsupportedBlindHoleComposition");
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
                && (existingHole.IsBlind || nextHole.IsBlind)
                && centerDistance < (requiredDistance + tolerance.Linear))
            {
                if (BrepBooleanSteppedHoleFamily.TryClassifyPair(
                    composition.OuterBox,
                    existingHole,
                    nextHole,
                    tolerance,
                    out _,
                    out var steppedDiagnostic,
                    nextFeatureId))
                {
                    continue;
                }

                if (steppedDiagnostic is not null)
                {
                    diagnostic = steppedDiagnostic;
                    return false;
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

    private static bool TryCreateSupportedHole(
        AxisAlignedBoxExtents outerBox,
        AnalyticSurface surface,
        ToleranceContext tolerance,
        bool hasExistingHoles,
        out SupportedBooleanHole hole,
        out BooleanDiagnostic? diagnostic,
        string? featureId)
    {
        switch (surface.Kind)
        {
            case AnalyticSurfaceKind.Cylinder when surface.Cylinder is RecognizedCylinder cylinder:
                if (!BrepBooleanCylinderRecognition.TryValidateCylinderSubtractProfile(outerBox, cylinder, tolerance, out var cylinderProfile, out diagnostic, featureId))
                {
                    hole = default;
                    return false;
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

    private static string FormatFeatureRef(string? featureId)
        => string.IsNullOrWhiteSpace(featureId) ? "<unknown>" : $"'{featureId}'";

    private static bool IsAxisAlignedWithWorldZ(Direction3D axis, ToleranceContext tolerance)
    {
        var v = axis.ToVector();
        return ToleranceMath.AlmostZero(v.X, tolerance)
               && ToleranceMath.AlmostZero(v.Y, tolerance)
               && ToleranceMath.AlmostEqual(System.Math.Abs(v.Z), 1d, tolerance);
    }

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
