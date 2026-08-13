using Aetheris.Kernel.Core.Math;

namespace Aetheris.Geometry;

public readonly record struct ParameterInterval2
{
    public ParameterInterval2(double minimum, double maximum)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || maximum <= minimum)
            throw new GeometryDefinitionException(GeometryQueryDiagnosticCode.InvalidParameterDomain, "A parameter interval must be finite and increasing.");
        Minimum = minimum;
        Maximum = maximum;
    }

    public double Minimum { get; }
    public double Maximum { get; }
    public double Map(double normalized) => Minimum + double.Clamp(normalized, 0d, 1d) * (Maximum - Minimum);
}

public sealed class GeometryDefinitionException : ArgumentException
{
    public GeometryDefinitionException(GeometryQueryDiagnosticCode code, string message) : base(message) => Code = code;
    public GeometryQueryDiagnosticCode Code { get; }
}

public sealed record ParametricDomain(ParameterInterval2 U, ParameterInterval2 V);

public readonly record struct GeometryIdentity
{
    public GeometryIdentity(string stableId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        StableId = stableId;
    }
    public string StableId { get; }
    public override string ToString() => StableId;
}

public sealed record GeometryProvenance
{
    public GeometryProvenance(string source, string? semanticOwner = null, bool isGenerated = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        Source = source;
        SemanticOwner = semanticOwner;
        IsGenerated = isGenerated;
    }
    public string Source { get; }
    public string? SemanticOwner { get; }
    public bool IsGenerated { get; }
}

public enum DifferentialSingularityKind { Regular, Singular, NonFinite, Undefined }

public interface IFirstJet3
{
    Point3D Point { get; }
    DifferentialSingularityKind Singularity { get; }
}

public enum GeometryRepresentationKind
{
    AnalyticExpression,
    ProceduralParametric,
    CertifiedApproximation,
    SampledApproximation,
    MaterializedBRep,
    ImportedGeometry
}

public readonly record struct UnitDimension(int LengthPower)
{
    public static UnitDimension Dimensionless => new(0);
    public static UnitDimension Length => new(1);
    public static UnitDimension operator *(UnitDimension a, UnitDimension b) => new(a.LengthPower + b.LengthPower);
    public static UnitDimension operator /(UnitDimension a, UnitDimension b) => new(a.LengthPower - b.LengthPower);
}

public readonly record struct ScalarJet(double Value, double Du, double Dv);

/// <summary>A deliberately small, unit-aware expression tree with forward automatic differentiation.</summary>
public abstract record SurfaceScalarExpression(UnitDimension Unit)
{
    public abstract ScalarJet Evaluate(double u, double v);

    public sealed record Constant(double ConstantValue, UnitDimension ConstantUnit) : SurfaceScalarExpression(ConstantUnit)
    { public override ScalarJet Evaluate(double u, double v) => new(ConstantValue, 0d, 0d); }
    public sealed record Parameter(bool IsU) : SurfaceScalarExpression(UnitDimension.Dimensionless)
    { public override ScalarJet Evaluate(double u, double v) => IsU ? new(u, 1d, 0d) : new(v, 0d, 1d); }
    public sealed record Sum(SurfaceScalarExpression Left, SurfaceScalarExpression Right) : SurfaceScalarExpression(RequireSame(Left, Right))
    { public override ScalarJet Evaluate(double u, double v) { var a = Left.Evaluate(u, v); var b = Right.Evaluate(u, v); return new(a.Value + b.Value, a.Du + b.Du, a.Dv + b.Dv); } }
    public sealed record Difference(SurfaceScalarExpression Left, SurfaceScalarExpression Right) : SurfaceScalarExpression(RequireSame(Left, Right))
    { public override ScalarJet Evaluate(double u, double v) { var a = Left.Evaluate(u, v); var b = Right.Evaluate(u, v); return new(a.Value - b.Value, a.Du - b.Du, a.Dv - b.Dv); } }
    public sealed record Product(SurfaceScalarExpression Left, SurfaceScalarExpression Right) : SurfaceScalarExpression(Left.Unit * Right.Unit)
    { public override ScalarJet Evaluate(double u, double v) { var a = Left.Evaluate(u, v); var b = Right.Evaluate(u, v); return new(a.Value * b.Value, a.Du * b.Value + a.Value * b.Du, a.Dv * b.Value + a.Value * b.Dv); } }
    public sealed record Quotient(SurfaceScalarExpression Left, SurfaceScalarExpression Right) : SurfaceScalarExpression(Left.Unit / Right.Unit)
    { public override ScalarJet Evaluate(double u, double v) { var a = Left.Evaluate(u, v); var b = Right.Evaluate(u, v); if (double.Abs(b.Value) <= 1e-15) throw new DivideByZeroException("Parametric expression divisor is zero."); var q = b.Value * b.Value; return new(a.Value / b.Value, (a.Du * b.Value - a.Value * b.Du) / q, (a.Dv * b.Value - a.Value * b.Dv) / q); } }
    public sealed record IntegerPower(SurfaceScalarExpression Operand, int Exponent) : SurfaceScalarExpression(new UnitDimension(Operand.Unit.LengthPower * Exponent))
    { public override ScalarJet Evaluate(double u, double v) { var a = Operand.Evaluate(u, v); var value = double.Pow(a.Value, Exponent); var scale = Exponent == 0 ? 0d : Exponent * double.Pow(a.Value, Exponent - 1); return new(value, scale * a.Du, scale * a.Dv); } }
    public sealed record Sine(SurfaceScalarExpression Operand) : SurfaceScalarExpression(RequireDimensionless(Operand))
    { public override ScalarJet Evaluate(double u, double v) { var a = Operand.Evaluate(u, v); var c = double.Cos(a.Value); return new(double.Sin(a.Value), c * a.Du, c * a.Dv); } }
    public sealed record Cosine(SurfaceScalarExpression Operand) : SurfaceScalarExpression(RequireDimensionless(Operand))
    { public override ScalarJet Evaluate(double u, double v) { var a = Operand.Evaluate(u, v); var s = -double.Sin(a.Value); return new(double.Cos(a.Value), s * a.Du, s * a.Dv); } }

    private static UnitDimension RequireSame(SurfaceScalarExpression a, SurfaceScalarExpression b) =>
        a.Unit == b.Unit ? a.Unit : throw new ArgumentException($"Cannot add/subtract dimensions L^{a.Unit.LengthPower} and L^{b.Unit.LengthPower}.");
    private static UnitDimension RequireDimensionless(SurfaceScalarExpression expression) =>
        expression.Unit == UnitDimension.Dimensionless ? UnitDimension.Dimensionless : throw new ArgumentException("Trigonometric operands must be dimensionless.");
}

public static class SurfaceExpression
{
    public static SurfaceScalarExpression U { get; } = new SurfaceScalarExpression.Parameter(true);
    public static SurfaceScalarExpression V { get; } = new SurfaceScalarExpression.Parameter(false);
    public static SurfaceScalarExpression Number(double value) => new SurfaceScalarExpression.Constant(value, UnitDimension.Dimensionless);
    public static SurfaceScalarExpression Length(double millimetres) => new SurfaceScalarExpression.Constant(millimetres, UnitDimension.Length);
    public static SurfaceScalarExpression Add(SurfaceScalarExpression a, SurfaceScalarExpression b) => new SurfaceScalarExpression.Sum(a, b);
    public static SurfaceScalarExpression Subtract(SurfaceScalarExpression a, SurfaceScalarExpression b) => new SurfaceScalarExpression.Difference(a, b);
    public static SurfaceScalarExpression Multiply(SurfaceScalarExpression a, SurfaceScalarExpression b) => new SurfaceScalarExpression.Product(a, b);
    public static SurfaceScalarExpression Divide(SurfaceScalarExpression a, SurfaceScalarExpression b) => new SurfaceScalarExpression.Quotient(a, b);
    public static SurfaceScalarExpression Power(SurfaceScalarExpression a, int exponent) => new SurfaceScalarExpression.IntegerPower(a, exponent);
    public static SurfaceScalarExpression Sin(SurfaceScalarExpression a) => new SurfaceScalarExpression.Sine(a);
    public static SurfaceScalarExpression Cos(SurfaceScalarExpression a) => new SurfaceScalarExpression.Cosine(a);
}

public sealed record SurfacePointExpression(SurfaceScalarExpression X, SurfaceScalarExpression Y, SurfaceScalarExpression Z)
{
    public void ValidateLengthOutput()
    {
        if (X.Unit != UnitDimension.Length || Y.Unit != UnitDimension.Length || Z.Unit != UnitDimension.Length)
            throw new ArgumentException("Parametric surface point components must all have Length dimension.");
    }
}

public sealed record SurfaceDifferential(Point3D Point, Vector3D Du, Vector3D Dv, Direction3D? Normal, bool IsSingular) : IFirstJet3
{
    public DifferentialSingularityKind Singularity => IsSingular ? DifferentialSingularityKind.Singular : DifferentialSingularityKind.Regular;
}

/// <summary>Authored rectangular parametric geometry, independent of CAD realization and topology.</summary>
public sealed class BoundedParametricPatch3
{
    private readonly Func<double, double, SurfaceDifferential>? _proceduralEvaluator;

    public BoundedParametricPatch3(string stableId, ParametricDomain domain, SurfacePointExpression pointExpression,
        string provenance, GeometryRepresentationKind representation = GeometryRepresentationKind.AnalyticExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(pointExpression);
        pointExpression.ValidateLengthOutput();
        StableId = stableId; Domain = domain; PointExpression = pointExpression; Provenance = provenance; Representation = representation;
        Identity = new(stableId); GeometryProvenance = new(provenance);
    }

    private BoundedParametricPatch3(string stableId, ParametricDomain domain, Func<double, double, SurfaceDifferential> evaluator,
        string provenance, GeometryRepresentationKind representation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        StableId = stableId; Domain = domain; Provenance = provenance; Representation = representation; _proceduralEvaluator = evaluator;
        Identity = new(stableId); GeometryProvenance = new(provenance);
    }

    public string StableId { get; }
    public GeometryIdentity Identity { get; }
    public ParametricDomain Domain { get; }
    public SurfacePointExpression? PointExpression { get; }
    public string Provenance { get; }
    public GeometryProvenance GeometryProvenance { get; }
    public GeometryRepresentationKind Representation { get; }
    public bool HasExpressionTree => PointExpression is not null;

    public static BoundedParametricPatch3 Procedural(string stableId, ParametricDomain domain,
        Func<double, double, SurfaceDifferential> evaluator, string provenance) =>
        new(stableId, domain, evaluator ?? throw new ArgumentNullException(nameof(evaluator)), provenance, GeometryRepresentationKind.ProceduralParametric);

    public SurfaceDifferential Evaluate(double u, double v)
    {
        if (!double.IsFinite(u) || !double.IsFinite(v)) throw new ArgumentOutOfRangeException(nameof(u), "Parameters must be finite.");
        if (u < Domain.U.Minimum || u > Domain.U.Maximum || v < Domain.V.Minimum || v > Domain.V.Maximum)
            throw new ArgumentOutOfRangeException(nameof(u), "Parameters must lie inside the authored rectangular domain.");
        if (_proceduralEvaluator is not null) return _proceduralEvaluator(u, v);
        var expression = PointExpression!;
        var x = expression.X.Evaluate(u, v); var y = expression.Y.Evaluate(u, v); var z = expression.Z.Evaluate(u, v);
        if (!new[] { x.Value, x.Du, x.Dv, y.Value, y.Du, y.Dv, z.Value, z.Du, z.Dv }.All(double.IsFinite))
            throw new ArithmeticException("Parametric patch evaluation produced a non-finite value.");
        var du = new Vector3D(x.Du, y.Du, z.Du); var dv = new Vector3D(x.Dv, y.Dv, z.Dv); var cross = du.Cross(dv);
        var singular = !cross.TryNormalize(out var normalized);
        return new(new(x.Value, y.Value, z.Value), du, dv, singular ? null : Direction3D.Create(normalized), singular);
    }

    public Point3D EvaluatePoint(double u, double v) => Evaluate(u, v).Point;
    public SurfaceDifferential EvaluateJet1(double u, double v) => Evaluate(u, v);
}
