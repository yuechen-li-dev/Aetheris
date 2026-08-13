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

public interface ISecondJet3 : IFirstJet3 { }

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

public readonly record struct ScalarJet(double Value, double Du, double Dv, double Duu, double Duv, double Dvv);

/// <summary>A deliberately small, unit-aware expression tree with forward automatic differentiation.</summary>
public abstract record SurfaceScalarExpression(UnitDimension Unit)
{
    public abstract ScalarJet Evaluate(double u, double v);

    public sealed record Constant(double ConstantValue, UnitDimension ConstantUnit) : SurfaceScalarExpression(ConstantUnit)
    { public override ScalarJet Evaluate(double u, double v) => new(ConstantValue, 0d, 0d, 0d, 0d, 0d); }
    public sealed record Parameter(bool IsU) : SurfaceScalarExpression(UnitDimension.Dimensionless)
    { public override ScalarJet Evaluate(double u, double v) => IsU ? new(u, 1d, 0d, 0d, 0d, 0d) : new(v, 0d, 1d, 0d, 0d, 0d); }
    public sealed record Sum(SurfaceScalarExpression Left, SurfaceScalarExpression Right) : SurfaceScalarExpression(RequireSame(Left, Right))
    { public override ScalarJet Evaluate(double u, double v) { var a = Left.Evaluate(u, v); var b = Right.Evaluate(u, v); return new(a.Value + b.Value, a.Du + b.Du, a.Dv + b.Dv, a.Duu + b.Duu, a.Duv + b.Duv, a.Dvv + b.Dvv); } }
    public sealed record Difference(SurfaceScalarExpression Left, SurfaceScalarExpression Right) : SurfaceScalarExpression(RequireSame(Left, Right))
    { public override ScalarJet Evaluate(double u, double v) { var a = Left.Evaluate(u, v); var b = Right.Evaluate(u, v); return new(a.Value - b.Value, a.Du - b.Du, a.Dv - b.Dv, a.Duu - b.Duu, a.Duv - b.Duv, a.Dvv - b.Dvv); } }
    public sealed record Product(SurfaceScalarExpression Left, SurfaceScalarExpression Right) : SurfaceScalarExpression(Left.Unit * Right.Unit)
    { public override ScalarJet Evaluate(double u, double v) { var a = Left.Evaluate(u, v); var b = Right.Evaluate(u, v); return new(a.Value*b.Value, a.Du*b.Value+a.Value*b.Du, a.Dv*b.Value+a.Value*b.Dv, a.Duu*b.Value+2*a.Du*b.Du+a.Value*b.Duu, a.Duv*b.Value+a.Du*b.Dv+a.Dv*b.Du+a.Value*b.Duv, a.Dvv*b.Value+2*a.Dv*b.Dv+a.Value*b.Dvv); } }
    public sealed record Quotient(SurfaceScalarExpression Left, SurfaceScalarExpression Right) : SurfaceScalarExpression(Left.Unit / Right.Unit)
    { public override ScalarJet Evaluate(double u, double v) { var a=Left.Evaluate(u,v);var b=Right.Evaluate(u,v);if(double.Abs(b.Value)<=1e-15)throw new DivideByZeroException("Parametric expression divisor is zero.");var inverse=Reciprocal(b);return Multiply(a,inverse); } }
    public sealed record IntegerPower(SurfaceScalarExpression Operand, int Exponent) : SurfaceScalarExpression(new UnitDimension(Operand.Unit.LengthPower * Exponent))
    { public override ScalarJet Evaluate(double u, double v) { var a=Operand.Evaluate(u,v);if(Exponent<0&&double.Abs(a.Value)<=1e-15)throw new DivideByZeroException("Negative power is singular at zero.");var value=double.Pow(a.Value,Exponent);var first=Exponent==0?0d:Exponent*double.Pow(a.Value,Exponent-1);var second=Exponent is 0 or 1?0d:Exponent*(Exponent-1)*double.Pow(a.Value,Exponent-2);return Compose(a,value,first,second); } }
    public sealed record Sine(SurfaceScalarExpression Operand) : SurfaceScalarExpression(RequireDimensionless(Operand))
    { public override ScalarJet Evaluate(double u, double v) { var a=Operand.Evaluate(u,v);return Compose(a,double.Sin(a.Value),double.Cos(a.Value),-double.Sin(a.Value)); } }
    public sealed record Cosine(SurfaceScalarExpression Operand) : SurfaceScalarExpression(RequireDimensionless(Operand))
    { public override ScalarJet Evaluate(double u, double v) { var a=Operand.Evaluate(u,v);return Compose(a,double.Cos(a.Value),-double.Sin(a.Value),-double.Cos(a.Value)); } }

    private static ScalarJet Compose(ScalarJet a,double value,double first,double second)=>new(value,first*a.Du,first*a.Dv,second*a.Du*a.Du+first*a.Duu,second*a.Du*a.Dv+first*a.Duv,second*a.Dv*a.Dv+first*a.Dvv);
    private static ScalarJet Reciprocal(ScalarJet b){var v=b.Value;return Compose(b,1d/v,-1d/(v*v),2d/(v*v*v));}
    private static ScalarJet Multiply(ScalarJet a,ScalarJet b)=>new(a.Value*b.Value,a.Du*b.Value+a.Value*b.Du,a.Dv*b.Value+a.Value*b.Dv,a.Duu*b.Value+2*a.Du*b.Du+a.Value*b.Duu,a.Duv*b.Value+a.Du*b.Dv+a.Dv*b.Du+a.Value*b.Duv,a.Dvv*b.Value+2*a.Dv*b.Dv+a.Value*b.Dvv);

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

public sealed record PatchJet2(Point3D Point, Vector3D Du, Vector3D Dv, Vector3D Duu, Vector3D Duv, Vector3D Dvv,
    DifferentialSingularityKind Singularity) : ISecondJet3
{
    public bool IsRegular => Singularity == DifferentialSingularityKind.Regular;
}

/// <summary>Authored rectangular parametric geometry, independent of CAD realization and topology.</summary>
public sealed class BoundedParametricPatch3
{
    private readonly Func<double, double, SurfaceDifferential>? _proceduralEvaluator;
    private readonly Func<double, double, PatchJet2>? _secondJetEvaluator;

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

    private BoundedParametricPatch3(string stableId, ParametricDomain domain, Func<double, double, SurfaceDifferential> evaluator, Func<double,double,PatchJet2>? secondJetEvaluator,
        string provenance, GeometryRepresentationKind representation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        StableId = stableId; Domain = domain; Provenance = provenance; Representation = representation; _proceduralEvaluator = evaluator; _secondJetEvaluator=secondJetEvaluator;
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
    public bool SupportsSecondJet => PointExpression is not null || _secondJetEvaluator is not null;

    public static BoundedParametricPatch3 Procedural(string stableId, ParametricDomain domain,
        Func<double, double, SurfaceDifferential> evaluator, string provenance) =>
        new(stableId, domain, evaluator ?? throw new ArgumentNullException(nameof(evaluator)), null, provenance, GeometryRepresentationKind.ProceduralParametric);

    public static BoundedParametricPatch3 Procedural(string stableId, ParametricDomain domain,
        Func<double,double,SurfaceDifferential> evaluator, Func<double,double,PatchJet2> secondJetEvaluator, string provenance) =>
        new(stableId,domain,evaluator ?? throw new ArgumentNullException(nameof(evaluator)),secondJetEvaluator ?? throw new ArgumentNullException(nameof(secondJetEvaluator)),provenance,GeometryRepresentationKind.ProceduralParametric);

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

    public PatchJet2 EvaluateJet2(double u,double v)
    {
        ValidateParameters(u,v);
        if(_secondJetEvaluator is not null)return Validate(_secondJetEvaluator(u,v));
        if(PointExpression is null)throw new NotSupportedException($"Patch '{StableId}' does not expose second-jet capability.");
        var x=PointExpression.X.Evaluate(u,v);var y=PointExpression.Y.Evaluate(u,v);var z=PointExpression.Z.Evaluate(u,v);
        var values=new[]{x.Value,x.Du,x.Dv,x.Duu,x.Duv,x.Dvv,y.Value,y.Du,y.Dv,y.Duu,y.Duv,y.Dvv,z.Value,z.Du,z.Dv,z.Duu,z.Duv,z.Dvv};
        if(!values.All(double.IsFinite))return new(new(x.Value,y.Value,z.Value),new(x.Du,y.Du,z.Du),new(x.Dv,y.Dv,z.Dv),new(x.Duu,y.Duu,z.Duu),new(x.Duv,y.Duv,z.Duv),new(x.Dvv,y.Dvv,z.Dvv),DifferentialSingularityKind.NonFinite);
        var du=new Vector3D(x.Du,y.Du,z.Du);var dv=new Vector3D(x.Dv,y.Dv,z.Dv);
        return new(new(x.Value,y.Value,z.Value),du,dv,new(x.Duu,y.Duu,z.Duu),new(x.Duv,y.Duv,z.Duv),new(x.Dvv,y.Dvv,z.Dvv),du.Cross(dv).TryNormalize(out _)?DifferentialSingularityKind.Regular:DifferentialSingularityKind.Singular);
    }

    private void ValidateParameters(double u,double v){if(!double.IsFinite(u)||!double.IsFinite(v)||u<Domain.U.Minimum||u>Domain.U.Maximum||v<Domain.V.Minimum||v>Domain.V.Maximum)throw new ArgumentOutOfRangeException(nameof(u),"Parameters must be finite and lie inside the authored rectangular domain.");}
    private static PatchJet2 Validate(PatchJet2 jet)=>new[]{jet.Point.X,jet.Point.Y,jet.Point.Z,jet.Du.X,jet.Du.Y,jet.Du.Z,jet.Dv.X,jet.Dv.Y,jet.Dv.Z,jet.Duu.X,jet.Duu.Y,jet.Duu.Z,jet.Duv.X,jet.Duv.Y,jet.Duv.Z,jet.Dvv.X,jet.Dvv.Y,jet.Dvv.Z}.All(double.IsFinite)?jet:jet with{Singularity=DifferentialSingularityKind.NonFinite};
}
