using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public readonly record struct ParameterInterval2
{
    public ParameterInterval2(double minimum,double maximum)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || maximum <= minimum)
            throw new ArgumentException("A parameter interval must be finite and increasing.");
        Minimum=minimum;Maximum=maximum;
    }
    public double Minimum { get; }
    public double Maximum { get; }
    public double Map(double normalized) => Minimum + System.Math.Clamp(normalized, 0d, 1d) * (Maximum - Minimum);
}

public sealed record ParametricDomain(ParameterInterval2 U, ParameterInterval2 V);
public enum SurfaceConstructionKind { ParametricSurface, HyperbolicParaboloid, ParabolicCylinder, EllipticParaboloid, Helicoid, RuledSurface, RuledTransition, SectionSurface, BoundaryPatch }
public enum SurfaceMaterializationKind { ExactAnalytic, ExactPolynomialBSpline, ApproximatedNonRationalBSpline }
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
    internal abstract ScalarJet Evaluate(double u, double v);

    public sealed record Constant(double ConstantValue, UnitDimension ConstantUnit) : SurfaceScalarExpression(ConstantUnit)
    { internal override ScalarJet Evaluate(double u, double v) => new(ConstantValue, 0d, 0d); }
    public sealed record Parameter(bool IsU) : SurfaceScalarExpression(UnitDimension.Dimensionless)
    { internal override ScalarJet Evaluate(double u, double v) => IsU ? new(u, 1d, 0d) : new(v, 0d, 1d); }
    public sealed record Sum(SurfaceScalarExpression Left, SurfaceScalarExpression Right) : SurfaceScalarExpression(RequireSame(Left, Right))
    { internal override ScalarJet Evaluate(double u, double v) { var a=Left.Evaluate(u,v);var b=Right.Evaluate(u,v);return new(a.Value+b.Value,a.Du+b.Du,a.Dv+b.Dv); } }
    public sealed record Difference(SurfaceScalarExpression Left, SurfaceScalarExpression Right) : SurfaceScalarExpression(RequireSame(Left, Right))
    { internal override ScalarJet Evaluate(double u, double v) { var a=Left.Evaluate(u,v);var b=Right.Evaluate(u,v);return new(a.Value-b.Value,a.Du-b.Du,a.Dv-b.Dv); } }
    public sealed record Product(SurfaceScalarExpression Left, SurfaceScalarExpression Right) : SurfaceScalarExpression(Left.Unit * Right.Unit)
    { internal override ScalarJet Evaluate(double u, double v) { var a=Left.Evaluate(u,v);var b=Right.Evaluate(u,v);return new(a.Value*b.Value,a.Du*b.Value+a.Value*b.Du,a.Dv*b.Value+a.Value*b.Dv); } }
    public sealed record Quotient(SurfaceScalarExpression Left, SurfaceScalarExpression Right) : SurfaceScalarExpression(Left.Unit / Right.Unit)
    { internal override ScalarJet Evaluate(double u, double v) { var a=Left.Evaluate(u,v);var b=Right.Evaluate(u,v);if(double.Abs(b.Value)<=1e-15)throw new DivideByZeroException("Parametric expression divisor is zero.");var q=b.Value*b.Value;return new(a.Value/b.Value,(a.Du*b.Value-a.Value*b.Du)/q,(a.Dv*b.Value-a.Value*b.Dv)/q); } }
    public sealed record IntegerPower(SurfaceScalarExpression Operand, int Exponent) : SurfaceScalarExpression(new UnitDimension(Operand.Unit.LengthPower * Exponent))
    { internal override ScalarJet Evaluate(double u, double v) { var a=Operand.Evaluate(u,v);var value=double.Pow(a.Value,Exponent);var scale=Exponent==0?0d:Exponent*double.Pow(a.Value,Exponent-1);return new(value,scale*a.Du,scale*a.Dv); } }
    public sealed record Sine(SurfaceScalarExpression Operand) : SurfaceScalarExpression(RequireDimensionless(Operand))
    { internal override ScalarJet Evaluate(double u,double v){var a=Operand.Evaluate(u,v);var c=double.Cos(a.Value);return new(double.Sin(a.Value),c*a.Du,c*a.Dv);} }
    public sealed record Cosine(SurfaceScalarExpression Operand) : SurfaceScalarExpression(RequireDimensionless(Operand))
    { internal override ScalarJet Evaluate(double u,double v){var a=Operand.Evaluate(u,v);var s=-double.Sin(a.Value);return new(double.Cos(a.Value),s*a.Du,s*a.Dv);} }

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
    public static SurfaceScalarExpression Add(SurfaceScalarExpression a, SurfaceScalarExpression b) => new SurfaceScalarExpression.Sum(a,b);
    public static SurfaceScalarExpression Subtract(SurfaceScalarExpression a, SurfaceScalarExpression b) => new SurfaceScalarExpression.Difference(a,b);
    public static SurfaceScalarExpression Multiply(SurfaceScalarExpression a, SurfaceScalarExpression b) => new SurfaceScalarExpression.Product(a,b);
    public static SurfaceScalarExpression Divide(SurfaceScalarExpression a, SurfaceScalarExpression b) => new SurfaceScalarExpression.Quotient(a,b);
    public static SurfaceScalarExpression Power(SurfaceScalarExpression a, int exponent) => new SurfaceScalarExpression.IntegerPower(a,exponent);
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

public sealed record SurfaceDifferential(Point3D Point, Vector3D Du, Vector3D Dv, Direction3D? Normal, bool IsSingular);
public sealed record ApproximationCertificate(double RequestedTolerance, double MaximumSampledPositionResidual,
    double? MaximumNormalDeviationDegrees, int ControlCountU, int ControlCountV, string SamplingPolicy, string SourceIdentity);

public sealed record ParametricSurfaceIr
{
    public ParametricSurfaceIr(string stableId,SurfaceConstructionKind constructionKind,ParametricDomain domain,
        SurfacePointExpression pointExpression,string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        pointExpression.ValidateLengthOutput();StableId=stableId;ConstructionKind=constructionKind;Domain=domain;PointExpression=pointExpression;Provenance=provenance;
    }
    public string StableId { get; }
    public SurfaceConstructionKind ConstructionKind { get; }
    public ParametricDomain Domain { get; }
    public SurfacePointExpression PointExpression { get; }
    public string Provenance { get; }

    public SurfaceDifferential Evaluate(double u, double v)
    {
        if (!double.IsFinite(u) || !double.IsFinite(v)) throw new ArgumentOutOfRangeException(nameof(u), "Parameters must be finite.");
        if(u<Domain.U.Minimum||u>Domain.U.Maximum||v<Domain.V.Minimum||v>Domain.V.Maximum)throw new ArgumentOutOfRangeException(nameof(u),"Parameters must lie inside the authored rectangular domain.");
        var x=PointExpression.X.Evaluate(u,v);var y=PointExpression.Y.Evaluate(u,v);var z=PointExpression.Z.Evaluate(u,v);
        var du=new Vector3D(x.Du,y.Du,z.Du);var dv=new Vector3D(x.Dv,y.Dv,z.Dv);var cross=du.Cross(dv);
        var singular=!cross.TryNormalize(out var normalized);
        return new(new(x.Value,y.Value,z.Value),du,dv,singular?null:Direction3D.Create(normalized),singular);
    }
}

public sealed record ParametricMaterialization(BSplineSurfaceWithKnots Surface, ApproximationCertificate Certificate,
    SurfaceMaterializationKind Kind);

public static class ParametricSurfaceMaterializer
{
    public static ParametricMaterialization Materialize(ParametricSurfaceIr source, int controlCountU=9, int controlCountV=9, double tolerance=0.1)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(controlCountU<2||controlCountV<2)throw new ArgumentOutOfRangeException(nameof(controlCountU));
        if(!double.IsFinite(tolerance)||tolerance<=0)throw new ArgumentOutOfRangeException(nameof(tolerance));
        var controls=new Point3D[controlCountU][];
        for(var i=0;i<controlCountU;i++){controls[i]=new Point3D[controlCountV];var u=source.Domain.U.Map(i/(double)(controlCountU-1));for(var j=0;j<controlCountV;j++){var v=source.Domain.V.Map(j/(double)(controlCountV-1));controls[i][j]=source.Evaluate(u,v).Point;}}
        var ku=Knots(controlCountU);var kv=Knots(controlCountV);
        var spline=new BSplineSurfaceWithKnots(1,1,controls,"SURFACE_OF_LINEAR_EXTRUSION",false,false,false,ku.multiplicities,kv.multiplicities,ku.values,kv.values,"UNSPECIFIED");
        var maxResidual=0d;var maxNormal=0d;
        for(var i=0;i<controlCountU-1;i++)for(var j=0;j<controlCountV-1;j++)
        {
            var un=(i+.5)/(controlCountU-1);var vn=(j+.5)/(controlCountV-1);var u=source.Domain.U.Map(un);var v=source.Domain.V.Map(vn);
            var exact=source.Evaluate(u,v);var approximate=spline.Evaluate(un,vn);maxResidual=double.Max(maxResidual,(approximate-exact.Point).Length);
            var h=1e-6;var adu=spline.Evaluate(double.Min(1,un+h),vn)-spline.Evaluate(double.Max(0,un-h),vn);var adv=spline.Evaluate(un,double.Min(1,vn+h))-spline.Evaluate(un,double.Max(0,vn-h));
            if(!exact.IsSingular&&adu.Cross(adv).TryNormalize(out var an)){var dot=System.Math.Clamp(exact.Normal!.Value.ToVector().Dot(an),-1d,1d);maxNormal=double.Max(maxNormal,double.Acos(dot)*180d/double.Pi);}
        }
        if(maxResidual>tolerance&&(controlCountU<129||controlCountV<129))return Materialize(source,System.Math.Min(129,controlCountU*2-1),System.Math.Min(129,controlCountV*2-1),tolerance);
        if(maxResidual>tolerance)throw new InvalidOperationException($"Parametric materialization did not meet {tolerance:G6} mm within the bounded 129 x 129 grid; sampled residual was {maxResidual:G6} mm.");
        var kind=maxResidual<=1e-12?SurfaceMaterializationKind.ExactPolynomialBSpline:SurfaceMaterializationKind.ApproximatedNonRationalBSpline;
        return new(spline,new(tolerance,maxResidual,maxNormal,controlCountU,controlCountV,"adaptive uniform tensor grid; residuals at cell centers",source.StableId),kind);
    }

    private static (int[] multiplicities,double[] values) Knots(int count)
    {var values=Enumerable.Range(0,count).Select(i=>i/(double)(count-1)).ToArray();var multiplicities=Enumerable.Repeat(1,count).ToArray();multiplicities[0]=2;multiplicities[^1]=2;return(multiplicities,values);}
}

public static class MathematicalSurfaces
{
    public static ParametricSurfaceIr HyperbolicParaboloid(string id,double halfX,double halfY,double rise) => Graph(id,SurfaceConstructionKind.HyperbolicParaboloid,halfX,halfY,
        SurfaceExpression.Multiply(SurfaceExpression.Length(rise),SurfaceExpression.Multiply(SurfaceExpression.U,SurfaceExpression.V)));
    public static ParametricSurfaceIr ParabolicCylinder(string id,double halfX,double halfY,double rise) => Graph(id,SurfaceConstructionKind.ParabolicCylinder,halfX,halfY,
        SurfaceExpression.Multiply(SurfaceExpression.Length(rise),SurfaceExpression.Power(SurfaceExpression.U,2)));
    public static ParametricSurfaceIr EllipticParaboloid(string id,double halfX,double halfY,double rise) => Graph(id,SurfaceConstructionKind.EllipticParaboloid,halfX,halfY,
        SurfaceExpression.Multiply(SurfaceExpression.Length(rise),SurfaceExpression.Add(SurfaceExpression.Power(SurfaceExpression.U,2),SurfaceExpression.Power(SurfaceExpression.V,2))));
    public static ParametricSurfaceIr Helicoid(string id,double radius,double risePerTurn,double turns=1)
    {
        if(radius<=0||!double.IsFinite(radius)||!double.IsFinite(risePerTurn)||turns<=0||!double.IsFinite(turns))throw new ArgumentOutOfRangeException(nameof(radius));
        var angle=SurfaceExpression.Multiply(SurfaceExpression.Number(2d*double.Pi*turns),SurfaceExpression.V);
        return new(id,SurfaceConstructionKind.Helicoid,new(new(0,1),new(0,1)),new(
            SurfaceExpression.Multiply(SurfaceExpression.Length(radius),SurfaceExpression.Multiply(SurfaceExpression.U,SurfaceExpression.Cos(angle))),
            SurfaceExpression.Multiply(SurfaceExpression.Length(radius),SurfaceExpression.Multiply(SurfaceExpression.U,SurfaceExpression.Sin(angle))),
            SurfaceExpression.Multiply(SurfaceExpression.Length(risePerTurn*turns),SurfaceExpression.V)),id+":named-helicoid");
    }

    private static ParametricSurfaceIr Graph(string id,SurfaceConstructionKind kind,double halfX,double halfY,SurfaceScalarExpression z)
    {
        if(halfX<=0||halfY<=0||!double.IsFinite(halfX)||!double.IsFinite(halfY))throw new ArgumentOutOfRangeException(nameof(halfX));
        return new(id,kind,new(new(-1,1),new(-1,1)),new(SurfaceExpression.Multiply(SurfaceExpression.Length(halfX),SurfaceExpression.U),SurfaceExpression.Multiply(SurfaceExpression.Length(halfY),SurfaceExpression.V),z),id+":named-mathematical-surface");
    }
}
