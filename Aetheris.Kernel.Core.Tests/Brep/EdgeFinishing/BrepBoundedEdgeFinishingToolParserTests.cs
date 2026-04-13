using Aetheris.Kernel.Core.Brep.EdgeFinishing;

namespace Aetheris.Kernel.Core.Tests.Brep.EdgeFinishing;

public sealed class BrepBoundedEdgeFinishingToolParserTests
{
    [Fact]
    public void TryParseChamferSelection_Allows_TwoEdgeCornerPair()
    {
        var fields = new Dictionary<string, string>
        {
            ["edges"] = "[\"x_max_y_max\", \"x_max_y_min\"]"
        };

        var ok = BrepBoundedEdgeFinishingToolParser.TryParseChamferSelection(fields, out var edge, out var edgePair, out var corner, out var error);

        Assert.True(ok, error);
        Assert.Null(edge);
        Assert.True(edgePair.HasValue);
        Assert.Null(corner);
    }
}
