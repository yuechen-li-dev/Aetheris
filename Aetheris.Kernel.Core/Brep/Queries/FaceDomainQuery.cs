using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Queries;

public enum FaceDomainClassification
{
    Inside,
    OnBoundary,
    Outside,
    Ambiguous,
    Unsupported
}

public sealed record FaceDomainQueryResult(
    bool IsSuccess,
    FaceId FaceId,
    SurfaceGeometryKind? SurfaceKind,
    FaceDomainClassification Classification,
    (double U, double V)? ProjectedUv,
    double? BoundaryDistance,
    bool NearEdge,
    bool NearVertex,
    bool SeamDuplicateRisk,
    bool TrimUnavailable,
    bool UnsupportedSurface,
    string Source,
    string Reason);

public static class FaceDomainQuery
{
    public static FaceDomainQueryResult TryClassifyPointOnFace(
        BrepBody body,
        FaceId faceId,
        Point3D point,
        ToleranceContext? tolerance = null)
    {
        var tol = tolerance ?? ToleranceContext.Default;
        if (!body.TryGetFaceSurfaceGeometry(faceId, out var surface) || surface is null)
        {
            return Unsupported(faceId, null, "FaceDomainQuery.MissingSurface", "Face has no bound surface geometry.", trimUnavailable: true);
        }

        return surface.Kind switch
        {
            SurfaceGeometryKind.Plane when surface.Plane is PlaneSurface plane => ClassifyPlanar(body, faceId, plane, point, tol),
            SurfaceGeometryKind.Cylinder when surface.Cylinder is CylinderSurface cylinder => ClassifyCylinder(body, faceId, cylinder, point, tol),
            SurfaceGeometryKind.Sphere when surface.Sphere is SphereSurface sphere => ClassifySphere(faceId, sphere, point, tol),
            SurfaceGeometryKind.Cone => Unsupported(faceId, surface.Kind, "FaceDomainQuery.UnsupportedCone", "Surface projection/trim classification for conical faces is not implemented in this milestone.", seamDuplicateRisk: true),
            SurfaceGeometryKind.Torus => Unsupported(faceId, surface.Kind, "FaceDomainQuery.UnsupportedTorus", "Surface projection/trim classification for toroidal faces is not implemented in this milestone.", seamDuplicateRisk: true),
            SurfaceGeometryKind.BSplineSurfaceWithKnots => Unsupported(faceId, surface.Kind, "FaceDomainQuery.UnsupportedBSpline", "Surface projection/trim classification for bspline faces is not implemented in this milestone.", trimUnavailable: true),
            _ => Unsupported(faceId, surface.Kind, "FaceDomainQuery.UnsupportedSurface", "Surface kind is unsupported for face-domain query.")
        };
    }
    private static FaceDomainQueryResult ClassifySphere(FaceId faceId, SphereSurface sphere, Point3D point, ToleranceContext tolerance)
    {
        var local = point - sphere.Center;
        var r = local.Length;
        var residual = double.Abs(r - sphere.Radius);
        var u = double.Atan2(local.Dot(sphere.YAxis.ToVector()), local.Dot(sphere.XAxis.ToVector()));
        var v = double.Asin(double.Clamp(local.Dot(sphere.Axis.ToVector()) / double.Max(sphere.Radius, 1e-12d), -1d, 1d));
        var onBoundary = residual <= tolerance.Linear;
        var classification = onBoundary ? FaceDomainClassification.OnBoundary : FaceDomainClassification.Outside;
        return new FaceDomainQueryResult(
            IsSuccess: classification != FaceDomainClassification.Outside,
            faceId,
            SurfaceGeometryKind.Sphere,
            classification,
            (u, v),
            residual,
            NearEdge: onBoundary,
            NearVertex: false,
            SeamDuplicateRisk: double.Abs(double.Abs(u) - double.Pi) <= tolerance.Angular,
            TrimUnavailable: false,
            UnsupportedSurface: false,
            Source: "FaceDomainQuery.Sphere",
            Reason: classification.ToString());
    }

    private static FaceDomainQueryResult ClassifyCylinder(BrepBody body, FaceId faceId, CylinderSurface cylinder, Point3D point, ToleranceContext tolerance)
    {
        var axis = cylinder.Axis.ToVector();
        var delta = point - cylinder.Origin;
        var v = delta.Dot(axis);
        var radial = delta - (axis * v);
        var radialDistance = radial.Length;
        var residual = double.Abs(radialDistance - cylinder.Radius);
        ResolveAxialBounds(body, faceId, cylinder.Axis, cylinder.Origin, out var minV, out var maxV);
        if (minV.HasValue && (v < minV.Value - tolerance.Linear || v > maxV!.Value + tolerance.Linear))
        {
            return new FaceDomainQueryResult(false, faceId, SurfaceGeometryKind.Cylinder, FaceDomainClassification.Outside, (double.Atan2(radial.Dot(cylinder.YAxis.ToVector()), radial.Dot(cylinder.XAxis.ToVector())), v), residual, false, false, false, false, false, "FaceDomainQuery.Cylinder", "OutsideTrimVBounds");
        }

        var nearAxialBoundary = (minV.HasValue && double.Abs(v - minV.Value) <= tolerance.Linear)
            || (maxV.HasValue && double.Abs(v - maxV.Value) <= tolerance.Linear);
        var nearSurface = residual <= tolerance.Linear;
        var u = double.Atan2(radial.Dot(cylinder.YAxis.ToVector()), radial.Dot(cylinder.XAxis.ToVector()));
        var seamRisk = double.Abs(double.Abs(u) - double.Pi) <= tolerance.Angular;
        var classification = nearSurface
            ? (nearAxialBoundary || seamRisk ? FaceDomainClassification.Ambiguous : FaceDomainClassification.Inside)
            : FaceDomainClassification.Outside;
        return new FaceDomainQueryResult(
            classification != FaceDomainClassification.Ambiguous,
            faceId,
            SurfaceGeometryKind.Cylinder,
            classification,
            (u, v),
            residual,
            NearEdge: nearAxialBoundary,
            NearVertex: false,
            SeamDuplicateRisk: seamRisk,
            TrimUnavailable: false,
            UnsupportedSurface: false,
            Source: "FaceDomainQuery.Cylinder",
            Reason: classification.ToString());
    }

    private static void ResolveAxialBounds(BrepBody body, FaceId sideFaceId, Direction3D axis, Point3D axisOrigin, out double? minV, out double? maxV)
    {
        minV = null;
        maxV = null;
        var axisVector = axis.ToVector();
        foreach (var faceBinding in body.Bindings.FaceBindings)
        {
            if (faceBinding.FaceId == sideFaceId) continue;
            var surface = body.Geometry.GetSurface(faceBinding.SurfaceGeometryId);
            if (surface.Kind != SurfaceGeometryKind.Plane || surface.Plane is not PlaneSurface plane) continue;
            var dot = plane.Normal.ToVector().Dot(axisVector);
            if (double.Abs(double.Abs(dot) - 1d) > 1e-6d) continue;
            var pv = (plane.Origin - axisOrigin).Dot(axisVector);
            minV = !minV.HasValue ? pv : double.Min(minV.Value, pv);
            maxV = !maxV.HasValue ? pv : double.Max(maxV.Value, pv);
        }
    }

    private static FaceDomainQueryResult ClassifyPlanar(BrepBody body, FaceId faceId, PlaneSurface plane, Point3D point, ToleranceContext tolerance)
    {
        var uv = ProjectToPlane(point, plane);
        if (!AnalyticPlanarFaceDomain.TryCreate(body, faceId, plane, out var domain))
        {
            return new FaceDomainQueryResult(
                IsSuccess: false,
                faceId,
                SurfaceGeometryKind.Plane,
                FaceDomainClassification.Ambiguous,
                uv,
                BoundaryDistance: null,
                NearEdge: false,
                NearVertex: false,
                SeamDuplicateRisk: false,
                TrimUnavailable: true,
                UnsupportedSurface: false,
                Source: "FaceDomainQuery.Planar.TrimUnavailable",
                Reason: "Planar face exists but trim/domain loops could not be resolved for classification.");
        }

        var inside = domain.Contains(point, tolerance);
        var nearBoundary = domain.IsNearBoundary(point, tolerance, out var boundaryDistance, out var nearVertex);
        var classification = nearBoundary
            ? FaceDomainClassification.OnBoundary
            : inside ? FaceDomainClassification.Inside : FaceDomainClassification.Outside;

        return new FaceDomainQueryResult(
            IsSuccess: true,
            faceId,
            SurfaceGeometryKind.Plane,
            classification,
            uv,
            boundaryDistance,
            NearEdge: nearBoundary,
            NearVertex: nearVertex,
            SeamDuplicateRisk: false,
            TrimUnavailable: false,
            UnsupportedSurface: false,
            Source: "FaceDomainQuery.Planar",
            Reason: classification.ToString());
    }

    private static FaceDomainQueryResult Unsupported(FaceId faceId, SurfaceGeometryKind? kind, string source, string reason, bool seamDuplicateRisk = false, bool trimUnavailable = false)
        => new(false, faceId, kind, FaceDomainClassification.Unsupported, null, null, false, false, seamDuplicateRisk, trimUnavailable, true, source, reason);

    private static (double U, double V) ProjectToPlane(Point3D point, PlaneSurface plane)
    {
        var local = point - plane.Origin;
        return (local.Dot(plane.UAxis.ToVector()), local.Dot(plane.VAxis.ToVector()));
    }
}
