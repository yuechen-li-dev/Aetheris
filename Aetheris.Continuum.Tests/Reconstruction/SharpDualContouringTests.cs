using Aetheris.Continuum.Reconstruction;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Tests.Reconstruction;

public sealed class SharpDualContouringTests
{
    [Fact]
    public void SharpBoxProducesDeterministicClosedApproximateMesh()
    {
        var grid=BoxGrid(25,1,new(-12.25,-12.25,-12.25),new(8,6,5));
        var first=SharpDualContouring.Reconstruct(grid);var second=SharpDualContouring.Reconstruct(grid);
        Assert.NotEmpty(first.Mesh.Vertices);Assert.NotEmpty(first.Mesh.Triangles);
        Assert.Equal(0,first.Diagnostics.BoundaryEdges);Assert.Equal(0,first.Diagnostics.NonManifoldEdges);Assert.Equal(1,first.Diagnostics.Components);
        Assert.Equal(first.Mesh.Vertices,second.Mesh.Vertices);Assert.Equal(first.Mesh.Triangles,second.Mesh.Triangles);
        Assert.Contains(first.Mesh.Vertices,v=>Math.Abs(v.X-8)<.25&&Math.Abs(v.Y-6)<.25&&Math.Abs(v.Z-5)<.25);
        var rms=Math.Sqrt(first.Mesh.Vertices.Average(v=>Math.Pow(BoxDistance(v,new(8,6,5)),2)));
        Assert.True(rms<.1,$"Expected sharp QEF vertices close to exact box; RMS was {rms:G6} mm.");
        Assert.Contains("not BRep",first.Mesh.Provenance.Authority,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiagnosticsAlwaysExposeTopologyFailures()
    {
        var mesh=new ApproximateSurfaceMesh(new[]{new Point3D(0,0,0),new(1,0,0),new(0,1,0),new(0,-1,0),new(0,0,1)},
            new[]{new MeshTriangle(0,1,2),new MeshTriangle(0,1,3),new MeshTriangle(0,1,4),new MeshTriangle(0,1,2)},new("test","test","approximate"));
        var diagnostics=SharpDualContouring.Diagnose(mesh);
        Assert.True(diagnostics.NonManifoldEdges>0);Assert.True(diagnostics.DuplicateFaces>0);Assert.True(diagnostics.BoundaryEdges>0);
    }

    private static SampledSdfGrid BoxGrid(int n,double spacing,Point3D origin,Vector3D h)
    {
        var values=new double[n*n*n];var i=0;
        for(var z=0;z<n;z++)for(var y=0;y<n;y++)for(var x=0;x<n;x++)
        {
            var px=origin.X+x*spacing;var py=origin.Y+y*spacing;var pz=origin.Z+z*spacing;
            var qx=Math.Abs(px)-h.X;var qy=Math.Abs(py)-h.Y;var qz=Math.Abs(pz)-h.Z;
            values[i++]=Math.Sqrt(Math.Pow(Math.Max(qx,0),2)+Math.Pow(Math.Max(qy,0),2)+Math.Pow(Math.Max(qz,0),2))+Math.Min(Math.Max(qx,Math.Max(qy,qz)),0);
        }
        return new(n,n,n,origin,new(spacing,spacing,spacing),values);
    }

    private static double BoxDistance(Point3D p,Vector3D h)
    {
        var qx=Math.Abs(p.X)-h.X;var qy=Math.Abs(p.Y)-h.Y;var qz=Math.Abs(p.Z)-h.Z;
        return Math.Sqrt(Math.Pow(Math.Max(qx,0),2)+Math.Pow(Math.Max(qy,0),2)+Math.Pow(Math.Max(qz,0),2))+Math.Min(Math.Max(qx,Math.Max(qy,qz)),0);
    }
}
