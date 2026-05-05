using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Queries;

public readonly record struct AnalyticRayHit(
    bool Hit,
    double Distance,
    Point3D Position,
    Direction3D Normal,
    BodyId BodyId,
    FaceId FaceId,
    SurfaceGeometryKind SurfaceKind,
    double? U = null,
    double? V = null);

public static class AnalyticDisplayQuery
{
    public static bool TryIntersectBody(BrepBody body, Ray3D ray, out AnalyticRayHit hit, double maxDistance = 1e6d, ToleranceContext? tolerance = null)
    {
        var context = tolerance ?? ToleranceContext.Default;
        hit = default;
        var faceIds = GetOrderedShellIds(body)
            .SelectMany(shellId => body.Topology.GetShell(shellId).FaceIds)
            .Distinct()
            .OrderBy(id => id.Value)
            .ToArray();
        var candidates = new List<AnalyticRayHit>(faceIds.Length);

        foreach (var faceId in faceIds)
        {
            if (TryIntersectFace(body, faceId, ray, out var candidate, maxDistance, context))
            {
                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        var ordered = candidates
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.FaceId.Value)
            .ToArray();

        var sampleOffset = double.Max(context.Linear * 32d, 1e-6d);
        foreach (var candidate in ordered)
        {
            var beforePoint = ray.PointAt(double.Max(0d, candidate.Distance - sampleOffset));
            var afterPoint = ray.PointAt(candidate.Distance + sampleOffset);
            var beforeInside = IsPointInMaterial(body, beforePoint, context);
            var afterInside = IsPointInMaterial(body, afterPoint, context);
            if (beforeInside != afterInside)
            {
                hit = candidate;
                return true;
            }
        }

        hit = ordered[0];
        return true;
    }

    public static bool TryIntersectFace(BrepBody body, FaceId faceId, Ray3D ray, out AnalyticRayHit hit, double maxDistance = 1e6d, ToleranceContext? tolerance = null)
    {
        var hits = new List<AnalyticRayHit>(2);
        CollectIntersectFaceHits(body, faceId, ray, hits, maxDistance, tolerance);
        if (hits.Count == 0)
        {
            hit = default;
            return false;
        }

        hit = hits.OrderBy(h => h.Distance).First();
        return true;
    }

    public static void CollectIntersectFaceHits(BrepBody body, FaceId faceId, Ray3D ray, List<AnalyticRayHit> hits, double maxDistance = 1e6d, ToleranceContext? tolerance = null)
    {
        var context = tolerance ?? ToleranceContext.Default;

        if (!AnalyticDisplaySupportPolicy.TryGetSupportedSurface(body, faceId, out var surface, out _))
        {
            return;
        }

        var bodyId = body.Topology.Bodies.OrderBy(b => b.Id.Value).FirstOrDefault()?.Id ?? BodyId.Invalid;

        switch (surface.Kind)
        {
            case SurfaceGeometryKind.Plane when surface.Plane is PlaneSurface plane:
                TryIntersectPlane(body, bodyId, faceId, plane, ray, maxDistance, context, hits);
                break;
            case SurfaceGeometryKind.Sphere when surface.Sphere is SphereSurface sphere:
                TryIntersectSphere(bodyId, faceId, sphere, ray, maxDistance, context, hits);
                break;
            case SurfaceGeometryKind.Cylinder when surface.Cylinder is CylinderSurface cylinder:
                TryIntersectCylinder(body, bodyId, faceId, cylinder, ray, maxDistance, context, hits);
                break;
            case SurfaceGeometryKind.Cone when surface.Cone is ConeSurface cone:
                TryIntersectCone(body, bodyId, faceId, cone, ray, maxDistance, context, hits);
                break;
            case SurfaceGeometryKind.Torus when surface.Torus is TorusSurface torus:
                TryIntersectTorus(bodyId, faceId, torus, ray, maxDistance, context, hits);
                break;
        }
    }

    private static void TryIntersectPlane(BrepBody body, BodyId bodyId, FaceId faceId, PlaneSurface plane, Ray3D ray, double maxDistance, ToleranceContext tolerance, List<AnalyticRayHit> hits)
    {
        var denom = plane.Normal.ToVector().Dot(ray.Direction.ToVector());
        if (ToleranceMath.AlmostZero(denom, tolerance))
        {
            return;
        }

        var t = (plane.Origin - ray.Origin).Dot(plane.Normal.ToVector()) / denom;
        if (t < -tolerance.Linear)
        {
            return;
        }

        t = ToleranceMath.ClampToZero(t, tolerance.Linear);
        if (t > maxDistance + tolerance.Linear)
        {
            return;
        }

        var point = ray.PointAt(t);
        if (!IsPointOnPlaneFaceDomain(body, faceId, plane, point, tolerance))
        {
            return;
        }

        var local = point - plane.Origin;
        var u = local.Dot(plane.UAxis.ToVector());
        var v = local.Dot(plane.VAxis.ToVector());
        hits.Add(new AnalyticRayHit(true, t, point, plane.Normal, bodyId, faceId, SurfaceGeometryKind.Plane, u, v));
    }

    private static void TryIntersectSphere(BodyId bodyId, FaceId faceId, SphereSurface sphere, Ray3D ray, double maxDistance, ToleranceContext tolerance, List<AnalyticRayHit> hits)
    {
        var oc = ray.Origin - sphere.Center;
        var d = ray.Direction.ToVector();
        var b = 2d * oc.Dot(d);
        var c = oc.Dot(oc) - (sphere.Radius * sphere.Radius);
        var discriminant = (b * b) - (4d * c);
        if (discriminant < -tolerance.Linear)
        {
            return;
        }

        var root = double.Sqrt(double.Max(discriminant, 0d));
        var t1 = (-b - root) / 2d;
        var t2 = (-b + root) / 2d;
        foreach (var t in new[] { t1, t2 }
                     .Where(t => t >= -tolerance.Linear && t <= maxDistance + tolerance.Linear)
                     .Select(t => ToleranceMath.ClampToZero(t, tolerance.Linear))
                     .OrderBy(t => t))
        {
            var point = ray.PointAt(t);
            var normal = Direction3D.Create(point - sphere.Center);
            var local = point - sphere.Center;
            var u = double.Atan2(local.Dot(sphere.YAxis.ToVector()), local.Dot(sphere.XAxis.ToVector()));
            var v = double.Asin(local.Dot(sphere.Axis.ToVector()) / sphere.Radius);
            hits.Add(new AnalyticRayHit(true, t, point, normal, bodyId, faceId, SurfaceGeometryKind.Sphere, u, v));
        }
    }

    private static void TryIntersectCylinder(BrepBody body, BodyId bodyId, FaceId faceId, CylinderSurface cylinder, Ray3D ray, double maxDistance, ToleranceContext tolerance, List<AnalyticRayHit> hits)
    {
        var axis = cylinder.Axis.ToVector();
        var delta = ray.Origin - cylinder.Origin;
        var direction = ray.Direction.ToVector();

        var oAxial = delta.Dot(axis);
        var dAxial = direction.Dot(axis);
        var oRadial = delta - (axis * oAxial);
        var dRadial = direction - (axis * dAxial);

        var a = dRadial.Dot(dRadial);
        var b = 2d * oRadial.Dot(dRadial);
        var c = oRadial.Dot(oRadial) - (cylinder.Radius * cylinder.Radius);

        if (ToleranceMath.AlmostZero(a, tolerance))
        {
            return;
        }

        var disc = (b * b) - (4d * a * c);
        if (disc < -tolerance.Linear)
        {
            return;
        }

        var root = double.Sqrt(double.Max(disc, 0d));
        var candidates = new[] { (-b - root) / (2d * a), (-b + root) / (2d * a) }
            .Where(t => t >= -tolerance.Linear && t <= maxDistance + tolerance.Linear)
            .Select(t => ToleranceMath.ClampToZero(t, tolerance.Linear))
            .OrderBy(t => t)
            .ToArray();

        if (candidates.Length == 0)
        {
            return;
        }

        ResolveAxialBounds(body, faceId, cylinder.Axis, cylinder.Origin, out var minV, out var maxV);

        foreach (var t in candidates)
        {
            var point = ray.PointAt(t);
            var axial = (point - cylinder.Origin).Dot(axis);
            if (minV.HasValue && axial < minV.Value - tolerance.Linear)
            {
                continue;
            }

            if (maxV.HasValue && axial > maxV.Value + tolerance.Linear)
            {
                continue;
            }

            var radial = (point - cylinder.Origin) - (axis * axial);
            if (!Direction3D.TryCreate(radial, out var normal))
            {
                continue;
            }

            var u = double.Atan2(radial.Dot(cylinder.YAxis.ToVector()), radial.Dot(cylinder.XAxis.ToVector()));
            hits.Add(new AnalyticRayHit(true, t, point, normal, bodyId, faceId, SurfaceGeometryKind.Cylinder, u, axial));
        }
    }

    private static void TryIntersectCone(BrepBody body, BodyId bodyId, FaceId faceId, ConeSurface cone, Ray3D ray, double maxDistance, ToleranceContext tolerance, List<AnalyticRayHit> hits)
    {
        var axis = cone.Axis.ToVector();
        var tan = double.Tan(cone.SemiAngleRadians);
        var tanSquared = tan * tan;

        var delta = ray.Origin - cone.Apex;
        var direction = ray.Direction.ToVector();

        var dAxial = direction.Dot(axis);
        var oAxial = delta.Dot(axis);
        var dPerp = direction - (axis * dAxial);
        var oPerp = delta - (axis * oAxial);

        var a = dPerp.Dot(dPerp) - (tanSquared * dAxial * dAxial);
        var b = 2d * (oPerp.Dot(dPerp) - (tanSquared * oAxial * dAxial));
        var c = oPerp.Dot(oPerp) - (tanSquared * oAxial * oAxial);

        if (ToleranceMath.AlmostZero(a, tolerance))
        {
            return;
        }

        var disc = (b * b) - (4d * a * c);
        if (disc < -tolerance.Linear)
        {
            return;
        }

        var root = double.Sqrt(double.Max(disc, 0d));
        var candidates = new[] { (-b - root) / (2d * a), (-b + root) / (2d * a) }
            .Where(t => t >= -tolerance.Linear && t <= maxDistance + tolerance.Linear)
            .Select(t => ToleranceMath.ClampToZero(t, tolerance.Linear))
            .OrderBy(t => t)
            .ToArray();

        if (candidates.Length == 0)
        {
            return;
        }

        ResolveAxialBounds(body, faceId, cone.Axis, cone.Apex, out var minV, out var maxV);

        foreach (var t in candidates)
        {
            var point = ray.PointAt(t);
            var v = (point - cone.Apex).Dot(axis);
            if (v < -tolerance.Linear)
            {
                continue;
            }

            if (minV.HasValue && v < minV.Value - tolerance.Linear)
            {
                continue;
            }

            if (maxV.HasValue && v > maxV.Value + tolerance.Linear)
            {
                continue;
            }

            var radial = (point - cone.Apex) - (axis * v);
            var radialNormal = Direction3D.TryCreate(radial, out var radialDirection)
                ? radialDirection.ToVector()
                : Vector3D.Zero;
            var normalVector = radialNormal - (axis * tan);
            if (!Direction3D.TryCreate(normalVector, out var normal))
            {
                continue;
            }

            var u = double.Atan2(radial.Dot(cone.ReferenceAxis.ToVector().Cross(axis)), radial.Dot(cone.ReferenceAxis.ToVector()));
            hits.Add(new AnalyticRayHit(true, t, point, normal, bodyId, faceId, SurfaceGeometryKind.Cone, u, v));
        }
    }

    private static void TryIntersectTorus(BodyId bodyId, FaceId faceId, TorusSurface torus, Ray3D ray, double maxDistance, ToleranceContext tolerance, List<AnalyticRayHit> hits)
    {
        if (!TryIntersectBoundingSphere(ray, torus.Center, torus.MajorRadius + torus.MinorRadius, out var near, out var far, tolerance))
        {
            return;
        }

        near = double.Max(0d, near);
        far = double.Min(maxDistance, far);
        if (far < near)
        {
            return;
        }

        const int samples = 512;
        var dt = (far - near) / samples;
        if (dt <= tolerance.Linear)
        {
            dt = tolerance.Linear;
        }

        double? bestT = null;
        var prevT = near;
        var prevF = TorusImplicit(ray.PointAt(prevT), torus);

        for (var i = 1; i <= samples; i++)
        {
            var currentT = i == samples ? far : near + (i * dt);
            var currentF = TorusImplicit(ray.PointAt(currentT), torus);

            if (double.Abs(currentF) <= 1e-10d)
            {
                bestT = currentT;
                break;
            }

            if ((prevF < 0d && currentF > 0d) || (prevF > 0d && currentF < 0d))
            {
                var root = BisectRoot(prevT, currentT, torus, ray);
                bestT = root;
                break;
            }

            prevT = currentT;
            prevF = currentF;
        }

        if (!bestT.HasValue)
        {
            return;
        }

        var t = ToleranceMath.ClampToZero(bestT.Value, tolerance.Linear);
        var point = ray.PointAt(t);
        var normal = TorusNormal(point, torus);

        var local = point - torus.Center;
        var x = local.Dot(torus.XAxis.ToVector());
        var y = local.Dot(torus.YAxis.ToVector());
        var z = local.Dot(torus.Axis.ToVector());
        var u = double.Atan2(y, x);
        var radial = double.Sqrt((x * x) + (y * y));
        var v = double.Atan2(z, radial - torus.MajorRadius);

        hits.Add(new AnalyticRayHit(true, t, point, normal, bodyId, faceId, SurfaceGeometryKind.Torus, u, v));
    }

    private static bool IsPointOnPlaneFaceDomain(BrepBody body, FaceId faceId, PlaneSurface plane, Point3D point, ToleranceContext tolerance)
    {
        if (!AnalyticPlanarFaceDomain.TryCreate(body, faceId, plane, out var domain))
        {
            return false;
        }

        return domain.Contains(point, tolerance);
    }

    private static void ResolveAxialBounds(BrepBody body, FaceId sideFaceId, Direction3D axis, Point3D axisOrigin, out double? minV, out double? maxV)
    {
        minV = null;
        maxV = null;
        var axisVector = axis.ToVector();

        foreach (var faceBinding in body.Bindings.FaceBindings)
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
            minV = !minV.HasValue ? v : double.Min(minV.Value, v);
            maxV = !maxV.HasValue ? v : double.Max(maxV.Value, v);
        }
    }

    private static bool TryIntersectBoundingSphere(Ray3D ray, Point3D center, double radius, out double near, out double far, ToleranceContext tolerance)
    {
        near = 0d;
        far = 0d;

        var oc = ray.Origin - center;
        var d = ray.Direction.ToVector();
        var b = 2d * oc.Dot(d);
        var c = oc.Dot(oc) - (radius * radius);
        var discriminant = (b * b) - (4d * c);
        if (discriminant < -tolerance.Linear)
        {
            return false;
        }

        var root = double.Sqrt(double.Max(0d, discriminant));
        near = (-b - root) / 2d;
        far = (-b + root) / 2d;
        return far >= -tolerance.Linear;
    }

    private static double TorusImplicit(Point3D p, TorusSurface torus)
    {
        var delta = p - torus.Center;
        var x = delta.Dot(torus.XAxis.ToVector());
        var y = delta.Dot(torus.YAxis.ToVector());
        var z = delta.Dot(torus.Axis.ToVector());
        var sum = (x * x) + (y * y) + (z * z) + (torus.MajorRadius * torus.MajorRadius) - (torus.MinorRadius * torus.MinorRadius);
        return (sum * sum) - (4d * torus.MajorRadius * torus.MajorRadius * ((x * x) + (y * y)));
    }

    private static Direction3D TorusNormal(Point3D point, TorusSurface torus)
    {
        var delta = point - torus.Center;
        var x = delta.Dot(torus.XAxis.ToVector());
        var y = delta.Dot(torus.YAxis.ToVector());
        var z = delta.Dot(torus.Axis.ToVector());
        var common = (x * x) + (y * y) + (z * z) + (torus.MajorRadius * torus.MajorRadius) - (torus.MinorRadius * torus.MinorRadius);
        var nx = 4d * x * (common - (2d * torus.MajorRadius * torus.MajorRadius));
        var ny = 4d * y * (common - (2d * torus.MajorRadius * torus.MajorRadius));
        var nz = 4d * z * common;

        var vector = (torus.XAxis.ToVector() * nx) + (torus.YAxis.ToVector() * ny) + (torus.Axis.ToVector() * nz);
        return Direction3D.Create(vector);
    }

    private static double BisectRoot(double a, double b, TorusSurface torus, Ray3D ray)
    {
        var fa = TorusImplicit(ray.PointAt(a), torus);
        for (var i = 0; i < 60; i++)
        {
            var mid = 0.5d * (a + b);
            var fm = TorusImplicit(ray.PointAt(mid), torus);
            if (double.Abs(fm) < 1e-12d)
            {
                return mid;
            }

            if ((fa < 0d && fm > 0d) || (fa > 0d && fm < 0d))
            {
                b = mid;
            }
            else
            {
                a = mid;
                fa = fm;
            }
        }

        return 0.5d * (a + b);
    }

    private static double PickNearestPositive(double t1, double t2, ToleranceContext tolerance, double maxDistance)
    {
        var candidates = new[] { t1, t2 }
            .Where(t => t >= -tolerance.Linear && t <= maxDistance + tolerance.Linear)
            .Select(t => ToleranceMath.ClampToZero(t, tolerance.Linear))
            .OrderBy(t => t)
            .ToArray();

        return candidates.Length == 0 ? double.NaN : candidates[0];
    }

    private static IReadOnlyList<ShellId> GetOrderedShellIds(BrepBody body)
    {
        if (body.ShellRepresentation is { } representation)
        {
            return representation.OrderedShellIds;
        }

        var topologyBody = body.Topology.Bodies.OrderBy(topologyBody => topologyBody.Id.Value).FirstOrDefault();
        return topologyBody?.ShellIds.OrderBy(shellId => shellId.Value).ToArray() ?? [];
    }

    private static bool IsPointInMaterial(BrepBody body, Point3D point, ToleranceContext tolerance)
    {
        var shellIds = GetOrderedShellIds(body);
        if (shellIds.Count == 0)
        {
            return false;
        }

        var outerShellId = shellIds[0];
        if (!IsPointInShell(body, outerShellId, point, tolerance))
        {
            return false;
        }

        for (var index = 1; index < shellIds.Count; index++)
        {
            if (IsPointInShell(body, shellIds[index], point, tolerance))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPointInShell(BrepBody body, ShellId shellId, Point3D point, ToleranceContext tolerance)
    {
        var shell = body.Topology.GetShell(shellId);
        var probeDirection = Direction3D.Create(new Vector3D(1d, 0.3125d, 0.1875d));
        var probeRay = new Ray3D(point, probeDirection);
        var intersections = new List<double>(shell.FaceIds.Count);

        foreach (var faceId in shell.FaceIds)
        {
            if (TryIntersectFace(body, faceId, probeRay, out var hit, maxDistance: 1e6d, tolerance)
                && hit.Distance > tolerance.Linear * 4d)
            {
                intersections.Add(hit.Distance);
            }
        }

        if (intersections.Count == 0)
        {
            return false;
        }

        intersections.Sort();
        var uniqueCount = 0;
        double? previous = null;
        foreach (var distance in intersections)
        {
            if (!previous.HasValue || double.Abs(distance - previous.Value) > tolerance.Linear * 16d)
            {
                uniqueCount++;
                previous = distance;
            }
        }

        return (uniqueCount & 1) == 1;
    }
}
