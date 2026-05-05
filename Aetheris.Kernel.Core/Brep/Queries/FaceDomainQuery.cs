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
            SurfaceGeometryKind.Cylinder => Unsupported(faceId, surface.Kind, "FaceDomainQuery.UnsupportedCylinder", "Surface projection/trim classification for cylindrical faces is not implemented in this milestone.", seamDuplicateRisk: true),
            SurfaceGeometryKind.Sphere => Unsupported(faceId, surface.Kind, "FaceDomainQuery.UnsupportedSphere", "Surface projection/trim classification for spherical faces is not implemented in this milestone.", seamDuplicateRisk: true),
            SurfaceGeometryKind.Cone => Unsupported(faceId, surface.Kind, "FaceDomainQuery.UnsupportedCone", "Surface projection/trim classification for conical faces is not implemented in this milestone.", seamDuplicateRisk: true),
            SurfaceGeometryKind.Torus => Unsupported(faceId, surface.Kind, "FaceDomainQuery.UnsupportedTorus", "Surface projection/trim classification for toroidal faces is not implemented in this milestone.", seamDuplicateRisk: true),
            SurfaceGeometryKind.BSplineSurfaceWithKnots => Unsupported(faceId, surface.Kind, "FaceDomainQuery.UnsupportedBSpline", "Surface projection/trim classification for bspline faces is not implemented in this milestone.", trimUnavailable: true),
            _ => Unsupported(faceId, surface.Kind, "FaceDomainQuery.UnsupportedSurface", "Surface kind is unsupported for face-domain query.")
        };
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
