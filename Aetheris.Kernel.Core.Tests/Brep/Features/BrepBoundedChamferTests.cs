using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.EdgeFinishing;

namespace Aetheris.Kernel.Core.Tests.Brep.Features;

public sealed class BrepBoundedChamferTests
{
    [Fact]
    public void ChamferAxisAlignedBoxVerticalEdge_Succeeds_ForSingleExplicitConvexEdge()
    {
        var box = new AxisAlignedBoxExtents(0d, 40d, 0d, 20d, 0d, 10d);

        var result = BrepBoundedChamfer.ChamferAxisAlignedBoxVerticalEdge(
            box,
            BrepBoundedChamferEdge.XMaxYMax,
            distance: 1.5d);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value.Topology.Faces.Count());
        Assert.True(result.Value.Topology.Edges.Count() > 12);
    }

    [Fact]
    public void ChamferAxisAlignedBoxVerticalEdge_Rejects_OverlargeDistance()
    {
        var box = new AxisAlignedBoxExtents(0d, 8d, 0d, 5d, 0d, 10d);

        var result = BrepBoundedChamfer.ChamferAxisAlignedBoxVerticalEdge(
            box,
            BrepBoundedChamferEdge.XMinYMin,
            distance: 5d);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("too large", StringComparison.Ordinal));
    }
}
