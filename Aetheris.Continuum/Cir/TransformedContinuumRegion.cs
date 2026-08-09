using Aetheris.Continuum.Boundaries;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Cir;

/// <summary>Rigid orientation policy over an existing exact CIR and its semantic planar faces.</summary>
public sealed class TransformedContinuumRegion : IContinuumRegion, IBoundsClassificationCapability, IPlanarBoundaryDomainCapability
{
    private readonly Transform3D inverse;
    private readonly IPlanarBoundaryDomainCapability planar;
    public TransformedContinuumRegion(IContinuumRegion source,Transform3D transform)
    {
        Source=source;Transform=transform;inverse=transform.Inverse();planar=source as IPlanarBoundaryDomainCapability??throw new ArgumentException("Source has no planar semantic-boundary capability.",nameof(source));
        Id=new(source.Id+":transformed");var corners=Corners(source.Bounds).Select(transform.Apply).ToArray();Bounds=new(new(corners.Min(p=>p.X),corners.Min(p=>p.Y),corners.Min(p=>p.Z)),new(corners.Max(p=>p.X),corners.Max(p=>p.Y),corners.Max(p=>p.Z)));
    }
    public IContinuumRegion Source{get;} public Transform3D Transform{get;} public RegionId Id{get;} public BoundingBox3D Bounds{get;}
    public ContinuumPointClassification Classify(Point3D point,double tolerance=1e-9)=>Source.Classify(inverse.Apply(point),tolerance);
    public ContinuumBoundsClassification ClassifyBounds(BoundingBox3D bounds,double tolerance=1e-9)
    {
        if(bounds.Max.X<Bounds.Min.X-tolerance||bounds.Min.X>Bounds.Max.X+tolerance||bounds.Max.Y<Bounds.Min.Y-tolerance||bounds.Min.Y>Bounds.Max.Y+tolerance||bounds.Max.Z<Bounds.Min.Z-tolerance||bounds.Min.Z>Bounds.Max.Z+tolerance)return ContinuumBoundsClassification.Outside;
        var classifications=Corners(bounds).Select(point=>Source.Classify(inverse.Apply(point),tolerance)).ToArray();
        if(Source.GetType().Name is "AxisAlignedBoxRegion" or "ExactBrepBoxContinuumRegion"&&classifications.All(item=>item!=ContinuumPointClassification.Outside))return ContinuumBoundsClassification.Inside;
        return ContinuumBoundsClassification.Cut;
    }
    public bool TryResolvePlanarBoundary(string path,string? faceId,out PlanarBoundaryDomain domain)
    {
        if(!planar.TryResolvePlanarBoundary(path,faceId,out var local)){domain=null!;return false;}
        var u=Transform.Apply(local.U);var v=Transform.Apply(local.V);var n=Transform.Apply(local.OutwardNormal);u.TryNormalize(out u);v.TryNormalize(out v);n.TryNormalize(out n);
        domain=local with{Origin=Transform.Apply(local.Origin),U=u,V=v,OutwardNormal=n,MaterialSideEvidence=local.MaterialSideEvidence+"; rigid transform preserved"};return true;
    }
    private static Point3D[] Corners(BoundingBox3D b)=>[new(b.Min.X,b.Min.Y,b.Min.Z),new(b.Max.X,b.Min.Y,b.Min.Z),new(b.Min.X,b.Max.Y,b.Min.Z),new(b.Max.X,b.Max.Y,b.Min.Z),new(b.Min.X,b.Min.Y,b.Max.Z),new(b.Max.X,b.Min.Y,b.Max.Z),new(b.Min.X,b.Max.Y,b.Max.Z),new(b.Max.X,b.Max.Y,b.Max.Z)];
}
