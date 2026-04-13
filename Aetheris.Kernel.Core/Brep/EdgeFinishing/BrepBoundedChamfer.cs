using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Brep.EdgeFinishing;

/// <summary>
/// M5a bounded functional chamfer for convex planar-planar external edges on axis-aligned box-like bodies.
/// This intentionally supports only a single explicit vertical box edge token with one uniform distance.
/// </summary>
public static class BrepBoundedChamfer
{
    public static KernelResult<BrepBody> ChamferAxisAlignedBoxVerticalEdge(
        AxisAlignedBoxExtents box,
        BrepBoundedChamferEdge edge,
        double distance)
    {
        if (!double.IsFinite(distance) || distance <= 0d)
        {
            return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer distance must be finite and greater than 0.", "firmament.chamfer-bounded")]);
        }

        var sizeX = box.MaxX - box.MinX;
        var sizeY = box.MaxY - box.MinY;
        var sizeZ = box.MaxZ - box.MinZ;
        if (sizeX <= 0d || sizeY <= 0d || sizeZ <= 0d)
        {
            return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer requires a source box with strictly positive extents.", "firmament.chamfer-bounded")]);
        }

        var maxDistance = System.Math.Min(sizeX, sizeY);
        if (distance >= maxDistance)
        {
            return KernelResult<BrepBody>.Failure(
            [
                Failure(
                    "Bounded chamfer distance is too large for the selected edge; it must be strictly less than the local adjacent face extents to preserve manifoldness.",
                    "firmament.chamfer-bounded")
            ]);
        }

        var profile = BuildProfile(box, edge, distance);
        var frame = new ExtrudeFrame3D(
            origin: new Point3D(0d, 0d, box.MinZ),
            normal: Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            uAxis: Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        return BrepExtrude.Create(profile, frame, sizeZ);
    }

    private static PolylineProfile2D BuildProfile(AxisAlignedBoxExtents box, BrepBoundedChamferEdge edge, double d)
        => CreateProfile(edge switch
        {
            BrepBoundedChamferEdge.XMaxYMax =>
            [
                new ProfilePoint2D(box.MinX, box.MinY),
                new ProfilePoint2D(box.MaxX, box.MinY),
                new ProfilePoint2D(box.MaxX, box.MaxY - d),
                new ProfilePoint2D(box.MaxX - d, box.MaxY),
                new ProfilePoint2D(box.MinX, box.MaxY)
            ],
            BrepBoundedChamferEdge.XMaxYMin =>
            [
                new ProfilePoint2D(box.MinX, box.MinY),
                new ProfilePoint2D(box.MaxX - d, box.MinY),
                new ProfilePoint2D(box.MaxX, box.MinY + d),
                new ProfilePoint2D(box.MaxX, box.MaxY),
                new ProfilePoint2D(box.MinX, box.MaxY)
            ],
            BrepBoundedChamferEdge.XMinYMax =>
            [
                new ProfilePoint2D(box.MinX, box.MinY),
                new ProfilePoint2D(box.MaxX, box.MinY),
                new ProfilePoint2D(box.MaxX, box.MaxY),
                new ProfilePoint2D(box.MinX + d, box.MaxY),
                new ProfilePoint2D(box.MinX, box.MaxY - d)
            ],
            BrepBoundedChamferEdge.XMinYMin =>
            [
                new ProfilePoint2D(box.MinX, box.MinY + d),
                new ProfilePoint2D(box.MinX + d, box.MinY),
                new ProfilePoint2D(box.MaxX, box.MinY),
                new ProfilePoint2D(box.MaxX, box.MaxY),
                new ProfilePoint2D(box.MinX, box.MaxY)
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, null)
        });

    private static PolylineProfile2D CreateProfile(IReadOnlyList<ProfilePoint2D> vertices)
    {
        var profileResult = PolylineProfile2D.Create(vertices);
        if (!profileResult.IsSuccess)
        {
            throw new InvalidOperationException("Bounded chamfer profile generation produced an invalid profile.");
        }

        return profileResult.Value;
    }

    private static KernelDiagnostic Failure(string message, string source)
        => new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, Source: source);
}

public enum BrepBoundedChamferEdge
{
    XMinYMin,
    XMinYMax,
    XMaxYMin,
    XMaxYMax
}
