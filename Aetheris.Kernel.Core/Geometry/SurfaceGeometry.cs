using Aetheris.Kernel.Core.Geometry.Surfaces;

namespace Aetheris.Kernel.Core.Geometry;

public enum SurfaceGeometryKind
{
    Plane,
    Cylinder,
    Cone,
    Sphere,
}

/// <summary>
/// Minimal discriminated wrapper for supported surface primitives.
/// </summary>
public sealed record SurfaceGeometry
{
    private SurfaceGeometry(
        SurfaceGeometryKind kind,
        PlaneSurface? plane,
        CylinderSurface? cylinder,
        ConeSurface? cone,
        SphereSurface? sphere)
    {
        Kind = kind;
        Plane = plane;
        Cylinder = cylinder;
        Cone = cone;
        Sphere = sphere;
    }

    public SurfaceGeometryKind Kind { get; }

    public PlaneSurface? Plane { get; }

    public CylinderSurface? Cylinder { get; }

    public ConeSurface? Cone { get; }

    public SphereSurface? Sphere { get; }

    public static SurfaceGeometry FromPlane(PlaneSurface plane) => new(SurfaceGeometryKind.Plane, plane, null, null, null);

    public static SurfaceGeometry FromCylinder(CylinderSurface cylinder) => new(SurfaceGeometryKind.Cylinder, null, cylinder, null, null);

    public static SurfaceGeometry FromCone(ConeSurface cone) => new(SurfaceGeometryKind.Cone, null, null, cone, null);

    public static SurfaceGeometry FromSphere(SphereSurface sphere) => new(SurfaceGeometryKind.Sphere, null, null, null, sphere);
}
