using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Reconstruction;

public sealed class SampledSdfGrid
{
    public SampledSdfGrid(int sizeX, int sizeY, int sizeZ, Point3D origin, Vector3D spacing, IReadOnlyList<double> values)
    {
        if (sizeX < 2 || sizeY < 2 || sizeZ < 2) throw new ArgumentOutOfRangeException(nameof(sizeX), "Grid dimensions must each be at least two.");
        if (spacing.X <= 0d || spacing.Y <= 0d || spacing.Z <= 0d) throw new ArgumentOutOfRangeException(nameof(spacing), "Grid spacing must be positive.");
        if (values.Count != checked(sizeX * sizeY * sizeZ)) throw new ArgumentException("Grid value count does not match dimensions.", nameof(values));
        if (values.Any(static v => !double.IsFinite(v))) throw new ArgumentException("Grid contains a non-finite sample.", nameof(values));
        SizeX = sizeX; SizeY = sizeY; SizeZ = sizeZ; Origin = origin; Spacing = spacing; Values = values.ToArray();
    }

    public int SizeX { get; }
    public int SizeY { get; }
    public int SizeZ { get; }
    public Point3D Origin { get; }
    public Vector3D Spacing { get; }
    public IReadOnlyList<double> Values { get; }
    public double this[int x, int y, int z] => Values[(z * SizeY * SizeX) + (y * SizeX) + x];
    public Point3D Point(int x, int y, int z) => new(Origin.X + (x * Spacing.X), Origin.Y + (y * Spacing.Y), Origin.Z + (z * Spacing.Z));
}

public sealed record ApproximateMeshProvenance(string Source, string Algorithm, string Authority, IReadOnlyDictionary<string, string>? Properties = null);
public readonly record struct MeshTriangle(int A, int B, int C);
public sealed record ApproximateSurfaceMesh(IReadOnlyList<Point3D> Vertices, IReadOnlyList<MeshTriangle> Triangles, ApproximateMeshProvenance Provenance);
public sealed record MeshTopologyDiagnostics(int BoundaryEdges, int NonManifoldEdges, int Components, int DuplicateFaces, int DuplicateVertices);
public sealed record SharpDualContourStatistics(int ActiveCells, int HermiteConstraints, double ElapsedMilliseconds);
public sealed record SharpDualContourResult(ApproximateSurfaceMesh Mesh, MeshTopologyDiagnostics Diagnostics, SharpDualContourStatistics Statistics);

/// <summary>Clean-room bounded sharp dual contouring of a regular sampled SDF grid.</summary>
public static class SharpDualContouring
{
    private static readonly (int A, int B)[] CellEdges =
    {
        (0,1),(2,3),(4,5),(6,7), (0,2),(1,3),(4,6),(5,7), (0,4),(1,5),(2,6),(3,7)
    };
    private static readonly (int X, int Y, int Z)[] Corners =
    {
        (0,0,0),(1,0,0),(0,1,0),(1,1,0),(0,0,1),(1,0,1),(0,1,1),(1,1,1)
    };

    public static SharpDualContourResult Reconstruct(SampledSdfGrid grid, string source = "sampled-sdf-grid")
    {
        ArgumentNullException.ThrowIfNull(grid);
        var started = System.Diagnostics.Stopwatch.StartNew();
        var vertices = new List<Point3D>();
        var cellVertices = new Dictionary<(int X, int Y, int Z), int>();
        var constraintCount = 0;
        for (var z = 0; z < grid.SizeZ - 1; z++)
        for (var y = 0; y < grid.SizeY - 1; y++)
        for (var x = 0; x < grid.SizeX - 1; x++)
        {
            var values = new double[8];
            var negative = 0;
            for (var c = 0; c < 8; c++)
            {
                var o = Corners[c]; values[c] = grid[x + o.X, y + o.Y, z + o.Z];
                if (values[c] < 0d) negative++;
            }
            if (negative is 0 or 8) continue;

            var constraints = new List<(Point3D Point, Vector3D Normal)>(12);
            foreach (var (a, b) in CellEdges)
            {
                if ((values[a] < 0d) == (values[b] < 0d)) continue;
                var oa = Corners[a]; var ob = Corners[b];
                var pa = grid.Point(x + oa.X, y + oa.Y, z + oa.Z);
                var pb = grid.Point(x + ob.X, y + ob.Y, z + ob.Z);
                var t = values[a] / (values[a] - values[b]);
                var p = Lerp(pa, pb, t);
                var normal = Gradient(grid, p);
                if (normal.TryNormalize(out var unit)) constraints.Add((p, unit));
            }
            if (constraints.Count == 0) continue;
            constraintCount += constraints.Count;
            var min = grid.Point(x, y, z); var max = grid.Point(x + 1, y + 1, z + 1);
            var vertex = SolveQef(constraints, min, max);
            cellVertices[(x, y, z)] = vertices.Count;
            vertices.Add(vertex);
        }

        var triangles = new List<MeshTriangle>();
        AddXEdgeFaces(grid, cellVertices, triangles);
        AddYEdgeFaces(grid, cellVertices, triangles);
        AddZEdgeFaces(grid, cellVertices, triangles);
        var provenance = new ApproximateMeshProvenance(source, "clean-room-bounded-sharp-dual-contouring",
            "Approximate reconstruction evidence only; not BRep, topology, feature, or manufacturing authority.",
            new Dictionary<string, string> { ["grid"] = $"{grid.SizeX}x{grid.SizeY}x{grid.SizeZ}", ["placement"] = "Hermite QEF constrained to source cell" });
        var mesh = new ApproximateSurfaceMesh(vertices, triangles, provenance);
        var diagnostics = Diagnose(mesh);
        started.Stop();
        return new(mesh, diagnostics, new(cellVertices.Count, constraintCount, started.Elapsed.TotalMilliseconds));
    }

    private static void AddXEdgeFaces(SampledSdfGrid g, Dictionary<(int,int,int),int> cells, List<MeshTriangle> triangles)
    {
        for (var z=1; z<g.SizeZ-1; z++) for (var y=1; y<g.SizeY-1; y++) for (var x=0; x<g.SizeX-1; x++)
            AddFaceIfCrossing(g[x,y,z], g[x+1,y,z], cells, triangles, (x,y-1,z-1),(x,y,z-1),(x,y,z),(x,y-1,z));
    }
    private static void AddYEdgeFaces(SampledSdfGrid g, Dictionary<(int,int,int),int> cells, List<MeshTriangle> triangles)
    {
        for (var z=1; z<g.SizeZ-1; z++) for (var y=0; y<g.SizeY-1; y++) for (var x=1; x<g.SizeX-1; x++)
            AddFaceIfCrossing(g[x,y,z], g[x,y+1,z], cells, triangles, (x-1,y,z-1),(x-1,y,z),(x,y,z),(x,y,z-1));
    }
    private static void AddZEdgeFaces(SampledSdfGrid g, Dictionary<(int,int,int),int> cells, List<MeshTriangle> triangles)
    {
        for (var z=0; z<g.SizeZ-1; z++) for (var y=1; y<g.SizeY-1; y++) for (var x=1; x<g.SizeX-1; x++)
            AddFaceIfCrossing(g[x,y,z], g[x,y,z+1], cells, triangles, (x-1,y-1,z),(x,y-1,z),(x,y,z),(x-1,y,z));
    }

    private static void AddFaceIfCrossing(double a, double b, Dictionary<(int,int,int),int> cells, List<MeshTriangle> triangles,
        (int,int,int) c0, (int,int,int) c1, (int,int,int) c2, (int,int,int) c3)
    {
        if ((a < 0d) == (b < 0d) || !cells.TryGetValue(c0,out var i0) || !cells.TryGetValue(c1,out var i1) ||
            !cells.TryGetValue(c2,out var i2) || !cells.TryGetValue(c3,out var i3)) return;
        if (a < 0d)
        {
            triangles.Add(new(i0,i1,i2)); triangles.Add(new(i0,i2,i3));
        }
        else
        {
            triangles.Add(new(i0,i2,i1)); triangles.Add(new(i0,i3,i2));
        }
    }

    private static Point3D SolveQef(IReadOnlyList<(Point3D Point, Vector3D Normal)> constraints, Point3D min, Point3D max)
    {
        var centroid = new Point3D(constraints.Average(c=>c.Point.X), constraints.Average(c=>c.Point.Y), constraints.Average(c=>c.Point.Z));
        var a00=0d; var a01=0d; var a02=0d; var a11=0d; var a12=0d; var a22=0d; var b0=0d; var b1=0d; var b2=0d;
        foreach (var (p,n) in constraints)
        {
            var d=(n.X*p.X)+(n.Y*p.Y)+(n.Z*p.Z);
            a00+=n.X*n.X; a01+=n.X*n.Y; a02+=n.X*n.Z; a11+=n.Y*n.Y; a12+=n.Y*n.Z; a22+=n.Z*n.Z;
            b0+=n.X*d; b1+=n.Y*d; b2+=n.Z*d;
        }
        var regularization = Math.Max(1e-10, constraints.Count * 1e-9);
        a00+=regularization; a11+=regularization; a22+=regularization;
        b0+=regularization*centroid.X; b1+=regularization*centroid.Y; b2+=regularization*centroid.Z;
        var solved = SolveSymmetric(a00,a01,a02,a11,a12,a22,b0,b1,b2, out var x, out var y, out var z);
        if (!solved) return centroid;
        return new(Math.Clamp(x,min.X,max.X), Math.Clamp(y,min.Y,max.Y), Math.Clamp(z,min.Z,max.Z));
    }

    private static bool SolveSymmetric(double a00,double a01,double a02,double a11,double a12,double a22,double b0,double b1,double b2,
        out double x,out double y,out double z)
    {
        var m = new[,] {{a00,a01,a02,b0},{a01,a11,a12,b1},{a02,a12,a22,b2}};
        for (var col=0; col<3; col++)
        {
            var pivot=col; for(var r=col+1;r<3;r++) if(Math.Abs(m[r,col])>Math.Abs(m[pivot,col])) pivot=r;
            if(Math.Abs(m[pivot,col])<1e-14){x=y=z=0d;return false;}
            if(pivot!=col) for(var c=col;c<4;c++) (m[col,c],m[pivot,c])=(m[pivot,c],m[col,c]);
            var scale=m[col,col]; for(var c=col;c<4;c++) m[col,c]/=scale;
            for(var r=0;r<3;r++) if(r!=col){var factor=m[r,col];for(var c=col;c<4;c++)m[r,c]-=factor*m[col,c];}
        }
        x=m[0,3];y=m[1,3];z=m[2,3];return double.IsFinite(x)&&double.IsFinite(y)&&double.IsFinite(z);
    }

    private static Vector3D Gradient(SampledSdfGrid grid, Point3D p)
    {
        var ex=grid.Spacing.X*.25d; var ey=grid.Spacing.Y*.25d; var ez=grid.Spacing.Z*.25d;
        return new((Sample(grid,new(p.X+ex,p.Y,p.Z))-Sample(grid,new(p.X-ex,p.Y,p.Z)))/(2d*ex),
            (Sample(grid,new(p.X,p.Y+ey,p.Z))-Sample(grid,new(p.X,p.Y-ey,p.Z)))/(2d*ey),
            (Sample(grid,new(p.X,p.Y,p.Z+ez))-Sample(grid,new(p.X,p.Y,p.Z-ez)))/(2d*ez));
    }

    private static double Sample(SampledSdfGrid g, Point3D p)
    {
        var fx=Math.Clamp((p.X-g.Origin.X)/g.Spacing.X,0d,g.SizeX-1d); var x=Math.Min((int)Math.Floor(fx),g.SizeX-2); var tx=fx-x;
        var fy=Math.Clamp((p.Y-g.Origin.Y)/g.Spacing.Y,0d,g.SizeY-1d); var y=Math.Min((int)Math.Floor(fy),g.SizeY-2); var ty=fy-y;
        var fz=Math.Clamp((p.Z-g.Origin.Z)/g.Spacing.Z,0d,g.SizeZ-1d); var z=Math.Min((int)Math.Floor(fz),g.SizeZ-2); var tz=fz-z;
        double L(double a,double b,double t)=>a+((b-a)*t);
        var z0=L(L(g[x,y,z],g[x+1,y,z],tx),L(g[x,y+1,z],g[x+1,y+1,z],tx),ty);
        var z1=L(L(g[x,y,z+1],g[x+1,y,z+1],tx),L(g[x,y+1,z+1],g[x+1,y+1,z+1],tx),ty);
        return L(z0,z1,tz);
    }

    private static Point3D Lerp(Point3D a, Point3D b, double t) => new(a.X+((b.X-a.X)*t),a.Y+((b.Y-a.Y)*t),a.Z+((b.Z-a.Z)*t));

    public static MeshTopologyDiagnostics Diagnose(ApproximateSurfaceMesh mesh)
    {
        var edges=new Dictionary<(int,int),int>(); var faces=new HashSet<(int,int,int)>(); var duplicateFaces=0;
        var adjacency=Enumerable.Range(0,mesh.Vertices.Count).Select(_=>new List<int>()).ToArray();
        foreach(var t in mesh.Triangles)
        {
            var key=Sort3(t.A,t.B,t.C); if(!faces.Add(key)) duplicateFaces++;
            AddEdge(t.A,t.B);AddEdge(t.B,t.C);AddEdge(t.C,t.A);
        }
        void AddEdge(int a,int b){if(a>b)(a,b)=(b,a);edges[(a,b)]=edges.GetValueOrDefault((a,b))+1;adjacency[a].Add(b);adjacency[b].Add(a);}
        var components=0; var visited=new bool[mesh.Vertices.Count];
        for(var i=0;i<visited.Length;i++) if(!visited[i]){components++;var q=new Queue<int>();q.Enqueue(i);visited[i]=true;while(q.Count>0){foreach(var n in adjacency[q.Dequeue()])if(!visited[n]){visited[n]=true;q.Enqueue(n);}}}
        var duplicateVertices=mesh.Vertices.Count-mesh.Vertices.Distinct().Count();
        return new(edges.Count(e=>e.Value==1),edges.Count(e=>e.Value>2),components,duplicateFaces,duplicateVertices);
    }

    private static (int,int,int) Sort3(int a,int b,int c){if(a>b)(a,b)=(b,a);if(b>c)(b,c)=(c,b);if(a>b)(a,b)=(b,a);return(a,b,c);}
}

public static class ApproximateMeshObj
{
    public static void Write(ApproximateSurfaceMesh mesh, TextWriter writer)
    {
        writer.WriteLine("# Aetheris approximate reconstruction; not canonical CAD authority");
        foreach(var v in mesh.Vertices) writer.WriteLine(FormattableString.Invariant($"v {v.X:R} {v.Y:R} {v.Z:R}"));
        foreach(var t in mesh.Triangles) writer.WriteLine($"f {t.A+1} {t.B+1} {t.C+1}");
    }
}
