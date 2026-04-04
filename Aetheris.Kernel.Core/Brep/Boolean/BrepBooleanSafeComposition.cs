using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public sealed record SafeBooleanComposition(
    AxisAlignedBoxExtents OuterBox,
    IReadOnlyList<SupportedBooleanHole> Holes,
    SafeBooleanRootDescriptor? Root = null)
{
    public SafeBooleanRootDescriptor RootDescriptor => Root ?? SafeBooleanRootDescriptor.FromBox(OuterBox);

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
            Holes.Select(hole => hole.Translate(translation)).ToArray(),
            RootDescriptor.Translate(translation));
    }
}

public enum SafeBooleanRootKind
{
    Box,
    Cylinder,
}

public readonly record struct SafeBooleanRootDescriptor(
    SafeBooleanRootKind Kind,
    AxisAlignedBoxExtents Box,
    RecognizedCylinder? Cylinder = null)
{
    public static SafeBooleanRootDescriptor FromBox(AxisAlignedBoxExtents box)
        => new(SafeBooleanRootKind.Box, box);

    public static SafeBooleanRootDescriptor FromCylinder(in RecognizedCylinder cylinder)
    {
        var minCenter = cylinder.MinCenter;
        var maxCenter = cylinder.MaxCenter;
        var minX = System.Math.Min(minCenter.X, maxCenter.X) - cylinder.Radius;
        var maxX = System.Math.Max(minCenter.X, maxCenter.X) + cylinder.Radius;
        var minY = System.Math.Min(minCenter.Y, maxCenter.Y) - cylinder.Radius;
        var maxY = System.Math.Max(minCenter.Y, maxCenter.Y) + cylinder.Radius;
        var minZ = System.Math.Min(minCenter.Z, maxCenter.Z);
        var maxZ = System.Math.Max(minCenter.Z, maxCenter.Z);
        return new SafeBooleanRootDescriptor(
            SafeBooleanRootKind.Cylinder,
            new AxisAlignedBoxExtents(minX, maxX, minY, maxY, minZ, maxZ),
            cylinder);
    }

    public SafeBooleanRootDescriptor Translate(Vector3D translation)
    {
        var translatedBox = new AxisAlignedBoxExtents(
            Box.MinX + translation.X,
            Box.MaxX + translation.X,
            Box.MinY + translation.Y,
            Box.MaxY + translation.Y,
            Box.MinZ + translation.Z,
            Box.MaxZ + translation.Z);

        if (Kind == SafeBooleanRootKind.Cylinder && Cylinder is RecognizedCylinder cylinder)
        {
            return new SafeBooleanRootDescriptor(
                Kind,
                translatedBox,
                cylinder with { AxisOrigin = cylinder.AxisOrigin + translation });
        }

        return new SafeBooleanRootDescriptor(Kind, translatedBox);
    }
}

public enum SupportedBooleanHoleSpanKind
{
    Through,
    BlindFromTop,
    BlindFromBottom,
}

public readonly record struct SupportedBooleanHole(
    string? FeatureId,
    AnalyticSurface Surface,
    double CenterX,
    double CenterY,
    Point3D StartCenter,
    Point3D EndCenter,
    Direction3D Axis,
    Direction3D ReferenceAxis,
    double BottomRadius,
    double TopRadius,
    SupportedBooleanHoleSpanKind SpanKind,
    double StartZ,
    double EndZ)
{
    public double MaxBoundaryRadius => System.Math.Max(BottomRadius, TopRadius);
    public bool IsBlind => SpanKind != SupportedBooleanHoleSpanKind.Through;

    public SupportedBooleanHole Translate(Vector3D translation)
        => this with
        {
            Surface = Surface.Translate(translation),
            CenterX = CenterX + translation.X,
            CenterY = CenterY + translation.Y,
            StartCenter = StartCenter + translation,
            EndCenter = EndCenter + translation,
            StartZ = StartZ + translation.Z,
            EndZ = EndZ + translation.Z,
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
            composition = new SafeBooleanComposition(box, [], SafeBooleanRootDescriptor.FromBox(box));
            return true;
        }

        if (BrepBooleanCylinderRecognition.TryRecognizeCylinder(body, tolerance, out var cylinder, out reason)
            && BrepBooleanCylinderRecognition.ValidateAxisAlignedZ(cylinder, tolerance))
        {
            var root = SafeBooleanRootDescriptor.FromCylinder(cylinder);
            composition = new SafeBooleanComposition(root.Box, [], root);
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
