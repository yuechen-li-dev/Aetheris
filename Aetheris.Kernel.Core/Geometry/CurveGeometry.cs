using Aetheris.Kernel.Core.Geometry.Curves;

namespace Aetheris.Kernel.Core.Geometry;

public enum CurveGeometryKind
{
    Line3,
    Circle3,
    BSpline3,
    Ellipse3,
    Hyperbola3,
    Unsupported,
}

/// <summary>
/// Minimal discriminated wrapper for supported curve primitives.
/// </summary>
public sealed record CurveGeometry
{
    private CurveGeometry(CurveGeometryKind kind, Line3Curve? line3, Circle3Curve? circle3, BSpline3Curve? bSpline3, Ellipse3Curve? ellipse3, Hyperbola3Curve? hyperbola3, string? unsupportedKind, QualifiedCylinderIntersectionRepresentation? certifiedIntersection = null)
    {
        Kind = kind;
        Line3 = line3;
        Circle3 = circle3;
        BSpline3 = bSpline3;
        Ellipse3 = ellipse3;
        Hyperbola3 = hyperbola3;
        UnsupportedKind = unsupportedKind;
        CertifiedIntersection = certifiedIntersection;
    }

    public CurveGeometryKind Kind { get; }

    public Line3Curve? Line3 { get; }

    public Circle3Curve? Circle3 { get; }

    public BSpline3Curve? BSpline3 { get; }

    public Ellipse3Curve? Ellipse3 { get; }
    public Hyperbola3Curve? Hyperbola3 { get; }

    public string? UnsupportedKind { get; }

    /// <summary>Present only when import has promoted a measured generic recovery to geometric authority.</summary>
    public SplineRecoveryProvenance? RecoveryProvenance { get; private init; }

    /// <summary>
    /// Exact parent-cylinder intersection and certified realization when this B-spline
    /// is only a finite representation of an analytic surface-intersection edge.
    /// </summary>
    public QualifiedCylinderIntersectionRepresentation? CertifiedIntersection { get; }

    public static CurveGeometry FromLine(Line3Curve line) => new(CurveGeometryKind.Line3, line, null, null, null, null, null);

    public static CurveGeometry FromCircle(Circle3Curve circle) => new(CurveGeometryKind.Circle3, null, circle, null, null, null, null);

    public static CurveGeometry FromBSpline(BSpline3Curve curve) => new(CurveGeometryKind.BSpline3, null, null, curve, null, null, null);

    public static CurveGeometry FromRecoveredBSpline(BSpline3Curve curve, SplineRecoveryProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        return FromBSpline(curve) with { RecoveryProvenance = provenance };
    }

    public static CurveGeometry FromCertifiedIntersection(QualifiedCylinderIntersectionRepresentation representation)
    {
        ArgumentNullException.ThrowIfNull(representation);
        return new(CurveGeometryKind.BSpline3, null, null, representation.Curve, null, null, null, representation);
    }

    public static CurveGeometry FromEllipse(Ellipse3Curve ellipse) => new(CurveGeometryKind.Ellipse3, null, null, null, ellipse, null, null);
    public static CurveGeometry FromHyperbola(Hyperbola3Curve hyperbola) => new(CurveGeometryKind.Hyperbola3, null, null, null, null, hyperbola, null);

    public static CurveGeometry FromUnsupported(string kindName) => new(CurveGeometryKind.Unsupported, null, null, null, null, null, kindName);
}
