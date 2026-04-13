using Aetheris.Kernel.Core.Brep.EdgeFinishing;

namespace Aetheris.Kernel.Core.Tests.Brep.EdgeFinishing;

public sealed class BrepBoundedEdgeFinishingToolParserTests
{
    [Fact]
    public void TryParseChamferSelection_Allows_CornerFirst_IncidentEdgePair()
    {
        var fields = new Dictionary<string, string>
        {
            ["corners"] = "[\"x_max_y_max_z_max\"]",
            ["corner_edges"] = "[\"x_neg\", \"z_neg\"]"
        };

        var ok = BrepBoundedEdgeFinishingToolParser.TryParseChamferSelection(fields, out var edge, out var incidentEdgePair, out var corner, out var error);

        Assert.True(ok, error);
        Assert.Null(edge);
        Assert.True(incidentEdgePair.HasValue);
        Assert.Equal(BrepBoundedChamferCorner.XMaxYMaxZMax, corner);
    }

    [Fact]
    public void TryParseChamferSelection_Rejects_Duplicate_CornerIncidentEdgePair()
    {
        var fields = new Dictionary<string, string>
        {
            ["corners"] = "[\"x_max_y_max_z_max\"]",
            ["corner_edges"] = "[\"x_neg\", \"x_neg\"]"
        };

        var ok = BrepBoundedEdgeFinishingToolParser.TryParseChamferSelection(fields, out _, out _, out _, out var error);

        Assert.False(ok);
        Assert.Equal("corner-edge selector requires two distinct incident edge tokens", error);
    }
}
