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

public readonly record struct AnalyticDisplayFaceDomainHint(double? MinU, double? MaxU, double? MinV, double? MaxV);

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
                return ResolveFaceDomain(body, faceId, cylinder.Origin, cylinder.Axis, cylinder.XAxis, cylinder.YAxis);
            case SurfaceGeometryKind.Cone when surface.Cone is ConeSurface cone:
                var coneAxis = cone.Axis.ToVector();
                var reference = cone.ReferenceAxis.ToVector();
                var coneX = Direction3D.Create(reference - (coneAxis * reference.Dot(coneAxis)));
                var coneY = Direction3D.Create(coneAxis.Cross(coneX.ToVector()));
                return ResolveFaceDomain(body, faceId, cone.Apex, cone.Axis, coneX, coneY);
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

    private static AnalyticDisplayFaceDomainHint ResolveFaceDomain(BrepBody body, FaceId faceId, Point3D origin, Direction3D axis, Direction3D xAxis, Direction3D yAxis)
    {
        var samples = new List<Point3D>();
        var hasFullCircleBoundary = false;
        foreach (var loopId in body.Topology.GetFace(faceId).LoopIds)
        foreach (var coedgeId in body.Topology.GetLoop(loopId).CoedgeIds)
        {
            var edgeId = body.Topology.GetCoedge(coedgeId).EdgeId;
            var edge = body.Topology.GetEdge(edgeId);
            if (body.TryGetVertexPoint(edge.StartVertexId, out var start)) samples.Add(start);
            if (body.TryGetVertexPoint(edge.EndVertexId, out var end)) samples.Add(end);
            if (!body.Bindings.TryGetEdgeBinding(edgeId, out var binding)
                || binding.TrimInterval is not { } trim
                || !body.Geometry.TryGetCurve(binding.CurveGeometryId, out var curve)
                || curve?.Circle3 is not { } circle)
            {
                continue;
            }
            if (trim.End - trim.Start >= (2d * double.Pi) - 1e-7d) hasFullCircleBoundary = true;
            else samples.Add(circle.Evaluate((trim.Start + trim.End) / 2d));
        }

        var axisVector = axis.ToVector();
        var axial = samples.Select(point => (point - origin).Dot(axisVector)).ToArray();
        var minV = axial.Length > 0 ? axial.Min() : (double?)null;
        var maxV = axial.Length > 0 ? axial.Max() : (double?)null;
        if (hasFullCircleBoundary) return new(null, null, minV, maxV);

        var x = xAxis.ToVector();
        var y = yAxis.ToVector();
        var angles = samples.Select(point => point - origin)
            .Select(radial => double.Atan2(radial.Dot(y), radial.Dot(x)))
            .Select(angle => angle < 0d ? angle + (2d * double.Pi) : angle)
            .Order()
            .Aggregate(new List<double>(), (unique, angle) =>
            {
                if (unique.Count == 0 || double.Abs(unique[^1] - angle) > 1e-7d) unique.Add(angle);
                return unique;
            });
        if (angles.Count < 2) return new(null, null, minV, maxV);

        var largestGap = double.NegativeInfinity;
        var gapIndex = 0;
        for (var i = 0; i < angles.Count; i++)
        {
            var next = i + 1 < angles.Count ? angles[i + 1] : angles[0] + (2d * double.Pi);
            var gap = next - angles[i];
            if (gap > largestGap) { largestGap = gap; gapIndex = i; }
        }
        var minU = angles[(gapIndex + 1) % angles.Count];
        var maxU = angles[gapIndex];
        if (maxU < minU) maxU += 2d * double.Pi;
        return new(minU, maxU, minV, maxV);
    }
}
