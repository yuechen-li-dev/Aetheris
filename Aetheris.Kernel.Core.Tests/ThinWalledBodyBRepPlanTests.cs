using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests;

public sealed class ThinWalledBodyBRepPlanTests
{
    [Fact]
    public void FrustumHollow_UsesParallelConicalOffsetRatherThanRadialShrink()
    {
        var result = ThinWalledBodyBRepPlanner.CreateFrustum(32, 43, 90, 2);
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics.Select(x => x.Message)));
        var value = result.Value;
        Assert.Equal("ParallelConicalOffset", value.Feature.Witness.Kind);
        Assert.Equal(5, value.Body.Topology.Faces.Count());
        Assert.Equal(2, value.Body.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Cone));
        Assert.Equal(2, value.Construction.ThicknessWitnesses[0].Distance);
        Assert.Null(value.Body.SafeBooleanComposition);
    }

    [Fact]
    public void CylinderHollow_HasExactPairedWallsClosedBottomAndOpenTop()
    {
        var result = ThinWalledBodyBRepPlanner.CreateCylinder(56, 142, 0.8);
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics.Select(x => x.Message)));
        var value = result.Value;
        Assert.Equal("CoaxialCylindricalOffset", value.Feature.Witness.Kind);
        Assert.Equal(5, value.Body.Topology.Faces.Count());
        Assert.Equal(2, value.Body.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(0.8, value.Construction.ThicknessWitnesses[0].Distance);
        Assert.Equal(0.8, value.Construction.ThicknessWitnesses[1].Distance);
        Assert.Single(value.Plan.RimFaces);
        Assert.Null(value.Body.SafeBooleanComposition);
        var step = Step242Exporter.ExportBody(value.Body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join(", ", step.Diagnostics.Select(x => x.Message)));
        Assert.True(Step242Importer.ImportBody(step.Value).IsSuccess);
    }

    [Theory]
    [InlineData(56, 142, 56)]
    [InlineData(56, 0.8, 0.8)]
    public void CylinderHollow_RejectsCollapsedInnerBoundary(double radius, double height, double thickness)
        => Assert.False(ThinWalledBodyBRepPlanner.CreateCylinder(radius, height, thickness).IsSuccess);
}
