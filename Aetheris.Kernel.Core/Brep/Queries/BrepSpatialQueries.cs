using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Queries;

/// <summary>
/// Spatial-query v1 support focused on primitive outputs produced by <see cref="BrepPrimitives"/>.
/// Unsupported body layouts return explicit diagnostics (and <see cref="PointContainment.Unknown"/> for classification).
/// </summary>
public static class BrepSpatialQueries
{
    public static KernelResult<IReadOnlyList<RayHit>> Raycast(
        BrepBody body,
        Ray3D ray,
        RayQueryOptions? options = null,
        ToleranceContext? tolerance = null)
    {
        var context = tolerance ?? ToleranceContext.Default;
        var queryOptions = options ?? new RayQueryOptions();

        if (!TryResolvePrimitive(body, out var primitive, out var diagnostic))
        {
            return KernelResult<IReadOnlyList<RayHit>>.Failure([diagnostic]);
        }

        var hits = primitive.Kind switch
        {
            PrimitiveKind.Box => IntersectBox(primitive, ray, queryOptions, context),
            PrimitiveKind.Cylinder => IntersectCylinder(primitive, ray, queryOptions, context),
            PrimitiveKind.Sphere => IntersectSphere(primitive, ray, queryOptions, context),
            _ => []
        };

        return KernelResult<IReadOnlyList<RayHit>>.Success(hits);
    }

    /// <summary>
    /// Returns Unknown for unsupported body layouts in this v1 query layer.
    /// Near-boundary checks are tolerance-aware and prefer Boundary over Inside/Outside.
    /// </summary>
    public static KernelResult<PointContainment> ClassifyPoint(
        BrepBody body,
        Point3D point,
        ToleranceContext? tolerance = null)
    {
        var context = tolerance ?? ToleranceContext.Default;

        if (!TryResolvePrimitive(body, out var primitive, out var diagnostic))
        {
            return KernelResult<PointContainment>.Success(PointContainment.Unknown, [diagnostic with { Severity = KernelDiagnosticSeverity.Warning }]);
        }

        var containment = primitive.Kind switch
        {
            PrimitiveKind.Box => ClassifyBoxPoint(primitive, point, context),
            PrimitiveKind.Cylinder => ClassifyCylinderPoint(primitive, point, context),
            PrimitiveKind.Sphere => ClassifySpherePoint(primitive, point, context),
            _ => PointContainment.Unknown
        };

        return KernelResult<PointContainment>.Success(containment);
    }

    private static IReadOnlyList<RayHit> IntersectBox(PrimitiveDescriptor primitive, Ray3D ray, RayQueryOptions options, ToleranceContext tolerance)
    {
        var min = primitive.BoxMin;
        var max = primitive.BoxMax;
        var origin = ray.Origin;
        var direction = ray.Direction.ToVector();

        var tMin = double.NegativeInfinity;
        var tMax = double.PositiveInfinity;
        FaceId? entryFace = null;
        FaceId? exitFace = null;

        if (!IntersectSlab(origin.X, direction.X, min.X, max.X, primitive.BoxXMinFace, primitive.BoxXMaxFace, tolerance, ref tMin, ref tMax, ref entryFace, ref exitFace)
            || !IntersectSlab(origin.Y, direction.Y, min.Y, max.Y, primitive.BoxYMinFace, primitive.BoxYMaxFace, tolerance, ref tMin, ref tMax, ref entryFace, ref exitFace)
            || !IntersectSlab(origin.Z, direction.Z, min.Z, max.Z, primitive.BoxZMinFace, primitive.BoxZMaxFace, tolerance, ref tMin, ref tMax, ref entryFace, ref exitFace))
        {
            return [];
        }

        var values = new List<(double T, FaceId? FaceId)>(2);

        if (tMax >= -tolerance.Linear)
        {
            if (tMin >= -tolerance.Linear)
            {
                values.Add((ToleranceMath.ClampToZero(tMin, tolerance.Linear), entryFace));
            }

            if (tMax >= -tolerance.Linear && !ToleranceMath.AlmostEqual(tMin, tMax, tolerance.Linear))
            {
                values.Add((ToleranceMath.ClampToZero(tMax, tolerance.Linear), exitFace));
            }
            else if (values.Count == 0)
            {
                values.Add((ToleranceMath.ClampToZero(tMax, tolerance.Linear), exitFace));
            }
        }

        return BuildHits(values, ray, options, tolerance, primitive.FaceNormals);
    }

    private static bool IntersectSlab(
        double origin,
        double direction,
        double min,
        double max,
        FaceId minFace,
        FaceId maxFace,
        ToleranceContext tolerance,
        ref double tMin,
        ref double tMax,
        ref FaceId? entryFace,
        ref FaceId? exitFace)
    {
        if (ToleranceMath.AlmostZero(direction, tolerance))
        {
            return ToleranceMath.GreaterThanOrAlmostEqual(origin, min, tolerance) && ToleranceMath.LessThanOrAlmostEqual(origin, max, tolerance);
        }

        var t1 = (min - origin) / direction;
        var t2 = (max - origin) / direction;
        var nearT = t1;
        var farT = t2;
        var nearFace = minFace;
        var farFace = maxFace;

        if (t1 > t2)
        {
            nearT = t2;
            farT = t1;
            nearFace = maxFace;
            farFace = minFace;
        }

        if (nearT > tMin)
        {
            tMin = nearT;
            entryFace = nearFace;
        }

        if (farT < tMax)
        {
            tMax = farT;
            exitFace = farFace;
        }

        return tMin <= tMax + tolerance.Linear;
    }

    private static IReadOnlyList<RayHit> IntersectSphere(PrimitiveDescriptor primitive, Ray3D ray, RayQueryOptions options, ToleranceContext tolerance)
    {
        var center = primitive.SphereCenter;
        var radius = primitive.SphereRadius;

        var oc = ray.Origin - center;
        var d = ray.Direction.ToVector();
        var b = 2d * oc.Dot(d);
        var c = oc.Dot(oc) - (radius * radius);
        var discriminant = (b * b) - (4d * c);

        if (discriminant < -tolerance.Linear)
        {
            return [];
        }

        var values = new List<(double T, FaceId? FaceId)>(2);

        if (ToleranceMath.AlmostZero(discriminant, tolerance))
        {
            values.Add((ToleranceMath.ClampToZero(-b / 2d, tolerance.Linear), primitive.SphereFace));
        }
        else
        {
            var root = double.Sqrt(double.Max(discriminant, 0d));
            var t1 = (-b - root) / 2d;
            var t2 = (-b + root) / 2d;
            values.Add((t1, primitive.SphereFace));
            values.Add((t2, primitive.SphereFace));
        }

        return BuildHits(values, ray, options, tolerance, _ => null, (point, _) =>
        {
            var normalVector = point - center;
            return Direction3D.Create(normalVector);
        });
    }

    private static IReadOnlyList<RayHit> IntersectCylinder(PrimitiveDescriptor primitive, Ray3D ray, RayQueryOptions options, ToleranceContext tolerance)
    {
        var axis = primitive.CylinderAxis;
        var axisVector = axis.ToVector();
        var origin = primitive.CylinderOrigin;
        var radius = primitive.CylinderRadius;
        var minV = primitive.CylinderMinV;
        var maxV = primitive.CylinderMaxV;

        var delta = ray.Origin - origin;
        var direction = ray.Direction.ToVector();

        var oAxial = delta.Dot(axisVector);
        var dAxial = direction.Dot(axisVector);

        var oRadial = delta - (axisVector * oAxial);
        var dRadial = direction - (axisVector * dAxial);

        var candidates = new List<(double T, FaceId? FaceId, Direction3D? Normal)>();

        var a = dRadial.Dot(dRadial);
        var b = 2d * oRadial.Dot(dRadial);
        var c = oRadial.Dot(oRadial) - (radius * radius);

        if (!ToleranceMath.AlmostZero(a, tolerance))
        {
            var discriminant = (b * b) - (4d * a * c);
            if (discriminant >= -tolerance.Linear)
            {
                if (ToleranceMath.AlmostZero(discriminant, tolerance))
                {
                    AddCylinderSideHit(-b / (2d * a));
                }
                else
                {
                    var root = double.Sqrt(double.Max(discriminant, 0d));
                    AddCylinderSideHit((-b - root) / (2d * a));
                    AddCylinderSideHit((-b + root) / (2d * a));
                }
            }
        }

        AddCapHit(minV, primitive.CylinderMinCapFace, -axisVector);
        AddCapHit(maxV, primitive.CylinderMaxCapFace, axisVector);

        var ordered = candidates
            .OrderBy(cn => cn.T)
            .ToList();

        var deduped = new List<(double T, FaceId? FaceId, Direction3D? Normal)>();
        foreach (var candidate in ordered)
        {
            if (deduped.Count > 0 && ToleranceMath.AlmostEqual(candidate.T, deduped[^1].T, tolerance))
            {
                continue;
            }

            deduped.Add(candidate);
        }

        return BuildHits(
            deduped.Select(d => (d.T, d.FaceId)).ToList(),
            ray,
            options,
            tolerance,
            _ => null,
            (_, index) => deduped[index].Normal);

        void AddCylinderSideHit(double t)
        {
            var v = oAxial + (dAxial * t);
            if (!ToleranceMath.GreaterThanOrAlmostEqual(v, minV, tolerance)
                || !ToleranceMath.LessThanOrAlmostEqual(v, maxV, tolerance))
            {
                return;
            }

            var point = ray.PointAt(t);
            var pointDelta = point - origin;
            var pointAxial = pointDelta.Dot(axisVector);
            var radial = pointDelta - (axisVector * pointAxial);

            if (!Direction3D.TryCreate(radial, out var normal))
            {
                return;
            }

            candidates.Add((t, primitive.CylinderSideFace, normal));
        }

        void AddCapHit(double capV, FaceId faceId, Vector3D normalVector)
        {
            if (ToleranceMath.AlmostZero(dAxial, tolerance))
            {
                return;
            }

            var t = (capV - oAxial) / dAxial;
            var point = ray.PointAt(t);
            var pointDelta = point - origin;
            var pointAxial = pointDelta.Dot(axisVector);
            var radial = pointDelta - (axisVector * pointAxial);
            var radialSquared = radial.Dot(radial);

            if (radialSquared <= (radius + tolerance.Linear) * (radius + tolerance.Linear))
            {
                candidates.Add((t, faceId, Direction3D.Create(normalVector)));
            }
        }
    }

    private static IReadOnlyList<RayHit> BuildHits(
        IReadOnlyList<(double T, FaceId? FaceId)> values,
        Ray3D ray,
        RayQueryOptions options,
        ToleranceContext tolerance,
        Func<FaceId, Direction3D?> normalByFace,
        Func<Point3D, int, Direction3D?>? normalByPoint = null)
    {
        var hits = new List<RayHit>(values.Count);

        for (var i = 0; i < values.Count; i++)
        {
            var (t, faceId) = values[i];
            if (t < -tolerance.Linear)
            {
                continue;
            }

            var clampedT = ToleranceMath.ClampToZero(t, tolerance.Linear);
            if (options.MaxDistance.HasValue && clampedT > options.MaxDistance.Value + tolerance.Linear)
            {
                continue;
            }

            var point = ray.PointAt(clampedT);
            var normal = normalByPoint?.Invoke(point, i)
                ?? (faceId.HasValue ? normalByFace(faceId.Value) : null);

            if (!options.IncludeBackfaces && normal.HasValue)
            {
                var facing = normal.Value.ToVector().Dot(ray.Direction.ToVector());
                if (facing > tolerance.Linear)
                {
                    continue;
                }
            }

            hits.Add(new RayHit(clampedT, point, normal, faceId));
        }

        return hits
            .OrderBy(hit => hit.T)
            .ToList();
    }

    private static PointContainment ClassifyBoxPoint(PrimitiveDescriptor primitive, Point3D point, ToleranceContext tolerance)
    {
        var min = primitive.BoxMin;
        var max = primitive.BoxMax;

        var outside = point.X < min.X - tolerance.Linear
            || point.X > max.X + tolerance.Linear
            || point.Y < min.Y - tolerance.Linear
            || point.Y > max.Y + tolerance.Linear
            || point.Z < min.Z - tolerance.Linear
            || point.Z > max.Z + tolerance.Linear;

        if (outside)
        {
            return PointContainment.Outside;
        }

        var boundary = ToleranceMath.AlmostEqual(point.X, min.X, tolerance)
            || ToleranceMath.AlmostEqual(point.X, max.X, tolerance)
            || ToleranceMath.AlmostEqual(point.Y, min.Y, tolerance)
            || ToleranceMath.AlmostEqual(point.Y, max.Y, tolerance)
            || ToleranceMath.AlmostEqual(point.Z, min.Z, tolerance)
            || ToleranceMath.AlmostEqual(point.Z, max.Z, tolerance);

        return boundary ? PointContainment.Boundary : PointContainment.Inside;
    }

    private static PointContainment ClassifySpherePoint(PrimitiveDescriptor primitive, Point3D point, ToleranceContext tolerance)
    {
        var distance = (point - primitive.SphereCenter).Length;
        if (ToleranceMath.AlmostEqual(distance, primitive.SphereRadius, tolerance))
        {
            return PointContainment.Boundary;
        }

        return distance < primitive.SphereRadius ? PointContainment.Inside : PointContainment.Outside;
    }

    private static PointContainment ClassifyCylinderPoint(PrimitiveDescriptor primitive, Point3D point, ToleranceContext tolerance)
    {
        var axis = primitive.CylinderAxis.ToVector();
        var delta = point - primitive.CylinderOrigin;
        var v = delta.Dot(axis);
        var radialVector = delta - (axis * v);
        var radialDistance = radialVector.Length;

        var outside = v < primitive.CylinderMinV - tolerance.Linear
            || v > primitive.CylinderMaxV + tolerance.Linear
            || radialDistance > primitive.CylinderRadius + tolerance.Linear;

        if (outside)
        {
            return PointContainment.Outside;
        }

        var onSide = ToleranceMath.AlmostEqual(radialDistance, primitive.CylinderRadius, tolerance)
            && ToleranceMath.GreaterThanOrAlmostEqual(v, primitive.CylinderMinV, tolerance)
            && ToleranceMath.LessThanOrAlmostEqual(v, primitive.CylinderMaxV, tolerance);

        var onCap = (ToleranceMath.AlmostEqual(v, primitive.CylinderMinV, tolerance)
                || ToleranceMath.AlmostEqual(v, primitive.CylinderMaxV, tolerance))
            && radialDistance <= primitive.CylinderRadius + tolerance.Linear;

        if (onSide || onCap)
        {
            return PointContainment.Boundary;
        }

        return PointContainment.Inside;
    }

    private static bool TryResolvePrimitive(BrepBody body, out PrimitiveDescriptor descriptor, out KernelDiagnostic diagnostic)
    {
        descriptor = default;
        var bindings = body.Bindings.FaceBindings.ToArray();

        if (bindings.Length == 1)
        {
            var surface = body.Geometry.GetSurface(bindings[0].SurfaceGeometryId);
            if (surface.Kind == SurfaceGeometryKind.Sphere && surface.Sphere is SphereSurface sphere)
            {
                descriptor = PrimitiveDescriptor.ForSphere(bindings[0].FaceId, sphere.Center, sphere.Radius);
                diagnostic = default;
                return true;
            }
        }

        if (bindings.Length == 3)
        {
            var surfaces = bindings
                .Select(binding => (binding.FaceId, Surface: body.Geometry.GetSurface(binding.SurfaceGeometryId)))
                .ToArray();

            var cylinderFace = surfaces.FirstOrDefault(s => s.Surface.Kind == SurfaceGeometryKind.Cylinder);
            var planes = surfaces.Where(s => s.Surface.Kind == SurfaceGeometryKind.Plane).ToArray();

            if (cylinderFace.Surface?.Cylinder is CylinderSurface cylinder && planes.Length == 2)
            {
                var axis = cylinder.Axis.ToVector();
                var minPlane = planes[0];
                var maxPlane = planes[1];

                var minV = (minPlane.Surface.Plane!.Value.Origin - cylinder.Origin).Dot(axis);
                var maxV = (maxPlane.Surface.Plane!.Value.Origin - cylinder.Origin).Dot(axis);
                var minFace = minPlane.FaceId;
                var maxFace = maxPlane.FaceId;

                if (minV > maxV)
                {
                    (minV, maxV) = (maxV, minV);
                    (minFace, maxFace) = (maxFace, minFace);
                }

                descriptor = PrimitiveDescriptor.ForCylinder(
                    cylinderFace.FaceId,
                    minFace,
                    maxFace,
                    cylinder.Origin,
                    cylinder.Axis,
                    cylinder.Radius,
                    minV,
                    maxV);
                diagnostic = default;
                return true;
            }
        }

        if (bindings.Length == 6)
        {
            var planes = bindings
                .Select(binding => (binding.FaceId, Surface: body.Geometry.GetSurface(binding.SurfaceGeometryId)))
                .Where(s => s.Surface.Kind == SurfaceGeometryKind.Plane)
                .ToArray();

            if (planes.Length == 6)
            {
                FaceId xMinFace = default;
                FaceId xMaxFace = default;
                FaceId yMinFace = default;
                FaceId yMaxFace = default;
                FaceId zMinFace = default;
                FaceId zMaxFace = default;
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

                foreach (var plane in planes)
                {
                    var normal = plane.Surface.Plane!.Value.Normal;
                    if (ToleranceMath.AlmostEqual(normal.X, -1d, ToleranceContext.Default) && ToleranceMath.AlmostZero(normal.Y, ToleranceContext.Default) && ToleranceMath.AlmostZero(normal.Z, ToleranceContext.Default))
                    {
                        minX = plane.Surface.Plane!.Value.Origin.X;
                        xMinFace = plane.FaceId;
                        hasXMin = true;
                    }
                    else if (ToleranceMath.AlmostEqual(normal.X, 1d, ToleranceContext.Default) && ToleranceMath.AlmostZero(normal.Y, ToleranceContext.Default) && ToleranceMath.AlmostZero(normal.Z, ToleranceContext.Default))
                    {
                        maxX = plane.Surface.Plane!.Value.Origin.X;
                        xMaxFace = plane.FaceId;
                        hasXMax = true;
                    }
                    else if (ToleranceMath.AlmostEqual(normal.Y, -1d, ToleranceContext.Default) && ToleranceMath.AlmostZero(normal.X, ToleranceContext.Default) && ToleranceMath.AlmostZero(normal.Z, ToleranceContext.Default))
                    {
                        minY = plane.Surface.Plane!.Value.Origin.Y;
                        yMinFace = plane.FaceId;
                        hasYMin = true;
                    }
                    else if (ToleranceMath.AlmostEqual(normal.Y, 1d, ToleranceContext.Default) && ToleranceMath.AlmostZero(normal.X, ToleranceContext.Default) && ToleranceMath.AlmostZero(normal.Z, ToleranceContext.Default))
                    {
                        maxY = plane.Surface.Plane!.Value.Origin.Y;
                        yMaxFace = plane.FaceId;
                        hasYMax = true;
                    }
                    else if (ToleranceMath.AlmostEqual(normal.Z, -1d, ToleranceContext.Default) && ToleranceMath.AlmostZero(normal.X, ToleranceContext.Default) && ToleranceMath.AlmostZero(normal.Y, ToleranceContext.Default))
                    {
                        minZ = plane.Surface.Plane!.Value.Origin.Z;
                        zMinFace = plane.FaceId;
                        hasZMin = true;
                    }
                    else if (ToleranceMath.AlmostEqual(normal.Z, 1d, ToleranceContext.Default) && ToleranceMath.AlmostZero(normal.X, ToleranceContext.Default) && ToleranceMath.AlmostZero(normal.Y, ToleranceContext.Default))
                    {
                        maxZ = plane.Surface.Plane!.Value.Origin.Z;
                        zMaxFace = plane.FaceId;
                        hasZMax = true;
                    }
                }

                if (hasXMin && hasXMax && hasYMin && hasYMax && hasZMin && hasZMax)
                {
                    descriptor = PrimitiveDescriptor.ForBox(
                        new Point3D(minX, minY, minZ),
                        new Point3D(maxX, maxY, maxZ),
                        xMinFace,
                        xMaxFace,
                        yMinFace,
                        yMaxFace,
                        zMinFace,
                        zMaxFace);
                    diagnostic = default;
                    return true;
                }
            }
        }

        diagnostic = new KernelDiagnostic(
            KernelDiagnosticCode.NotImplemented,
            KernelDiagnosticSeverity.Error,
            "Spatial query v1 only supports primitive Brep bodies from BrepPrimitives.CreateBox/CreateCylinder/CreateSphere.",
            Source: nameof(BrepSpatialQueries));
        return false;
    }

    private enum PrimitiveKind
    {
        Box,
        Cylinder,
        Sphere,
    }

    private readonly record struct PrimitiveDescriptor
    {
        public PrimitiveKind Kind { get; init; }

        public Point3D BoxMin { get; init; }

        public Point3D BoxMax { get; init; }

        public FaceId BoxXMinFace { get; init; }

        public FaceId BoxXMaxFace { get; init; }

        public FaceId BoxYMinFace { get; init; }

        public FaceId BoxYMaxFace { get; init; }

        public FaceId BoxZMinFace { get; init; }

        public FaceId BoxZMaxFace { get; init; }

        public Point3D SphereCenter { get; init; }

        public double SphereRadius { get; init; }

        public FaceId SphereFace { get; init; }

        public Point3D CylinderOrigin { get; init; }

        public Direction3D CylinderAxis { get; init; }

        public double CylinderRadius { get; init; }

        public double CylinderMinV { get; init; }

        public double CylinderMaxV { get; init; }

        public FaceId CylinderSideFace { get; init; }

        public FaceId CylinderMinCapFace { get; init; }

        public FaceId CylinderMaxCapFace { get; init; }

        public Direction3D? FaceNormals(FaceId faceId)
        {
            if (Kind != PrimitiveKind.Box)
            {
                return null;
            }

            if (faceId == BoxXMinFace)
            {
                return Direction3D.Create(new Vector3D(-1d, 0d, 0d));
            }

            if (faceId == BoxXMaxFace)
            {
                return Direction3D.Create(new Vector3D(1d, 0d, 0d));
            }

            if (faceId == BoxYMinFace)
            {
                return Direction3D.Create(new Vector3D(0d, -1d, 0d));
            }

            if (faceId == BoxYMaxFace)
            {
                return Direction3D.Create(new Vector3D(0d, 1d, 0d));
            }

            if (faceId == BoxZMinFace)
            {
                return Direction3D.Create(new Vector3D(0d, 0d, -1d));
            }

            if (faceId == BoxZMaxFace)
            {
                return Direction3D.Create(new Vector3D(0d, 0d, 1d));
            }

            return null;
        }

        public static PrimitiveDescriptor ForSphere(FaceId sphereFace, Point3D center, double radius)
        {
            return new PrimitiveDescriptor
            {
                Kind = PrimitiveKind.Sphere,
                SphereFace = sphereFace,
                SphereCenter = center,
                SphereRadius = radius,
            };
        }

        public static PrimitiveDescriptor ForCylinder(
            FaceId sideFace,
            FaceId minCapFace,
            FaceId maxCapFace,
            Point3D origin,
            Direction3D axis,
            double radius,
            double minV,
            double maxV)
        {
            return new PrimitiveDescriptor
            {
                Kind = PrimitiveKind.Cylinder,
                CylinderSideFace = sideFace,
                CylinderMinCapFace = minCapFace,
                CylinderMaxCapFace = maxCapFace,
                CylinderOrigin = origin,
                CylinderAxis = axis,
                CylinderRadius = radius,
                CylinderMinV = minV,
                CylinderMaxV = maxV,
            };
        }

        public static PrimitiveDescriptor ForBox(
            Point3D min,
            Point3D max,
            FaceId xMinFace,
            FaceId xMaxFace,
            FaceId yMinFace,
            FaceId yMaxFace,
            FaceId zMinFace,
            FaceId zMaxFace)
        {
            return new PrimitiveDescriptor
            {
                Kind = PrimitiveKind.Box,
                BoxMin = min,
                BoxMax = max,
                BoxXMinFace = xMinFace,
                BoxXMaxFace = xMaxFace,
                BoxYMinFace = yMinFace,
                BoxYMaxFace = yMaxFace,
                BoxZMinFace = zMinFace,
                BoxZMaxFace = zMaxFace,
            };
        }
    }
}
