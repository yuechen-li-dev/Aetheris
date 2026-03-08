using Aetheris.Kernel.Core.Geometry.Curves;

namespace Aetheris.Kernel.Core.Geometry;

public enum CurveGeometryKind
{
    Line3,
    Circle3,
    BSpline3,
    Unsupported,
}

/// <summary>
/// Minimal discriminated wrapper for supported curve primitives.
/// </summary>
public sealed record CurveGeometry
{
    private CurveGeometry(CurveGeometryKind kind, Line3Curve? line3, Circle3Curve? circle3, BSpline3Curve? bSpline3, string? unsupportedKind)
    {
        Kind = kind;
        Line3 = line3;
        Circle3 = circle3;
        BSpline3 = bSpline3;
        UnsupportedKind = unsupportedKind;
    }

    public CurveGeometryKind Kind { get; }

    public Line3Curve? Line3 { get; }

    public Circle3Curve? Circle3 { get; }

    public BSpline3Curve? BSpline3 { get; }

    public string? UnsupportedKind { get; }

    public static CurveGeometry FromLine(Line3Curve line) => new(CurveGeometryKind.Line3, line, null, null, null);

    public static CurveGeometry FromCircle(Circle3Curve circle) => new(CurveGeometryKind.Circle3, null, circle, null, null);

    public static CurveGeometry FromBSpline(BSpline3Curve curve) => new(CurveGeometryKind.BSpline3, null, null, curve, null);

    public static CurveGeometry FromUnsupported(string kindName) => new(CurveGeometryKind.Unsupported, null, null, null, kindName);
}
