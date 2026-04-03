using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Queries;

internal static class AnalyticDisplaySupportPolicy
{
    internal static bool TryGetSupportedSurface(
        BrepBody body,
        FaceId faceId,
        out SurfaceGeometry surface,
        out AnalyticDisplayFallbackReason fallbackReason)
    {
        surface = null!;
        fallbackReason = AnalyticDisplayFallbackReason.MissingFaceBinding;

        if (!body.Bindings.TryGetFaceBinding(faceId, out var faceBinding))
        {
            return false;
        }

        if (!body.Geometry.TryGetSurface(faceBinding.SurfaceGeometryId, out var candidate) || candidate is null)
        {
            fallbackReason = AnalyticDisplayFallbackReason.MissingSurfaceGeometry;
            return false;
        }

        if (!IsSupportedSurfaceKind(candidate.Kind))
        {
            fallbackReason = AnalyticDisplayFallbackReason.UnsupportedSurfaceKind;
            return false;
        }

        if (!IsSupportedTrim(body, faceId, candidate.Kind))
        {
            fallbackReason = AnalyticDisplayFallbackReason.UnsupportedTrim;
            return false;
        }

        surface = candidate;
        return true;
    }

    private static bool IsSupportedSurfaceKind(SurfaceGeometryKind kind)
        => kind is SurfaceGeometryKind.Plane
            or SurfaceGeometryKind.Sphere
            or SurfaceGeometryKind.Cylinder
            or SurfaceGeometryKind.Cone
            or SurfaceGeometryKind.Torus;

    private static bool IsSupportedTrim(BrepBody body, FaceId faceId, SurfaceGeometryKind kind)
    {
        if (kind is SurfaceGeometryKind.Plane)
        {
            return IsSupportedPlanarTrim(body, faceId);
        }

        if (kind is SurfaceGeometryKind.Sphere or SurfaceGeometryKind.Torus)
        {
            return kind switch
            {
                SurfaceGeometryKind.Sphere => body.Topology.GetFace(faceId).LoopIds.Count == 0,
                SurfaceGeometryKind.Torus => IsSupportedWholeNativeTorus(body, faceId),
                _ => false,
            };
        }

        var face = body.Topology.GetFace(faceId);
        if (face.LoopIds.Count != 1)
        {
            return false;
        }

        var loop = body.Topology.GetLoop(face.LoopIds[0]);
        foreach (var coedgeId in loop.CoedgeIds)
        {
            var edgeId = body.Topology.GetCoedge(coedgeId).EdgeId;
            if (!body.Bindings.TryGetEdgeBinding(edgeId, out var edgeBinding)
                || !edgeBinding.TrimInterval.HasValue
                || !body.Geometry.TryGetCurve(edgeBinding.CurveGeometryId, out var curve)
                || curve is null
                || (curve.Kind is not CurveGeometryKind.Line3 and not CurveGeometryKind.Circle3))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSupportedWholeNativeTorus(BrepBody body, FaceId faceId)
    {
        var face = body.Topology.GetFace(faceId);
        if (face.LoopIds.Count != 1)
        {
            return false;
        }

        var loop = body.Topology.GetLoop(face.LoopIds[0]);
        if (loop.CoedgeIds.Count != 4)
        {
            return false;
        }

        foreach (var coedgeId in loop.CoedgeIds)
        {
            var edgeId = body.Topology.GetCoedge(coedgeId).EdgeId;
            if (!body.Bindings.TryGetEdgeBinding(edgeId, out var edgeBinding)
                || !edgeBinding.TrimInterval.HasValue
                || !body.Geometry.TryGetCurve(edgeBinding.CurveGeometryId, out var curve)
                || curve is null
                || curve.Kind != CurveGeometryKind.Circle3)
            {
                return false;
            }

            var trim = edgeBinding.TrimInterval.Value;
            if (double.Abs(trim.Start) > 1e-9d || double.Abs(trim.End - (2d * double.Pi)) > 1e-9d)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSupportedPlanarTrim(BrepBody body, FaceId faceId)
    {
        if (!body.Bindings.TryGetFaceBinding(faceId, out var faceBinding)
            || !body.Geometry.TryGetSurface(faceBinding.SurfaceGeometryId, out var surface)
            || surface?.Plane is not Geometry.Surfaces.PlaneSurface plane
            || !body.Topology.TryGetFace(faceId, out var face)
            || face is null)
        {
            return false;
        }

        var normal = plane.Normal.ToVector();
        var axisAlignedPlane = double.Abs(normal.X) > 0.9d || double.Abs(normal.Y) > 0.9d || double.Abs(normal.Z) > 0.9d;
        if (axisAlignedPlane)
        {
            return true;
        }

        if (face.LoopIds.Count != 1)
        {
            return false;
        }

        var loop = body.Topology.GetLoop(face.LoopIds[0]);
        if (loop.CoedgeIds.Count == 0)
        {
            return false;
        }

        foreach (var coedgeId in loop.CoedgeIds)
        {
            var edgeId = body.Topology.GetCoedge(coedgeId).EdgeId;
            if (!body.Bindings.TryGetEdgeBinding(edgeId, out var edgeBinding)
                || !body.Geometry.TryGetCurve(edgeBinding.CurveGeometryId, out var curve)
                || curve is null
                || curve.Kind != CurveGeometryKind.Circle3)
            {
                return false;
            }
        }

        return true;
    }
}
