namespace Aetheris.Kernel.Core.Geometry;

/// <summary>
/// Stable in-memory geometry ID for a curve definition. The default value (0) is invalid.
/// </summary>
public readonly record struct CurveGeometryId(int Value)
{
    public static CurveGeometryId Invalid => default;

    public bool IsValid => Value > 0;
}

/// <summary>
/// Stable in-memory geometry ID for a surface definition. The default value (0) is invalid.
/// </summary>
public readonly record struct SurfaceGeometryId(int Value)
{
    public static SurfaceGeometryId Invalid => default;

    public bool IsValid => Value > 0;
}
