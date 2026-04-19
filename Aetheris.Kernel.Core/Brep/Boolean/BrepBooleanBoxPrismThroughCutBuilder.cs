using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public static class BrepBooleanBoxPrismThroughCutBuilder
{
    public static KernelResult<BrepBody> Build(
        AxisAlignedBoxExtents box,
        IReadOnlyList<(double X, double Y)> footprint,
        ToleranceContext _)
    {
        var outerFootprint = new[]
        {
            (box.MinX, box.MinY),
            (box.MaxX, box.MinY),
            (box.MaxX, box.MaxY),
            (box.MinX, box.MaxY),
        };

        return BrepBooleanPolygonalPrismThroughCutBuilder.Build(outerFootprint, box, footprint);
    }
}
