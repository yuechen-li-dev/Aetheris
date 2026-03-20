using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public sealed record SafeBooleanComposition(
    AxisAlignedBoxExtents OuterBox,
    IReadOnlyList<SupportedBooleanHole> Holes)
{
    public SafeBooleanComposition Translate(Vector3D translation)
    {
        var translatedOuter = new AxisAlignedBoxExtents(
            OuterBox.MinX + translation.X,
            OuterBox.MaxX + translation.X,
            OuterBox.MinY + translation.Y,
            OuterBox.MaxY + translation.Y,
            OuterBox.MinZ + translation.Z,
            OuterBox.MaxZ + translation.Z);

        return new SafeBooleanComposition(
            translatedOuter,
            Holes.Select(hole => hole.Translate(translation)).ToArray());
    }
}

public readonly record struct SupportedBooleanHole(
    AnalyticSurface Surface,
    double CenterX,
    double CenterY,
    double BottomRadius,
    double TopRadius)
{
    public double MaxBoundaryRadius => System.Math.Max(BottomRadius, TopRadius);

    public SupportedBooleanHole Translate(Vector3D translation)
        => this with
        {
            Surface = Surface.Translate(translation),
            CenterX = CenterX + translation.X,
            CenterY = CenterY + translation.Y,
        };
}

public static class BrepBooleanSafeComposition
{
    public static bool TryRecognize(BrepBody body, ToleranceContext tolerance, out SafeBooleanComposition composition, out string reason)
    {
        if (body.SafeBooleanComposition is not null)
        {
            composition = body.SafeBooleanComposition;
            reason = string.Empty;
            return true;
        }

        if (BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(body, tolerance, out var box, out reason))
        {
            composition = new SafeBooleanComposition(box, []);
            return true;
        }

        composition = default!;
        return false;
    }

    public static bool TryAppend(
        SafeBooleanComposition composition,
        AnalyticSurface surface,
        ToleranceContext tolerance,
        out SafeBooleanComposition updatedComposition,
        out BooleanDiagnostic? diagnostic)
    {
        updatedComposition = composition;
        diagnostic = null;

        if (!TryCreateSupportedHole(composition.OuterBox, surface, tolerance, out var nextHole, out diagnostic))
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
                    "analytic hole footprint is tangent to an existing composed hole.");
                return false;
            }

            if (centerDistance < (requiredDistance - tolerance.Linear))
            {
                diagnostic = new BooleanDiagnostic(
                    BooleanDiagnosticCode.HoleInterference,
                    "Boolean Subtract: analytic hole candidate failed diagnostic HoleInterference (analytic hole footprint overlaps an existing composed hole).",
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
        out BooleanDiagnostic? diagnostic)
    {
        switch (surface.Kind)
        {
            case AnalyticSurfaceKind.Cylinder when surface.Cylinder is RecognizedCylinder cylinder:
                if (!BrepBooleanCylinderRecognition.ValidateThroughHole(outerBox, cylinder, tolerance, out diagnostic))
                {
                    hole = default;
                    return false;
                }

                hole = new SupportedBooleanHole(
                    surface,
                    cylinder.MinCenter.X,
                    cylinder.MinCenter.Y,
                    cylinder.Radius,
                    cylinder.Radius);
                return true;

            case AnalyticSurfaceKind.Cone when surface.Cone is RecognizedCone cone:
                if (!BrepBooleanCylinderRecognition.ValidateThroughHole(outerBox, cone, tolerance, out diagnostic))
                {
                    hole = default;
                    return false;
                }

                var bottomAxisParameter = AxisParameterAtZ(cone, outerBox.MinZ, tolerance);
                var topAxisParameter = AxisParameterAtZ(cone, outerBox.MaxZ, tolerance);
                var boundaryBottomCenter = cone.PointAtAxisParameter(bottomAxisParameter);
                hole = new SupportedBooleanHole(
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
                    surface.Kind);
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
}

internal static class AnalyticSurfaceCompositionExtensions
{
    public static AnalyticSurface Translate(this AnalyticSurface surface, Vector3D translation)
        => surface.Kind switch
        {
            AnalyticSurfaceKind.Cylinder when surface.Cylinder is RecognizedCylinder cylinder
                => new AnalyticSurface(
                    surface.Kind,
                    Cylinder: cylinder with
                    {
                        AxisOrigin = cylinder.AxisOrigin + translation,
                    }),
            AnalyticSurfaceKind.Cone when surface.Cone is RecognizedCone cone
                => new AnalyticSurface(
                    surface.Kind,
                    Cone: cone with
                    {
                        AxisOrigin = cone.AxisOrigin + translation,
                    }),
            AnalyticSurfaceKind.Sphere when surface.Sphere is RecognizedSphere sphere
                => new AnalyticSurface(surface.Kind, Sphere: sphere with { Center = sphere.Center + translation }),
            AnalyticSurfaceKind.Torus when surface.Torus is RecognizedTorus torus
                => new AnalyticSurface(surface.Kind, Torus: torus with { Center = torus.Center + translation }),
            _ => surface,
        };
}
