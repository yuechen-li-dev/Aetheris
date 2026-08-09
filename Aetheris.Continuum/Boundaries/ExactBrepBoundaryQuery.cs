using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Continuum.Boundaries;

public readonly record struct BoundarySurfaceParameters(double U, double V)
{
    public BoundarySurfaceParameters UnwrappedNear(BoundarySurfaceParameters reference) =>
        new(ExactBrepBoundaryQuery.UnwrapPeriodic(U, reference.U), ExactBrepBoundaryQuery.UnwrapPeriodic(V, reference.V));
}

public readonly record struct PrincipalCurvatureData(
    Vector3D DirectionU,
    Vector3D DirectionV,
    double CurvatureU,
    double CurvatureV);

/// <summary>
/// Minimal exact-face bridge for analytic Continuum boundary supports. It deliberately admits only
/// surface families with a complete exact query implementation; it never falls back to tessellation.
/// </summary>
public sealed class ExactBrepBoundaryQuery
{
    private readonly SphereSurface? _sphere;
    private readonly TorusSurface? _torus;
    private readonly bool _sameSense;
    private readonly Transform3D _transform;
    private readonly Transform3D _inverse;

    public ExactBrepBoundaryQuery(BrepBody body, FaceId faceId, Transform3D transform)
    {
        Body = body ?? throw new ArgumentNullException(nameof(body));
        FaceId = faceId;
        if (!body.TryGetFaceSurfaceGeometry(faceId, out var geometry) || geometry is null)
            throw new ArgumentException($"BRep face {faceId.Value} has no exact surface binding.", nameof(faceId));
        if (geometry.Kind == SurfaceGeometryKind.Sphere && geometry.Sphere is { } sphere) _sphere = sphere;
        else if (geometry.Kind == SurfaceGeometryKind.Torus && geometry.Torus is { } torus) _torus = torus;
        else throw new NotSupportedException($"Exact Continuum boundary queries support Sphere and Torus; face {faceId.Value} is {geometry.Kind}.");
        if (!body.Bindings.TryGetFaceBinding(faceId, out var binding))
            throw new ArgumentException($"BRep face {faceId.Value} has no geometry binding.", nameof(faceId));

        _sameSense = binding.SameSense;
        _transform = transform;
        _inverse = transform.Inverse();
    }

    public BrepBody Body { get; }
    public FaceId FaceId { get; }
    public string SupportKind => _torus.HasValue ? nameof(SurfaceGeometryKind.Torus) : nameof(SurfaceGeometryKind.Sphere);
    public double Radius => _sphere?.Radius ?? throw new InvalidOperationException("The exact support is not a sphere.");
    public double MajorRadius => _torus?.MajorRadius ?? throw new InvalidOperationException("The exact support is not a torus.");
    public double MinorRadius => _torus?.MinorRadius ?? throw new InvalidOperationException("The exact support is not a torus.");
    public Point3D Center => _transform.Apply(_sphere?.Center ?? _torus!.Value.Center);
    public Vector3D Axis => TransformDirection(_sphere?.Axis ?? _torus!.Value.Axis);
    public Vector3D XAxis => TransformDirection(_sphere?.XAxis ?? _torus!.Value.XAxis);
    public Vector3D YAxis => TransformDirection(_sphere?.YAxis ?? _torus!.Value.YAxis);

    public Point3D Evaluate(double u, double v) => _transform.Apply(
        _sphere is { } sphere ? sphere.Evaluate(u, v) : _torus!.Value.Evaluate(u, v));

    public Vector3D ExactSupportNormal(double u, double v)
    {
        var source = _sphere is { } sphere ? sphere.Normal(u, v) : _torus!.Value.Normal(u, v);
        return TransformDirection(source);
    }

    /// <summary>Support normal adjusted only for BRep parameterization orientation; this is not a material-side answer.</summary>
    public Vector3D ParameterizationNormal(double u,double v) => _sameSense?ExactSupportNormal(u,v):-ExactSupportNormal(u,v);

    public BoundarySurfaceParameters RecoverParameters(Point3D point)
    {
        var localPoint = _inverse.Apply(point);
        if (_sphere is { } sphere)
        {
            var local = localPoint - sphere.Center;
            var x = local.Dot(sphere.XAxis.ToVector());
            var y = local.Dot(sphere.YAxis.ToVector());
            var z = local.Dot(sphere.Axis.ToVector());
            return new(double.Atan2(y, x), double.Atan2(z, double.Sqrt((x * x) + (y * y))));
        }

        var torus = _torus!.Value;
        var delta = localPoint - torus.Center;
        var axial = delta.Dot(torus.Axis.ToVector());
        var xValue = delta.Dot(torus.XAxis.ToVector());
        var yValue = delta.Dot(torus.YAxis.ToVector());
        var radial = double.Sqrt((xValue * xValue) + (yValue * yValue));
        return new(NormalizePeriodic(double.Atan2(yValue, xValue)),
            NormalizePeriodic(double.Atan2(axial, radial - torus.MajorRadius)));
    }

    public Point3D Project(Point3D point)
    {
        if (_sphere is { } sphere)
        {
            var local = _inverse.Apply(point);
            var radial = local - sphere.Center;
            if (!radial.TryNormalize(out var direction)) direction = sphere.XAxis.ToVector();
            return _transform.Apply(sphere.Center + (direction * sphere.Radius));
        }
        var parameters = RecoverParameters(point);
        return Evaluate(parameters.U, parameters.V);
    }

    public Vector3D SupportNormalAt(Point3D boundaryPoint)
    {
        var parameters = RecoverParameters(boundaryPoint);
        return ExactSupportNormal(parameters.U, parameters.V);
    }

    public PrincipalCurvatureData PrincipalCurvatures(double u, double v)
    {
        if (_sphere is { } sphere)
        {
            var sphereDirectionU = TransformDirection(Direction3D.Create((-sphere.XAxis.ToVector() * double.Sin(u)) + (sphere.YAxis.ToVector() * double.Cos(u))));
            var faceNormal = ParameterizationNormal(u, v);
            var sphereDirectionV = faceNormal.Cross(sphereDirectionU);
            sphereDirectionV.TryNormalize(out sphereDirectionV);
            var sphereSign = _sameSense ? 1d : -1d;
            return new(sphereDirectionU, sphereDirectionV, sphereSign / sphere.Radius, sphereSign / sphere.Radius);
        }

        var torus = _torus!.Value;
        var radial = (torus.XAxis.ToVector() * double.Cos(u)) + (torus.YAxis.ToVector() * double.Sin(u));
        var azimuth = (-torus.XAxis.ToVector() * double.Sin(u)) + (torus.YAxis.ToVector() * double.Cos(u));
        var minor = (-radial * double.Sin(v)) + (torus.Axis.ToVector() * double.Cos(v));
        var directionU = TransformDirection(Direction3D.Create(azimuth));
        var directionV = TransformDirection(Direction3D.Create(minor));
        if (!_sameSense) directionV = -directionV;
        var sign = _sameSense ? 1d : -1d;
        return new(directionU, directionV,
            sign * double.Cos(v) / (torus.MajorRadius + (torus.MinorRadius * double.Cos(v))),
            sign / torus.MinorRadius);
    }

    public BoundaryLocalFrame CreateMaterialSideFrame(Point3D nearPoint, bool materialInside)
    {
        var origin = Project(nearPoint);
        var parameters = RecoverParameters(origin);
        var supportNormal = ExactSupportNormal(parameters.U, parameters.V);
        var normal = materialInside ? -supportNormal : supportNormal;
        if (_sphere is { } sphere)
        {
            var seeds = new[] { TransformDirection(sphere.XAxis), TransformDirection(sphere.YAxis), TransformDirection(sphere.Axis) };
            var seed = seeds.OrderBy(value => double.Abs(value.Dot(normal))).First();
            var projected = seed - (normal * seed.Dot(normal));
            if (!projected.TryNormalize(out var sphereTangentU)) throw new InvalidOperationException("Could not construct sphere tangent frame.");
            var sphereTangentV = normal.Cross(sphereTangentU);
            sphereTangentV.TryNormalize(out sphereTangentV);
            return new(origin, normal, sphereTangentU, sphereTangentV);
        }
        var principal = PrincipalCurvatures(parameters.U, parameters.V);
        var tangentU = principal.DirectionU;
        var tangentV = normal.Cross(tangentU);
        if (!tangentV.TryNormalize(out tangentV)) throw new InvalidOperationException("Could not construct principal boundary frame.");
        return new(origin, normal, tangentU, tangentV);
    }

    public static double NormalizePeriodic(double angle)
    {
        var period = 2d * double.Pi;
        angle %= period;
        return angle < 0d ? angle + period : angle;
    }

    public static double UnwrapPeriodic(double angle, double reference)
    {
        var period = 2d * double.Pi;
        return angle + (period * double.Round((reference - angle) / period, MidpointRounding.ToEven));
    }

    private Vector3D TransformDirection(Direction3D direction)
    {
        var value = _transform.Apply(direction).ToVector();
        if (!value.TryNormalize(out value)) throw new InvalidOperationException("Rigid support transform produced a degenerate direction.");
        return value;
    }
}
