using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Firmament.Air;

namespace Aetheris.Kernel.Firmament.Tests.Air;

public sealed class AirProfileExtrudeWrapperTests
{
    [Fact]
    public void AirProfileExtrudeWrapper_ProducesSummary_WithProvenance()
    {
        var summary = AirProfileExtrudeWrapper.LowerCanonicalRectangleExtrude();
        Assert.True(summary.Succeeded);
        Assert.Equal(AirNodeKind.ProfileExtrude, summary.NodeKind);
        Assert.Equal(AirRouteKind.ProfileExtrudeEmitter, summary.RouteKind);
        Assert.Equal("AIR-X1", summary.Provenance.Milestone);
        Assert.Equal(AirSelectionClass.None, summary.Provenance.SelectionClass);
        Assert.Equal(AirRuleKind.None, summary.Provenance.RuleKind);
        Assert.True(summary.Provenance.IsProductionRoute);
        Assert.True(summary.TopologySummary.VertexCount > 0);
        Assert.True(summary.TopologySummary.FaceCount > 0);
        Assert.Contains(summary.Diagnostics, d => d.Code == "air-x1-no-production-route-replacement");
        Assert.Contains(summary.Diagnostics, d => d.Code == "air-x1-profile-extrude-existing-emitter-invoked");
    }
}
