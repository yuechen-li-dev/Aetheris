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
    string? FeatureId,
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
        => BrepBooleanSafeCompositionGraphValidator.TryValidateNextSubtract(
            composition,
            surface,
            tolerance,
            out updatedComposition,
            out diagnostic);
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
