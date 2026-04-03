using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Queries;

public enum AnalyticDisplayFallbackReason
{
    MissingFaceBinding,
    MissingSurfaceGeometry,
    UnsupportedSurfaceKind,
    UnsupportedTrim,
}

public enum AnalyticDisplayShellRole
{
    Outer,
    InnerVoid,
    Unknown,
}

public readonly record struct AnalyticDisplayFaceDomainHint(double? MinV, double? MaxV);

public sealed record AnalyticDisplayFaceEntry(
    FaceId FaceId,
    ShellId ShellId,
    AnalyticDisplayShellRole ShellRole,
    SurfaceGeometryId SurfaceGeometryId,
    SurfaceGeometryKind SurfaceKind,
    SurfaceGeometry SurfaceGeometry,
    int LoopCount,
    AnalyticDisplayFaceDomainHint? DomainHint);

public sealed record AnalyticDisplayFallbackFaceEntry(
    FaceId FaceId,
    ShellId ShellId,
    AnalyticDisplayShellRole ShellRole,
    AnalyticDisplayFallbackReason Reason,
    SurfaceGeometryKind? SurfaceKind = null,
    string? Detail = null);

public sealed record AnalyticDisplayPacket(
    BodyId BodyId,
    IReadOnlyList<AnalyticDisplayFaceEntry> AnalyticFaces,
    IReadOnlyList<AnalyticDisplayFallbackFaceEntry> FallbackFaces);

public static class AnalyticDisplayPacketBuilder
{
    public static AnalyticDisplayPacket Build(BrepBody body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var bodyId = body.Topology.Bodies.OrderBy(candidate => candidate.Id.Value).Select(candidate => candidate.Id).FirstOrDefault();
        var shellRoles = ResolveShellRoles(body);
        var analyticFaces = new List<AnalyticDisplayFaceEntry>();
        var fallbackFaces = new List<AnalyticDisplayFallbackFaceEntry>();

        foreach (var shellId in GetOrderedShellIds(body))
        {
            var shellRole = shellRoles.TryGetValue(shellId, out var role) ? role : AnalyticDisplayShellRole.Unknown;
            var faceIds = body.Topology.GetShell(shellId).FaceIds.OrderBy(faceId => faceId.Value);
            foreach (var faceId in faceIds)
            {
                if (!body.Bindings.TryGetFaceBinding(faceId, out var faceBinding))
                {
                    fallbackFaces.Add(new AnalyticDisplayFallbackFaceEntry(faceId, shellId, shellRole, AnalyticDisplayFallbackReason.MissingFaceBinding));
                    continue;
                }

                if (!body.Geometry.TryGetSurface(faceBinding.SurfaceGeometryId, out var surface) || surface is null)
                {
                    fallbackFaces.Add(new AnalyticDisplayFallbackFaceEntry(faceId, shellId, shellRole, AnalyticDisplayFallbackReason.MissingSurfaceGeometry));
                    continue;
                }

                if (!IsSupportedSurfaceKind(surface.Kind))
                {
                    fallbackFaces.Add(new AnalyticDisplayFallbackFaceEntry(faceId, shellId, shellRole, AnalyticDisplayFallbackReason.UnsupportedSurfaceKind, surface.Kind));
                    continue;
                }

                if (!IsSupportedTrim(body, faceId, surface.Kind))
                {
                    fallbackFaces.Add(new AnalyticDisplayFallbackFaceEntry(faceId, shellId, shellRole, AnalyticDisplayFallbackReason.UnsupportedTrim, surface.Kind));
                    continue;
                }

                analyticFaces.Add(new AnalyticDisplayFaceEntry(
                    faceId,
                    shellId,
                    shellRole,
                    faceBinding.SurfaceGeometryId,
                    surface.Kind,
                    surface,
                    body.Topology.GetFace(faceId).LoopIds.Count,
                    TryResolveDomainHint(body, faceId, surface)));
            }
        }

        return new AnalyticDisplayPacket(bodyId, analyticFaces, fallbackFaces);
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
            return true;
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


    private static bool IsSupportedPlanarTrim(BrepBody body, FaceId faceId)
    {
        if (!body.Bindings.TryGetFaceBinding(faceId, out var faceBinding)
            || !body.Geometry.TryGetSurface(faceBinding.SurfaceGeometryId, out var surface)
            || surface?.Plane is not PlaneSurface plane
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

    private static AnalyticDisplayFaceDomainHint? TryResolveDomainHint(BrepBody body, FaceId faceId, SurfaceGeometry surface)
    {
        switch (surface.Kind)
        {
            case SurfaceGeometryKind.Cylinder when surface.Cylinder is CylinderSurface cylinder:
                ResolveAxialBounds(body, faceId, cylinder.Axis, cylinder.Origin, out var cylinderMin, out var cylinderMax);
                return new AnalyticDisplayFaceDomainHint(cylinderMin, cylinderMax);
            case SurfaceGeometryKind.Cone when surface.Cone is ConeSurface cone:
                ResolveAxialBounds(body, faceId, cone.Axis, cone.Apex, out var coneMin, out var coneMax);
                return new AnalyticDisplayFaceDomainHint(coneMin, coneMax);
            default:
                return null;
        }
    }

    private static IReadOnlyList<ShellId> GetOrderedShellIds(BrepBody body)
    {
        if (body.ShellRepresentation is { } representation)
        {
            return representation.OrderedShellIds;
        }

        var topologyBody = body.Topology.Bodies.OrderBy(candidate => candidate.Id.Value).FirstOrDefault();
        return topologyBody?.ShellIds.OrderBy(shellId => shellId.Value).ToArray() ?? [];
    }

    private static IReadOnlyDictionary<ShellId, AnalyticDisplayShellRole> ResolveShellRoles(BrepBody body)
    {
        var result = new Dictionary<ShellId, AnalyticDisplayShellRole>();
        if (body.ShellRepresentation is { } representation)
        {
            result[representation.OuterShellId] = AnalyticDisplayShellRole.Outer;
            foreach (var innerShellId in representation.InnerShellIds)
            {
                result[innerShellId] = AnalyticDisplayShellRole.InnerVoid;
            }
        }

        return result;
    }

    private static void ResolveAxialBounds(BrepBody body, FaceId sideFaceId, Direction3D axis, Point3D axisOrigin, out double? minV, out double? maxV)
    {
        minV = null;
        maxV = null;
        var axisVector = axis.ToVector();

        foreach (var faceBinding in body.Bindings.FaceBindings.OrderBy(binding => binding.FaceId.Value))
        {
            if (faceBinding.FaceId == sideFaceId)
            {
                continue;
            }

            var surface = body.Geometry.GetSurface(faceBinding.SurfaceGeometryId);
            if (surface.Kind != SurfaceGeometryKind.Plane || surface.Plane is not PlaneSurface plane)
            {
                continue;
            }

            var dot = plane.Normal.ToVector().Dot(axisVector);
            if (double.Abs(double.Abs(dot) - 1d) > 1e-6d)
            {
                continue;
            }

            var v = (plane.Origin - axisOrigin).Dot(axisVector);
            minV = minV.HasValue ? double.Min(minV.Value, v) : v;
            maxV = maxV.HasValue ? double.Max(maxV.Value, v) : v;
        }
    }
}
