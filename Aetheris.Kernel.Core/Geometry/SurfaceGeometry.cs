using Aetheris.Kernel.Core.Geometry.Surfaces;

namespace Aetheris.Kernel.Core.Geometry;

public enum SurfaceGeometryKind
{
    Plane,
    Cylinder,
    Cone,
    Sphere,
    Torus,
    BSplineSurfaceWithKnots,
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
        SphereSurface? sphere,
        TorusSurface? torus,
        BSplineSurfaceWithKnots? bSplineSurfaceWithKnots)
    {
        Kind = kind;
        Plane = plane;
        Cylinder = cylinder;
        Cone = cone;
        Sphere = sphere;
        Torus = torus;
        BSplineSurfaceWithKnots = bSplineSurfaceWithKnots;
    }

    public SurfaceGeometryKind Kind { get; }

    public PlaneSurface? Plane { get; }

    public CylinderSurface? Cylinder { get; }

    public ConeSurface? Cone { get; }

    public SphereSurface? Sphere { get; }

    public TorusSurface? Torus { get; }

    public BSplineSurfaceWithKnots? BSplineSurfaceWithKnots { get; }

    public static SurfaceGeometry FromPlane(PlaneSurface plane) => new(SurfaceGeometryKind.Plane, plane, null, null, null, null, null);

    public static SurfaceGeometry FromCylinder(CylinderSurface cylinder) => new(SurfaceGeometryKind.Cylinder, null, cylinder, null, null, null, null);

    public static SurfaceGeometry FromCone(ConeSurface cone) => new(SurfaceGeometryKind.Cone, null, null, cone, null, null, null);

    public static SurfaceGeometry FromSphere(SphereSurface sphere) => new(SurfaceGeometryKind.Sphere, null, null, null, sphere, null, null);

    public static SurfaceGeometry FromTorus(TorusSurface torus) => new(SurfaceGeometryKind.Torus, null, null, null, null, torus, null);

    public static SurfaceGeometry FromBSplineSurfaceWithKnots(BSplineSurfaceWithKnots surface) => new(SurfaceGeometryKind.BSplineSurfaceWithKnots, null, null, null, null, null, surface);
}
