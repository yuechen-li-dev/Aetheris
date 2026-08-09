using Aetheris.Kernel.Core.Math;
using Aetheris.Continuum.Cir;

namespace Aetheris.Continuum.Boundaries;

/// <summary>Exact planar semantic-face domain supplied by BRep/CIR-associated geometry.</summary>
public sealed record PlanarBoundaryDomain(
    BoundaryReference Boundary,
    Point3D Origin,
    Vector3D U,
    Vector3D V,
    Vector3D OutwardNormal,
    IReadOnlyList<(double U, double V)> OuterLoop,
    IReadOnlyList<IReadOnlyList<(double U, double V)>> InnerLoops,
    string MaterialSideEvidence)
{
    public Point3D Evaluate(double u,double v)=>Origin+(U*u)+(V*v);
    public (double U,double V) Project(Point3D point){var d=point-Origin;return(d.Dot(U),d.Dot(V));}
    public double ExactArea => double.Abs(SignedArea(OuterLoop))-InnerLoops.Sum(loop=>double.Abs(SignedArea(loop)));
    private static double SignedArea(IReadOnlyList<(double U,double V)> polygon){var area=0d;for(var i=0;i<polygon.Count;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Count];area+=(a.U*b.V)-(b.U*a.V);}return area*.5;}

    /// <summary>Validates and, when necessary, flips the support normal using occupied-material classification on both sides.</summary>
    public bool TryOrientFromMaterialSide(IContinuumRegion material,out PlanarBoundaryDomain oriented)
    {
        var scale=double.Sqrt(ExactArea);var epsilon=double.Max(scale*1e-6,1e-8);
        var candidates=new List<(double U,double V)>
        {
            (OuterLoop.Average(p=>p.U),OuterLoop.Average(p=>p.V))
        };
        for(var i=1;i<OuterLoop.Count-1;i++)candidates.Add(((OuterLoop[0].U+OuterLoop[i].U+OuterLoop[i+1].U)/3,(OuterLoop[0].V+OuterLoop[i].V+OuterLoop[i+1].V)/3));
        foreach(var uv in candidates)
        {
            var point=Evaluate(uv.U,uv.V);var plus=material.Classify(point+OutwardNormal*epsilon);var minus=material.Classify(point-OutwardNormal*epsilon);
            if(plus==ContinuumPointClassification.Outside&&minus!=ContinuumPointClassification.Outside)
            {
                oriented=this with{MaterialSideEvidence=$"CIR two-sided probe +N={plus}, -N={minus}, epsilon={epsilon:R}"};return true;
            }
            if(minus==ContinuumPointClassification.Outside&&plus!=ContinuumPointClassification.Outside)
            {
                oriented=this with{OutwardNormal=-OutwardNormal,MaterialSideEvidence=$"CIR two-sided probe flipped support normal: +N={plus}, -N={minus}, epsilon={epsilon:R}"};return true;
            }
        }
        oriented=this with{MaterialSideEvidence="CIR two-sided material probe was ambiguous"};return false;
    }
}

public interface IPlanarBoundaryDomainCapability
{
    bool TryResolvePlanarBoundary(string semanticPath,string? exactBrepFaceId,out PlanarBoundaryDomain domain);
}

public static class BoxPlanarBoundaryDomains
{
    public static IReadOnlyList<PlanarBoundaryDomain> Create(RegionId id,BoundingBox3D bounds,IReadOnlyDictionary<string,string>? faceIds=null)
    {
        var x=bounds.Max.X-bounds.Min.X;var y=bounds.Max.Y-bounds.Min.Y;var z=bounds.Max.Z-bounds.Min.Z;
        PlanarBoundaryDomain Face(string token,Point3D origin,Vector3D u,Vector3D v,Vector3D outward,double du,double dv)
        {
            string? faceId=null;faceIds?.TryGetValue(token,out faceId);var reference=new BoundaryReference("exact-analytic",$"{id}:{token}",faceId,token);
            return new(reference,origin,u,v,outward,[(0d,0d),(du,0d),(du,dv),(0d,dv)],[],"CIR probe: inward=-outward");
        }
        return
        [
            Face("x-min",new(bounds.Min.X,bounds.Min.Y,bounds.Min.Z),new(0,1,0),new(0,0,1),new(-1,0,0),y,z),
            Face("x-max",new(bounds.Max.X,bounds.Min.Y,bounds.Min.Z),new(0,1,0),new(0,0,1),new(1,0,0),y,z),
            Face("y-min",new(bounds.Min.X,bounds.Min.Y,bounds.Min.Z),new(1,0,0),new(0,0,1),new(0,-1,0),x,z),
            Face("y-max",new(bounds.Min.X,bounds.Max.Y,bounds.Min.Z),new(1,0,0),new(0,0,1),new(0,1,0),x,z),
            Face("z-min",new(bounds.Min.X,bounds.Min.Y,bounds.Min.Z),new(1,0,0),new(0,1,0),new(0,0,-1),x,y),
            Face("z-max",new(bounds.Min.X,bounds.Min.Y,bounds.Max.Z),new(1,0,0),new(0,1,0),new(0,0,1),x,y),
        ];
    }

    public static bool Resolve(IReadOnlyList<PlanarBoundaryDomain> domains,string path,string? faceId,out PlanarBoundaryDomain domain)
    {
        domain=domains.FirstOrDefault(item=>(faceId is not null&&item.Boundary.ExactBrepFaceId==faceId)||Matches(path,item.Boundary.SemanticRegion!))!;
        return domain is not null;
    }

    private static bool Matches(string path,string token)=>token switch
    {
        "x-min"=>path.Contains("-X",StringComparison.OrdinalIgnoreCase)||path.Contains("x-min",StringComparison.OrdinalIgnoreCase),
        "x-max"=>path.Contains("+X",StringComparison.OrdinalIgnoreCase)||path.Contains("x-max",StringComparison.OrdinalIgnoreCase),
        "y-min"=>path.Contains("-Y",StringComparison.OrdinalIgnoreCase)||path.Contains("y-min",StringComparison.OrdinalIgnoreCase),
        "y-max"=>path.Contains("+Y",StringComparison.OrdinalIgnoreCase)||path.Contains("y-max",StringComparison.OrdinalIgnoreCase),
        "z-min"=>path.Contains("-Z",StringComparison.OrdinalIgnoreCase)||path.Contains("z-min",StringComparison.OrdinalIgnoreCase),
        _=>path.Contains("+Z",StringComparison.OrdinalIgnoreCase)||path.Contains("z-max",StringComparison.OrdinalIgnoreCase),
    };
}
