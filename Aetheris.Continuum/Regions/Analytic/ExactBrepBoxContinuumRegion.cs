using Aetheris.Continuum.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Regions.Analytic;

/// <summary>An exact oriented-box CIR used with the corresponding exact BRep shell.</summary>
public sealed class ExactBrepBoxContinuumRegion : IContinuumRegion, ISignedDistanceCapability, IBoundsClassificationCapability
{
    private readonly Transform3D _inverse;
    private readonly Vector3D _half;
    private readonly Point3D[] _worldCorners;
    private readonly Vector3D[] _axes;

    public ExactBrepBoxContinuumRegion(RegionId id, double width, double height, double depth, Transform3D transform)
    {
        if (!(width > 0d && height > 0d && depth > 0d)) throw new ArgumentOutOfRangeException(nameof(width));
        Id=id; Width=width; Height=height; Depth=depth; Transform=transform; _inverse=transform.Inverse(); _half=new(width*.5d,height*.5d,depth*.5d);
        _worldCorners=LocalCorners().Select(transform.Apply).ToArray();
        _axes=[Unit(transform.Apply(new Vector3D(1,0,0))),Unit(transform.Apply(new Vector3D(0,1,0))),Unit(transform.Apply(new Vector3D(0,0,1)))];
        Bounds=BoundsOf(_worldCorners);
    }

    public RegionId Id { get; }
    public double Width { get; }
    public double Height { get; }
    public double Depth { get; }
    public Transform3D Transform { get; }
    public BoundingBox3D Bounds { get; }
    public double ExactVolume => Width*Height*Depth;
    public double ExactBoundaryArea => 2d*((Width*Height)+(Width*Depth)+(Height*Depth));

    public ContinuumPointClassification Classify(Point3D point,double tolerance=1e-9d)
    {
        var p=_inverse.Apply(point); var ax=double.Abs(p.X); var ay=double.Abs(p.Y); var az=double.Abs(p.Z);
        if(ax>_half.X+tolerance||ay>_half.Y+tolerance||az>_half.Z+tolerance) return ContinuumPointClassification.Outside;
        return double.Abs(ax-_half.X)<=tolerance||double.Abs(ay-_half.Y)<=tolerance||double.Abs(az-_half.Z)<=tolerance
            ? ContinuumPointClassification.Boundary : ContinuumPointClassification.Inside;
    }

    public double SignedDistance(Point3D point)
    {
        var p=_inverse.Apply(point); var q=new Vector3D(double.Abs(p.X)-_half.X,double.Abs(p.Y)-_half.Y,double.Abs(p.Z)-_half.Z);
        var outside=new Vector3D(double.Max(q.X,0d),double.Max(q.Y,0d),double.Max(q.Z,0d)).Length;
        return outside+double.Min(double.Max(q.X,double.Max(q.Y,q.Z)),0d);
    }

    public ContinuumBoundsClassification ClassifyBounds(BoundingBox3D bounds,double tolerance=1e-9d)
    {
        var world=Corners(bounds); var testAxes=new List<Vector3D>{new(1,0,0),new(0,1,0),new(0,0,1)};testAxes.AddRange(_axes);
        foreach(var a in new[]{new Vector3D(1,0,0),new Vector3D(0,1,0),new Vector3D(0,0,1)}) foreach(var b in _axes){var cross=a.Cross(b);if(cross.TryNormalize(out cross))testAxes.Add(cross);}
        if(testAxes.Any(axis=>Separated(world,_worldCorners,axis,tolerance))) return ContinuumBoundsClassification.Outside;
        var local=world.Select(_inverse.Apply).ToArray();
        return local.All(p=>double.Abs(p.X)<_half.X-tolerance&&double.Abs(p.Y)<_half.Y-tolerance&&double.Abs(p.Z)<_half.Z-tolerance)
            ? ContinuumBoundsClassification.Inside : ContinuumBoundsClassification.Cut;
    }

    private IEnumerable<Point3D> LocalCorners()=>Corners(new(new(-_half.X,-_half.Y,-_half.Z),new(_half.X,_half.Y,_half.Z)));
    private static Point3D[] Corners(BoundingBox3D b)=>[new(b.Min.X,b.Min.Y,b.Min.Z),new(b.Max.X,b.Min.Y,b.Min.Z),new(b.Min.X,b.Max.Y,b.Min.Z),new(b.Max.X,b.Max.Y,b.Min.Z),new(b.Min.X,b.Min.Y,b.Max.Z),new(b.Max.X,b.Min.Y,b.Max.Z),new(b.Min.X,b.Max.Y,b.Max.Z),new(b.Max.X,b.Max.Y,b.Max.Z)];
    private static BoundingBox3D BoundsOf(IEnumerable<Point3D> values){var p=values.ToArray();return new(new(p.Min(x=>x.X),p.Min(x=>x.Y),p.Min(x=>x.Z)),new(p.Max(x=>x.X),p.Max(x=>x.Y),p.Max(x=>x.Z)));}
    private static Vector3D Unit(Vector3D value){if(!value.TryNormalize(out value))throw new InvalidOperationException("Box transform collapsed an axis.");return value;}
    private static bool Separated(IReadOnlyList<Point3D> a,IReadOnlyList<Point3D> b,Vector3D axis,double tolerance)
    {var ap=a.Select(p=>new Vector3D(p.X,p.Y,p.Z).Dot(axis)).ToArray();var bp=b.Select(p=>new Vector3D(p.X,p.Y,p.Z).Dot(axis)).ToArray();return ap.Max()<bp.Min()-tolerance||bp.Max()<ap.Min()-tolerance;}
}
