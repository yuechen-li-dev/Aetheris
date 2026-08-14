using Aetheris.Kernel.Core.Brep.Recipes;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Brep.Boolean;

/// <summary>
/// Compatibility/rollback adapter retained while callers still enter through
/// Boolean vocabulary. Topology ownership lives in Brep.Recipes.
/// </summary>
internal static class BrepBooleanPolygonalPrismThroughCutBuilder
{
    public static KernelResult<BrepBody> Build(
        IReadOnlyList<(double X, double Y)> outerFootprint,
        AxisAlignedBoxExtents rootBounds,
        IReadOnlyList<(double X, double Y)> innerFootprint)
        => PolygonalThroughCutRecipe.Execute(new(
            outerFootprint,
            rootBounds,
            innerFootprint,
            ToleranceContext.Default));

    internal static KernelResult<BrepBody> BuildLegacy(
        IReadOnlyList<(double X, double Y)> outerFootprint,
        AxisAlignedBoxExtents rootBounds,
        IReadOnlyList<(double X, double Y)> innerFootprint)
        => PolygonalThroughCutTopologyRealizer.Build(outerFootprint, rootBounds, innerFootprint);
}
