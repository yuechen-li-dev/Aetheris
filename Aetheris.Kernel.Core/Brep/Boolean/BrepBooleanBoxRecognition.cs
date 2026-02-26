using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public readonly record struct AxisAlignedBoxExtents(double MinX, double MaxX, double MinY, double MaxY, double MinZ, double MaxZ)
{
    public bool HasPositiveVolume(ToleranceContext tolerance)
        => (MaxX - MinX) > tolerance.Linear && (MaxY - MinY) > tolerance.Linear && (MaxZ - MinZ) > tolerance.Linear;

    public bool ApproximatelyEquals(AxisAlignedBoxExtents other, ToleranceContext tolerance)
        => ToleranceMath.AlmostEqual(MinX, other.MinX, tolerance)
           && ToleranceMath.AlmostEqual(MaxX, other.MaxX, tolerance)
           && ToleranceMath.AlmostEqual(MinY, other.MinY, tolerance)
           && ToleranceMath.AlmostEqual(MaxY, other.MaxY, tolerance)
           && ToleranceMath.AlmostEqual(MinZ, other.MinZ, tolerance)
           && ToleranceMath.AlmostEqual(MaxZ, other.MaxZ, tolerance);

    public bool Contains(AxisAlignedBoxExtents other, ToleranceContext tolerance)
        => ToleranceMath.LessThanOrAlmostEqual(MinX, other.MinX, tolerance)
           && ToleranceMath.GreaterThanOrAlmostEqual(MaxX, other.MaxX, tolerance)
           && ToleranceMath.LessThanOrAlmostEqual(MinY, other.MinY, tolerance)
           && ToleranceMath.GreaterThanOrAlmostEqual(MaxY, other.MaxY, tolerance)
           && ToleranceMath.LessThanOrAlmostEqual(MinZ, other.MinZ, tolerance)
           && ToleranceMath.GreaterThanOrAlmostEqual(MaxZ, other.MaxZ, tolerance);

    public bool OverlapsWithPositiveVolume(AxisAlignedBoxExtents other, ToleranceContext tolerance)
        => Intersection(this, other) is { } intersection && intersection.HasPositiveVolume(tolerance);

    public static AxisAlignedBoxExtents? Intersection(AxisAlignedBoxExtents a, AxisAlignedBoxExtents b)
    {
        var minX = System.Math.Max(a.MinX, b.MinX);
        var maxX = System.Math.Min(a.MaxX, b.MaxX);
        var minY = System.Math.Max(a.MinY, b.MinY);
        var maxY = System.Math.Min(a.MaxY, b.MaxY);
        var minZ = System.Math.Max(a.MinZ, b.MinZ);
        var maxZ = System.Math.Min(a.MaxZ, b.MaxZ);

        return minX <= maxX && minY <= maxY && minZ <= maxZ
            ? new AxisAlignedBoxExtents(minX, maxX, minY, maxY, minZ, maxZ)
            : null;
    }

    public static AxisAlignedBoxExtents Bounding(AxisAlignedBoxExtents a, AxisAlignedBoxExtents b)
        => new(System.Math.Min(a.MinX, b.MinX), System.Math.Max(a.MaxX, b.MaxX), System.Math.Min(a.MinY, b.MinY), System.Math.Max(a.MaxY, b.MaxY), System.Math.Min(a.MinZ, b.MinZ), System.Math.Max(a.MaxZ, b.MaxZ));

    public static bool UnionIsSingleBox(AxisAlignedBoxExtents a, AxisAlignedBoxExtents b, AxisAlignedBoxExtents bounds, ToleranceContext tolerance)
    {
        var xAligned = ToleranceMath.AlmostEqual(a.MinX, bounds.MinX, tolerance)
            && ToleranceMath.AlmostEqual(a.MaxX, bounds.MaxX, tolerance)
            && ToleranceMath.AlmostEqual(b.MinX, bounds.MinX, tolerance)
            && ToleranceMath.AlmostEqual(b.MaxX, bounds.MaxX, tolerance);
        var yAligned = ToleranceMath.AlmostEqual(a.MinY, bounds.MinY, tolerance)
            && ToleranceMath.AlmostEqual(a.MaxY, bounds.MaxY, tolerance)
            && ToleranceMath.AlmostEqual(b.MinY, bounds.MinY, tolerance)
            && ToleranceMath.AlmostEqual(b.MaxY, bounds.MaxY, tolerance);
        var zAligned = ToleranceMath.AlmostEqual(a.MinZ, bounds.MinZ, tolerance)
            && ToleranceMath.AlmostEqual(a.MaxZ, bounds.MaxZ, tolerance)
            && ToleranceMath.AlmostEqual(b.MinZ, bounds.MinZ, tolerance)
            && ToleranceMath.AlmostEqual(b.MaxZ, bounds.MaxZ, tolerance);

        var alignedAxisCount = (xAligned ? 1 : 0) + (yAligned ? 1 : 0) + (zAligned ? 1 : 0);
        if (alignedAxisCount < 2)
        {
            return false;
        }

        var xTouchOrOverlap = System.Math.Min(a.MaxX, b.MaxX) >= System.Math.Max(a.MinX, b.MinX) - tolerance.Linear;
        var yTouchOrOverlap = System.Math.Min(a.MaxY, b.MaxY) >= System.Math.Max(a.MinY, b.MinY) - tolerance.Linear;
        var zTouchOrOverlap = System.Math.Min(a.MaxZ, b.MaxZ) >= System.Math.Max(a.MinZ, b.MinZ) - tolerance.Linear;

        if (!xAligned && !xTouchOrOverlap)
        {
            return false;
        }

        if (!yAligned && !yTouchOrOverlap)
        {
            return false;
        }

        if (!zAligned && !zTouchOrOverlap)
        {
            return false;
        }

        return true;
    }
}

public static class BrepBooleanBoxRecognition
{
    public static bool TryRecognizeAxisAlignedBox(BrepBody body, ToleranceContext tolerance, out AxisAlignedBoxExtents extents, out string reason)
    {
        extents = default;
        reason = string.Empty;

        if (body.Topology.Vertices.Count() != 8 || body.Topology.Edges.Count() != 12 || body.Topology.Faces.Count() != 6)
        {
            reason = "topology does not match M08 box counts.";
            return false;
        }

        var faceBindings = body.Bindings.FaceBindings.ToArray();
        if (faceBindings.Length != 6)
        {
            reason = "box recognition requires exactly six face bindings.";
            return false;
        }

        if (body.Bindings.EdgeBindings.Count() != 12)
        {
            reason = "box recognition requires exactly twelve edge bindings.";
            return false;
        }

        foreach (var edgeBinding in body.Bindings.EdgeBindings)
        {
            var edgeCurve = body.Geometry.GetCurve(edgeBinding.CurveGeometryId);
            if (edgeCurve.Kind != CurveGeometryKind.Line3)
            {
                reason = "box recognition requires line curves on all edges.";
                return false;
            }
        }

        var hasXMin = false;
        var hasXMax = false;
        var hasYMin = false;
        var hasYMax = false;
        var hasZMin = false;
        var hasZMax = false;

        var minX = 0d;
        var maxX = 0d;
        var minY = 0d;
        var maxY = 0d;
        var minZ = 0d;
        var maxZ = 0d;

        foreach (var binding in faceBindings)
        {
            var surface = body.Geometry.GetSurface(binding.SurfaceGeometryId);
            if (surface.Kind != SurfaceGeometryKind.Plane || surface.Plane is null)
            {
                reason = "box recognition requires planar surfaces for all faces.";
                return false;
            }

            var normal = surface.Plane.Value.Normal;
            if (ToleranceMath.AlmostEqual(normal.X, -1d, tolerance) && ToleranceMath.AlmostZero(normal.Y, tolerance) && ToleranceMath.AlmostZero(normal.Z, tolerance))
            {
                minX = surface.Plane.Value.Origin.X;
                hasXMin = true;
            }
            else if (ToleranceMath.AlmostEqual(normal.X, 1d, tolerance) && ToleranceMath.AlmostZero(normal.Y, tolerance) && ToleranceMath.AlmostZero(normal.Z, tolerance))
            {
                maxX = surface.Plane.Value.Origin.X;
                hasXMax = true;
            }
            else if (ToleranceMath.AlmostEqual(normal.Y, -1d, tolerance) && ToleranceMath.AlmostZero(normal.X, tolerance) && ToleranceMath.AlmostZero(normal.Z, tolerance))
            {
                minY = surface.Plane.Value.Origin.Y;
                hasYMin = true;
            }
            else if (ToleranceMath.AlmostEqual(normal.Y, 1d, tolerance) && ToleranceMath.AlmostZero(normal.X, tolerance) && ToleranceMath.AlmostZero(normal.Z, tolerance))
            {
                maxY = surface.Plane.Value.Origin.Y;
                hasYMax = true;
            }
            else if (ToleranceMath.AlmostEqual(normal.Z, -1d, tolerance) && ToleranceMath.AlmostZero(normal.X, tolerance) && ToleranceMath.AlmostZero(normal.Y, tolerance))
            {
                minZ = surface.Plane.Value.Origin.Z;
                hasZMin = true;
            }
            else if (ToleranceMath.AlmostEqual(normal.Z, 1d, tolerance) && ToleranceMath.AlmostZero(normal.X, tolerance) && ToleranceMath.AlmostZero(normal.Y, tolerance))
            {
                maxZ = surface.Plane.Value.Origin.Z;
                hasZMax = true;
            }
            else
            {
                reason = "face normals are not axis-aligned as required for M13 boxes.";
                return false;
            }
        }

        if (!(hasXMin && hasXMax && hasYMin && hasYMax && hasZMin && hasZMax))
        {
            reason = "box recognition requires ±X/±Y/±Z face normals.";
            return false;
        }

        extents = new AxisAlignedBoxExtents(minX, maxX, minY, maxY, minZ, maxZ);
        if (!extents.HasPositiveVolume(tolerance))
        {
            reason = "recognized extents do not have positive volume.";
            return false;
        }

        return true;
    }

    public static KernelResult<BrepBody> CreateBoxFromExtents(AxisAlignedBoxExtents extents)
    {
        var width = extents.MaxX - extents.MinX;
        var height = extents.MaxY - extents.MinY;
        var depth = extents.MaxZ - extents.MinZ;
        var create = BrepPrimitives.CreateBox(width, height, depth);
        if (!create.IsSuccess)
        {
            return create;
        }

        var center = new Vector3D((extents.MinX + extents.MaxX) * 0.5d, (extents.MinY + extents.MaxY) * 0.5d, (extents.MinZ + extents.MaxZ) * 0.5d);
        var translatedGeometry = new BrepGeometryStore();

        foreach (var curve in create.Value.Geometry.Curves)
        {
            translatedGeometry.AddCurve(curve.Key, curve.Value.Kind == CurveGeometryKind.Line3 && curve.Value.Line3 is Line3Curve line
                ? CurveGeometry.FromLine(new Line3Curve(line.Origin + center, line.Direction))
                : curve.Value);
        }

        foreach (var surface in create.Value.Geometry.Surfaces)
        {
            translatedGeometry.AddSurface(surface.Key, surface.Value.Kind == SurfaceGeometryKind.Plane && surface.Value.Plane is PlaneSurface plane
                ? SurfaceGeometry.FromPlane(new PlaneSurface(plane.Origin + center, plane.Normal, plane.UAxis))
                : surface.Value);
        }

        return KernelResult<BrepBody>.Success(new BrepBody(create.Value.Topology, translatedGeometry, create.Value.Bindings), create.Diagnostics);
    }
}
