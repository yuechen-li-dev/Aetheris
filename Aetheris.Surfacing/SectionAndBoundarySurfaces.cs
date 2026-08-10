using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public enum BoundaryContinuity { PositionG0, TangentG1 }

public sealed record ConstructedSurfacePatch(string StableId,SurfaceConstructionKind ConstructionKind,ParametricDomain Domain,
    SurfaceGeometry Support,Func<double,double,Point3D> Evaluate,SurfaceMaterializationKind MaterializationKind,
    ApproximationCertificate? ApproximationCertificate,IReadOnlyList<BoundaryProvenance> Provenance,DevelopabilityEvidence Developability)
{
    public SurfaceDifferential Differential(double u,double v)
    {
        const double h=1e-6;var uc=System.Math.Clamp(u,0,1);var vc=System.Math.Clamp(v,0,1);
        var du=Evaluate(double.Min(1,uc+h),vc)-Evaluate(double.Max(0,uc-h),vc);var dv=Evaluate(uc,double.Min(1,vc+h))-Evaluate(uc,double.Max(0,vc-h));var cross=du.Cross(dv);var singular=!cross.TryNormalize(out var n);
        return new(Evaluate(uc,vc),du,dv,singular?null:Direction3D.Create(n),singular);
    }
}

public sealed record SectionSurfaceIr
{
    public SectionSurfaceIr(string stableId,IReadOnlyList<RuledBoundary> orderedSections,IReadOnlyList<BoundaryProvenance> sectionProvenance,
        ParameterCorrespondenceKind parameterCorrespondence=ParameterCorrespondenceKind.SharedNormalizedNativeParameter)
    {
        if(orderedSections is null||orderedSections.Count<2)throw new ArgumentException("SectionSurface requires at least two ordered sections.");
        if(sectionProvenance is null||sectionProvenance.Count!=orderedSections.Count)throw new ArgumentException("Every section must retain provenance.");
        StableId=stableId;OrderedSections=orderedSections;SectionProvenance=sectionProvenance;ParameterCorrespondence=parameterCorrespondence;
    }
    public string StableId { get; }
    public IReadOnlyList<RuledBoundary> OrderedSections { get; }
    public IReadOnlyList<BoundaryProvenance> SectionProvenance { get; }
    public ParameterCorrespondenceKind ParameterCorrespondence { get; }
}

public sealed record BoundaryPatchIr(string StableId,RuledBoundary South,RuledBoundary North,RuledBoundary West,RuledBoundary East,
    IReadOnlyList<BoundaryProvenance> BoundaryProvenance,BoundaryContinuity Continuity=BoundaryContinuity.PositionG0,double CornerTolerance=1e-6);

public sealed record ConstructedSurfaceResult(ConstructedSurfacePatch? Patch,IReadOnlyList<SurfacingDiagnostic> Diagnostics)
{ public bool IsSuccess=>Patch is not null&&Diagnostics.Count==0; }

public static class SectionSurfaceLowering
{
    public static ConstructedSurfaceResult Lower(SectionSurfaceIr ir,int controlCountU=13)
    {
        ArgumentNullException.ThrowIfNull(ir);
        if(ir.OrderedSections.Count==2)
        {
            var ruledIr=new RuledSurfaceIr(ir.StableId,RuledConstructionKind.RuledTransition,ir.OrderedSections[0],ir.OrderedSections[1],ir.SectionProvenance[0],ir.SectionProvenance[1],true,ir.ParameterCorrespondence);
            var ruled=RuledSurfaceLowering.Lower(ruledIr);if(!ruled.IsSuccess)return new(null,ruled.Diagnostics);var p=ruled.Patch!;
            return new(new(ir.StableId,SurfaceConstructionKind.RuledTransition,p.Domain,p.ExactSurface,p.Evaluate,p.MaterializationKind,p.ApproximationCertificate,p.BoundaryProvenance,p.Developability),[]);
        }
        var evaluators=new List<Func<double,Point3D>>();
        foreach(var section in ir.OrderedSections)
        {if(!RuledSurfaceLowering.TryBoundaryEvaluator(section,out var evaluate,out _,out var diagnostic))return Failure("surfacing-section-incompatible",diagnostic!);evaluators.Add(evaluate!);}
        Point3D Evaluate(double u,double v)
        {
            var result=new Vector3D(0,0,0);var origin=Point3D.Origin;var n=evaluators.Count;
            for(var i=0;i<n;i++){var vi=i/(double)(n-1);var basis=1d;for(var j=0;j<n;j++)if(j!=i){var vj=j/(double)(n-1);basis*=(v-vj)/(vi-vj);}result+=(evaluators[i](u)-origin)*basis;}
            return origin+result;
        }
        var mat=ProceduralSurfaceMaterializer.Materialize(ir.StableId,Evaluate,controlCountU,System.Math.Max(2,ir.OrderedSections.Count*2-1));
        var developability=new DevelopabilityEvidence(DevelopabilityKind.Indeterminate,"section interpolation curvature sampling deferred",null,0,"Three or more interpolated sections may be genuinely double-curved and are not assumed developable.");
        return new(new(ir.StableId,SurfaceConstructionKind.SectionSurface,new(new(0,1),new(0,1)),SurfaceGeometry.FromBSplineSurfaceWithKnots(mat.Surface),Evaluate,mat.Kind,mat.Certificate,ir.SectionProvenance,developability),[]);
    }
    private static ConstructedSurfaceResult Failure(string code,string message)=>new(null,[new(code,message)]);
}

public static class BoundaryPatchLowering
{
    public static ConstructedSurfaceResult Lower(BoundaryPatchIr ir,int controlCount=13)
    {
        ArgumentNullException.ThrowIfNull(ir);
        if(ir.Continuity==BoundaryContinuity.TangentG1)return Failure("surfacing-tangent-constraint-unsupported","BoundaryPatch G1 requires adjacent support tangent evidence; M1 does not infer it from curves alone.");
        if(!double.IsFinite(ir.CornerTolerance)||ir.CornerTolerance<=0)return Failure("surfacing-corner-tolerance-invalid","Corner tolerance must be finite and positive.");
        if(!Try(ir.South,out var south,out var d)||!Try(ir.North,out var north,out d)||!Try(ir.West,out var west,out d)||!Try(ir.East,out var east,out d))return Failure("surfacing-boundary-invalid",d!);
        var sw=south!(0);var se=south(1);var nw=north!(0);var ne=north(1);
        if((west!(0)-sw).Length>ir.CornerTolerance||(west(1)-nw).Length>ir.CornerTolerance||(east!(0)-se).Length>ir.CornerTolerance||(east(1)-ne).Length>ir.CornerTolerance)
            return Failure("surfacing-boundary-corners-inconsistent","BoundaryPatch expects South/North west-to-east and West/East south-to-north; corner endpoints do not agree within tolerance.");
        Point3D Evaluate(double u,double v)
        {
            var o=Point3D.Origin;var blend=(south(u)-o)*(1-v)+(north(u)-o)*v+(west(v)-o)*(1-u)+(east(v)-o)*u;
            var corners=(sw-o)*((1-u)*(1-v))+(se-o)*(u*(1-v))+(nw-o)*((1-u)*v)+(ne-o)*(u*v);
            return o+blend-corners;
        }
        var mat=ProceduralSurfaceMaterializer.Materialize(ir.StableId,Evaluate,controlCount,controlCount);
        var differential=FiniteDevelopability(Evaluate);
        return new(new(ir.StableId,SurfaceConstructionKind.BoundaryPatch,new(new(0,1),new(0,1)),SurfaceGeometry.FromBSplineSurfaceWithKnots(mat.Surface),Evaluate,mat.Kind,mat.Certificate,ir.BoundaryProvenance,differential),[]);
    }
    private static bool Try(RuledBoundary boundary,out Func<double,Point3D>? evaluate,out string? diagnostic)=>RuledSurfaceLowering.TryBoundaryEvaluator(boundary,out evaluate,out _,out diagnostic);
    private static ConstructedSurfaceResult Failure(string code,string message)=>new(null,[new(code,message)]);
    private static DevelopabilityEvidence FiniteDevelopability(Func<double,double,Point3D> evaluate)
    {
        _=evaluate(.5,.5);
        return new(DevelopabilityKind.Indeterminate,"boundary patch Gaussian curvature classification deferred",null,1,"A boundary patch is not assumed developable.");
    }
}

internal static class ProceduralSurfaceMaterializer
{
    internal static ParametricMaterialization Materialize(string id,Func<double,double,Point3D> evaluate,int countU,int countV,double tolerance=0.1)
    {
        if(countU<2||countV<2)throw new ArgumentOutOfRangeException(nameof(countU));var controls=new Point3D[countU][];
        for(var i=0;i<countU;i++){controls[i]=new Point3D[countV];for(var j=0;j<countV;j++)controls[i][j]=evaluate(i/(double)(countU-1),j/(double)(countV-1));}
        var ku=Knots(countU);var kv=Knots(countV);var spline=new BSplineSurfaceWithKnots(1,1,controls,"UNSPECIFIED",false,false,false,ku.m,kv.m,ku.k,kv.k,"UNSPECIFIED");var residual=0d;
        for(var i=0;i<countU-1;i++)for(var j=0;j<countV-1;j++){var u=(i+.5)/(countU-1);var v=(j+.5)/(countV-1);residual=double.Max(residual,(evaluate(u,v)-spline.Evaluate(u,v)).Length);}
        if(residual>tolerance&&(countU<129||countV<129))return Materialize(id,evaluate,System.Math.Min(129,countU*2-1),System.Math.Min(129,countV*2-1),tolerance);
        if(residual>tolerance)throw new InvalidOperationException($"Procedural materialization did not meet {tolerance:G6} mm within the bounded 129 x 129 grid; sampled residual was {residual:G6} mm.");
        return new(spline,new(tolerance,residual,null,countU,countV,"adaptive uniform tensor grid; residuals at cell centers; normal deviation not sampled",id),SurfaceMaterializationKind.ApproximatedNonRationalBSpline);
    }
    private static (int[] m,double[] k) Knots(int count){var k=Enumerable.Range(0,count).Select(i=>i/(double)(count-1)).ToArray();var m=Enumerable.Repeat(1,count).ToArray();m[0]=2;m[^1]=2;return(m,k);}
}
