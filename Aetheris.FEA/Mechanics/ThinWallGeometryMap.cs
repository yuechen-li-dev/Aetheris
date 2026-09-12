using Aetheris.FEA.Analysis;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FEA.Mechanics;

public readonly record struct ThinWallMapEvaluation(
    Point3D Position,
    Vector3D DerivativeR,
    Vector3D DerivativeS,
    Vector3D DerivativeT);

/// <summary>M(r,s,t) = m(r,s) + t k n(r,s), with t in [-1,1].</summary>
public sealed class ThinWallGeometryMap
{
    private readonly ExperimentalShellSettings settings;
    private readonly BoundingBox3D masterDomain;
    private readonly double midZ;

    public ThinWallGeometryMap(ExperimentalShellSettings settings,BoundingBox3D masterDomain)
    {
        this.settings=settings;this.masterDomain=masterDomain;midZ=(masterDomain.Min.Z+masterDomain.Max.Z)/2;
        if(settings.GeometryMap==ThinWallGeometryMapKind.Cylindrical)
        {
            var expected=settings.CylinderRadiusMeters!.Value*settings.CylinderAngleRadians!.Value;
            var actual=masterDomain.Max.Y-masterDomain.Min.Y;
            if(double.Abs(expected-actual)>double.Max(1e-12,expected*1e-9))
                throw new ThinWallMapException("thinwall-map-singular",$"Cylindrical master S span must equal radius times angle ({expected:R} m); got {actual:R} m.");
        }
    }

    public ThinWallMapEvaluation Evaluate(double r,double s,double t)
    {
        var half=settings.ThicknessMeters/2;
        if(settings.GeometryMap==ThinWallGeometryMapKind.Flat)
            return new(new(r,s,midZ+t*half),new(1,0,0),new(0,1,0),new(0,0,half));

        var radius=settings.CylinderRadiusMeters!.Value;
        var theta=(s-masterDomain.Min.Y)/radius;
        var sin=double.Sin(theta);var cos=double.Cos(theta);var current=radius+t*half;
        return new(
            new(r,current*sin,current*cos),
            new(1,0,0),
            new(0,current*cos/radius,-current*sin/radius),
            new(0,half*sin,half*cos));
    }

    public static double ElementJacobianDeterminant(ThinWallMapEvaluation value,double cellSpanR,double cellSpanS)
    {
        var dr=value.DerivativeR*(cellSpanR/2);var ds=value.DerivativeS*(cellSpanS/2);var dt=value.DerivativeT;
        return dr.Dot(ds.Cross(dt));
    }
}

public sealed class ThinWallMapException(string code,string message):Exception(message)
{
    public string Code { get; }=code;
}
