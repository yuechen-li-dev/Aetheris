using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

/// <summary>Parts as hobbyists actually export them: AP214 from Fusion, filleted everywhere.</summary>
public sealed class Step242CommunityPartTests
{
    private const string So101UnderArm = "testdata/step242/community/so101/Under_arm_SO101.step";

    [Fact]
    public void So101UnderArm_ImportsDisplaysAndRoundTrips()
    {
        // Reversed coedges on planar faces bounded by ellipses (oblique cuts through fillet
        // cylinders) once built a backwards ParameterInterval and rejected the whole file.
        var body = Step242Corpus.Body(So101UnderArm);
        Assert.Equal(295, body.Topology.Faces.Count());
        Assert.True(BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true).IsSuccess);
        Assert.True(BrepDisplayTessellator.Tessellate(body).IsSuccess);

        var exported = Step242Exporter.ExportBody(body);
        Assert.True(exported.IsSuccess, string.Join("\n", exported.Diagnostics.Select(d => d.Message)));
        var reimported = Step242Importer.ImportBody(exported.Value);
        Assert.True(reimported.IsSuccess, string.Join("\n", reimported.Diagnostics.Select(d => d.Message)));
        Assert.Equal(body.Topology.Faces.Count(), reimported.Value.Topology.Faces.Count());
        Assert.Equal(body.Topology.Edges.Count(), reimported.Value.Topology.Edges.Count());
        Assert.Equal(body.Topology.Vertices.Count(), reimported.Value.Topology.Vertices.Count());
    }
}
