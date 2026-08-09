using System.Globalization;
using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Lattice;
using Aetheris.FEA.Analysis;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FEA.Mechanics;

public sealed class MechanicsBoundaryLoweringException(string code,string message):Exception(message)
{
    public string Code { get; }=code;
}

public static class MechanicsBoundaryQuadrature
{
    public static MechanicsBoundaryQuadraturePlan Create(PlanarBoundaryDomain domain,SemanticRegionBinding binding,IReadOnlyList<ContinuumCell> cells)
    {
        if(double.Abs(domain.U.Length-1)>1e-10||double.Abs(domain.V.Length-1)>1e-10||double.Abs(domain.OutwardNormal.Length-1)>1e-10||double.Abs(domain.U.Dot(domain.V))>1e-10||double.Abs(domain.U.Dot(domain.OutwardNormal))>1e-10||double.Abs(domain.V.Dot(domain.OutwardNormal))>1e-10)
            throw new MechanicsBoundaryLoweringException("fea-invalid-face-local-frame","Exact planar face did not provide a finite orthonormal (U,V,N) frame.");
        if(string.IsNullOrWhiteSpace(domain.MaterialSideEvidence))throw new MechanicsBoundaryLoweringException("fea-material-side-ambiguity","Exact planar face has no CIR/BRep material-side evidence for its outward normal.");
        if(domain.ExactArea<=0)throw new MechanicsBoundaryLoweringException("fea-projected-trim-invalid","Exact planar trim has non-positive selected area.");
        var outer=domain.OuterLoop.Select(p=>domain.Evaluate(p.U,p.V)).ToArray();
        var holes=domain.InnerLoops.Select(loop=>(IReadOnlyList<Point3D>)loop.Select(p=>domain.Evaluate(p.U,p.V)).ToArray()).ToArray();
        if(!PlanarPolygonTriangulator.TryTriangulateWithHoles(outer,holes,domain.OutwardNormal,out var domainPoints,out var triangleIndices,out var failure))
            throw new MechanicsBoundaryLoweringException("fea-projected-trim-invalid",$"Exact planar trim could not be deterministically decomposed ({failure}).");
        var exactArea=0d;var exactCentroidMoment=Vector3D.Zero;
        for(var triangle=0;triangle<triangleIndices.Count;triangle+=3)
        {
            var a=domainPoints[triangleIndices[triangle]];var b=domainPoints[triangleIndices[triangle+1]];var c=domainPoints[triangleIndices[triangle+2]];var area=(b-a).Cross(c-a).Length*.5;
            exactArea+=area;exactCentroidMoment+=new Vector3D((a.X+b.X+c.X)/3,(a.Y+b.Y+c.Y)/3,(a.Z+b.Z+c.Z)/3)*area;
        }
        if(double.Abs(exactArea-domain.ExactArea)>double.Max(1,domain.ExactArea)*1e-10)throw new MechanicsBoundaryLoweringException("fea-projected-trim-invalid",$"Planar trim decomposition area {exactArea:R} does not match exact loop area {domain.ExactArea:R}.");
        var exactCentroidVector=exactCentroidMoment/exactArea;var exactCentroid=new Point3D(exactCentroidVector.X,exactCentroidVector.Y,exactCentroidVector.Z);
        var fragments=new List<MechanicsBoundaryFragment>();
        foreach(var cell in cells.OrderBy(c=>c.Index.K).ThenBy(c=>c.Index.J).ThenBy(c=>c.Index.I))
        {
            for(var triangle=0;triangle<triangleIndices.Count;triangle+=3)
            {
                var uv=new List<(double U,double V)>(3);
                for(var corner=0;corner<3;corner++)
                {
                    var offset=domainPoints[triangleIndices[triangle+corner]]-domain.Origin;
                    uv.Add((offset.Dot(domain.U),offset.Dot(domain.V)));
                }
                uv=ClipToCell(uv,domain,cell.Bounds);
                if(uv.Count<3)continue;
                var world=uv.Select(p=>domain.Evaluate(p.U,p.V)).ToArray();var area=Area(uv);
                var scale=(cell.Bounds.Max-cell.Bounds.Min).Length;if(area<=double.Max(1,scale*scale)*1e-14)continue;
                var points=TriangleRule(world,cell.Bounds,domain.OutwardNormal);var key=Signature(world);
                fragments.Add(new(cell.Index,world,points,area,key));
            }
        }
        // Coincident lattice-plane fragments are produced by both adjacent cells. Keep one deterministic owner.
        fragments=fragments.GroupBy(item=>item.OwnershipKey,StringComparer.Ordinal).Select(group=>group.OrderBy(item=>item.Cell.K).ThenBy(item=>item.Cell.J).ThenBy(item=>item.Cell.I).First()).OrderBy(item=>item.Cell.K).ThenBy(item=>item.Cell.J).ThenBy(item=>item.Cell.I).ToList();
        if(fragments.Count==0)throw new MechanicsBoundaryLoweringException("fea-no-owned-boundary-fragment","The exact selected planar face has no positive-area fragment in the admitted mechanics cells.");
        var frame=new BoundaryLocalFrame(domain.Origin,domain.OutwardNormal,domain.U,domain.V);
        return new(binding.Path,domain.Boundary.SourceId,domain.Boundary.ExactBrepFaceId,frame,domain.ExactArea,exactCentroid,fragments,"exact trim triangulation + positive-area cell partition; coincident polygon -> lexicographically smaller (K,J,I)",domain.MaterialSideEvidence,binding.Provenance);
    }

    private static List<(double U,double V)> ClipToCell(List<(double U,double V)> polygon,PlanarBoundaryDomain face,BoundingBox3D cell)
    {
        var inequalities=new (double A,double B,double C)[]
        {
            ( face.U.X, face.V.X, cell.Max.X-face.Origin.X),(-face.U.X,-face.V.X,-cell.Min.X+face.Origin.X),
            ( face.U.Y, face.V.Y, cell.Max.Y-face.Origin.Y),(-face.U.Y,-face.V.Y,-cell.Min.Y+face.Origin.Y),
            ( face.U.Z, face.V.Z, cell.Max.Z-face.Origin.Z),(-face.U.Z,-face.V.Z,-cell.Min.Z+face.Origin.Z),
        };
        foreach(var inequality in inequalities){polygon=Clip(polygon,inequality.A,inequality.B,inequality.C);if(polygon.Count<3)break;}
        return polygon;
    }

    private static List<(double U,double V)> Clip(IReadOnlyList<(double U,double V)> source,double a,double b,double c)
    {
        const double tolerance=1e-12;var output=new List<(double U,double V)>();
        for(var i=0;i<source.Count;i++)
        {
            var p=source[i];var q=source[(i+1)%source.Count];var dp=c-(a*p.U+b*p.V);var dq=c-(a*q.U+b*q.V);var pin=dp>=-tolerance;var qin=dq>=-tolerance;
            if(pin)Add(output,p);
            if(pin!=qin){var t=dp/(dp-dq);Add(output,(p.U+(q.U-p.U)*t,p.V+(q.V-p.V)*t));}
        }
        if(output.Count>1&&Distance(output[0],output[^1])<tolerance)output.RemoveAt(output.Count-1);return output;
    }

    private static IReadOnlyList<MechanicsBoundaryQuadraturePoint> TriangleRule(IReadOnlyList<Point3D> polygon,BoundingBox3D cell,Vector3D normal)
    {
        var result=new List<MechanicsBoundaryQuadraturePoint>();var a=polygon[0];
        for(var i=1;i<polygon.Count-1;i++)
        {
            var b=polygon[i];var c=polygon[i+1];var area=(b-a).Cross(c-a).Length*.5;
            // Degree-three four-point triangle rule; exactly integrates restricted trilinear Q1 functions.
            AddPoint(1d/3,1d/3,1d/3,-27d/48d);AddPoint(.6,.2,.2,25d/48d);AddPoint(.2,.6,.2,25d/48d);AddPoint(.2,.2,.6,25d/48d);
            void AddPoint(double la,double lb,double lc,double weight){var p=new Point3D(la*a.X+lb*b.X+lc*c.X,la*a.Y+lb*b.Y+lc*c.Y,la*a.Z+lb*b.Z+lc*c.Z);result.Add(new(p,area*weight,normal,Shape(cell,p)));}
        }
        return result;
    }

    private static double[] Shape(BoundingBox3D b,Point3D p)
    {
        var xi=2*(p.X-b.Min.X)/(b.Max.X-b.Min.X)-1;var eta=2*(p.Y-b.Min.Y)/(b.Max.Y-b.Min.Y)-1;var zeta=2*(p.Z-b.Min.Z)/(b.Max.Z-b.Min.Z)-1;
        (double X,double Y,double Z)[] s=[(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)];return s.Select(v=>.125*(1+v.X*xi)*(1+v.Y*eta)*(1+v.Z*zeta)).ToArray();
    }
    private static double Area(IReadOnlyList<(double U,double V)> p){var a=0d;for(var i=0;i<p.Count;i++){var n=p[(i+1)%p.Count];a+=p[i].U*n.V-n.U*p[i].V;}return double.Abs(a)*.5;}
    private static void Add(List<(double U,double V)> values,(double U,double V) value){if(values.Count==0||Distance(values[^1],value)>1e-12)values.Add(value);}
    private static double Distance((double U,double V) a,(double U,double V)b)=>double.Sqrt((a.U-b.U)*(a.U-b.U)+(a.V-b.V)*(a.V-b.V));
    private static string Signature(IEnumerable<Point3D> points)=>string.Join(";",points.Select(p=>$"{double.Round(p.X,12).ToString("R",CultureInfo.InvariantCulture)},{double.Round(p.Y,12).ToString("R",CultureInfo.InvariantCulture)},{double.Round(p.Z,12).ToString("R",CultureInfo.InvariantCulture)}").Order(StringComparer.Ordinal));
}
