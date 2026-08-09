using System.Diagnostics;

namespace Aetheris.Continuum.Experiments.Attention;

public interface IE1Preconditioner
{
    string Name { get; }
    int Size { get; }
    int UndirectedEdges { get; }
    double InteractionsPerUnknown { get; }
    long EstimatedStorageBytes { get; }
    long EstimatedFlopsPerApply { get; }
    double SetupMilliseconds { get; }
    void Apply(ReadOnlySpan<double> residual, Span<double> correction);
}

public abstract class E1PreconditionerBase(string name, int size) : IE1Preconditioner
{
    public string Name { get; }=name; public int Size { get; }=size;
    public virtual int UndirectedEdges=>0; public virtual double InteractionsPerUnknown=>2d*UndirectedEdges/Size;
    public virtual long EstimatedStorageBytes=>0; public abstract long EstimatedFlopsPerApply{get;}
    public virtual double SetupMilliseconds=>0d;
    public abstract void Apply(ReadOnlySpan<double> residual,Span<double> correction);
}

public sealed class E1IdentityPreconditioner(int size):E1PreconditionerBase("CG",size)
{
    public override long EstimatedFlopsPerApply=>0;
    public override void Apply(ReadOnlySpan<double>r,Span<double>z)=>r.CopyTo(z);
}

public sealed class E1JacobiPreconditioner:E1PreconditionerBase
{
    private readonly double[] inverse;
    public E1JacobiPreconditioner(HeterogeneousAnisotropicSystem s):base("Jacobi-PCG",s.UnknownCount){inverse=s.Diagonal.Select(x=>1d/x).ToArray();}
    public override long EstimatedStorageBytes=>8L*Size; public override long EstimatedFlopsPerApply=>Size;
    public override void Apply(ReadOnlySpan<double>r,Span<double>z){for(var i=0;i<Size;i++)z[i]=inverse[i]*r[i];}
}

public readonly record struct InteractionEdge(int A,int B,double RawWeight,double NormalizedWeight,string Reason);
public sealed record InteractionSample(string CellKind,int Cell,int I,int J,int K,ContinuumCellField Fields,IReadOnlyList<InteractionNeighborSample> Neighbors);
public sealed record InteractionNeighborSample(int Cell,int I,int J,int K,double Weight,double OperatorScore,string Reason);

/// <summary>
/// SPD compact inverse action D^-1/2 (I + beta S_G) D^-1/2. S_G is a
/// symmetrically degree-normalized weighted adjacency, so its spectrum is in
/// [-1,1]; 0&lt;beta&lt;1 therefore gives strictly positive energy.
/// </summary>
public sealed class SparseInteractionPreconditioner:E1PreconditionerBase
{
    private readonly double[] inverseSqrtDiagonal,degree,scaled; private readonly InteractionEdge[] edges; private readonly double beta;
    public SparseInteractionPreconditioner(string name,HeterogeneousAnisotropicSystem system,IReadOnlyList<(int A,int B,double Weight,string Reason)> selected,double beta=.8d,double setupMilliseconds=0d)
        :base(name,system.UnknownCount)
    {
        if(!(beta>=0d&&beta<1d))throw new ArgumentOutOfRangeException(nameof(beta));this.beta=beta;SetupMilliseconds=setupMilliseconds;
        inverseSqrtDiagonal=system.Diagonal.Select(x=>1d/Math.Sqrt(x)).ToArray();degree=new double[Size];scaled=new double[Size];
        foreach(var e in selected){if(!(e.Weight>0d)||e.A==e.B)throw new ArgumentException("Graph weights must be positive off-diagonal interactions.");degree[e.A]+=e.Weight;degree[e.B]+=e.Weight;}
        edges=selected.Select(e=>new InteractionEdge(e.A,e.B,e.Weight,e.Weight/Math.Sqrt(degree[e.A]*degree[e.B]),e.Reason)).ToArray();
    }
    public IReadOnlyList<InteractionEdge> Edges=>edges; public override int UndirectedEdges=>edges.Length;
    public override long EstimatedStorageBytes=>24L*Size+40L*edges.Length; public override long EstimatedFlopsPerApply=>3L*Size+6L*edges.Length;
    public override double SetupMilliseconds{get;}
    public override void Apply(ReadOnlySpan<double>r,Span<double>z)
    {
        for(var i=0;i<Size;i++){scaled[i]=inverseSqrtDiagonal[i]*r[i];z[i]=scaled[i];}
        foreach(var e in edges){var w=beta*e.NormalizedWeight;z[e.A]+=w*scaled[e.B];z[e.B]+=w*scaled[e.A];}
        for(var i=0;i<Size;i++)z[i]*=inverseSqrtDiagonal[i];
    }
}

public static class InteractionGraphBuilder
{
    private static readonly (int X,int Y,int Z)[] HalfOffsets = BuildOffsets();

    public static SparseInteractionPreconditioner Build(HeterogeneousAnisotropicSystem system,ContinuumFieldMask fields,bool fieldWeights,int edgeBudgetPerUnknown=8)
    {
        if(edgeBudgetPerUnknown<2||edgeBudgetPerUnknown%2!=0)throw new ArgumentOutOfRangeException(nameof(edgeBudgetPerUnknown));
        var sw=Stopwatch.StartNew();var n=system.PointsPerAxis;var candidates=new List<Candidate>(system.UnknownCount*HalfOffsets.Length);
        for(var k=0;k<n;k++)for(var j=0;j<n;j++)for(var i=0;i<n;i++)
        {
            var a=system.Flatten(i,j,k);
            foreach(var o in HalfOffsets){var ni=i+o.X;var nj=j+o.Y;var nk=k+o.Z;if((uint)ni>=(uint)n||(uint)nj>=(uint)n||(uint)nk>=(uint)n)continue;
                var b=system.Flatten(ni,nj,nk);var op=OperatorPathScore(system,a,b,o);var factors=fields==ContinuumFieldMask.None?(G:1d,M:1d,A:1d):FieldFactors(system,a,b,o);
                var score=op*SelectionMultiplier(fields,factors);var weight=op*(fieldWeights?WeightMultiplier(fields,factors):1d);
                candidates.Add(new(a,b,score,Math.Max(weight,1e-12),op,factors.G,factors.M,factors.A));}
        }
        candidates.Sort(static(x,y)=>{var c=y.Score.CompareTo(x.Score);if(c!=0)return c;c=x.A.CompareTo(y.A);return c!=0?c:x.B.CompareTo(y.B);});
        var target=checked(system.UnknownCount*edgeBudgetPerUnknown/2);var localDegreeLimit=edgeBudgetPerUnknown+4;var degree=new byte[system.UnknownCount];var chosen=new List<(int,int,double,string)>(target);
        foreach(var e in candidates){if(chosen.Count==target)break;if(degree[e.A]>=localDegreeLimit||degree[e.B]>=localDegreeLimit)continue;chosen.Add((e.A,e.B,e.Weight,Reason(fields,e.OperatorScore,(e.G,e.M,e.Authority))));degree[e.A]++;degree[e.B]++;}
        if(chosen.Count!=target)throw new InvalidOperationException($"Could not fill exact {edgeBudgetPerUnknown}-interaction budget: {chosen.Count}/{target} undirected edges.");
        sw.Stop();var label=fields==ContinuumFieldMask.None?"coefficient-only":fields.ToString().Replace(", ","+");
        return new SparseInteractionPreconditioner($"graph-{label}-{(fieldWeights?"selection+weight":"selection-only")}-b{edgeBudgetPerUnknown}",system,chosen,.8d,sw.Elapsed.TotalMilliseconds);
    }

    public static SparseInteractionPreconditioner BuildE0CompactControl(HeterogeneousAnisotropicSystem system)
    {
        var sw=Stopwatch.StartNew();var edges=system.Edges.Select(e=>(e.A,e.B,1d,"E0 one-edge lattice adjacency; variable-D symmetric normalization")).ToArray();sw.Stop();
        return new SparseInteractionPreconditioner("E0-compact-symmetric(beta=0.12)",system,edges,.12d,sw.Elapsed.TotalMilliseconds);
    }

    public static IReadOnlyList<InteractionSample> Sample(HeterogeneousAnisotropicSystem s,SparseInteractionPreconditioner p)
    {
        var n=s.PointsPerAxis;var cells=new[]{("homogeneous-interior",s.Flatten(n/4,n/2,n/2)),("material-interface",Nearest(s,f=>Math.Abs(f.InterfaceSignedDistance))),
            ("authority-localized",Nearest(s,f=>Math.Abs(f.MaterialConfidence-.95d))),("anisotropic-region",s.Flatten(3*n/4,n/2,n/2))};
        return cells.Select(c=>{var xyz=s.Coordinates(c.Item2);var neighbors=p.Edges.Where(e=>e.A==c.Item2||e.B==c.Item2).Select(e=>{var q=e.A==c.Item2?e.B:e.A;var qx=s.Coordinates(q);var d=(qx.I-xyz.I,qx.J-xyz.J,qx.K-xyz.K);return new InteractionNeighborSample(q,qx.I,qx.J,qx.K,e.NormalizedWeight,OperatorPathScore(s,c.Item2,q,d),e.Reason);}).OrderByDescending(x=>Math.Abs(x.Weight)).ToArray();return new InteractionSample(c.Item1,c.Item2,xyz.I,xyz.J,xyz.K,s.Fields[c.Item2],neighbors);}).ToArray();
    }

    private static int Nearest(HeterogeneousAnisotropicSystem s,Func<ContinuumCellField,double> score){var best=0;var value=double.MaxValue;for(var i=0;i<s.UnknownCount;i++){var v=score(s.Fields[i]);if(v<value){value=v;best=i;}}return best;}
    private static (double G,double M,double A) FieldFactors(HeterogeneousAnisotropicSystem s,int a,int b,(int X,int Y,int Z)o)
    {
        var fa=s.Fields[a];var fb=s.Fields[b];var len=Math.Sqrt(o.X*o.X+o.Y*o.Y+o.Z*o.Z);var dx=o.X/len;var dy=o.Y/len;var dz=o.Z/len;
        var normal=Math.Abs(dx*.5d*(fa.InterfaceNormalX+fb.InterfaceNormalX)+dy*.5d*(fa.InterfaceNormalY+fb.InterfaceNormalY)+dz*.5d*(fa.InterfaceNormalZ+fb.InterfaceNormalZ));
        var gp=.5d*(fa.GeometryConfidence+fb.GeometryConfidence);var g=.5d+1.5d*gp*(1d-normal*normal);
        var align=Math.Abs(dx*.5d*(fa.MaterialAxisX+fb.MaterialAxisX)+dy*.5d*(fa.MaterialAxisY+fb.MaterialAxisY)+dz*.5d*(fa.MaterialAxisZ+fb.MaterialAxisZ));
        var mp=.5d*(fa.MaterialConfidence+fb.MaterialConfidence);var m=(.5d+1.5d*mp*align*align)*(fa.MaterialId==fb.MaterialId?1d:.6d);
        static double Own(ContinuumCellField f)=>f.MaterialConfidence/(f.MaterialConfidence+f.GeometryConfidence);
        var authority=.75d+.5d*(1d-Math.Abs(Own(fa)-Own(fb)));return(g,m,authority);
    }
    private static double SelectionMultiplier(ContinuumFieldMask f,(double G,double M,double A)x)
    {var values=new List<double>(3);if(f.HasFlag(ContinuumFieldMask.Geometry))values.Add(x.G);if(f.HasFlag(ContinuumFieldMask.Material))values.Add(x.M);if(f.HasFlag(ContinuumFieldMask.Authority))values.Add(x.A);return values.Count==0?1d:values.Aggregate(1d,(a,b)=>a*b);}
    private static double WeightMultiplier(ContinuumFieldMask f,(double G,double M,double A)x)=>Math.Sqrt(SelectionMultiplier(f,x));
    private static string Reason(ContinuumFieldMask f,double op,(double G,double M,double A)x)=>$"operator={op:G5}; fields={f}; geometry={x.G:G4}; material={x.M:G4}; authority={x.A:G4}";
    private static double OperatorPathScore(HeterogeneousAnisotropicSystem s,int a,int b,(int X,int Y,int Z)o)
    {
        var direct=s.DirectConductance(a,b);if(direct>0d)return direct/Math.Sqrt(s.Diagonal[a]*s.Diagonal[b]);
        var count=Math.Abs(o.X)+Math.Abs(o.Y)+Math.Abs(o.Z);
        double Path(bool reverse){var p=a;var score=1d;void Axis(int dx,int dy,int dz,int repetitions){for(var t=0;t<repetitions;t++){var xyz=s.Coordinates(p);var q=s.Flatten(xyz.I+dx,xyz.J+dy,xyz.K+dz);var c=s.DirectConductance(p,q)/Math.Sqrt(s.Diagonal[p]*s.Diagonal[q]);score*=Math.Max(c,1e-15);p=q;}}if(!reverse){Axis(Math.Sign(o.X),0,0,Math.Abs(o.X));Axis(0,Math.Sign(o.Y),0,Math.Abs(o.Y));Axis(0,0,Math.Sign(o.Z),Math.Abs(o.Z));}else{Axis(0,0,Math.Sign(o.Z),Math.Abs(o.Z));Axis(0,Math.Sign(o.Y),0,Math.Abs(o.Y));Axis(Math.Sign(o.X),0,0,Math.Abs(o.X));}return Math.Pow(score,1d/count);}
        return .5d*(Path(false)+Path(true))/(1d+.15d*(count-1));
    }
    private static (int X,int Y,int Z)[] BuildOffsets(){var v=new List<(int,int,int)>();for(var z=-1;z<=1;z++)for(var y=-1;y<=1;y++)for(var x=-1;x<=1;x++){if(x==0&&y==0&&z==0)continue;if(z>0||(z==0&&y>0)||(z==0&&y==0&&x>0))v.Add((x,y,z));}v.AddRange([(2,0,0),(0,2,0),(0,0,2),(2,1,0),(2,-1,0),(1,2,0),(1,-2,0)]);return v.ToArray();}
    private readonly record struct Candidate(int A,int B,double Score,double Weight,double OperatorScore,double G,double M,double Authority);
}

public sealed class GeometricTwoLevelPreconditioner:E1PreconditionerBase
{
    private readonly HeterogeneousAnisotropicSystem fine;private readonly double[] fineInv,coarseDiagonal,coarseInv;private readonly ContinuumEdge[] coarseEdges;private readonly int coarseCount,steps;private readonly double[] rhs,x,ax,residual;
    public GeometricTwoLevelPreconditioner(HeterogeneousAnisotropicSystem system,int steps=8):base($"two-level-geometric(Richardson-{steps})",system.UnknownCount)
    {
        var sw=Stopwatch.StartNew();fine=system;this.steps=steps;fineInv=system.Diagonal.Select(d=>1d/d).ToArray();var m=system.PointsPerAxis/2;coarseCount=m*m*m;
        var map=new Dictionary<(int,int),double>();var boundary=new double[coarseCount];var incident=new double[system.UnknownCount];
        foreach(var e in system.Edges){incident[e.A]+=e.Conductance;incident[e.B]+=e.Conductance;}
        for(var q=0;q<system.UnknownCount;q++){var c=Aggregate(q);boundary[c]+=system.Diagonal[q]-incident[q];}
        foreach(var e in system.Edges){var a=Aggregate(e.A);var b=Aggregate(e.B);if(a==b)continue;if(a>b)(a,b)=(b,a);map[(a,b)]=map.GetValueOrDefault((a,b))+e.Conductance;}
        coarseEdges=map.Select(k=>new ContinuumEdge(k.Key.Item1,k.Key.Item2,k.Value)).ToArray();coarseDiagonal=boundary;foreach(var e in coarseEdges){coarseDiagonal[e.A]+=e.Conductance;coarseDiagonal[e.B]+=e.Conductance;}coarseInv=coarseDiagonal.Select(d=>1d/d).ToArray();rhs=new double[coarseCount];x=new double[coarseCount];ax=new double[coarseCount];residual=new double[coarseCount];sw.Stop();SetupMilliseconds=sw.Elapsed.TotalMilliseconds;
    }
    public override double SetupMilliseconds{get;}public override long EstimatedStorageBytes=>8L*(Size+4L*coarseCount)+24L*coarseEdges.Length;public override long EstimatedFlopsPerApply=>2L*Size+steps*(5L*coarseCount+4L*coarseEdges.Length);
    public override void Apply(ReadOnlySpan<double>r,Span<double>z){Array.Clear(rhs);Array.Clear(x);for(var q=0;q<Size;q++){z[q]=fineInv[q]*r[q];rhs[Aggregate(q)]+=r[q];}for(var it=0;it<steps;it++){ApplyCoarse(x,ax);for(var c=0;c<coarseCount;c++){residual[c]=rhs[c]-ax[c];x[c]+=.5d*coarseInv[c]*residual[c];}}for(var q=0;q<Size;q++)z[q]+=x[Aggregate(q)];}
    private int Aggregate(int q){var n=fine.PointsPerAxis;var m=n/2;var(i,j,k)=fine.Coordinates(q);return((k/2*m)+(j/2))*m+i/2;}
    private void ApplyCoarse(ReadOnlySpan<double>s,Span<double>d){for(var i=0;i<coarseCount;i++)d[i]=coarseDiagonal[i]*s[i];foreach(var e in coarseEdges){d[e.A]-=e.Conductance*s[e.B];d[e.B]-=e.Conductance*s[e.A];}}
}
