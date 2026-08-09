using Aetheris.Kernel.Core.Construction;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Construction;

public sealed class ContinuumConstructionDescriptorTests
{
    [Fact]
    public void DescriptorCarriesOnlyBoundedGeometryLineageAndAdmittedOperations()
    {
        Point3D[] a=[new(0,0,0),new(0,1,0),new(0,0,1)];Point3D[] b=[new(1,0,0),new(1,1,0),new(1,0,1)];
        var descriptor=new ContinuumConstructionDescriptor("air:feature-7","body:region-1",[new(0,a),new(1,b)],[0,1,2],["PrismaticSectionTransition"],["generated/native AIR","recipe:v1"]);
        Assert.Same(descriptor,descriptor.Validate());Assert.DoesNotContain(descriptor.AdmittedOperations,x=>x.Contains("BRepPlan",StringComparison.Ordinal));
    }
}
