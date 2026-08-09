using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Continuum.Boundaries;

internal readonly record struct ExactSupportRayHit(
    double Parameter,
    Point3D Point,
    Vector3D SupportNormal,
    FaceId FaceId,
    SurfaceGeometryKind SupportKind);

/// <summary>
/// Deterministic bounded ray intersections for the M4C support family. Torus intersections use
/// derivative-partitioned quartic root isolation, so two visible branches are retained without
/// assuming that the torus is one height map.
/// </summary>
internal static class ExactSupportRayIntersections
{
    public static IReadOnlyList<ExactSupportRayHit> Intersect(WholeShellBoundaryQuery shell,
        WholeShellBoundaryCandidate face, Point3D worldOrigin, Vector3D worldDirection,
        double minimumParameter, double maximumParameter)
    {
        var inverse=shell.Transform.Inverse();var origin=inverse.Apply(worldOrigin);var direction=inverse.Apply(worldDirection);
        if(!direction.TryNormalize(out direction))throw new InvalidOperationException("Composite ray direction is degenerate.");
        var surface=shell.Body.GetFaceSurface(face.FaceId);var coefficients=Coefficients(surface,origin,direction);
        var roots=RealPolynomialRoots.InInterval(coefficients,minimumParameter,maximumParameter);
        return roots.Select(t=>
        {
            var point=worldOrigin+(worldDirection*t);
            var normal=ExactSupportBoundaryQuery.ExactSupportNormal(shell.Body,face.FaceId,point,shell.Transform);
            return new ExactSupportRayHit(t,point,normal,face.FaceId,face.SupportKind);
        }).ToArray();
    }

    private static double[] Coefficients(SurfaceGeometry surface,Point3D origin,Vector3D direction)=>surface.Kind switch
    {
        SurfaceGeometryKind.Plane=>Plane(surface.Plane!.Value,origin,direction),
        SurfaceGeometryKind.Cylinder=>Cylinder(surface.Cylinder!.Value,origin,direction),
        SurfaceGeometryKind.Cone=>Cone(surface.Cone!.Value,origin,direction),
        SurfaceGeometryKind.Torus=>Torus(surface.Torus!.Value,origin,direction),
        _=>throw new NotSupportedException($"Exact composite rays do not support {surface.Kind}.")
    };

    private static double[] Plane(PlaneSurface s,Point3D o,Vector3D d)
    {var n=s.Normal.ToVector();return[(o-s.Origin).Dot(n),d.Dot(n)];}

    private static double[] Cylinder(CylinderSurface s,Point3D o,Vector3D d)
    {
        var axis=s.Axis.ToVector();var delta=o-s.Origin;
        var r0=delta-(axis*delta.Dot(axis));var rd=d-(axis*d.Dot(axis));
        return[r0.LengthSquared-(s.Radius*s.Radius),2d*r0.Dot(rd),rd.LengthSquared];
    }

    private static double[] Cone(ConeSurface s,Point3D o,Vector3D d)
    {
        var axis=s.Axis.ToVector();var delta=o-s.Apex;var a0=delta.Dot(axis);var ad=d.Dot(axis);
        var r0=delta-(axis*a0);var rd=d-(axis*ad);var tangent=double.Tan(s.SemiAngleRadians);var t2=tangent*tangent;
        return[r0.LengthSquared-(t2*a0*a0),2d*(r0.Dot(rd)-(t2*a0*ad)),rd.LengthSquared-(t2*ad*ad)];
    }

    private static double[] Torus(TorusSurface s,Point3D o,Vector3D d)
    {
        var q=o-s.Center;var axis=s.Axis.ToVector();var qd=q.Dot(d);var qa=q.Dot(axis);var da=d.Dot(axis);
        var radiusTerm=(s.MajorRadius*s.MajorRadius)-(s.MinorRadius*s.MinorRadius);
        var a0=q.LengthSquared+radiusTerm;var a1=2d*qd;var a2=d.LengthSquared;
        var b0=q.LengthSquared-(qa*qa);var b1=2d*(qd-(qa*da));var b2=d.LengthSquared-(da*da);
        return[
            (a0*a0)-(4d*s.MajorRadius*s.MajorRadius*b0),
            2d*a0*a1-(4d*s.MajorRadius*s.MajorRadius*b1),
            (a1*a1)+(2d*a0*a2)-(4d*s.MajorRadius*s.MajorRadius*b2),
            2d*a1*a2,
            a2*a2];
    }
}

internal static class RealPolynomialRoots
{
    public static IReadOnlyList<double> InInterval(IReadOnlyList<double> ascending,double minimum,double maximum)
    {
        if(!(minimum<=maximum))throw new ArgumentOutOfRangeException(nameof(minimum));
        var coefficients=Trim(ascending.ToArray());var roots=Find(coefficients,minimum,maximum);
        var scale=double.Max(1d,maximum-minimum);var tolerance=scale*2e-10d;
        return roots.Where(x=>x>=minimum-tolerance&&x<=maximum+tolerance).Select(x=>double.Clamp(x,minimum,maximum))
            .Order().Aggregate(new List<double>(),(list,value)=>{if(list.Count==0||double.Abs(value-list[^1])>tolerance)list.Add(value);return list;});
    }

    private static List<double> Find(double[] c,double minimum,double maximum)
    {
        var degree=c.Length-1;if(degree<=0)return[];
        if(degree==1)return double.Abs(c[1])<=1e-18d?[]:[-c[0]/c[1]];
        var derivative=new double[degree];for(var i=1;i<c.Length;i++)derivative[i-1]=i*c[i];
        var stationary=Find(Trim(derivative),minimum,maximum).Where(x=>x>minimum&&x<maximum).Order().ToArray();
        var points=new[]{minimum}.Concat(stationary).Concat(new[]{maximum}).ToArray();var roots=new List<double>();
        foreach(var point in points){var value=Evaluate(c,point);if(double.Abs(value)<=Tolerance(c,point))roots.Add(point);}
        for(var i=0;i<points.Length-1;i++)
        {
            var left=points[i];var right=points[i+1];var fl=Evaluate(c,left);var fr=Evaluate(c,right);
            if(fl==0d||fr==0d||double.Sign(fl)==double.Sign(fr))continue;
            for(var iteration=0;iteration<80;iteration++)
            {var mid=.5d*(left+right);var fm=Evaluate(c,mid);if(double.Abs(fm)<=Tolerance(c,mid)||right-left<=2e-12d*double.Max(1d,double.Abs(mid))){left=right=mid;break;}if(double.Sign(fl)==double.Sign(fm)){left=mid;fl=fm;}else{right=mid;fr=fm;}}
            roots.Add(.5d*(left+right));
        }
        return roots;
    }

    private static double[] Trim(double[] values)
    {var scale=values.Select(double.Abs).DefaultIfEmpty().Max();var n=values.Length;while(n>1&&double.Abs(values[n-1])<=double.Max(1e-18d,scale*1e-14d))n--;return values[..n];}
    private static double Evaluate(IReadOnlyList<double> c,double x){var value=0d;for(var i=c.Count-1;i>=0;i--)value=(value*x)+c[i];return value;}
    private static double Tolerance(IReadOnlyList<double> c,double x)
    {var scale=0d;var power=1d;foreach(var value in c){scale+=double.Abs(value)*power;power*=double.Max(1d,double.Abs(x));}return double.Max(1e-12d,scale*2e-11d);}
}
