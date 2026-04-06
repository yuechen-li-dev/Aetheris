using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;

namespace Aetheris.Kernel.Core.Tests.Brep.Features;

public sealed class BrepBoundedManufacturingFilletPreflightTests
{
    [Fact]
    public void ResolveInternalConcaveVerticalEdge_Succeeds_ForOrthogonalLRoot_WithBoundedRadius()
    {
        var composition = new SafeBooleanComposition(
            new AxisAlignedBoxExtents(0d, 30d, 0d, 20d, 0d, 10d),
            [],
            OccupiedCells:
            [
                new AxisAlignedBoxExtents(0d, 30d, 0d, 10d, 0d, 10d),
                new AxisAlignedBoxExtents(0d, 10d, 10d, 20d, 0d, 10d)
            ]);

        var result = BrepBoundedManufacturingFilletPreflight.ResolveInternalConcaveVerticalEdge(
            composition,
            BrepBoundedManufacturingFilletEdge.InnerXMaxYMax,
            radius: 2d);

        Assert.True(result.IsSuccess);
        Assert.Equal(10d, result.Value.EdgeX);
        Assert.Equal(10d, result.Value.EdgeY);
    }

    [Fact]
    public void ResolveInternalConcaveVerticalEdge_Rejects_OverlargeRadius()
    {
        var composition = new SafeBooleanComposition(
            new AxisAlignedBoxExtents(0d, 30d, 0d, 20d, 0d, 10d),
            [],
            OccupiedCells:
            [
                new AxisAlignedBoxExtents(0d, 30d, 0d, 10d, 0d, 10d),
                new AxisAlignedBoxExtents(0d, 10d, 10d, 20d, 0d, 10d)
            ]);

        var result = BrepBoundedManufacturingFilletPreflight.ResolveInternalConcaveVerticalEdge(
            composition,
            BrepBoundedManufacturingFilletEdge.InnerXMaxYMax,
            radius: 5d);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("too large", StringComparison.Ordinal));
    }
}
