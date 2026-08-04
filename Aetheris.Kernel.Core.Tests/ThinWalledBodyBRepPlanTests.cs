using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests;

public sealed class ThinWalledBodyBRepPlanTests
{
    [Fact]
    public void RoundedBoxHollow_UsesOneAuthoritativePairedBoundaryPlan()
    {
        var result = ThinWalledBodyBRepPlanner.CreateRoundedBox(120, 80, 24, 12, 2);
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics.Select(x => x.Message)));
        var value = result.Value;
        Assert.True(value.Plan.IsAuthoritative);
        Assert.Equal("ThinWalledBody", value.Plan.Kind);
        Assert.Equal("InsetRoundedProfile", value.Feature.Witness.Kind);
        Assert.Equal(26, value.Body.Topology.Faces.Count());
        Assert.Equal(8, value.Body.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Null(value.Body.SafeBooleanComposition);
        var step = Step242Exporter.ExportBody(value.Body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join(", ", step.Diagnostics.Select(x => x.Message)));
        Assert.True(Step242Importer.ImportBody(step.Value).IsSuccess);
    }

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

    [Theory]
    [InlineData(120, 80, 24, 12)]
    [InlineData(120, 80, 2, 2)]
    public void RoundedBoxHollow_RejectsCollapsedInnerBoundary(double width, double depth, double height, double thickness)
        => Assert.False(ThinWalledBodyBRepPlanner.CreateRoundedBox(width, depth, height, 12, thickness).IsSuccess);
}
