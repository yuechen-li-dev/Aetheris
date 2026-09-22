namespace Aetheris.Kernel.Core.Geometry;

/// <summary>
/// Parameter-space periodicity of a support surface. Null means that the axis
/// is not known to be periodic; periods are expressed in native UV units.
/// </summary>
public readonly record struct SurfacePeriodicity(double? UPeriod, double? VPeriod)
{
    public bool IsPeriodic => UPeriod.HasValue || VPeriod.HasValue;

    public static SurfacePeriodicity Of(SurfaceGeometry surface) => surface.Kind switch
    {
        SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Cone or SurfaceGeometryKind.Sphere
            => new(double.Tau, null),
        SurfaceGeometryKind.Torus => new(double.Tau, double.Tau),
        SurfaceGeometryKind.BSplineSurfaceWithKnots => new(
            surface.BSplineSurfaceWithKnots!.UClosed
                ? surface.BSplineSurfaceWithKnots.DomainEndU - surface.BSplineSurfaceWithKnots.DomainStartU
                : null,
            surface.BSplineSurfaceWithKnots.VClosed
                ? surface.BSplineSurfaceWithKnots.DomainEndV - surface.BSplineSurfaceWithKnots.DomainStartV
                : null),
        _ => default,
    };

    public static bool EquivalentModuloPeriod(double left, double right, double period, double tolerance)
    {
        if (!double.IsFinite(left) || !double.IsFinite(right) || !double.IsFinite(period) || period <= 0d)
            return false;
        var delta = double.Abs(left - right);
        var remainder = delta % period;
        return remainder <= tolerance || period - remainder <= tolerance;
    }
}
