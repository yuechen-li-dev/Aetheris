using Aetheris.Kernel.Core.Math;

namespace Aetheris.Geometry;

public sealed record DifferentialPolicy(
    double MinimumFirstDerivativeMagnitude=1e-10,
    double MinimumNormalMagnitude=1e-12,
    double MinimumMetricDeterminant=1e-18,
    double MaximumMetricCondition=1e12,
    double CurvatureTolerance=1e-9)
{
    public static DifferentialPolicy Default { get; }=new();
}

public enum DifferentialQueryStatus { Available, Unknown }

public sealed record CurveCurvatureResult(double? Curvature,double? RadiusOfCurvature,PredicateEvidenceKind Evidence,
    DifferentialQueryStatus Status,DifferentialSingularityKind Singularity,string? Diagnostic);

public sealed record FundamentalForm(double E,double F,double G);
public sealed record SecondFundamentalForm(double E2,double F2,double G2);

/// <summary>Principal curvatures are ordered K1 &gt;= K2. Directions are absent at umbilics or unstable metrics.</summary>
public sealed record PatchCurvatureResult(
    double? GaussianCurvature,double? MeanCurvature,double? K1,double? K2,
    Direction3D? Direction1,Direction3D? Direction2,Direction3D? OrientedNormal,
    FundamentalForm? FirstFundamentalForm,SecondFundamentalForm? SecondFundamentalForm,
    PredicateEvidenceKind Evidence,DifferentialQueryStatus Status,DifferentialSingularityKind Singularity,string? Diagnostic);

public sealed record NormalCurvatureResult(double? Curvature,PredicateEvidenceKind Evidence,DifferentialQueryStatus Status,
    DifferentialSingularityKind Singularity,string? Diagnostic);

public static class CurvatureQuery
{
    public static CurveCurvatureResult Curve(BoundedParametricCurve3 curve,double parameter,DifferentialPolicy? policy=null)
    {
        ArgumentNullException.ThrowIfNull(curve);policy??=DifferentialPolicy.Default;
        if(!curve.SupportsSecondJet)return UnknownCurve(DifferentialSingularityKind.Undefined,"Second-jet capability is unavailable.");
        CurveJet2 jet;
        try{jet=curve.EvaluateJet2(parameter);}catch(ArithmeticException ex){return UnknownCurve(DifferentialSingularityKind.Undefined,ex.Message);}
        var speed=jet.FirstDerivative.Length;
        if(jet.Singularity!=DifferentialSingularityKind.Regular||!double.IsFinite(speed)||speed<policy.MinimumFirstDerivativeMagnitude)
            return UnknownCurve(jet.Singularity==DifferentialSingularityKind.Regular?DifferentialSingularityKind.Singular:jet.Singularity,"First derivative is too small or non-finite for stable curvature.");
        var denominator=speed*speed*speed;var curvature=jet.FirstDerivative.Cross(jet.SecondDerivative).Length/denominator;
        if(!double.IsFinite(curvature))return UnknownCurve(DifferentialSingularityKind.NonFinite,"Curvature denominator is ill-conditioned.");
        return new(curvature,curvature<=policy.CurvatureTolerance?double.PositiveInfinity:1d/curvature,PredicateEvidenceKind.ToleranceBounded,DifferentialQueryStatus.Available,DifferentialSingularityKind.Regular,null);
    }

    public static PatchCurvatureResult Patch(BoundedParametricPatch3 patch,double u,double v,DifferentialPolicy? policy=null)
    {
        ArgumentNullException.ThrowIfNull(patch);policy??=DifferentialPolicy.Default;
        if(!patch.SupportsSecondJet)return UnknownPatch(DifferentialSingularityKind.Undefined,"Second-jet capability is unavailable.");
        PatchJet2 jet;try{jet=patch.EvaluateJet2(u,v);}catch(ArithmeticException ex){return UnknownPatch(DifferentialSingularityKind.Undefined,ex.Message);}
        if(jet.Singularity!=DifferentialSingularityKind.Regular)return UnknownPatch(jet.Singularity,"Patch second jet is singular or non-finite.");
        var cross=jet.Du.Cross(jet.Dv);var area=cross.Length;
        if(!double.IsFinite(area)||area<policy.MinimumNormalMagnitude)return UnknownPatch(DifferentialSingularityKind.Singular,"Surface normal magnitude is too small for stable curvature.");
        var normalVector=cross*(1d/area);var normal=Direction3D.Create(normalVector);
        var E=jet.Du.Dot(jet.Du);var F=jet.Du.Dot(jet.Dv);var G=jet.Dv.Dot(jet.Dv);var determinant=E*G-F*F;
        var trace=E+G;var discriminant=double.Sqrt(double.Max(0d,trace*trace-4d*determinant));var minEigen=(trace-discriminant)/2d;var maxEigen=(trace+discriminant)/2d;
        if(!double.IsFinite(determinant)||determinant<policy.MinimumMetricDeterminant||minEigen<=0||maxEigen/minEigen>policy.MaximumMetricCondition)
            return UnknownPatch(DifferentialSingularityKind.Singular,"First fundamental form is singular or ill-conditioned.");
        var e=normalVector.Dot(jet.Duu);var f=normalVector.Dot(jet.Duv);var g=normalVector.Dot(jet.Dvv);
        var gaussian=(e*g-f*f)/determinant;var mean=(E*g-2d*F*f+G*e)/(2d*determinant);
        var root=double.Sqrt(double.Max(0d,mean*mean-gaussian));var k1=mean+root;var k2=mean-root;
        if(!new[]{gaussian,mean,k1,k2}.All(double.IsFinite))return UnknownPatch(DifferentialSingularityKind.NonFinite,"Curvature evaluation produced a non-finite value.");
        Direction3D? d1=null,d2=null;
        if(double.Abs(k1-k2)>policy.CurvatureTolerance)
        {
            d1=PrincipalDirection(jet,E,F,G,e,f,g,k1);d2=PrincipalDirection(jet,E,F,G,e,f,g,k2);
            if(d1 is not null&&d2 is null)d2=Direction3D.TryCreate(normalVector.Cross(d1.Value.ToVector()),out var perpendicular)?perpendicular:null;
            if(d2 is not null&&d1 is null)d1=Direction3D.TryCreate(d2.Value.ToVector().Cross(normalVector),out var perpendicular)?perpendicular:null;
        }
        return new(gaussian,mean,k1,k2,d1,d2,normal,new(E,F,G),new(e,f,g),PredicateEvidenceKind.ToleranceBounded,DifferentialQueryStatus.Available,DifferentialSingularityKind.Regular,d1 is null?"Principal directions are indeterminate at an umbilic or repeated-curvature point.":null);
    }

    public static NormalCurvatureResult NormalCurvature(BoundedParametricPatch3 patch,double u,double v,Vector3D tangentDirection,DifferentialPolicy? policy=null)
    {
        policy??=DifferentialPolicy.Default;var curvature=Patch(patch,u,v,policy);
        if(curvature.Status!=DifferentialQueryStatus.Available||curvature.FirstFundamentalForm is null||curvature.SecondFundamentalForm is null)
            return new(null,PredicateEvidenceKind.Unknown,DifferentialQueryStatus.Unknown,curvature.Singularity,curvature.Diagnostic);
        var jet=patch.EvaluateJet2(u,v);var form=curvature.FirstFundamentalForm;var second=curvature.SecondFundamentalForm;
        var rhsU=tangentDirection.Dot(jet.Du);var rhsV=tangentDirection.Dot(jet.Dv);var determinant=form.E*form.G-form.F*form.F;
        var a=(rhsU*form.G-rhsV*form.F)/determinant;var b=(rhsV*form.E-rhsU*form.F)/determinant;
        var denominator=form.E*a*a+2d*form.F*a*b+form.G*b*b;
        if(!double.IsFinite(denominator)||denominator<policy.MinimumFirstDerivativeMagnitude*policy.MinimumFirstDerivativeMagnitude)
            return new(null,PredicateEvidenceKind.Unknown,DifferentialQueryStatus.Unknown,DifferentialSingularityKind.Singular,"Tangent direction has insufficient magnitude in the patch tangent plane.");
        var value=(second.E2*a*a+2d*second.F2*a*b+second.G2*b*b)/denominator;
        return double.IsFinite(value)?new(value,PredicateEvidenceKind.ToleranceBounded,DifferentialQueryStatus.Available,DifferentialSingularityKind.Regular,null):new(null,PredicateEvidenceKind.Unknown,DifferentialQueryStatus.Unknown,DifferentialSingularityKind.NonFinite,"Normal curvature is non-finite.");
    }

    private static Direction3D? PrincipalDirection(PatchJet2 jet,double E,double F,double G,double e,double f,double g,double k)
    {
        var a=e-k*E;var b=f-k*F;var c=g-k*G;var x=double.Abs(a)+double.Abs(b)>=double.Abs(b)+double.Abs(c)?-b:-c;var y=double.Abs(a)+double.Abs(b)>=double.Abs(b)+double.Abs(c)?a:b;
        return Direction3D.TryCreate(jet.Du*x+jet.Dv*y,out var direction)?direction:null;
    }
    private static CurveCurvatureResult UnknownCurve(DifferentialSingularityKind singularity,string diagnostic)=>new(null,null,PredicateEvidenceKind.Unknown,DifferentialQueryStatus.Unknown,singularity,diagnostic);
    private static PatchCurvatureResult UnknownPatch(DifferentialSingularityKind singularity,string diagnostic)=>new(null,null,null,null,null,null,null,null,null,PredicateEvidenceKind.Unknown,DifferentialQueryStatus.Unknown,singularity,diagnostic);
}
