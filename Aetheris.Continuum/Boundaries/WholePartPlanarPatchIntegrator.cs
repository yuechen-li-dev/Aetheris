using Aetheris.Continuum.Cir;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Boundaries;

/// <summary>Face-owned planar area quadrature over the exact plane and BRep-derived trim hull.</summary>
internal static class WholePartPlanarPatchIntegrator
{
    public static (double Area, int CirQueries) Integrate(BoundingBox3D cell, IContinuumRegion region,
        WholeShellBoundaryQuery shell, WholeShellBoundaryCandidate face, int resolution = 20)
    {
        var surface = shell.Body.GetFaceSurface(face.FaceId).Plane!.Value;
        var origin = shell.Transform.Apply(surface.Origin);
        var u = shell.Transform.Apply(surface.UAxis).ToVector(); var v = shell.Transform.Apply(surface.VAxis).ToVector();
        u.TryNormalize(out u); v.TryNormalize(out v);
        var normal = shell.Transform.Apply(surface.Normal).ToVector(); normal.TryNormalize(out normal);
        var frame = new BoundaryLocalFrame(origin, normal, u, v);
        var trim = WholePartBoundaryMapFactory.ProjectedTrimDomain.Create(face.ExactBoundarySamples, frame);
        var corners = Corners(cell).Select(p => p - origin).ToArray();
        var u0=corners.Min(p=>p.Dot(u));var u1=corners.Max(p=>p.Dot(u));var v0=corners.Min(p=>p.Dot(v));var v1=corners.Max(p=>p.Dot(v));
        var du=(u1-u0)/resolution;var dv=(v1-v0)/resolution;var area=0d;var queries=0;
        var tolerance=double.Max(1d,(region.Bounds.Max-region.Bounds.Min).Length)*2e-7d;
        for(var j=0;j<resolution;j++)for(var i=0;i<resolution;i++)
        {
            var a=u0+(i+.5d)*du;var b=v0+(j+.5d)*dv;var point=origin+(u*a)+(v*b);
            if(!Contains(cell,point,1e-10d)||trim.SignedDistance(a,b)<0d)continue;
            queries++;if(region.Classify(point,tolerance)==ContinuumPointClassification.Boundary)area+=du*dv;
        }
        return(area,queries);
    }

    private static bool Contains(BoundingBox3D b,Point3D p,double t)=>p.X>=b.Min.X-t&&p.X<=b.Max.X+t&&p.Y>=b.Min.Y-t&&p.Y<=b.Max.Y+t&&p.Z>=b.Min.Z-t&&p.Z<=b.Max.Z+t;
    private static Point3D[] Corners(BoundingBox3D b)=>[new(b.Min.X,b.Min.Y,b.Min.Z),new(b.Max.X,b.Min.Y,b.Min.Z),new(b.Min.X,b.Max.Y,b.Min.Z),new(b.Max.X,b.Max.Y,b.Min.Z),new(b.Min.X,b.Min.Y,b.Max.Z),new(b.Max.X,b.Min.Y,b.Max.Z),new(b.Min.X,b.Max.Y,b.Max.Z),new(b.Max.X,b.Max.Y,b.Max.Z)];
}
