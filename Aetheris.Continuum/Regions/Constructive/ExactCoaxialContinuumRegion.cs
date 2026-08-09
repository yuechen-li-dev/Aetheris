using Aetheris.Continuum.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.Continuum.Regions.Constructive;

/// <summary>
/// Complete analytic CIR lowering of the admitted exact coaxial construction plan.
/// It is produced from the plan in parallel with BRep and never inspects tessellation or BRep topology.
/// </summary>
public sealed class ExactCoaxialContinuumRegion : IContinuumRegion, IImplicitFieldCapability, IConstructiveLineageRegion, IBoundsClassificationCapability
{
    private readonly Transform3D _inverse;

    public ExactCoaxialContinuumRegion(ExactCoaxialConstructionPlan plan, Transform3D transform)
    {
        Plan=plan ?? throw new ArgumentNullException(nameof(plan));
        if(!transform.IsRigid()) throw new ArgumentException("Exact coaxial CIR placement must be rigid.",nameof(transform));
        Transform=transform; _inverse=transform.Inverse(); Id=new RegionId($"{plan.StableId}:continuum");
        var vertices=Enumerable.Range(0,plan.Prism.SideCount).Select(i=>(plan.Prism.OrientationDegrees*double.Pi/180d)+(i*2d*double.Pi/plan.Prism.SideCount))
            .Select(a=>(Y:plan.Prism.Circumradius*double.Cos(a),Z:plan.Prism.Circumradius*double.Sin(a))).ToArray();
        Bounds=TransformBounds(new(new(plan.TopCap.Position,vertices.Min(v=>v.Y),vertices.Min(v=>v.Z)),new(plan.EndCap.Position,vertices.Max(v=>v.Y),vertices.Max(v=>v.Z))),transform);
        ConstructionSourceIdentity=$"{plan.StableId}:{plan.DeterministicSignature}";
    }

    public ExactCoaxialConstructionPlan Plan { get; }
    public Transform3D Transform { get; }
    public RegionId Id { get; }
    public BoundingBox3D Bounds { get; }
    public string ConstructionSourceIdentity { get; }
    public double AnalyticReferenceVolume => ComputeReferenceVolume();
    public double AnalyticReferenceBoundaryArea => ComputeReferenceArea();

    public ContinuumPointClassification Classify(Point3D point,double tolerance=1e-9d)
    {
        var local=_inverse.Apply(point);var rho=double.Sqrt((local.Y*local.Y)+(local.Z*local.Z));
        if(double.Abs(local.X-Plan.Prism.Start)<=tolerance&&rho>=Plan.RootBlend.ShoulderRadius-tolerance
            && RegularPolygonField(local.Y,local.Z,Plan.Prism)<=tolerance)return ContinuumPointClassification.Boundary;
        var value=FieldValue(point);
        return double.Abs(value)<=tolerance?ContinuumPointClassification.Boundary
            :value<0d?ContinuumPointClassification.Inside:ContinuumPointClassification.Outside;
    }

    /// <summary>Sign-correct constructive field. Its magnitude is intentionally not advertised as Euclidean distance.</summary>
    public double FieldValue(Point3D point)
    {
        var p=_inverse.Apply(point); var x=p.X; var rho=double.Sqrt((p.Y*p.Y)+(p.Z*p.Z));
        var headX=double.Clamp(x,Plan.TopCap.Position,Plan.Prism.End);
        var polygon=RegularPolygonField(p.Y,p.Z,Plan.Prism);
        var coneRadius=(headX-Plan.ConePlanarTrim.Apex)*double.Tan(Plan.ConePlanarTrim.SemiAngleDegrees*double.Pi/180d);
        var cone=double.Max(double.Max(polygon,rho-coneRadius),double.Max(Plan.TopCap.Position-x,x-Plan.Prism.End));
        var prism=double.Max(polygon,double.Max(double.Min(Plan.Prism.Start,Plan.Prism.End)-x,x-double.Max(Plan.Prism.Start,Plan.Prism.End)));
        var head=double.Min(cone,prism);
        var rootX=double.Clamp(x,Plan.Prism.Start,Plan.RootBlend.End);var r=Plan.RootBlend.Radius;var dx=rootX-Plan.RootBlend.End;
        var profile=Plan.RootBlend.ShoulderRadius-double.Sqrt(double.Max(0d,(r*r)-(dx*dx)));
        var root=double.Max(rho-profile,double.Max(Plan.Prism.Start-x,x-Plan.RootBlend.End));
        var cylinder=double.Max(rho-Plan.Cylinder.Radius,double.Max(Plan.Cylinder.Start-x,x-Plan.Cylinder.End));
        var t=double.Clamp((x-Plan.EndFrustum.Start)/(Plan.EndFrustum.End-Plan.EndFrustum.Start),0d,1d);
        var frustumRadius=Plan.EndFrustum.StartRadius+((Plan.EndFrustum.EndRadius-Plan.EndFrustum.StartRadius)*t);
        var frustum=double.Max(rho-frustumRadius,double.Max(Plan.EndFrustum.Start-x,x-Plan.EndFrustum.End));
        return double.Min(double.Min(head,root),double.Min(cylinder,frustum));
    }

    public ContinuumBoundsClassification ClassifyBounds(BoundingBox3D bounds,double tolerance=1e-9d)
    {
        if(bounds.Max.X<Bounds.Min.X||bounds.Min.X>Bounds.Max.X||bounds.Max.Y<Bounds.Min.Y||bounds.Min.Y>Bounds.Max.Y||bounds.Max.Z<Bounds.Min.Z||bounds.Min.Z>Bounds.Max.Z)return ContinuumBoundsClassification.Outside;
        Point3D[] world=[new(bounds.Min.X,bounds.Min.Y,bounds.Min.Z),new(bounds.Max.X,bounds.Min.Y,bounds.Min.Z),new(bounds.Min.X,bounds.Max.Y,bounds.Min.Z),new(bounds.Max.X,bounds.Max.Y,bounds.Min.Z),new(bounds.Min.X,bounds.Min.Y,bounds.Max.Z),new(bounds.Max.X,bounds.Min.Y,bounds.Max.Z),new(bounds.Min.X,bounds.Max.Y,bounds.Max.Z),new(bounds.Max.X,bounds.Max.Y,bounds.Max.Z)];
        var local=world.Select(_inverse.Apply).ToArray();var minX=local.Min(p=>p.X);var maxX=local.Max(p=>p.X);var minY=local.Min(p=>p.Y);var maxY=local.Max(p=>p.Y);var minZ=local.Min(p=>p.Z);var maxZ=local.Max(p=>p.Z);
        var dy=minY<=0&&maxY>=0?0d:double.Min(double.Abs(minY),double.Abs(maxY));var dz=minZ<=0&&maxZ>=0?0d:double.Min(double.Abs(minZ),double.Abs(maxZ));var minRho=double.Sqrt(dy*dy+dz*dz);
        var maxRho=local.Max(p=>double.Sqrt(p.Y*p.Y+p.Z*p.Z));var maximumProfile=0d;
        if(maxX>=Plan.TopCap.Position&&minX<=Plan.Prism.Start)maximumProfile=double.Max(maximumProfile,Plan.Prism.Circumradius);
        if(maxX>=Plan.RootBlend.Start&&minX<=Plan.RootBlend.End)maximumProfile=double.Max(maximumProfile,Plan.RootBlend.ShoulderRadius);
        if(maxX>=Plan.Cylinder.Start&&minX<=Plan.Cylinder.End)maximumProfile=double.Max(maximumProfile,Plan.Cylinder.Radius);
        if(maxX>=Plan.EndFrustum.Start&&minX<=Plan.EndFrustum.End)maximumProfile=double.Max(maximumProfile,Plan.EndFrustum.StartRadius);
        if(maximumProfile==0d||minRho>maximumProfile+tolerance)return ContinuumBoundsClassification.Outside;
        var polygonInside=local.All(p=>RegularPolygonField(p.Y,p.Z,Plan.Prism)<-tolerance);
        if(minX>=double.Min(Plan.Prism.Start,Plan.Prism.End)&&maxX<=double.Max(Plan.Prism.Start,Plan.Prism.End)&&polygonInside)return ContinuumBoundsClassification.Inside;
        if(minX>=Plan.TopCap.Position&&maxX<=Plan.Prism.End&&polygonInside&&maxRho<(minX-Plan.ConePlanarTrim.Apex)*double.Tan(Plan.ConePlanarTrim.SemiAngleDegrees*double.Pi/180d)-tolerance)return ContinuumBoundsClassification.Inside;
        if(minX>=Plan.RootBlend.Start&&maxX<=Plan.RootBlend.End){var r=Plan.RootBlend.Radius;double Profile(double x){var dx=x-Plan.RootBlend.End;return Plan.RootBlend.ShoulderRadius-double.Sqrt(double.Max(0,(r*r)-(dx*dx)));}if(maxRho<double.Min(Profile(minX),Profile(maxX))-tolerance)return ContinuumBoundsClassification.Inside;}
        if(minX>=Plan.Cylinder.Start&&maxX<=Plan.Cylinder.End&&maxRho<Plan.Cylinder.Radius-tolerance)return ContinuumBoundsClassification.Inside;
        if(minX>=Plan.EndFrustum.Start&&maxX<=Plan.EndFrustum.End){var t=(maxX-Plan.EndFrustum.Start)/(Plan.EndFrustum.End-Plan.EndFrustum.Start);var radius=Plan.EndFrustum.StartRadius+((Plan.EndFrustum.EndRadius-Plan.EndFrustum.StartRadius)*t);if(maxRho<radius-tolerance)return ContinuumBoundsClassification.Inside;}
        return ContinuumBoundsClassification.Cut;
    }

    private static double RegularPolygonField(double y,double z,RegularPrismConstruction prism)
    {
        var step=2d*double.Pi/prism.SideCount; var orientation=prism.OrientationDegrees*double.Pi/180d;
        var maximum=double.NegativeInfinity;
        for(var i=0;i<prism.SideCount;i++)
        {
            var angle=orientation+(step*.5d)+(i*step);
            maximum=double.Max(maximum,(y*double.Cos(angle))+(z*double.Sin(angle))-prism.Apothem);
        }
        return maximum;
    }

    private double ComputeReferenceVolume()
    {
        var polygonArea=Plan.Prism.SideCount*Plan.Prism.Apothem*Plan.Prism.Apothem*double.Tan(double.Pi/Plan.Prism.SideCount);
        var prism=polygonArea*double.Abs(Plan.Prism.Start-Plan.Prism.End);
        var cone=Simpson(Plan.TopCap.Position,Plan.Prism.End,512,x=>PolarArea((x-Plan.ConePlanarTrim.Apex)*double.Tan(Plan.ConePlanarTrim.SemiAngleDegrees*double.Pi/180d)));
        var root=Simpson(Plan.RootBlend.Start,Plan.RootBlend.End,2048,x=>{var r=Plan.RootBlend.Radius;var dx=x-Plan.RootBlend.End;var profile=Plan.RootBlend.ShoulderRadius-double.Sqrt(double.Max(0,(r*r)-(dx*dx)));return double.Pi*profile*profile;});
        var cylinder=double.Pi*Plan.Cylinder.Radius*Plan.Cylinder.Radius*(Plan.Cylinder.End-Plan.Cylinder.Start);
        var h=Plan.EndFrustum.End-Plan.EndFrustum.Start;var frustum=double.Pi*h*(Plan.EndFrustum.StartRadius*Plan.EndFrustum.StartRadius+Plan.EndFrustum.StartRadius*Plan.EndFrustum.EndRadius+Plan.EndFrustum.EndRadius*Plan.EndFrustum.EndRadius)/3d;
        return prism+cone+root+cylinder+frustum;
    }

    private double ComputeReferenceArea()
    {
        var n=Plan.Prism.SideCount;var step=2d*double.Pi/n;var orientation=Plan.Prism.OrientationDegrees*double.Pi/180d;var prismSides=0d;
        for(var side=0;side<n;side++)
        {var a=orientation+(side*step);var b=a+step;prismSides+=Simpson(0,1,512,t=>{var y=Plan.Prism.Circumradius*((1-t)*double.Cos(a)+t*double.Cos(b));var z=Plan.Prism.Circumradius*((1-t)*double.Sin(a)+t*double.Sin(b));var rho=double.Sqrt(y*y+z*z);var coneX=Plan.ConePlanarTrim.Apex+rho/double.Tan(Plan.ConePlanarTrim.SemiAngleDegrees*double.Pi/180d);return double.Abs(Plan.Prism.Start-coneX)*(2d*Plan.Prism.Circumradius*double.Sin(step*.5d));});}
        var k=double.Tan(Plan.ConePlanarTrim.SemiAngleDegrees*double.Pi/180d);var cap=Plan.TopCap.Radius;
        var cone=Simpson(0,2d*double.Pi,4096,theta=>{var limit=PolygonRadius(theta);return .5d*double.Sqrt(1d+k*k)/k*double.Max(0d,(limit*limit)-(cap*cap));});
        var polygonArea=n*Plan.Prism.Apothem*Plan.Prism.Apothem*double.Tan(double.Pi/n);
        var shoulder=polygonArea-(double.Pi*Plan.RootBlend.ShoulderRadius*Plan.RootBlend.ShoulderRadius);
        var torus=(double.Pi*double.Pi*Plan.RootBlend.ShoulderRadius*Plan.RootBlend.Radius)-(2d*double.Pi*Plan.RootBlend.Radius*Plan.RootBlend.Radius);
        var cylinder=2d*double.Pi*Plan.Cylinder.Radius*(Plan.Cylinder.End-Plan.Cylinder.Start);
        var h=Plan.EndFrustum.End-Plan.EndFrustum.Start;var slant=double.Sqrt(h*h+double.Pow(Plan.EndFrustum.StartRadius-Plan.EndFrustum.EndRadius,2));var frustum=double.Pi*(Plan.EndFrustum.StartRadius+Plan.EndFrustum.EndRadius)*slant;
        return prismSides+cone+shoulder+torus+cylinder+frustum+(double.Pi*cap*cap)+(double.Pi*Plan.EndCap.Radius*Plan.EndCap.Radius);
    }

    private double PolarArea(double radius)=>.5d*Simpson(0,2d*double.Pi,2048,theta=>double.Pow(double.Min(radius,PolygonRadius(theta)),2));
    private double PolygonRadius(double theta)
    {var step=2d*double.Pi/Plan.Prism.SideCount;var orientation=Plan.Prism.OrientationDegrees*double.Pi/180d;var best=double.PositiveInfinity;for(var i=0;i<Plan.Prism.SideCount;i++){var normal=orientation+(step*.5d)+(i*step);var cosine=double.Cos(theta-normal);if(cosine>1e-14d)best=double.Min(best,Plan.Prism.Apothem/cosine);}return best;}
    private static double Simpson(double a,double b,int intervals,Func<double,double> f)
    {if((intervals&1)!=0)intervals++;var h=(b-a)/intervals;var sum=f(a)+f(b);for(var i=1;i<intervals;i++)sum+=(i%2==0?2d:4d)*f(a+(i*h));return sum*h/3d;}

    private static BoundingBox3D TransformBounds(BoundingBox3D b,Transform3D t)
    {
        Point3D[] p=[new(b.Min.X,b.Min.Y,b.Min.Z),new(b.Max.X,b.Min.Y,b.Min.Z),new(b.Min.X,b.Max.Y,b.Min.Z),new(b.Max.X,b.Max.Y,b.Min.Z),new(b.Min.X,b.Min.Y,b.Max.Z),new(b.Max.X,b.Min.Y,b.Max.Z),new(b.Min.X,b.Max.Y,b.Max.Z),new(b.Max.X,b.Max.Y,b.Max.Z)];
        var q=p.Select(t.Apply).ToArray();return new(new(q.Min(v=>v.X),q.Min(v=>v.Y),q.Min(v=>v.Z)),new(q.Max(v=>v.X),q.Max(v=>v.Y),q.Max(v=>v.Z)));
    }
}

public sealed record ExactCoaxialDualLowering(
    ExactCoaxialConstructionPlan Plan,
    ExactConstructionResult Brep,
    ExactCoaxialContinuumRegion Continuum,
    string ConstructionSourceIdentity);

public static class ExactCoaxialDualMaterializer
{
    public static ExactCoaxialDualLowering Materialize(ExactCoaxialConstructionPlan plan,Transform3D transform)
    {
        var brep=ExactConstructionMaterializer.Materialize(plan);
        if(!brep.IsSuccess) throw new InvalidOperationException(string.Join("; ",brep.Diagnostics.Select(d=>d.Message)));
        var continuum=new ExactCoaxialContinuumRegion(plan,transform);
        return new(plan,brep.Value,continuum,continuum.ConstructionSourceIdentity);
    }
}
