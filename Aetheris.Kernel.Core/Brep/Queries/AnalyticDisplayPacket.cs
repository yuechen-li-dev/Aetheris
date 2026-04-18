using Aetheris.Kernel.Core.Geometry;
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
    AnalyticDisplayFaceDomainHint? DomainHint,
    IReadOnlyList<Point3D>? PlanarOuterBoundary);

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
                if (!AnalyticDisplaySupportPolicy.TryGetSupportedSurface(body, faceId, out var surface, out var fallbackReason))
                {
                    SurfaceGeometryKind? surfaceKind = body.Bindings.TryGetFaceBinding(faceId, out var binding)
                        && body.Geometry.TryGetSurface(binding.SurfaceGeometryId, out var candidate)
                        && candidate is not null
                        ? candidate.Kind
                        : (SurfaceGeometryKind?)null;
                    fallbackFaces.Add(new AnalyticDisplayFallbackFaceEntry(faceId, shellId, shellRole, fallbackReason, surfaceKind));
                    continue;
                }

                var faceBinding = body.Bindings.GetFaceBinding(faceId);
                analyticFaces.Add(new AnalyticDisplayFaceEntry(
                    faceId,
                    shellId,
                    shellRole,
                    faceBinding.SurfaceGeometryId,
                    surface.Kind,
                    surface,
                    body.Topology.GetFace(faceId).LoopIds.Count,
                    TryResolveDomainHint(body, faceId, surface),
                    TryResolvePlanarOuterBoundary(body, faceId, surface)));
            }
        }

        return new AnalyticDisplayPacket(bodyId, analyticFaces, fallbackFaces);
    }

    private static IReadOnlyList<Point3D>? TryResolvePlanarOuterBoundary(BrepBody body, FaceId faceId, SurfaceGeometry surface)
    {
        if (surface.Kind != SurfaceGeometryKind.Plane || surface.Plane is not PlaneSurface plane)
        {
            return null;
        }

        return AnalyticPlanarFaceDomain.TryGetOuterBoundaryWorld(body, faceId, plane, out var outerBoundary)
            ? outerBoundary
            : null;
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
