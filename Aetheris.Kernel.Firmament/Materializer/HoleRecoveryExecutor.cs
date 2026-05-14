using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Materializer;

public enum HoleRecoveryExecutionStatus
{
    Succeeded,
    UnsupportedPlan,
    PrimitiveConstructionFailed,
    BooleanFailed,
    InvalidResult,
    Failed
}

public sealed record HoleRecoveryExecutionResult(
    HoleRecoveryExecutionStatus Status,
    BrepBody? Body,
    IReadOnlyList<string> Diagnostics);

public static class HoleRecoveryExecutor
{
    public static HoleRecoveryExecutionResult Execute(HoleRecoveryPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var diagnostics = new List<string>
        {
            "HoleRecoveryExecutor started.",
            "No STEP export attempted by hole executor."
        };

        diagnostics.Add($"Plan kind inspection: holeKind={plan.HoleKind}, depthKind={plan.DepthKind}.");
        if (plan.HoleKind == HoleKind.Through)
        {
            diagnostics.Add("Plan kind accepted: through-hole delegated to ThroughHoleRecoveryExecutor.");
            if (!ThroughHoleRecoveryPlanAdapter.TryConvert(plan, out var throughPlan) || throughPlan is null)
            {
                diagnostics.Add("Through-hole adapter conversion failed.");
                return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
            }

            var through = ThroughHoleRecoveryExecutor.Execute(throughPlan);
            diagnostics.AddRange(through.Diagnostics);
            return new((HoleRecoveryExecutionStatus)through.Status, through.Body, diagnostics);
        }

        if (plan.HoleKind != HoleKind.Counterbore || plan.DepthKind != HoleDepthKind.ThroughWithEntryRelief)
        {
            diagnostics.Add("Plan rejected: only bounded counterbore ThroughWithEntryRelief is supported.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        diagnostics.Add("Counterbore plan accepted for bounded execution.");
        if (plan.HostKind != HoleHostKind.RectangularBox || plan.Axis != HoleAxisKind.Z || plan.ProfileStack.Count != 2)
        {
            diagnostics.Add("Plan rejected: host/axis/profile shape mismatch for bounded counterbore.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        var large = plan.ProfileStack[0];
        var small = plan.ProfileStack[1];
        if (large.SegmentKind != HoleProfileSegmentKind.Cylindrical || small.SegmentKind != HoleProfileSegmentKind.Cylindrical)
        {
            diagnostics.Add("Plan rejected: both profile segments must be cylindrical.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        var tolerance = Aetheris.Kernel.Core.Numerics.ToleranceContext.Default.Linear;
        if (Math.Abs(small.DepthEnd - plan.ThroughLength) > tolerance || large.RadiusStart <= small.RadiusStart + tolerance)
        {
            diagnostics.Add("Plan rejected: profile stack does not match counterbore (large shallow + small through).");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        diagnostics.Add("Profile stack validated.");

        var boxResult = BrepPrimitives.CreateBox(plan.HostSizeX, plan.HostSizeY, plan.HostSizeZ);
        if (!boxResult.IsSuccess)
        {
            diagnostics.Add("Box primitive construction failed.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        var boxBody = TranslateBody(boxResult.Value, plan.HostTranslation);

        var throughHeight = double.Max(plan.ThroughLength, plan.HostSizeZ);
        var smallResult = BrepPrimitives.CreateCylinder(small.RadiusStart, throughHeight);
        if (!smallResult.IsSuccess)
        {
            diagnostics.Add("Small through cylinder primitive construction failed.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        var smallBody = TranslateBody(smallResult.Value, plan.ToolTranslation);
        diagnostics.Add("Small through cylinder constructed.");

        var firstSubtract = BrepBoolean.Subtract(boxBody, smallBody);
        if (!firstSubtract.IsSuccess || firstSubtract.Value is null)
        {
            diagnostics.Add("First subtract failed (host - through cylinder).");
            return new(HoleRecoveryExecutionStatus.BooleanFailed, null, diagnostics);
        }

        diagnostics.Add("First subtract succeeded.");

        var shallowDepth = large.DepthEnd - large.DepthStart;
        if (shallowDepth <= tolerance)
        {
            diagnostics.Add("Plan rejected: counterbore shallow depth must be positive.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        var entryFaceZ = plan.HostTranslation.Z - (plan.HostSizeZ * 0.5d);
        var largeCenterZ = entryFaceZ + (shallowDepth * 0.5d);
        var largeToolTranslation = new Vector3D(plan.ToolTranslation.X, plan.ToolTranslation.Y, largeCenterZ);

        var largeResult = BrepPrimitives.CreateCylinder(large.RadiusStart, shallowDepth);
        if (!largeResult.IsSuccess)
        {
            diagnostics.Add("Large counterbore cylinder primitive construction failed.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        var largeBody = TranslateBody(largeResult.Value, largeToolTranslation);
        diagnostics.Add("Large counterbore cylinder constructed.");

        var secondSubtract = BrepBoolean.Subtract(firstSubtract.Value, largeBody);
        if (!secondSubtract.IsSuccess || secondSubtract.Value is null)
        {
            diagnostics.Add("Second subtract failed (through result - counterbore relief).");
            return new(HoleRecoveryExecutionStatus.BooleanFailed, null, diagnostics);
        }

        diagnostics.Add("Second subtract succeeded.");
        diagnostics.Add("Result BRep body produced.");
        return new(HoleRecoveryExecutionStatus.Succeeded, secondSubtract.Value, diagnostics);
    }

    private static BrepBody TranslateBody(BrepBody body, Vector3D translation)
        => translation == Vector3D.Zero ? body : FirmamentPrimitiveExecutionTranslation.TranslateBody(body, translation);
}
