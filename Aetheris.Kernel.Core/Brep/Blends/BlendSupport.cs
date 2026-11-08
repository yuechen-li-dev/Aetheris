using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Blends;

/// <summary>
/// An analytic face support as a distance function: exact distance to the plane, cylinder, sphere and
/// torus, and to the cone's generators. A blend's anchors must have one: the rolling-ball certificate
/// measures how far a ball centre is from them. Freeform supports have none (no projection yet).
/// </summary>
public sealed class BlendSupport
{
    private readonly SurfaceGeometry _surface;

    private BlendSupport(FaceId face, SurfaceGeometry surface) { Face = face; _surface = surface; }

    public FaceId Face { get; }

    public SurfaceGeometryKind Kind => _surface.Kind;

    /// <summary>The support's radius of curvature where it is constant (cylinder, sphere, torus tube); a ball of
    /// that radius on that side has a collapsed offset and does not roll on the face.</summary>
    public double? ConstantRadius => _surface.Kind switch
    {
        SurfaceGeometryKind.Cylinder => _surface.Cylinder!.Value.Radius,
        SurfaceGeometryKind.Sphere => _surface.Sphere!.Value.Radius,
        SurfaceGeometryKind.Torus => _surface.Torus!.Value.MinorRadius,
        _ => null,
    };

    /// <summary>Distance from the point to the support's core: a cylinder's axis, a sphere's centre, a torus's tube
    /// circle; null for supports without one. A ball whose centre sits on the core has a collapsed offset.</summary>
    public double? CoreDistance(Point3D x)
    {
        switch (_surface.Kind)
        {
            case SurfaceGeometryKind.Cylinder:
                var c = _surface.Cylinder!.Value;
                return Radial(x - c.Origin, c.Axis.ToVector()).Length;
            case SurfaceGeometryKind.Sphere:
                return (x - _surface.Sphere!.Value.Center).Length;
            case SurfaceGeometryKind.Torus:
                var t = _surface.Torus!.Value; var axis = t.Axis.ToVector();
                var radial = Radial(x - t.Center, axis);
                var tube = radial.Length > 1e-12 ? t.Center + radial * (t.MajorRadius / radial.Length) : t.Center;
                return (x - tube).Length;
            default:
                return null;
        }
    }

    public static BlendSupport? Of(BrepBody body, FaceId face) =>
        body.TryGetFaceSurfaceGeometry(face, out var surface) && surface is { Kind: SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder
            or SurfaceGeometryKind.Cone or SurfaceGeometryKind.Sphere or SurfaceGeometryKind.Torus }
            ? new BlendSupport(face, surface) : null;

    /// <summary>Unsigned distance from the point to the support surface.</summary>
    public double Distance(Point3D x)
    {
        switch (_surface.Kind)
        {
            case SurfaceGeometryKind.Plane:
            {
                var p = _surface.Plane!.Value;
                return double.Abs(p.Normal.ToVector().Dot(x - p.Origin));
            }
            case SurfaceGeometryKind.Sphere:
            {
                var s = _surface.Sphere!.Value;
                return double.Abs((x - s.Center).Length - s.Radius);
            }
            case SurfaceGeometryKind.Cylinder:
            {
                var c = _surface.Cylinder!.Value;
                return double.Abs(Radial(x - c.Origin, c.Axis.ToVector()).Length - c.Radius);
            }
            case SurfaceGeometryKind.Cone:
            {
                var k = _surface.Cone!.Value;
                var v = x - k.Apex; var axis = k.Axis.ToVector();
                var h = v.Dot(axis); var rho = Radial(v, axis).Length;
                return double.Abs(rho * double.Cos(k.SemiAngleRadians) - double.Abs(h) * double.Sin(k.SemiAngleRadians));
            }
            default:
            {
                var t = _surface.Torus!.Value;
                var axis = t.Axis.ToVector();
                var radial = Radial(x - t.Center, axis);
                var tube = radial.Length > 1e-12 ? t.Center + radial * (t.MajorRadius / radial.Length) : t.Center;
                return double.Abs((x - tube).Length - t.MinorRadius);
            }
        }
    }

    /// <summary>Unit normal of the support at (or nearest) the point, unsigned: only its line matters here.</summary>
    public Vector3D Normal(Point3D x)
    {
        Vector3D n = _surface.Kind switch
        {
            SurfaceGeometryKind.Plane => _surface.Plane!.Value.Normal.ToVector(),
            SurfaceGeometryKind.Sphere => x - _surface.Sphere!.Value.Center,
            SurfaceGeometryKind.Cylinder => Radial(x - _surface.Cylinder!.Value.Origin, _surface.Cylinder!.Value.Axis.ToVector()),
            SurfaceGeometryKind.Cone => ConeNormal(x),
            _ => TorusNormal(x),
        };
        return n.TryNormalize(out var unit) ? unit : Vector3D.Zero;
    }

    private Vector3D ConeNormal(Point3D x)
    {
        var k = _surface.Cone!.Value; var axis = k.Axis.ToVector();
        var v = x - k.Apex; var h = v.Dot(axis);
        var radial = Radial(v, axis);
        if (!radial.TryNormalize(out var unit)) return axis;
        return unit * double.Cos(k.SemiAngleRadians) - axis * (double.Sin(k.SemiAngleRadians) * double.Sign(h));
    }

    private Vector3D TorusNormal(Point3D x)
    {
        var t = _surface.Torus!.Value; var axis = t.Axis.ToVector();
        var radial = Radial(x - t.Center, axis);
        var tube = radial.Length > 1e-12 ? t.Center + radial * (t.MajorRadius / radial.Length) : t.Center;
        return x - tube;
    }

    private static Vector3D Radial(Vector3D v, Vector3D axis) => v - axis * v.Dot(axis);
}
