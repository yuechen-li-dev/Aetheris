using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Geometry;

public enum DistanceRelation { Separated, WithinTolerance, Coincident, Unknown }
public enum DistanceQueryStatus { Available, Unknown }

/// <summary>
/// Mandatory interpretation and work policy for bounded distance queries. The default model-space
/// tolerance is the kernel CAD tolerance (1e-6 mm); relative tolerance is applied to the largest
/// coordinate/distance scale observed by the result.
/// </summary>
public sealed record DistanceQueryPolicy
{
    public static DistanceQueryPolicy Default { get; } = new();
    public double LinearTolerance { get; init; } = ToleranceContext.Default.Linear;
    public double RelativeTolerance { get; init; } = ToleranceContext.Default.Relative;
    public double ParameterTolerance { get; init; } = 1e-10;
    public int IterationBudget { get; init; } = 96;
    public int SubdivisionBudget { get; init; } = 10_000;

    public DistanceQueryPolicy Validate()
    {
        Positive(LinearTolerance, nameof(LinearTolerance));
        Positive(RelativeTolerance, nameof(RelativeTolerance));
        Positive(ParameterTolerance, nameof(ParameterTolerance));
        if (IterationBudget < 1) throw new ArgumentOutOfRangeException(nameof(IterationBudget));
        if (SubdivisionBudget < 16) throw new ArgumentOutOfRangeException(nameof(SubdivisionBudget));
        return this;
    }

    public double EffectiveTolerance(double scale) => double.Max(LinearTolerance, RelativeTolerance * double.Max(1d, double.Abs(scale)));
    private static void Positive(double value, string name) { if (!double.IsFinite(value) || value <= 0d) throw new ArgumentOutOfRangeException(name); }
}

public sealed record DistanceParameters(double? T = null, double? U = null, double? V = null);
public sealed record DistanceQueryStatistics(int Iterations, int Subdivisions, int CandidateCount, bool BudgetExhausted);

public sealed record ClosestPointResult(
    DistanceQueryStatus Status,
    DistanceRelation Relation,
    double? ComputedDistance,
    double? SquaredDistance,
    double? DistanceLowerBound,
    double? DistanceUpperBound,
    Point3D? PointOnA,
    Point3D? PointOnB,
    DistanceParameters? ParameterOnA,
    DistanceParameters? ParameterOnB,
    DistanceQueryPolicy ToleranceUsed,
    PredicateEvidenceKind Evidence,
    double? Residual,
    DistanceQueryStatistics Statistics,
    GeometryIdentity? IdentityA,
    GeometryIdentity? IdentityB,
    GeometryProvenance? ProvenanceA,
    GeometryProvenance? ProvenanceB,
    IReadOnlyList<GeometryQueryDiagnostic> Diagnostics);

/// <summary>
/// Observational closest-point/minimum-distance queries. Results never author intersections,
/// topology, contact order, or motion. A numerical zero only means WithinTolerance unless
/// structural identity or a certified argument establishes coincidence.
/// </summary>
public static class ClosestPointQuery
{
    public static ClosestPointResult Between(Point3D point, BoundedParametricCurve3 curve) => Between(point, curve, DistanceQueryPolicy.Default);
    public static ClosestPointResult Between(Point3D point, BoundedParametricCurve3 curve, DistanceQueryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(curve); Validate(point, policy);
        if (curve.NativeFamily == "Line3") return PointSegment(point, curve, policy);
        return Optimize([curve.Domain], x => (point, curve.Evaluate(x[0])), policy, null, curve,
            _ => null, x => new(T: x[0]));
    }

    public static ClosestPointResult Between(BoundedParametricCurve3 curve, Point3D point) => Swap(Between(point, curve));
    public static ClosestPointResult Between(BoundedParametricCurve3 curve, Point3D point, DistanceQueryPolicy policy) => Swap(Between(point, curve, policy));

    public static ClosestPointResult Between(Point3D point, BoundedParametricPatch3 patch) => Between(point, patch, DistanceQueryPolicy.Default);
    public static ClosestPointResult Between(Point3D point, BoundedParametricPatch3 patch, DistanceQueryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(patch); Validate(point, policy);
        return Optimize([ToDomain(patch.Domain.U), ToDomain(patch.Domain.V)], x => (point, patch.Evaluate(x[0], x[1]).Point), policy,
            null, patch, _ => null, x => new(U: x[0], V: x[1]));
    }

    public static ClosestPointResult Between(BoundedParametricPatch3 patch, Point3D point) => Swap(Between(point, patch));
    public static ClosestPointResult Between(BoundedParametricPatch3 patch, Point3D point, DistanceQueryPolicy policy) => Swap(Between(point, patch, policy));

    public static ClosestPointResult Between(BoundedParametricCurve3 a, BoundedParametricCurve3 b) => Between(a, b, DistanceQueryPolicy.Default);
    public static ClosestPointResult Between(BoundedParametricCurve3 a, BoundedParametricCurve3 b, DistanceQueryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(a); ArgumentNullException.ThrowIfNull(b); policy.Validate();
        if (a.Identity == b.Identity) return Structural(a, b, policy, a.Evaluate(a.Domain.Minimum), new(T: a.Domain.Minimum), new(T: b.Domain.Minimum));
        if (a.NativeFamily == "Line3" && b.NativeFamily == "Line3") return SegmentSegment(a, b, policy);
        return Optimize([a.Domain, b.Domain], x => (a.Evaluate(x[0]), b.Evaluate(x[1])), policy, a, b,
            x => new(T: x[0]), x => new(T: x[1]));
    }

    public static ClosestPointResult Between(BoundedParametricCurve3 curve, BoundedParametricPatch3 patch) => Between(curve, patch, DistanceQueryPolicy.Default);
    public static ClosestPointResult Between(BoundedParametricCurve3 curve, BoundedParametricPatch3 patch, DistanceQueryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(curve); ArgumentNullException.ThrowIfNull(patch); policy.Validate();
        return Optimize([curve.Domain, ToDomain(patch.Domain.U), ToDomain(patch.Domain.V)],
            x => (curve.Evaluate(x[0]), patch.Evaluate(x[1], x[2]).Point), policy, curve, patch,
            x => new(T: x[0]), x => new(U: x[1], V: x[2]));
    }

    public static ClosestPointResult Between(BoundedParametricPatch3 patch, BoundedParametricCurve3 curve) => Swap(Between(curve, patch));
    public static ClosestPointResult Between(BoundedParametricPatch3 patch, BoundedParametricCurve3 curve, DistanceQueryPolicy policy) => Swap(Between(curve, patch, policy));

    public static ClosestPointResult Between(BoundedParametricPatch3 a, BoundedParametricPatch3 b) => Between(a, b, DistanceQueryPolicy.Default);
    public static ClosestPointResult Between(BoundedParametricPatch3 a, BoundedParametricPatch3 b, DistanceQueryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(a); ArgumentNullException.ThrowIfNull(b); policy.Validate();
        if (a.Identity == b.Identity)
        {
            var p = a.Evaluate(a.Domain.U.Minimum, a.Domain.V.Minimum).Point;
            return Structural(a, b, policy, p, new(U: a.Domain.U.Minimum, V: a.Domain.V.Minimum), new(U: b.Domain.U.Minimum, V: b.Domain.V.Minimum));
        }
        return Optimize([ToDomain(a.Domain.U), ToDomain(a.Domain.V), ToDomain(b.Domain.U), ToDomain(b.Domain.V)],
            x => (a.Evaluate(x[0], x[1]).Point, b.Evaluate(x[2], x[3]).Point), policy, a, b,
            x => new(U: x[0], V: x[1]), x => new(U: x[2], V: x[3]));
    }

    private static ClosestPointResult PointSegment(Point3D point, BoundedParametricCurve3 curve, DistanceQueryPolicy policy)
    {
        var p0 = curve.Evaluate(curve.Domain.Minimum); var p1 = curve.Evaluate(curve.Domain.Maximum); var d = p1 - p0;
        var fraction = double.Clamp((point - p0).Dot(d) / d.LengthSquared, 0d, 1d);
        var parameter = curve.Domain.Minimum + fraction * curve.Domain.Length; var closest = curve.Evaluate(parameter);
        return Exact(point, closest, policy, null, curve, null, new(T: parameter), PredicateEvidenceKind.Certified);
    }

    private static ClosestPointResult SegmentSegment(BoundedParametricCurve3 a, BoundedParametricCurve3 b, DistanceQueryPolicy policy)
    {
        var p1 = a.Evaluate(a.Domain.Minimum); var q1 = a.Evaluate(a.Domain.Maximum);
        var p2 = b.Evaluate(b.Domain.Minimum); var q2 = b.Evaluate(b.Domain.Maximum);
        var d1=q1-p1;var d2=q2-p2;var r=p1-p2;var aa=d1.Dot(d1);var e=d2.Dot(d2);var f=d2.Dot(r);double s,t;
        var eps=1e-30;
        if(aa<=eps){s=0;t=double.Clamp(f/e,0,1);} else {var c=d1.Dot(r);if(e<=eps){t=0;s=double.Clamp(-c/aa,0,1);}else{var bb=d1.Dot(d2);var denom=aa*e-bb*bb;s=denom!=0?double.Clamp((bb*f-c*e)/denom,0,1):0;t=(bb*s+f)/e;if(t<0){t=0;s=double.Clamp(-c/aa,0,1);}else if(t>1){t=1;s=double.Clamp((bb-c)/aa,0,1);}}}
        var ta=a.Domain.Minimum+s*a.Domain.Length;var tb=b.Domain.Minimum+t*b.Domain.Length;
        return Exact(a.Evaluate(ta),b.Evaluate(tb),policy,a,b,new(T:ta),new(T:tb),PredicateEvidenceKind.Certified);
    }

    private static ClosestPointResult Optimize(ParameterDomain1[] domains, Func<double[],(Point3D A,Point3D B)> evaluate,
        DistanceQueryPolicy policy, object? a, object? b, Func<double[],DistanceParameters?> pa, Func<double[],DistanceParameters?> pb)
    {
        policy.Validate(); var diagnostics=new List<GeometryQueryDiagnostic>();
        var dimensions=domains.Length; var fineN=dimensions switch {1=>65,2=>33,3=>17,_=>9};
        while(Pow(fineN,dimensions)+Pow((fineN+1)/2,dimensions)>policy.SubdivisionBudget&&fineN>3)fineN-=2;
        var coarseN=(fineN+1)/2; var candidates=0;
        var subdivisionCount=Pow(coarseN,dimensions)+Pow(fineN,dimensions);
        if(subdivisionCount>policy.SubdivisionBudget)
            return Unknown(policy,a,b,[new(GeometryQueryDiagnosticCode.SubdivisionBudgetExhausted,$"The minimum deterministic {dimensions}D lattice requires {subdivisionCount} candidates; policy permits {policy.SubdivisionBudget}.")],0);
        Candidate coarse, fine;
        try { coarse=Grid(coarseN); fine=Grid(fineN); }
        catch(ArithmeticException ex){diagnostics.Add(new(GeometryQueryDiagnosticCode.NonFiniteEvaluation,ex.Message));return Unknown(policy,a,b,diagnostics,candidates);}
        var coarseRefined=Refine(coarse,coarseN,policy.IterationBudget/2,out var coarseIterations);
        var best=Refine(fine,fineN,policy.IterationBudget-coarseIterations,out var fineIterations);var iterations=coarseIterations+fineIterations;
        var scale=Scale(best.A,best.B,best.Distance);var tolerance=policy.EffectiveTolerance(scale);
        var residual=double.Abs(coarseRefined.Distance-best.Distance);var converged=residual<=tolerance;
        if(!converged)diagnostics.Add(new(GeometryQueryDiagnosticCode.SubdivisionBudgetExhausted,$"Bounded deterministic refinement did not stabilize within {tolerance:R}; best change was {residual:R}."));
        var relation=converged?Classify(best.Distance,tolerance):DistanceRelation.Unknown;
        var evidence=converged?PredicateEvidenceKind.ToleranceBounded:PredicateEvidenceKind.Unknown;
        var lower=double.Max(0d,best.Distance-residual);var status=converged?DistanceQueryStatus.Available:DistanceQueryStatus.Unknown;
        return new(status,relation,best.Distance,best.Distance*best.Distance,lower,best.Distance,best.A,best.B,pa(best.X),pb(best.X),policy,evidence,residual,
            new(iterations,subdivisionCount,candidates,!converged),Identity(a),Identity(b),Provenance(a),Provenance(b),diagnostics);

        Candidate Grid(int n){Candidate? current=null;var x0=new double[dimensions];Walk(0);return current!.Value;
            void Walk(int axis){if(axis==dimensions){var c=At((double[])x0.Clone());candidates++;if(current is null||c.Distance<current.Value.Distance)current=c;return;}for(var i=0;i<n;i++){x0[axis]=domains[axis].Minimum+domains[axis].Length*i/(n-1d);Walk(axis+1);}}}
        Candidate Refine(Candidate seed,int n,int maximumIterations,out int used)
        {var local=seed;var x=(double[])seed.X.Clone();var step=domains.Select(d=>d.Length/(n-1)).ToArray();
            for(used=0;used<maximumIterations;used++){var improved=false;for(var axis=0;axis<dimensions;axis++)foreach(var sign in new[]{-1d,1d})
                {var trial=(double[])x.Clone();trial[axis]=domains[axis].Clamp(trial[axis]+sign*step[axis]);var c=At(trial);candidates++;if(c.Distance<local.Distance){local=c;x=trial;improved=true;}}
                if(!improved){for(var i=0;i<step.Length;i++)step[i]*=.5;if(step.Select((v,i)=>v/domains[i].Length).Max()<=policy.ParameterTolerance)break;}}
            return local;}
        Candidate At(double[] values){var pair=evaluate(values);if(!Finite(pair.A)||!Finite(pair.B))throw new ArithmeticException("Distance objective produced a non-finite point.");return new(values,pair.A,pair.B,(pair.A-pair.B).Length);}
    }

    private static ClosestPointResult Exact(Point3D a,Point3D b,DistanceQueryPolicy policy,object? oa,object? ob,DistanceParameters? pa,DistanceParameters? pb,PredicateEvidenceKind evidence)
    {policy.Validate();var d=(a-b).Length;var tol=policy.EffectiveTolerance(Scale(a,b,d));return new(DistanceQueryStatus.Available,Classify(d,tol),d,d*d,d,d,a,b,pa,pb,policy,evidence,0,new(0,0,1,false),Identity(oa),Identity(ob),Provenance(oa),Provenance(ob),[]);}
    private static ClosestPointResult Structural(object a,object b,DistanceQueryPolicy policy,Point3D point,DistanceParameters pa,DistanceParameters pb)
    {policy.Validate();return new(DistanceQueryStatus.Available,DistanceRelation.Coincident,0,0,0,0,point,point,pa,pb,policy,PredicateEvidenceKind.Structural,0,new(0,0,0,false),Identity(a),Identity(b),Provenance(a),Provenance(b),[]);}
    private static ClosestPointResult Unknown(DistanceQueryPolicy p,object? a,object? b,List<GeometryQueryDiagnostic>d,int c)=>new(DistanceQueryStatus.Unknown,DistanceRelation.Unknown,null,null,null,null,null,null,null,null,p,PredicateEvidenceKind.Unknown,null,new(0,0,c,true),Identity(a),Identity(b),Provenance(a),Provenance(b),d);
    private static ClosestPointResult Swap(ClosestPointResult r)=>r with{PointOnA=r.PointOnB,PointOnB=r.PointOnA,ParameterOnA=r.ParameterOnB,ParameterOnB=r.ParameterOnA,IdentityA=r.IdentityB,IdentityB=r.IdentityA,ProvenanceA=r.ProvenanceB,ProvenanceB=r.ProvenanceA};
    private static DistanceRelation Classify(double d,double tolerance)=>d<=tolerance?DistanceRelation.WithinTolerance:DistanceRelation.Separated;
    private static ParameterDomain1 ToDomain(ParameterInterval2 d)=>new(d.Minimum,d.Maximum);
    private static int Pow(int value,int exponent){var result=1;for(var i=0;i<exponent;i++)result=checked(result*value);return result;}
    private static double Scale(Point3D a,Point3D b,double d)=>new[]{double.Abs(a.X),double.Abs(a.Y),double.Abs(a.Z),double.Abs(b.X),double.Abs(b.Y),double.Abs(b.Z),d}.Max();
    private static bool Finite(Point3D p)=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&double.IsFinite(p.Z);
    private static void Validate(Point3D p,DistanceQueryPolicy policy){ArgumentNullException.ThrowIfNull(policy);policy.Validate();if(!Finite(p))throw new ArgumentException("Query point must be finite.",nameof(p));}
    private static GeometryIdentity? Identity(object? x)=>x switch{BoundedParametricCurve3 c=>c.Identity,BoundedParametricPatch3 p=>p.Identity,_=>null};
    private static GeometryProvenance? Provenance(object? x)=>x switch{BoundedParametricCurve3 c=>c.Provenance,BoundedParametricPatch3 p=>p.GeometryProvenance,_=>null};
    private readonly record struct Candidate(double[] X,Point3D A,Point3D B,double Distance);
}

public enum ClearanceExpectationRelation { AtLeast, AtMost }
public sealed record ClearanceExpectation(double RequiredDistance, ClearanceExpectationRelation Relation = ClearanceExpectationRelation.AtLeast)
{
    public ClearanceExpectationResult Evaluate(ClosestPointResult result)
    {
        ArgumentNullException.ThrowIfNull(result);if(!double.IsFinite(RequiredDistance)||RequiredDistance<0)throw new ArgumentOutOfRangeException(nameof(RequiredDistance));
        var satisfied=result.Status==DistanceQueryStatus.Available&&result.ComputedDistance is double d&&(Relation==ClearanceExpectationRelation.AtLeast?d>=RequiredDistance:d<=RequiredDistance);
        return new(this,result,satisfied,satisfied?null:$"Expected clearance {Relation} {RequiredDistance:R}; observed {result.ComputedDistance?.ToString("R")??"Unknown"}.");
    }
}
public sealed record ClearanceExpectationResult(ClearanceExpectation Expectation,ClosestPointResult Evidence,bool Satisfied,string? Diagnostic);
