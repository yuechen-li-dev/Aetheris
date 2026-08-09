using System.Diagnostics;

namespace Aetheris.Continuum.Experiments.Attention;

[Flags]
public enum ContinuumFieldMask { None = 0, Geometry = 1, Material = 2, Authority = 4 }
public enum AuthorityConfiguration { Uniform, Opposed, Localized, Asymmetric }

public sealed record AttentionE1Configuration(
    int PointsPerAxis = 16,
    double MaterialContrast = 100d,
    double AnisotropyRatio = 16d,
    double OrientationDegrees = 30d,
    AuthorityConfiguration Authority = AuthorityConfiguration.Opposed)
{
    public string Id => $"n{PointsPerAxis}-c{MaterialContrast:R}-a{AnisotropyRatio:R}-o{OrientationDegrees:R}-{Authority}";
}

public readonly record struct ContinuumCellField(
    byte MaterialId,
    double InterfaceSignedDistance,
    double InterfaceNormalX,
    double InterfaceNormalY,
    double InterfaceNormalZ,
    double MaterialAxisX,
    double MaterialAxisY,
    double MaterialAxisZ,
    double GeometryConfidence,
    double MaterialConfidence);

public readonly record struct ContinuumEdge(int A, int B, double Conductance);

/// <summary>
/// Cell-centred finite-volume discretization of -div(A grad u) on the unit cube.
/// A is represented by isotropic axial directional energy plus an explicit
/// lattice-representable in-plane principal-direction bond. Bond conductance
/// uses the harmonic mean across the material interface.
/// Homogeneous Dirichlet faces contribute to the diagonal.
/// </summary>
public sealed class HeterogeneousAnisotropicSystem
{
    private readonly Dictionary<long,double> conductanceLookup=[];

    private HeterogeneousAnisotropicSystem(AttentionE1Configuration configuration)
    {
        Configuration = configuration;
        var n = configuration.PointsPerAxis;
        if (n < 4 || n % 2 != 0) throw new ArgumentOutOfRangeException(nameof(configuration), "E1 requires an even grid of at least 4^3.");
        UnknownCount = checked(n * n * n);
        Spacing = 1d / n;
        Fields = new ContinuumCellField[UnknownCount];
        Diagonal = new double[UnknownCount];
        BuildFieldsAndFaces();
        NonzeroCount = UnknownCount + (2 * Edges.Count);
    }

    public AttentionE1Configuration Configuration { get; }
    public int PointsPerAxis => Configuration.PointsPerAxis;
    public int UnknownCount { get; }
    public int NonzeroCount { get; }
    public double Spacing { get; }
    public ContinuumCellField[] Fields { get; }
    public double[] Diagonal { get; }
    public IReadOnlyList<ContinuumEdge> Edges { get; private set; } = [];

    public static AttentionE1Problem Create(AttentionE1Configuration configuration)
    {
        var system = new HeterogeneousAnisotropicSystem(configuration);
        var exact = new double[system.UnknownCount];
        var n = system.PointsPerAxis;
        for (var k = 0; k < n; k++)
        for (var j = 0; j < n; j++)
        for (var i = 0; i < n; i++)
        {
            var x = (i + 0.5d) / n; var y = (j + 0.5d) / n; var z = (k + 0.5d) / n;
            exact[system.Flatten(i, j, k)] = x * (1d - x) * y * (1d - y) * z * (1d - z)
                * (1d + 0.15d * Math.Sin(2d * Math.PI * x) * Math.Cos(Math.PI * y));
        }
        var rhs = new double[exact.Length];
        system.Apply(exact, rhs); // exact discrete manufactured/reference solution
        return new AttentionE1Problem(system, rhs, exact,
            "discrete manufactured u=x(1-x)y(1-y)z(1-z)[1+0.15 sin(2pi x) cos(pi y)]; f=K u");
    }

    public void Apply(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (source.Length != UnknownCount || destination.Length != UnknownCount) throw new ArgumentException("Vector size mismatch.");
        for(var q=0;q<UnknownCount;q++)destination[q]=Diagonal[q]*source[q];
        foreach(var e in Edges){destination[e.A]-=e.Conductance*source[e.B];destination[e.B]-=e.Conductance*source[e.A];}
    }

    public int Flatten(int i, int j, int k) => ((k * PointsPerAxis) + j) * PointsPerAxis + i;
    public (int I, int J, int K) Coordinates(int index) => (index % PointsPerAxis, (index / PointsPerAxis) % PointsPerAxis, index / (PointsPerAxis * PointsPerAxis));

    public double DirectConductance(int a, int b)
    {
        if(a>b)(a,b)=(b,a);return conductanceLookup.GetValueOrDefault(((long)a<<32)|(uint)b);
    }

    private void BuildFieldsAndFaces()
    {
        var n = PointsPerAxis; var h2 = Spacing * Spacing;
        var radians = Configuration.OrientationDegrees * Math.PI / 180d;
        var requested = (X: Math.Cos(radians), Y: Math.Sin(radians));
        var candidates = new[]{(X:1,Y:0),(X:2,Y:1),(X:1,Y:1),(X:1,Y:2),(X:0,Y:1)};
        var principal=candidates.OrderBy(d=>Math.Abs(Math.Atan2(d.Y,d.X)-Math.Atan2(Math.Abs(requested.Y),Math.Abs(requested.X)))).First();
        principal=(principal.X*Math.Sign(requested.X==0?1:requested.X),principal.Y*Math.Sign(requested.Y==0?1:requested.Y));
        var plen=Math.Sqrt(principal.X*principal.X+principal.Y*principal.Y);var axis=(X:principal.X/plen,Y:principal.Y/plen,Z:0d);
        var scale = new double[UnknownCount];
        for (var k = 0; k < n; k++)
        for (var j = 0; j < n; j++)
        for (var i = 0; i < n; i++)
        {
            var x = (i + 0.5d) / n; var y = (j + 0.5d) / n; var z = (k + 0.5d) / n;
            // A slanted planar material interface. Geometry is distinct from the Cartesian boundary.
            var signed = (x + 0.35d * y - 0.675d) / Math.Sqrt(1d + 0.35d * 0.35d);
            var material = (byte)(signed <= 0d ? 0 : 1);
            scale[Flatten(i,j,k)] = material == 0 ? 1d : Configuration.MaterialContrast;
            var g = 0.15d + 0.85d * Clamp01(1d - Math.Abs(signed) / (1.5d * Spacing));
            var m = Configuration.Authority switch
            {
                AuthorityConfiguration.Uniform => 0.75d,
                AuthorityConfiguration.Opposed => x < 0.5d ? 0.90d : 0.25d,
                AuthorityConfiguration.Localized => Math.Sqrt((x-.75d)*(x-.75d)+(y-.25d)*(y-.25d)+(z-.5d)*(z-.5d)) < .22d ? .95d : .25d,
                _ => Clamp01(0.15d + 0.8d * (0.65d*x + 0.25d*y + 0.1d*z)),
            };
            var norm = 1d / Math.Sqrt(1d + 0.35d * 0.35d);
            Fields[Flatten(i,j,k)] = new(material, signed, norm, .35d*norm, 0d, axis.X, axis.Y, axis.Z, g, m);
        }

        var directions=new List<(int X,int Y,int Z,double Factor)>{(1,0,0,1d),(0,1,0,1d),(0,0,1,1d)};
        if(Configuration.AnisotropyRatio>1d)directions.Add((principal.X,principal.Y,0,Configuration.AnisotropyRatio-1d));
        var map=new Dictionary<(int A,int B),double>();
        foreach(var d in directions)
        for(var k=0;k<n;k++)for(var j=0;j<n;j++)for(var i=0;i<n;i++)
        {
            var q=Flatten(i,j,k);var denom=h2*(d.X*d.X+d.Y*d.Y+d.Z*d.Z);var local=scale[q]*d.Factor/denom;
            foreach(var sign in new[]{-1,1})
            {
                var ni=i+sign*d.X;var nj=j+sign*d.Y;var nk=k+sign*d.Z;
                if((uint)ni>=(uint)n||(uint)nj>=(uint)n||(uint)nk>=(uint)n){Diagonal[q]+=local;continue;}
                if(sign<0)continue;var other=Flatten(ni,nj,nk);var weight=Harmonic(scale[q]*d.Factor,scale[other]*d.Factor)/denom;
                var key=q<other?(q,other):(other,q);map[key]=map.GetValueOrDefault(key)+weight;
            }
        }
        var edges=map.Select(x=>new ContinuumEdge(x.Key.A,x.Key.B,x.Value)).ToArray();
        foreach(var e in edges){Diagonal[e.A]+=e.Conductance;Diagonal[e.B]+=e.Conductance;conductanceLookup[((long)e.A<<32)|(uint)e.B]=e.Conductance;}
        Edges=edges;
    }

    private static double Harmonic(double a, double b) => 2d*a*b/(a+b);
    private static double Clamp01(double x) => Math.Max(0d, Math.Min(1d,x));
}

public sealed record AttentionE1Problem(HeterogeneousAnisotropicSystem System, double[] RightHandSide, double[] ExactSolution, string ManufacturedSolution);

public sealed record E1SolveResult(double[] Solution, int Iterations, double RelativeResidual, double RelativeSolutionError,
    double RuntimeMilliseconds, double MatvecMilliseconds, double PreconditionerMilliseconds, IReadOnlyList<ResidualSample> ResidualHistory);

public static class AttentionE1Pcg
{
    public static E1SolveResult Solve(AttentionE1Problem problem, IE1Preconditioner preconditioner, double tolerance=1e-8, int? maximumIterations=null)
    {
        var system=problem.System; var n=system.UnknownCount;
        var x=new double[n]; var r=problem.RightHandSide.ToArray(); var z=new double[n]; var p=new double[n]; var ap=new double[n];
        var initial=Norm(r); var history=new List<ResidualSample>{new(0,initial,1d)}; long mt=0,pt=0; var total=Stopwatch.StartNew();
        var stamp=Stopwatch.GetTimestamp(); preconditioner.Apply(r,z); pt+=Stopwatch.GetTimestamp()-stamp; Array.Copy(z,p,n); var rz=Dot(r,z);
        if (!(rz>0d)) throw new InvalidOperationException($"{preconditioner.Name} violated positive energy.");
        var limit=maximumIterations ?? Math.Max(1000,16*system.PointsPerAxis); var iterations=0;
        for(var it=1;it<=limit;it++)
        {
            stamp=Stopwatch.GetTimestamp(); system.Apply(p,ap); mt+=Stopwatch.GetTimestamp()-stamp;
            var den=Dot(p,ap); if(!(den>0d)) throw new InvalidOperationException("K is not SPD.");
            var alpha=rz/den; for(var q=0;q<n;q++){x[q]+=alpha*p[q];r[q]-=alpha*ap[q];}
            var rel=Norm(r)/initial; history.Add(new(it,rel*initial,rel)); iterations=it; if(rel<=tolerance) break;
            stamp=Stopwatch.GetTimestamp();preconditioner.Apply(r,z);pt+=Stopwatch.GetTimestamp()-stamp;
            var next=Dot(r,z);if(!(next>0d))throw new InvalidOperationException($"{preconditioner.Name} violated positive energy.");
            var beta=next/rz;for(var q=0;q<n;q++)p[q]=z[q]+beta*p[q];rz=next;
        }
        total.Stop(); var error=new double[n];for(var q=0;q<n;q++)error[q]=x[q]-problem.ExactSolution[q];
        return new(x,iterations,history[^1].RelativeResidual,Norm(error)/Norm(problem.ExactSolution),total.Elapsed.TotalMilliseconds,
            1000d*mt/Stopwatch.Frequency,1000d*pt/Stopwatch.Frequency,history);
    }
    internal static double Dot(ReadOnlySpan<double>a,ReadOnlySpan<double>b){double s=0;for(var i=0;i<a.Length;i++)s+=a[i]*b[i];return s;}
    internal static double Norm(ReadOnlySpan<double>a)=>Math.Sqrt(Dot(a,a));
}
