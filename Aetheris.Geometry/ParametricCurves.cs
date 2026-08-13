using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Geometry;

/// <summary>An inclusive, finite, non-degenerate authored curve domain.</summary>
public readonly record struct ParameterDomain1
{
    public ParameterDomain1(double minimum, double maximum)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || maximum <= minimum)
            throw new GeometryDefinitionException(GeometryQueryDiagnosticCode.InvalidParameterDomain,
                "A curve parameter domain must be finite and increasing.");
        Minimum = minimum;
        Maximum = maximum;
    }

    public double Minimum { get; }
    public double Maximum { get; }
    public double Length => Maximum - Minimum;
    public bool Contains(double parameter) => double.IsFinite(parameter) && parameter >= Minimum && parameter <= Maximum;
    public double Clamp(double parameter) => double.Clamp(parameter, Minimum, Maximum);
    public double Map(double normalized) => Minimum + double.Clamp(normalized, 0d, 1d) * Length;
}

public sealed record CurveJet1(
    Point3D Point,
    Vector3D Derivative,
    Direction3D? UnitTangent,
    DifferentialSingularityKind Singularity) : IFirstJet3
{
    public bool IsRegular => Singularity == DifferentialSingularityKind.Regular;
}

public sealed record CurvePointExpression(
    SurfaceScalarExpression X,
    SurfaceScalarExpression Y,
    SurfaceScalarExpression Z)
{
    internal void ValidateLengthOutput()
    {
        if (X.Unit != UnitDimension.Length || Y.Unit != UnitDimension.Length || Z.Unit != UnitDimension.Length)
            throw new ArgumentException("Parametric curve point components must all have Length dimension.");
    }
}

/// <summary>One-parameter expression vocabulary backed by the same unit-aware AD tree as patches.</summary>
public static class CurveExpression
{
    public static SurfaceScalarExpression T => SurfaceExpression.U;
    public static SurfaceScalarExpression Number(double value) => SurfaceExpression.Number(value);
    public static SurfaceScalarExpression Length(double millimetres) => SurfaceExpression.Length(millimetres);
    public static SurfaceScalarExpression Add(SurfaceScalarExpression a, SurfaceScalarExpression b) => SurfaceExpression.Add(a, b);
    public static SurfaceScalarExpression Subtract(SurfaceScalarExpression a, SurfaceScalarExpression b) => SurfaceExpression.Subtract(a, b);
    public static SurfaceScalarExpression Multiply(SurfaceScalarExpression a, SurfaceScalarExpression b) => SurfaceExpression.Multiply(a, b);
    public static SurfaceScalarExpression Divide(SurfaceScalarExpression a, SurfaceScalarExpression b) => SurfaceExpression.Divide(a, b);
    public static SurfaceScalarExpression Power(SurfaceScalarExpression a, int exponent) => SurfaceExpression.Power(a, exponent);
    public static SurfaceScalarExpression Sin(SurfaceScalarExpression a) => SurfaceExpression.Sin(a);
    public static SurfaceScalarExpression Cos(SurfaceScalarExpression a) => SurfaceExpression.Cos(a);
}

/// <summary>
/// Authored bounded parametric curve geometry, independent of B-rep ownership and topology.
/// Increasing public parameters always follow the authored direction, even for a reversed native support.
/// </summary>
public sealed class BoundedParametricCurve3
{
    private readonly Func<double, (Point3D Point, Vector3D Derivative)> _evaluator;

    public BoundedParametricCurve3(
        string stableId,
        ParameterDomain1 domain,
        CurvePointExpression pointExpression,
        string provenance,
        string? semanticOwner = null,
        bool isGenerated = false)
    {
        ArgumentNullException.ThrowIfNull(pointExpression);
        pointExpression.ValidateLengthOutput();
        PointExpression = pointExpression;
        _evaluator = t =>
        {
            var x = pointExpression.X.Evaluate(t, 0d);
            var y = pointExpression.Y.Evaluate(t, 0d);
            var z = pointExpression.Z.Evaluate(t, 0d);
            return (new(x.Value, y.Value, z.Value), new(x.Du, y.Du, z.Du));
        };
        Initialize(stableId, domain, provenance, semanticOwner, isGenerated, GeometryRepresentationKind.AnalyticExpression);
    }

    private BoundedParametricCurve3(string stableId, ParameterDomain1 domain,
        Func<double, (Point3D Point, Vector3D Derivative)> evaluator, string provenance,
        string? semanticOwner, bool isGenerated, GeometryRepresentationKind representation,
        bool isPeriodic, string? nativeFamily, int? degree = null)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        IsPeriodic = isPeriodic;
        NativeFamily = nativeFamily;
        Degree = degree;
        Initialize(stableId, domain, provenance, semanticOwner, isGenerated, representation);
    }

    public GeometryIdentity Identity { get; private set; }
    public string StableId => Identity.StableId;
    public ParameterDomain1 Domain { get; private set; }
    public GeometryProvenance Provenance { get; private set; } = null!;
    public GeometryRepresentationKind Representation { get; private set; }
    public CurvePointExpression? PointExpression { get; }
    public bool HasExpressionTree => PointExpression is not null;
    public bool IsPeriodic { get; }
    public string? NativeFamily { get; }
    public int? Degree { get; }

    public Point3D Evaluate(double parameter) => EvaluateJet1(parameter).Point;

    public CurveJet1 EvaluateJet1(double parameter)
    {
        if (!Domain.Contains(parameter))
            throw new ArgumentOutOfRangeException(nameof(parameter), "Parameter must be finite and lie inside the authored curve domain.");
        Point3D point;
        Vector3D derivative;
        try { (point, derivative) = _evaluator(parameter); }
        catch (ArithmeticException) { throw; }
        if (!Finite(point) || !Finite(derivative))
            throw new ArithmeticException("Parametric curve evaluation produced a non-finite value.");
        if (!derivative.TryNormalize(out var normalized))
            return new(point, derivative, null, DifferentialSingularityKind.Singular);
        return new(point, derivative, Direction3D.Create(normalized), DifferentialSingularityKind.Regular);
    }

    public static BoundedParametricCurve3 Procedural(string stableId, ParameterDomain1 domain,
        Func<double, (Point3D Point, Vector3D Derivative)> evaluator, string provenance,
        string? semanticOwner = null, bool isGenerated = false,
        GeometryRepresentationKind representation = GeometryRepresentationKind.ProceduralParametric) =>
        new(stableId, domain, evaluator, provenance, semanticOwner, isGenerated, representation, false, null);

    public static BoundedParametricCurve3 FromCurveGeometry(string stableId, CurveGeometry curve,
        double parameterStart, double parameterEnd, string provenance, string? semanticOwner = null,
        bool isGenerated = false, GeometryRepresentationKind representation = GeometryRepresentationKind.ProceduralParametric)
    {
        ArgumentNullException.ThrowIfNull(curve);
        if (!double.IsFinite(parameterStart) || !double.IsFinite(parameterEnd) || parameterStart == parameterEnd)
            throw new GeometryDefinitionException(GeometryQueryDiagnosticCode.InvalidParameterDomain, "A bounded native curve requires two distinct finite trim parameters.");
        var minimum = double.Min(parameterStart, parameterEnd);
        var maximum = double.Max(parameterStart, parameterEnd);
        var forward = parameterEnd > parameterStart;
        var domain = new ParameterDomain1(minimum, maximum);
        double Native(double publicParameter) => forward ? publicParameter : minimum + maximum - publicParameter;
        var sign = forward ? 1d : -1d;
        return curve.Kind switch
        {
            CurveGeometryKind.Line3 => NativeCurve("Line3", false, t =>
                (curve.Line3!.Value.Evaluate(Native(t)), curve.Line3.Value.Direction.ToVector() * sign)),
            CurveGeometryKind.Circle3 => NativeCurve("Circle3", IsFullPeriod(minimum, maximum), t =>
                (curve.Circle3!.Value.Evaluate(Native(t)), curve.Circle3.Value.Tangent(Native(t)) * sign)),
            CurveGeometryKind.Ellipse3 => NativeCurve("Ellipse3", IsFullPeriod(minimum, maximum), t =>
            {
                var e = curve.Ellipse3!.Value; var n = Native(t);
                var derivative = e.XAxis.ToVector() * (-e.MajorRadius * double.Sin(n)) + e.YAxis.ToVector() * (e.MinorRadius * double.Cos(n));
                return (e.Evaluate(n), derivative * sign);
            }),
            CurveGeometryKind.Hyperbola3 => NativeCurve("Hyperbola3", false, t =>
                (curve.Hyperbola3!.Value.Evaluate(Native(t)), curve.Hyperbola3.Value.FirstDerivative(Native(t)) * sign)),
            CurveGeometryKind.BSpline3 => NativeCurve("BSpline3", curve.BSpline3!.Value.ClosedCurve, t =>
                (curve.BSpline3.Value.Evaluate(Native(t)), curve.BSpline3.Value.EvaluateTangent(Native(t)) * sign)),
            _ => throw new NotSupportedException($"Curve family '{curve.Kind}' has no bounded public adapter.")
        };

        BoundedParametricCurve3 NativeCurve(string family, bool periodic,
            Func<double, (Point3D Point, Vector3D Derivative)> evaluator) =>
            new(stableId, domain, evaluator, provenance, semanticOwner, isGenerated,
                representation, periodic, family, family == "BSpline3" ? curve.BSpline3!.Value.Degree : null);
    }

    public static BoundedParametricCurve3 LineSegment(string stableId, Point3D start, Point3D end, string provenance, string? semanticOwner = null, bool isGenerated = false)
    {
        var delta = end - start;
        if (!delta.TryNormalize(out var direction)) throw new ArgumentException("Line segment endpoints must be distinct.", nameof(end));
        return FromCurveGeometry(stableId, CurveGeometry.FromLine(new(start, Direction3D.Create(direction))), 0d, delta.Length, provenance, semanticOwner, isGenerated);
    }

    private void Initialize(string stableId, ParameterDomain1 domain, string provenance, string? semanticOwner,
        bool isGenerated, GeometryRepresentationKind representation)
    {
        Identity = new(stableId);
        Domain = domain;
        Provenance = new(provenance, semanticOwner, isGenerated);
        Representation = representation;
    }

    private static bool IsFullPeriod(double minimum, double maximum) => double.Abs((maximum - minimum) - 2d * double.Pi) <= 1e-12;
    private static bool Finite(Point3D p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
    private static bool Finite(Vector3D v) => double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z);
}
