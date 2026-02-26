using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class BoxBrepFixtureTests
{
    [Fact]
    public void ManualBoxBrep_CanBeConstructedAndValidated()
    {
        var body = BoxBrepFixture.BuildUnitBoxBrep();

        var result = BrepBindingValidator.Validate(body);

        Assert.True(result.IsSuccess);
        Assert.True(body.TryGetEdgeCurveGeometry(new EdgeId(1), out var edgeCurve));
        Assert.Equal(CurveGeometryKind.Line3, edgeCurve!.Kind);

        Assert.True(body.TryGetFaceSurfaceGeometry(new FaceId(1), out var faceSurface));
        Assert.Equal(SurfaceGeometryKind.Plane, faceSurface!.Kind);
    }
}
