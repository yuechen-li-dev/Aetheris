using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Lowering;

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

        if (plan.HoleKind == HoleKind.Blind && plan.DepthKind == HoleDepthKind.Blind)
        {
            diagnostics.Add("Blind-hole plan accepted for bounded execution.");
            return ExecuteBlind(plan, diagnostics);
        }

        if (plan.HoleKind == HoleKind.Countersink && (plan.DepthKind == HoleDepthKind.ThroughWithEntryRelief || plan.DepthKind == HoleDepthKind.BlindWithEntryRelief))
        {
            diagnostics.Add("Countersink plan accepted for bounded execution.");
            return ExecuteCountersink(plan, diagnostics);
        }

        if (plan.HoleKind == HoleKind.Stepped && plan.DepthKind == HoleDepthKind.ThroughWithEntryRelief)
        {
            diagnostics.Add("Stepped-hole plan recognized with explicit tier placement metadata; execution intentionally deferred in V13.2 pending production route re-enable.");
            diagnostics.Add("Deferred reason: stepped-execution-route-disabled-until-v13.3.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        if (plan.HoleKind != HoleKind.Counterbore || plan.DepthKind != HoleDepthKind.ThroughWithEntryRelief)
        {
            diagnostics.Add("Plan rejected: only bounded through/blind/counterbore/countersink/stepped plans are supported.");
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
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
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
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        diagnostics.Add("Second subtract succeeded.");
        diagnostics.Add("Result BRep body produced.");
        return new(HoleRecoveryExecutionStatus.Succeeded, secondSubtract.Value, diagnostics);
    }



    private static HoleRecoveryExecutionResult ExecuteStepped(HoleRecoveryPlan plan, List<string> diagnostics)
    {
        if (plan.HostKind != HoleHostKind.RectangularBox || plan.Axis != HoleAxisKind.Z || plan.ProfileStack.Count != 3)
        {
            diagnostics.Add("Stepped plan rejected before Boolean: host must be rectangular box, axis must be Z, and profile stack count must be exactly 3.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        var tolerance = Aetheris.Kernel.Core.Numerics.ToleranceContext.Default.Linear;
        var large = plan.ProfileStack[0];
        var medium = plan.ProfileStack[1];
        var small = plan.ProfileStack[2];
        if (large.SegmentKind != HoleProfileSegmentKind.Cylindrical || medium.SegmentKind != HoleProfileSegmentKind.Cylindrical || small.SegmentKind != HoleProfileSegmentKind.Cylindrical)
        {
            diagnostics.Add("Stepped plan rejected before Boolean: all profile segments must be cylindrical.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        if (small.RadiusStart >= medium.RadiusStart - tolerance || medium.RadiusStart >= large.RadiusStart - tolerance)
        {
            diagnostics.Add("Stepped plan rejected before Boolean: strict radius ordering (small < medium < large) is required.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        var largeDepth = large.DepthEnd - large.DepthStart;
        var mediumDepth = medium.DepthEnd - medium.DepthStart;
        if (Math.Abs(small.DepthEnd - plan.ThroughLength) > tolerance || largeDepth <= tolerance || mediumDepth <= tolerance || largeDepth >= mediumDepth - tolerance || mediumDepth >= plan.ThroughLength - tolerance)
        {
            diagnostics.Add("Stepped plan rejected before Boolean: strict depth ordering (large < medium < through) is required.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        diagnostics.Add("Stepped plan shape validated.");
        diagnostics.Add("Stepped repeated-subtract route selected: small-through -> medium-depth -> large-shallow.");
        var box = BrepPrimitives.CreateBox(plan.HostSizeX, plan.HostSizeY, plan.HostSizeZ);
        if (!box.IsSuccess)
        {
            diagnostics.Add("Stepped host box primitive construction failed.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        var body = TranslateBody(box.Value, plan.HostTranslation);
        var throughHeight = double.Max(plan.ThroughLength, plan.HostSizeZ);
        var smallResult = BrepPrimitives.CreateCylinder(small.RadiusStart, throughHeight);
        if (!smallResult.IsSuccess)
        {
            diagnostics.Add("Stepped small-through cylinder primitive construction failed.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        diagnostics.Add("Stepped subtract small invoked.");
        var smallSubtract = BrepBoolean.Subtract(body, TranslateBody(smallResult.Value, plan.ToolTranslation));
        if (!smallSubtract.IsSuccess || smallSubtract.Value is null)
        {
            diagnostics.Add("Stepped subtract small failed.");
            return new(HoleRecoveryExecutionStatus.BooleanFailed, null, diagnostics);
        }

        diagnostics.Add("Stepped subtract small succeeded.");
        var entryFaceZ = plan.HostTranslation.Z + (plan.HostSizeZ * 0.5d);
        diagnostics.Add($"Stepped entry face resolved to top(+Z) at z={entryFaceZ:0.###}.");
        diagnostics.Add($"Stepped route geometry: box=({plan.HostSizeX:0.###},{plan.HostSizeY:0.###},{plan.HostSizeZ:0.###})@({plan.HostTranslation.X:0.###},{plan.HostTranslation.Y:0.###},{plan.HostTranslation.Z:0.###}); small(r={small.RadiusStart:0.###},h={throughHeight:0.###},z=[{(plan.ToolTranslation.Z - (throughHeight * 0.5d)):0.###},{(plan.ToolTranslation.Z + (throughHeight * 0.5d)):0.###}]); medium(r={medium.RadiusStart:0.###},h={mediumDepth:0.###}); large(r={large.RadiusStart:0.###},h={largeDepth:0.###}).");
        var mediumCenter = new Vector3D(plan.ToolTranslation.X, plan.ToolTranslation.Y, entryFaceZ + (mediumDepth * 0.5d));
        var mediumResult = BrepPrimitives.CreateCylinder(medium.RadiusStart, mediumDepth);
        if (!mediumResult.IsSuccess)
        {
            diagnostics.Add("Stepped medium-depth cylinder primitive construction failed.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        diagnostics.Add("Stepped subtract medium invoked.");
        var mediumSubtract = BrepBoolean.Subtract(smallSubtract.Value, TranslateBody(mediumResult.Value, mediumCenter));
        if (!mediumSubtract.IsSuccess || mediumSubtract.Value is null)
        {
            diagnostics.Add($"Stepped subtract medium failed at center=({mediumCenter.X:0.###},{mediumCenter.Y:0.###},{mediumCenter.Z:0.###}) z=[{(mediumCenter.Z - (mediumDepth * 0.5d)):0.###},{(mediumCenter.Z + (mediumDepth * 0.5d)):0.###}].");
            return new(HoleRecoveryExecutionStatus.BooleanFailed, null, diagnostics);
        }

        diagnostics.Add("Stepped subtract medium succeeded.");
        var largeCenter = new Vector3D(plan.ToolTranslation.X, plan.ToolTranslation.Y, entryFaceZ + (largeDepth * 0.5d));
        var largeResult = BrepPrimitives.CreateCylinder(large.RadiusStart, largeDepth);
        if (!largeResult.IsSuccess)
        {
            diagnostics.Add("Stepped large-shallow cylinder primitive construction failed.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        diagnostics.Add("Stepped subtract large invoked.");
        var largeSubtract = BrepBoolean.Subtract(mediumSubtract.Value, TranslateBody(largeResult.Value, largeCenter));
        if (!largeSubtract.IsSuccess || largeSubtract.Value is null)
        {
            diagnostics.Add($"Stepped subtract large failed at center=({largeCenter.X:0.###},{largeCenter.Y:0.###},{largeCenter.Z:0.###}) z=[{(largeCenter.Z - (largeDepth * 0.5d)):0.###},{(largeCenter.Z + (largeDepth * 0.5d)):0.###}].");
            return new(HoleRecoveryExecutionStatus.BooleanFailed, null, diagnostics);
        }

        diagnostics.Add("Stepped subtract large succeeded.");
        diagnostics.Add("Stepped repeated-subtract route succeeded.");
        diagnostics.Add("Result BRep body produced.");
        return new(HoleRecoveryExecutionStatus.Succeeded, largeSubtract.Value, diagnostics);
    }
    private static HoleRecoveryExecutionResult ExecuteBlind(HoleRecoveryPlan plan, List<string> diagnostics)
    {
        if (plan.HostKind != HoleHostKind.RectangularBox || plan.Axis != HoleAxisKind.Z || plan.ProfileStack.Count != 1)
        {
            diagnostics.Add("Blind plan rejected: host/axis/profile mismatch.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        var seg = plan.ProfileStack[0];
        if (seg.SegmentKind != HoleProfileSegmentKind.Cylindrical)
        {
            diagnostics.Add("Blind plan rejected: profile segment must be cylindrical.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        var depth = seg.DepthEnd - seg.DepthStart;
        if (depth <= Aetheris.Kernel.Core.Numerics.ToleranceContext.Default.Linear)
        {
            diagnostics.Add("Blind plan rejected: depth must be positive.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        var boxResult = BrepPrimitives.CreateBox(plan.HostSizeX, plan.HostSizeY, plan.HostSizeZ);
        if (!boxResult.IsSuccess)
        {
            diagnostics.Add("Blind-hole box primitive construction failed.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        var toolResult = BrepPrimitives.CreateCylinder(seg.RadiusStart, depth);
        if (!toolResult.IsSuccess)
        {
            diagnostics.Add("Blind-hole cylinder primitive construction failed.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        var boxBody = TranslateBody(boxResult.Value, plan.HostTranslation);
        var entryTopZ = plan.HostTranslation.Z + (plan.HostSizeZ * 0.5d);
        var entryBottomZ = plan.HostTranslation.Z - (plan.HostSizeZ * 0.5d);
        var touchTop = Math.Abs((plan.ToolTranslation.Z + (depth * 0.5d)) - entryTopZ) <= Aetheris.Kernel.Core.Numerics.ToleranceContext.Default.Linear;
        var centerZ = touchTop ? entryTopZ - (depth * 0.5d) : entryBottomZ + (depth * 0.5d);
        var toolBody = TranslateBody(toolResult.Value, new Vector3D(plan.ToolTranslation.X, plan.ToolTranslation.Y, centerZ));
        diagnostics.Add($"Blind cylinder constructed with entry side {(touchTop ? "top(+Z)" : "bottom(-Z)")}.");

        var sub = BrepBoolean.Subtract(boxBody, toolBody);
        diagnostics.Add("Blind subtract invoked.");
        if (!sub.IsSuccess || sub.Value is null)
        {
            diagnostics.Add("Blind subtract failed.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        diagnostics.Add("Blind subtract succeeded.");
        diagnostics.Add("Result BRep body produced.");
        return new(HoleRecoveryExecutionStatus.Succeeded, sub.Value, diagnostics);
    }

    private static HoleRecoveryExecutionResult ExecuteCountersink(HoleRecoveryPlan plan, List<string> diagnostics)
    {
        if (plan.HostKind != HoleHostKind.RectangularBox || plan.Axis != HoleAxisKind.Z || plan.ProfileStack.Count != 2)
        {
            diagnostics.Add("Countersink plan rejected: host/axis/profile mismatch.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        var coneSeg = plan.ProfileStack[0];
        var cylSeg = plan.ProfileStack[1];
        if (coneSeg.SegmentKind != HoleProfileSegmentKind.Conical || cylSeg.SegmentKind != HoleProfileSegmentKind.Cylindrical)
        {
            diagnostics.Add("Countersink plan rejected: expected conical then cylindrical profile segments.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        var boxResult = BrepPrimitives.CreateBox(plan.HostSizeX, plan.HostSizeY, plan.HostSizeZ);
        var cylResult = BrepPrimitives.CreateCylinder(cylSeg.RadiusStart, double.Max(plan.ThroughLength, plan.HostSizeZ));
        if (!boxResult.IsSuccess || !cylResult.IsSuccess)
        {
            diagnostics.Add("Countersink primitive construction failed for host/cylinder.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        var boxBody = TranslateBody(boxResult.Value, plan.HostTranslation);
        var cylBody = TranslateBody(cylResult.Value, plan.ToolTranslation);
        diagnostics.Add("cylinder subtract invoked.");
        var firstSub = BrepBoolean.Subtract(boxBody, cylBody);
        if (!firstSub.IsSuccess || firstSub.Value is null)
        {
            diagnostics.Add("cylinder subtract failed.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }
        diagnostics.Add("cylinder subtract succeeded.");

        var coneHeight = coneSeg.DepthEnd - coneSeg.DepthStart;
        var coneResult = FirmamentPrimitiveExecutor.ExecuteCone(new FirmamentLoweredConeParameters(coneSeg.RadiusEnd, coneSeg.RadiusStart, coneHeight));
        if (!coneResult.IsSuccess)
        {
            diagnostics.Add("cone primitive construction failed.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        var entryTopZ = plan.HostTranslation.Z + (plan.HostSizeZ * 0.5d);
        var coneCenterZ = entryTopZ - (coneHeight * 0.5d);
        var coneBody = TranslateBody(coneResult.Value, new Vector3D(plan.ToolTranslation.X, plan.ToolTranslation.Y, coneCenterZ));
        diagnostics.Add("cone subtract invoked.");
        var secondSub = BrepBoolean.Subtract(firstSub.Value, coneBody);
        if (!secondSub.IsSuccess || secondSub.Value is null)
        {
            diagnostics.Add("cone subtract failed.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        diagnostics.Add("cone subtract succeeded.");
        diagnostics.Add("Result BRep body produced.");
        return new(HoleRecoveryExecutionStatus.Succeeded, secondSub.Value, diagnostics);
    }


        private static BrepBody TranslateBody(BrepBody body, Vector3D translation)
        => translation == Vector3D.Zero ? body : FirmamentPrimitiveExecutionTranslation.TranslateBody(body, translation);
}
