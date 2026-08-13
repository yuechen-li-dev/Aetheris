using Aetheris.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public enum SurfaceConstructionKind { ParametricSurface, HyperbolicParaboloid, ParabolicCylinder, EllipticParaboloid, Helicoid, RuledSurface, RuledTransition, SectionSurface, BoundaryPatch }
public enum SurfaceMaterializationKind { ExactAnalytic, ExactPolynomialBSpline, ApproximatedNonRationalBSpline }
public sealed record ApproximationCertificate(double RequestedTolerance, double MaximumSampledPositionResidual,
    double? MaximumNormalDeviationDegrees, int ControlCountU, int ControlCountV, string SamplingPolicy, string SourceIdentity);

public sealed record ParametricSurfaceIr
{
    public ParametricSurfaceIr(string stableId,SurfaceConstructionKind constructionKind,ParametricDomain domain,
        SurfacePointExpression pointExpression,string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        Patch=new BoundedParametricPatch3(stableId,domain,pointExpression,provenance);
        StableId=stableId;ConstructionKind=constructionKind;Domain=domain;PointExpression=pointExpression;Provenance=provenance;
    }
    public string StableId { get; }
    public SurfaceConstructionKind ConstructionKind { get; }
    public ParametricDomain Domain { get; }
    public SurfacePointExpression PointExpression { get; }
    public string Provenance { get; }
    public BoundedParametricPatch3 Patch { get; }

    public SurfaceDifferential Evaluate(double u, double v) => Patch.Evaluate(u,v);
    public PatchJet2 EvaluateJet2(double u,double v)=>Patch.EvaluateJet2(u,v);
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
