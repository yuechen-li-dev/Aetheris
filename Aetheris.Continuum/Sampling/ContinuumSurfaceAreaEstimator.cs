using Aetheris.Continuum.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Sampling;

/// <summary>Deterministic orientation-corrected Crofton control over CIR occupancy. This is an estimate, never boundary identity authority.</summary>
public static class ContinuumSurfaceAreaEstimator
{
    public static double Estimate(IContinuumRegion region,int nx,int ny,int nz)
    {
        var b=region.Bounds;var dx=(b.Max.X-b.Min.X)/nx;var dy=(b.Max.Y-b.Min.Y)/ny;var dz=(b.Max.Z-b.Min.Z)/nz;
        var occupied=new bool[nx+2,ny+2,nz+2];
        Point3D P(int i,int j,int k)=>new(b.Min.X+((i-.5)*dx),b.Min.Y+((j-.5)*dy),b.Min.Z+((k-.5)*dz));
        for(var k=0;k<nz+2;k++)for(var j=0;j<ny+2;j++)for(var i=0;i<nx+2;i++)occupied[i,j,k]=region.Classify(P(i,j,k))!=ContinuumPointClassification.Outside;
        double Correction(Point3D p)
        {
            if(region is not IImplicitFieldCapability f)return 2d/3d;var h=double.Min(dx,double.Min(dy,dz))*.2d;
            var g=new Vector3D((f.FieldValue(p+new Vector3D(h,0,0))-f.FieldValue(p-new Vector3D(h,0,0)))/(2*h),(f.FieldValue(p+new Vector3D(0,h,0))-f.FieldValue(p-new Vector3D(0,h,0)))/(2*h),(f.FieldValue(p+new Vector3D(0,0,h))-f.FieldValue(p-new Vector3D(0,0,h)))/(2*h));
            return g.TryNormalize(out g)?1d/double.Max(1d,double.Abs(g.X)+double.Abs(g.Y)+double.Abs(g.Z)):2d/3d;
        }
        var area=0d;for(var k=0;k<nz+2;k++)for(var j=0;j<ny+2;j++)for(var i=0;i<nx+2;i++)
        {var p=P(i,j,k);if(i+1<nx+2&&occupied[i,j,k]!=occupied[i+1,j,k])area+=dy*dz*Correction(new(p.X+dx*.5,p.Y,p.Z));if(j+1<ny+2&&occupied[i,j,k]!=occupied[i,j+1,k])area+=dx*dz*Correction(new(p.X,p.Y+dy*.5,p.Z));if(k+1<nz+2&&occupied[i,j,k]!=occupied[i,j,k+1])area+=dx*dy*Correction(new(p.X,p.Y,p.Z+dz*.5));}
        return area;
    }
}
