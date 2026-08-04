using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests;

public sealed class CubicLatticeBRepPlanTests
{
    [Fact]
    public void CanonicalGraph_IsDeterministicAndSharesNodesAndMembers()
    {
        var graph = CubicLatticeBRepPlanner.BuildGraph(3, 3, 3, 8d);

        Assert.Equal(27, graph.Cells.Count);
        Assert.Equal(64, graph.Nodes.Count);
        Assert.Equal(144, graph.Members.Count);
        Assert.Equal(8, graph.Nodes.Count(node => node.Valence == 3));
        Assert.Equal(24, graph.Nodes.Count(node => node.Valence == 4));
        Assert.Equal(24, graph.Nodes.Count(node => node.Valence == 5));
        Assert.Equal(8, graph.Nodes.Count(node => node.Valence == 6));
        Assert.Equal(graph.Signature, CubicLatticeBRepPlanner.BuildGraph(3, 3, 3, 8d).Signature);
    }

    [Fact]
    public void CanonicalPrimitive_UsesOneAuthoritativeAnalyticPlanAndRoundTripsStep()
    {
        var result = CubicLatticeBRepPlanner.Create(3, 3, 3, 8d, .8d, 1.2d);

        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var realization = result.Value;
        Assert.True(realization.Plan.IsAuthoritative);
        Assert.Equal(288, realization.Plan.SeamCount);
        Assert.Equal(64 + 144, realization.Body.Topology.Faces.Count());
        Assert.Equal(64, realization.Body.Geometry.Surfaces.Count(pair => pair.Value.Kind == SurfaceGeometryKind.Sphere));
        Assert.Equal(144, realization.Body.Geometry.Surfaces.Count(pair => pair.Value.Kind == SurfaceGeometryKind.Cylinder));
        var step = Step242Exporter.ExportBody(realization.Body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join(", ", step.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var imported = Step242Importer.ImportBody(step.Value);
        Assert.True(imported.IsSuccess, string.Join(", ", imported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var mass = BrepMassProperties.Evaluate(realization.Body);
        Assert.True(mass.Status != BrepMassPropertiesStatus.Unavailable, string.Join(", ", mass.Diagnostics));
        Assert.True(mass.AbsoluteVolume > 0d, string.Join(", ", mass.Diagnostics));
    }

    [Theory]
    [InlineData(.8d, 1.1d, CubicLatticeBRepPlanner.NodeRadiusTooSmallForStruts)]
    [InlineData(.8d, 1.2d, "")]
    public void GeometryAdmissions_AreExplicit(double strutRadius, double nodeRadius, string expectedFailure)
    {
        var result = CubicLatticeBRepPlanner.Create(1, 1, 1, 2d, strutRadius, nodeRadius);

        Assert.Equal(string.IsNullOrEmpty(expectedFailure), result.IsSuccess);
        if (!result.IsSuccess) Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message == expectedFailure);
    }
}
