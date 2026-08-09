using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Continuum.Boundaries;

public readonly record struct BoundarySurfaceParameters(double U, double V);

/// <summary>
/// Minimal exact-face bridge for analytic BRep supports. M2 deliberately admits spheres only;
/// unsupported families fail explicitly instead of falling back to tessellation.
/// </summary>
public sealed class ExactBrepBoundaryQuery
{
    private readonly SphereSurface _sphere;
    private readonly bool _sameSense;
    private readonly Transform3D _transform;
    private readonly Transform3D _inverse;

    public ExactBrepBoundaryQuery(BrepBody body, FaceId faceId, Transform3D transform)
    {
        Body = body ?? throw new ArgumentNullException(nameof(body));
        FaceId = faceId;
        if (!body.TryGetFaceSurfaceGeometry(faceId, out var geometry) || geometry is null)
            throw new ArgumentException($"BRep face {faceId.Value} has no exact surface binding.", nameof(faceId));
        if (geometry.Kind != SurfaceGeometryKind.Sphere || geometry.Sphere is not { } sphere)
            throw new NotSupportedException($"M2 exact boundary queries support Sphere; face {faceId.Value} is {geometry.Kind}.");
        if (!body.Bindings.TryGetFaceBinding(faceId, out var binding))
            throw new ArgumentException($"BRep face {faceId.Value} has no geometry binding.", nameof(faceId));

        _sphere = sphere;
        _sameSense = binding.SameSense;
        _transform = transform;
        _inverse = transform.Inverse();
    }

    public BrepBody Body { get; }
    public FaceId FaceId { get; }
    public string SupportKind => nameof(SurfaceGeometryKind.Sphere);
    public double Radius => _sphere.Radius;
    public Point3D Center => _transform.Apply(_sphere.Center);

    public Point3D Evaluate(double u, double v) => _transform.Apply(_sphere.Evaluate(u, v));

    public Vector3D ExactFaceNormal(double u, double v)
    {
        var normal = _transform.Apply(_sphere.Normal(u, v)).ToVector();
        normal.TryNormalize(out normal);
        return _sameSense ? normal : -normal;
    }

    public BoundarySurfaceParameters RecoverParameters(Point3D point)
    {
        var local = _inverse.Apply(point) - _sphere.Center;
        var x = local.Dot(_sphere.XAxis.ToVector());
        var y = local.Dot(_sphere.YAxis.ToVector());
        var z = local.Dot(_sphere.Axis.ToVector());
        return new BoundarySurfaceParameters(double.Atan2(y, x), double.Atan2(z, double.Sqrt((x * x) + (y * y))));
    }

    public Point3D Project(Point3D point)
    {
        var local = _inverse.Apply(point);
        var radial = local - _sphere.Center;
        if (!radial.TryNormalize(out var direction)) direction = _sphere.XAxis.ToVector();
        return _transform.Apply(_sphere.Center + (direction * _sphere.Radius));
    }

    public Vector3D OutwardNormal(Point3D boundaryPoint)
    {
        var radial = boundaryPoint - Center;
        if (!radial.TryNormalize(out var normal)) throw new InvalidOperationException("Sphere normal is undefined at its center.");
        return _sameSense ? normal : -normal;
    }

    public BoundaryLocalFrame CreateMaterialSideFrame(Point3D nearPoint, bool materialInside)
    {
        var origin = Project(nearPoint);
        var outward = OutwardNormal(origin);
        var normal = materialInside ? -outward : outward;

        // Exact parameter axes seed a deterministic seam-stable tangent. Pick the least parallel seed.
        var seeds = new[]
        {
            _transform.Apply(_sphere.XAxis).ToVector(),
            _transform.Apply(_sphere.YAxis).ToVector(),
            _transform.Apply(_sphere.Axis).ToVector(),
        };
        var seed = seeds.OrderBy(value => double.Abs(value.Dot(normal))).First();
        var projected = seed - (normal * seed.Dot(normal));
        if (!projected.TryNormalize(out var tangentU)) throw new InvalidOperationException("Could not construct sphere tangent frame.");
        var tangentV = normal.Cross(tangentU);
        tangentV.TryNormalize(out tangentV);
        return new BoundaryLocalFrame(origin, normal, tangentU, tangentV);
    }
}
