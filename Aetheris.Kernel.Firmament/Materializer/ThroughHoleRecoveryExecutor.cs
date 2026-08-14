using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Recipes;

namespace Aetheris.Kernel.Firmament.Materializer;

public enum ThroughHoleRecoveryExecutionStatus
{
    Succeeded,
    UnsupportedPlan,
    PrimitiveConstructionFailed,
    BooleanFailed,
    InvalidResult,
    Failed
}

public sealed record ThroughHoleRecoveryExecutionResult(
    ThroughHoleRecoveryExecutionStatus Status,
    BrepBody? Body,
    IReadOnlyList<string> Diagnostics);

public static class ThroughHoleRecoveryExecutor
{
    public static ThroughHoleRecoveryExecutionResult Execute(ThroughHoleRecoveryPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var diagnostics = new List<string>
        {
            "ThroughHoleRecoveryExecutor started.",
            "No STEP export attempted in CIR-RECOVERY-V2.",
            "No rematerializer/fall-forward wiring attempted in CIR-RECOVERY-V2."
        };

        if (plan.HostKind != ThroughHoleHostKind.RectangularBox
            || plan.ToolKind != ThroughHoleToolKind.Cylindrical
            || plan.ProfileKind != ThroughHoleProfileKind.Circular
            || plan.Axis != ThroughHoleAxisKind.Z)
        {
            diagnostics.Add($"Plan rejected: unsupported specialization host={plan.HostKind}, tool={plan.ToolKind}, profile={plan.ProfileKind}, axis={plan.Axis}.");
            return new(ThroughHoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        diagnostics.Add("Plan specialization accepted: rectangular-box + cylindrical + circular + Z-axis.");

        var throughLength = double.Max(plan.ThroughLength, plan.HostSizeZ);
        var request = ThroughHoleRecipeRequestBuilder.FromBoxAndZCylinder(
            plan.HostSizeX,
            plan.HostSizeY,
            plan.HostSizeZ,
            plan.HostTranslation,
            plan.ToolRadius,
            throughLength,
            plan.ToolTranslation);
        if (!request.IsSuccess)
        {
            diagnostics.Add($"Recipe request construction failed: {string.Join(" | ", request.Diagnostics.Select(d => d.Message))}");
            return new(ThroughHoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        diagnostics.Add($"Through-hole Recipe request constructed from recognized semantics (height={throughLength:G17}).");
        diagnostics.Add("Direct ThroughHoleConstructionRecipe invoked; no temporary tool BRep or Boolean recognition pass.");
        var recipe = ThroughHoleConstructionRecipe.Execute(request.Value);
        if (!recipe.IsSuccess)
        {
            diagnostics.Add($"Through-hole Recipe failed: {string.Join(" | ", recipe.Diagnostics.Select(d => d.Message))}");
            return new(ThroughHoleRecoveryExecutionStatus.BooleanFailed, null, diagnostics);
        }

        diagnostics.Add("Through-hole Recipe succeeded.");

        if (recipe.Value is null)
        {
            diagnostics.Add("Invalid result: Recipe success returned null body.");
            return new(ThroughHoleRecoveryExecutionStatus.InvalidResult, null, diagnostics);
        }

        diagnostics.Add("Result BRep body produced.");
        return new(ThroughHoleRecoveryExecutionStatus.Succeeded, recipe.Value, diagnostics);
    }
}
