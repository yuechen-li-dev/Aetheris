using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public sealed record SafeBooleanComposition(
    AxisAlignedBoxExtents OuterBox,
    IReadOnlyList<SupportedBooleanHole> Holes,
    SafeBooleanRootDescriptor? Root = null,
    IReadOnlyList<AxisAlignedBoxExtents>? OccupiedCells = null,
    IReadOnlyList<SupportedCylinderOpenSlot>? OpenSlots = null,
    SupportedThroughVoidSet? ThroughVoids = null,
    SupportedBlindPrismaticPocket? BlindPrismaticPocket = null)
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
            RootDescriptor.Translate(translation),
            OccupiedCells?.Select(cell => new AxisAlignedBoxExtents(
                cell.MinX + translation.X,
                cell.MaxX + translation.X,
                cell.MinY + translation.Y,
                cell.MaxY + translation.Y,
                cell.MinZ + translation.Z,
                cell.MaxZ + translation.Z)).ToArray(),
            OpenSlots?.Select(slot => slot.Translate(translation)).ToArray(),
            ThroughVoids?.Translate(translation),
            BlindPrismaticPocket?.Translate(translation));
    }
}

public sealed record SupportedThroughVoidSet(
    IReadOnlyList<SupportedBooleanHole> AnalyticVoids,
    IReadOnlyList<SupportedPrismaticThroughVoid> PrismaticVoids)
{
    public SupportedThroughVoidSet Translate(Vector3D translation)
        => new(
            AnalyticVoids.Select(hole => hole.Translate(translation)).ToArray(),
            PrismaticVoids.Select(voidFeature => voidFeature.Translate(translation)).ToArray());
}

public readonly record struct SupportedPrismaticThroughVoid(
    AxisAlignedBoxExtents Bounds,
    IReadOnlyList<(double X, double Y)> Footprint)
{
    public SupportedPrismaticThroughVoid Translate(Vector3D translation)
        => new(
            new AxisAlignedBoxExtents(
                Bounds.MinX + translation.X,
                Bounds.MaxX + translation.X,
                Bounds.MinY + translation.Y,
                Bounds.MaxY + translation.Y,
                Bounds.MinZ + translation.Z,
                Bounds.MaxZ + translation.Z),
            Footprint.Select(point => (point.X + translation.X, point.Y + translation.Y)).ToArray());
}

public readonly record struct SupportedBlindPrismaticPocket(
    AxisAlignedBoxExtents Bounds,
    IReadOnlyList<(double X, double Y)> Footprint)
{
    public SupportedBlindPrismaticPocket Translate(Vector3D translation)
        => new(
            new AxisAlignedBoxExtents(
                Bounds.MinX + translation.X,
                Bounds.MaxX + translation.X,
                Bounds.MinY + translation.Y,
                Bounds.MaxY + translation.Y,
                Bounds.MinZ + translation.Z,
                Bounds.MaxZ + translation.Z),
            Footprint.Select(point => (point.X + translation.X, point.Y + translation.Y)).ToArray());
}

public enum SafeBooleanRootKind
{
    Box,
    Cylinder,
    PolygonalExtrusion,
}

public readonly record struct SafeBooleanRootDescriptor(
    SafeBooleanRootKind Kind,
    AxisAlignedBoxExtents Box,
    RecognizedCylinder? Cylinder = null,
    IReadOnlyList<(double X, double Y)>? PolygonFootprint = null)
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

    public static SafeBooleanRootDescriptor FromPolygonalExtrusion(in RecognizedPrismaticProfile profile)
        => new(SafeBooleanRootKind.PolygonalExtrusion, profile.Bounds, Cylinder: null, PolygonFootprint: profile.Footprint);

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

        if (Kind == SafeBooleanRootKind.PolygonalExtrusion && PolygonFootprint is { Count: > 0 } footprint)
        {
            return new SafeBooleanRootDescriptor(
                Kind,
                translatedBox,
                Cylinder: null,
                PolygonFootprint: footprint.Select(point => (point.X + translation.X, point.Y + translation.Y)).ToArray());
        }

        return new SafeBooleanRootDescriptor(Kind, translatedBox, PolygonFootprint: PolygonFootprint);
    }
}

public enum SupportedBooleanHoleSpanKind
{
    Through,
    BlindFromTop,
    BlindFromBottom,
    Contained,
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

public readonly record struct SupportedCylinderOpenSlot(
    AxisAlignedBoxExtents ToolExtents)
{
    public SupportedCylinderOpenSlot Translate(Vector3D translation)
        => new(new AxisAlignedBoxExtents(
            ToolExtents.MinX + translation.X,
            ToolExtents.MaxX + translation.X,
            ToolExtents.MinY + translation.Y,
            ToolExtents.MaxY + translation.Y,
            ToolExtents.MinZ + translation.Z,
            ToolExtents.MaxZ + translation.Z));
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

        if (BrepBooleanPrismaticProfileRecognition.TryRecognize(body, tolerance, out var profile, out reason))
        {
            var root = SafeBooleanRootDescriptor.FromPolygonalExtrusion(profile);
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
