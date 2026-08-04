using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;

namespace Aetheris.Kernel.Core.Tests.Air;

public sealed class RoundedBoxBRepPlanTests
{
    [Theory]
    [InlineData(120, 80, 18, 12, 2)]
    [InlineData(80, 50, 20, 8, 1)]
    [InlineData(150, 90, 25, 15, 3)]
    public void TopBoundaryFillet_UsesOneAuthoritativePlanWithExactToroidalCorners(double width, double depth, double height, double corner, double fillet)
    {
        var result = RoundedBoxBRepPlanner.Create(width, depth, height, corner, fillet);

        Assert.True(result.IsSuccess);
        var realization = result.Value;
        Assert.True(realization.Plan.IsAuthoritative);
        Assert.Equal(18, realization.Body.Topology.Faces.Count());
        Assert.Equal(4, realization.Body.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Torus));
        Assert.Equal(8, realization.Body.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(6, realization.Body.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Plane));
        Assert.All(realization.Body.Geometry.Surfaces.Where(x => x.Value.Kind == SurfaceGeometryKind.Torus), x =>
        {
            Assert.Equal(corner - fillet, x.Value.Torus!.Value.MajorRadius, 8);
            Assert.Equal(fillet, x.Value.Torus!.Value.MinorRadius, 8);
        });
        Assert.True(BrepExportPreflight.Validate(realization.Body).IsValid);
    }

    [Fact]
    public void PrimitiveOnly_HasCylindricalSilhouetteCornersButNoEdgeFinishFaces()
    {
        var result = RoundedBoxBRepPlanner.Create(120, 80, 18, 12);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.Body.Topology.Faces.Count());
        Assert.Equal(4, result.Value.Body.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.DoesNotContain(result.Value.Body.Geometry.Surfaces, x => x.Value.Kind == SurfaceGeometryKind.Torus);
    }

    [Theory]
    [InlineData(120, 80, 18, 0, 2)]
    [InlineData(120, 80, 18, 40, 2)]
    [InlineData(120, 80, 18, 12, 0)]
    [InlineData(120, 80, 18, 12, 12)]
    [InlineData(0, 80, 18, 12, 2)]
    public void InvalidDimensionsAndRadii_AreRejectedBeforeTopologyEmission(double width, double depth, double height, double corner, double fillet)
    {
        var result = RoundedBoxBRepPlanner.Create(width, depth, height, corner, fillet);

        Assert.False(result.IsSuccess);
    }
}
