using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Firmament.Execution;

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

        var boxResult = BrepPrimitives.CreateBox(plan.HostSizeX, plan.HostSizeY, plan.HostSizeZ);
        if (!boxResult.IsSuccess)
        {
            diagnostics.Add($"Box primitive construction failed: {string.Join(" | ", boxResult.Diagnostics.Select(d => d.Message))}");
            return new(ThroughHoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        var boxBody = TranslateBody(boxResult.Value, plan.HostTranslation);
        diagnostics.Add("Box primitive constructed.");

        var throughLength = double.Max(plan.ThroughLength, plan.HostSizeZ);
        var cylinderResult = BrepPrimitives.CreateCylinder(plan.ToolRadius, throughLength);
        if (!cylinderResult.IsSuccess)
        {
            diagnostics.Add($"Cylinder primitive construction failed: {string.Join(" | ", cylinderResult.Diagnostics.Select(d => d.Message))}");
            return new(ThroughHoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        var cylinderBody = TranslateBody(cylinderResult.Value, plan.ToolTranslation);
        diagnostics.Add($"Cylinder primitive constructed (height={throughLength:G17}).");

        diagnostics.Add("Boolean subtract invoked.");
        var subtract = BrepBoolean.Subtract(boxBody, cylinderBody);
        if (!subtract.IsSuccess)
        {
            diagnostics.Add($"Boolean subtract failed: {string.Join(" | ", subtract.Diagnostics.Select(d => d.Message))}");
            return new(ThroughHoleRecoveryExecutionStatus.BooleanFailed, null, diagnostics);
        }

        diagnostics.Add("Boolean subtract succeeded.");

        if (subtract.Value is null)
        {
            diagnostics.Add("Invalid result: subtract success returned null body.");
            return new(ThroughHoleRecoveryExecutionStatus.InvalidResult, null, diagnostics);
        }

        diagnostics.Add("Result BRep body produced.");
        return new(ThroughHoleRecoveryExecutionStatus.Succeeded, subtract.Value, diagnostics);
    }

    private static BrepBody TranslateBody(BrepBody body, Aetheris.Kernel.Core.Math.Vector3D translation)
        => translation == Aetheris.Kernel.Core.Math.Vector3D.Zero
            ? body
            : FirmamentPrimitiveExecutionTranslation.TranslateBody(body, translation);
}
