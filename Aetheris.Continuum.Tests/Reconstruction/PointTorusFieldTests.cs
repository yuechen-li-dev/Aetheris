using Aetheris.Continuum.Reconstruction;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Tests.Reconstruction;

public sealed class PointTorusFieldTests
{
    [Fact]
    public void DegenerateTorusEvaluatesSphereAndQualifiesNearSource()
    {
        var samples=Axes().Select(axis=>new PointTorusSample(new(axis.X*10,axis.Y*10,axis.Z*10),axis,Point3D.Origin,new(0,0,1),0,10)).ToArray();
        var field=new PointTorusField(samples,new(6,3,PointTorusSignCapability.SignedClosedOriented),new("sphere","test",null,null));
        var surface=field.Evaluate(new(10,0,0));var outside=field.Evaluate(new(12,0,0));
        Assert.Equal(PointTorusQualification.Qualified,surface.Qualification);
        Assert.Equal(0,surface.ValueMm,10);Assert.Equal(2,outside.ValueMm,10);Assert.True(outside.IsSigned);
    }

    [Fact]
    public void OpenSurfaceNeverAdvertisesSign()
    {
        var sample=new PointTorusSample(new(10,0,0),new(1,0,0),Point3D.Origin,new(0,0,1),0,10);
        var field=new PointTorusField(new[]{sample},new(1,5,PointTorusSignCapability.UnsignedOnlyOpenSurface),new("open","test",null,null));
        var result=field.Evaluate(new(9,0,0));
        Assert.Equal(PointTorusQualification.UnsignedOnly,result.Qualification);Assert.False(result.IsSigned);Assert.Equal(1,result.ValueMm,10);
    }

    [Fact]
    public void FarQueryIsOutsideDeclaredBand()
    {
        var sample=new PointTorusSample(new(10,0,0),new(1,0,0),Point3D.Origin,new(0,0,1),0,10);
        var field=new PointTorusField(new[]{sample},new(1,2),new("sphere","test",null,null));
        Assert.Equal(PointTorusQualification.OutsideValidityBand,field.Evaluate(Point3D.Origin).Qualification);
    }

    private static Vector3D[] Axes()=>new[]{new Vector3D(1,0,0),new(-1,0,0),new(0,1,0),new(0,-1,0),new(0,0,1),new(0,0,-1)};
}
