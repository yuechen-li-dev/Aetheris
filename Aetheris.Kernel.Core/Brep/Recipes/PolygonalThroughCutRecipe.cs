using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Brep.Recipes;

/// <summary>
/// Recognized polygonal-prism subtraction intent. Correspondence and winding
/// of both footprints are policy facts supplied above the recipe, not inferred
/// here from arbitrary BReps.
/// </summary>
internal sealed record PolygonalThroughCutRecipeRequest(
    IReadOnlyList<(double X, double Y)> OuterFootprint,
    AxisAlignedBoxExtents RootBounds,
    IReadOnlyList<(double X, double Y)> InnerFootprint,
    ToleranceContext Tolerance,
    SafeBooleanComposition? ConstructionHistory = null);

/// <summary>
/// Contrasts with the circular through-hole: each polygon segment produces a
/// separately ordered planar cavity wall, while the support faces receive
/// multi-edge inner loops. The same feature-agnostic Surgery loop, face, shell,
/// and validation mechanics realize this different expected topology.
/// </summary>
internal static class PolygonalThroughCutRecipe
{
    public static KernelResult<BrepBody> Execute(PolygonalThroughCutRecipeRequest? request)
    {
        if (request is null)
        {
            return Failure("Polygonal through-cut recipe requires recognized intent.");
        }

        if (request.OuterFootprint.Count < 3 || request.InnerFootprint.Count < 3)
        {
            return Failure("Polygonal through-cut recipe requires outer and inner footprints with at least three ordered vertices.");
        }

        _ = request.Tolerance; // Recognition consumed tolerance; realization is topology-exact.
        return PolygonalThroughCutTopologyRealizer.Build(
            request.OuterFootprint,
            request.RootBounds,
            request.InnerFootprint);
    }

    private static KernelResult<BrepBody> Failure(string message)
        => KernelResult<BrepBody>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error,
                message,
                "Brep.Recipes.PolygonalThroughCut"),
        ]);
}
