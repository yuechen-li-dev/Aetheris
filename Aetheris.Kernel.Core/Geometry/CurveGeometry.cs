using Aetheris.Kernel.Core.Geometry.Curves;

namespace Aetheris.Kernel.Core.Geometry;

public enum CurveGeometryKind
{
    Line3,
    Circle3,
    Unsupported,
}

/// <summary>
/// Minimal discriminated wrapper for supported curve primitives.
/// </summary>
public sealed record CurveGeometry
{
    private CurveGeometry(CurveGeometryKind kind, Line3Curve? line3, Circle3Curve? circle3, string? unsupportedKind)
    {
        Kind = kind;
        Line3 = line3;
        Circle3 = circle3;
        UnsupportedKind = unsupportedKind;
    }

    public CurveGeometryKind Kind { get; }

    public Line3Curve? Line3 { get; }

    public Circle3Curve? Circle3 { get; }

    public string? UnsupportedKind { get; }

    public static CurveGeometry FromLine(Line3Curve line) => new(CurveGeometryKind.Line3, line, null, null);

    public static CurveGeometry FromCircle(Circle3Curve circle) => new(CurveGeometryKind.Circle3, null, circle, null);

    public static CurveGeometry FromUnsupported(string kindName) => new(CurveGeometryKind.Unsupported, null, null, kindName);
}
