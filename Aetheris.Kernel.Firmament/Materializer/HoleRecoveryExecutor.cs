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
        if (plan.HoleKind == HoleKind.ChamferedEntry && (plan.DepthKind == HoleDepthKind.ThroughWithEntryRelief || plan.DepthKind == HoleDepthKind.BlindWithEntryRelief))
        {
            diagnostics.Add("Chamfered-entry plan accepted for bounded execution.");
            return ExecuteCountersinkLike(plan, diagnostics, "chamfer cone");
        }

        if (plan.HoleKind == HoleKind.Stepped && plan.DepthKind == HoleDepthKind.ThroughWithEntryRelief)
        {
            diagnostics.Add("Stepped-hole plan recognized; stepped execution started.");
            return ExecuteStepped(plan, diagnostics);
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

        if (!TryBuildPlacementCylinderTool(plan, small, "counterbore-through", diagnostics, out var smallBody) || smallBody is null)
        {
            diagnostics.Add("Counterbore plan rejected before Boolean: placement-driven through segment failed.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        var firstSubtract = BrepBoolean.Subtract(boxBody, smallBody);
        if (!firstSubtract.IsSuccess || firstSubtract.Value is null)
        {
            diagnostics.Add("First subtract failed (host - through cylinder).");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        diagnostics.Add("First subtract succeeded.");

        if (!TryBuildPlacementCylinderTool(plan, large, "counterbore-relief", diagnostics, out var largeBody) || largeBody is null)
        {
            diagnostics.Add("Counterbore plan rejected before Boolean: placement-driven relief segment failed.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

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



    private static bool TryBuildPlacementCylinderTool(HoleRecoveryPlan plan, HoleProfileSegment segment, string role, List<string> diagnostics, out BrepBody? body)
    {
        body = null;
        if (!TryValidateExecutablePlacement(plan, segment, role, diagnostics, out var height, out var centerZ)) return false;
        var cylinder = BrepPrimitives.CreateCylinder(segment.RadiusStart, height);
        if (!cylinder.IsSuccess)
        {
            diagnostics.Add($"hole-executor: primitive-failed segment={role} kind=cylinder radius={segment.RadiusStart:0.###} height={height:0.###}");
            return false;
        }

        body = TranslateBody(cylinder.Value, new Vector3D(plan.ToolTranslation.X, plan.ToolTranslation.Y, centerZ));
        diagnostics.Add($"hole-executor: placement-driven segment={role} anchor={segment.AnchorSide} zMin={segment.ZMin:0.###} zMax={segment.ZMax:0.###} height={height:0.###} centerZ={centerZ:0.###}");
        diagnostics.Add($"Blind cylinder constructed with entry side {(segment.AnchorSide == HoleTierAnchorSide.Top ? "top(+Z)" : segment.AnchorSide == HoleTierAnchorSide.Bottom ? "bottom(-Z)" : "through")}.");
        return true;
    }

    private static bool TryBuildPlacementConeTool(HoleRecoveryPlan plan, HoleProfileSegment segment, string role, string coneLabel, List<string> diagnostics, out BrepBody? body)
    {
        body = null;
        if (!TryValidateExecutablePlacement(plan, segment, role, diagnostics, out var height, out var centerZ)) return false;
        var cone = FirmamentPrimitiveExecutor.ExecuteCone(new FirmamentLoweredConeParameters(segment.RadiusEnd, segment.RadiusStart, height));
        if (!cone.IsSuccess)
        {
            diagnostics.Add($"hole-executor: primitive-failed segment={role} kind={coneLabel} rBottom={segment.RadiusEnd:0.###} rTop={segment.RadiusStart:0.###} height={height:0.###}");
            return false;
        }

        body = TranslateBody(cone.Value, new Vector3D(plan.ToolTranslation.X, plan.ToolTranslation.Y, centerZ));
        diagnostics.Add($"hole-executor: placement-driven segment={role} anchor={segment.AnchorSide} zMin={segment.ZMin:0.###} zMax={segment.ZMax:0.###} height={height:0.###} centerZ={centerZ:0.###}");
        diagnostics.Add($"entry side resolved for {coneLabel}: {(segment.AnchorSide == HoleTierAnchorSide.Top ? "top(+Z)" : segment.AnchorSide == HoleTierAnchorSide.Bottom ? "bottom(-Z)" : "through") }.");
        return true;
    }

    private static bool TryValidateExecutablePlacement(HoleRecoveryPlan plan, HoleProfileSegment segment, string role, List<string> diagnostics, out double height, out double centerZ)
    {
        height = 0d; centerZ = 0d;
        var tol = Aetheris.Kernel.Core.Numerics.ToleranceContext.Default.Linear;
        var hostMinZ = plan.HostTranslation.Z - (plan.HostSizeZ * 0.5d);
        var hostMaxZ = plan.HostTranslation.Z + (plan.HostSizeZ * 0.5d);

        if (segment.PlacementDiagnostics is null || segment.PlacementDiagnostics.Count == 0)
        {
            diagnostics.Add($"hole-executor: placement-validation-failed segment={role} reason=missing-placement-diagnostics");
            return false;
        }

        if (double.IsNaN(segment.ZMin) || double.IsNaN(segment.ZMax) || segment.ZMax - segment.ZMin <= tol)
        {
            diagnostics.Add($"hole-executor: placement-validation-failed segment={role} reason=invalid-z-span zMin={segment.ZMin:0.###} zMax={segment.ZMax:0.###}");
            return false;
        }

        height = segment.ZMax - segment.ZMin;
        centerZ = (segment.ZMin + segment.ZMax) * 0.5d;

        if (segment.IsThrough)
        {
            if (segment.AnchorSide != HoleTierAnchorSide.Through)
            {
                diagnostics.Add($"hole-executor: placement-validation-failed segment={role} reason=through-anchor-mismatch anchor={segment.AnchorSide}");
                return false;
            }

            if (segment.ZMin > hostMinZ + tol || segment.ZMax < hostMaxZ - tol)
            {
                diagnostics.Add($"hole-executor: placement-validation-failed segment={role} reason=through-z-coverage hostZMin={hostMinZ:0.###} hostZMax={hostMaxZ:0.###}");
                return false;
            }
        }
        else if (segment.AnchorSide != HoleTierAnchorSide.Top && segment.AnchorSide != HoleTierAnchorSide.Bottom)
        {
            diagnostics.Add($"hole-executor: placement-validation-failed segment={role} reason=blind-anchor-invalid anchor={segment.AnchorSide}");
            return false;
        }

        return true;
    }

    private static HoleRecoveryExecutionResult ExecuteStepped(HoleRecoveryPlan plan, List<string> diagnostics)
    {
        diagnostics.Add("Stepped explicit-placement validation started.");
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

        foreach (var segment in plan.ProfileStack)
        {
            if (segment.AnchorSide == HoleTierAnchorSide.Unknown)
            {
                diagnostics.Add("Stepped plan rejected before Boolean: anchor side cannot be Unknown.");
                return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
            }

            if (double.IsNaN(segment.ZMin) || double.IsNaN(segment.ZMax) || segment.ZMax - segment.ZMin <= tolerance)
            {
                diagnostics.Add("Stepped plan rejected before Boolean: every tier must provide a valid explicit z-span.");
                return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
            }

            if (segment.PlacementDiagnostics is null || segment.PlacementDiagnostics.Count == 0)
            {
                diagnostics.Add("Stepped plan rejected before Boolean: every tier must provide non-empty placement diagnostics.");
                return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
            }
        }

        if (!small.IsThrough || small.AnchorSide != HoleTierAnchorSide.Through)
        {
            diagnostics.Add("Stepped plan rejected before Boolean: small tier must explicitly be through with through anchor.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        if (medium.IsThrough || large.IsThrough)
        {
            diagnostics.Add("Stepped plan rejected before Boolean: medium/large tiers must be blind tiers (IsThrough=false).");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        if (medium.AnchorSide != large.AnchorSide || (medium.AnchorSide != HoleTierAnchorSide.Top && medium.AnchorSide != HoleTierAnchorSide.Bottom))
        {
            diagnostics.Add("Stepped plan rejected before Boolean: medium and large blind tiers must share a concrete entry anchor side.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        var hostMinZ = plan.HostTranslation.Z - (plan.HostSizeZ * 0.5d);
        var hostMaxZ = plan.HostTranslation.Z + (plan.HostSizeZ * 0.5d);
        if (small.ZMin > hostMinZ + tolerance || small.ZMax < hostMaxZ - tolerance)
        {
            diagnostics.Add("Stepped plan rejected before Boolean: through tier explicit z-span must cover host z-range.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        if ((medium.ZMin < hostMinZ - tolerance || medium.ZMax > hostMaxZ + tolerance) || (large.ZMin < hostMinZ - tolerance || large.ZMax > hostMaxZ + tolerance))
        {
            diagnostics.Add("Stepped plan rejected before Boolean: blind tier explicit z-span must stay within host z-range.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        diagnostics.Add("Stepped explicit-placement validation succeeded.");
        diagnostics.Add("Stepped executor marker: no-hidden-placement-inference; explicit z-span authority.");
        diagnostics.Add($"Stepped segment placement: large radius={large.RadiusStart:0.###} zMin={large.ZMin:0.###} zMax={large.ZMax:0.###} anchor={large.AnchorSide} through={large.IsThrough}.");
        diagnostics.Add($"Stepped segment placement: medium radius={medium.RadiusStart:0.###} zMin={medium.ZMin:0.###} zMax={medium.ZMax:0.###} anchor={medium.AnchorSide} through={medium.IsThrough}.");
        diagnostics.Add($"Stepped segment placement: small radius={small.RadiusStart:0.###} zMin={small.ZMin:0.###} zMax={small.ZMax:0.###} anchor={small.AnchorSide} through={small.IsThrough}.");
        diagnostics.Add("Stepped executor route: repeated-subtract-small-medium-large.");
        var box = BrepPrimitives.CreateBox(plan.HostSizeX, plan.HostSizeY, plan.HostSizeZ);
        if (!box.IsSuccess)
        {
            diagnostics.Add("Stepped host box primitive construction failed.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        var body = TranslateBody(box.Value, plan.HostTranslation);
        var smallHeight = small.ZMax - small.ZMin;
        var smallCenterZ = (small.ZMin + small.ZMax) * 0.5d;
        var smallResult = BrepPrimitives.CreateCylinder(small.RadiusStart, smallHeight);
        if (!smallResult.IsSuccess)
        {
            diagnostics.Add("Stepped small-through cylinder primitive construction failed.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        diagnostics.Add("Stepped subtract small invoked.");
        var smallSubtract = BrepBoolean.Subtract(body, TranslateBody(smallResult.Value, new Vector3D(plan.ToolTranslation.X, plan.ToolTranslation.Y, smallCenterZ)));
        if (!smallSubtract.IsSuccess || smallSubtract.Value is null)
        {
            diagnostics.Add($"Stepped subtract small failed: codes={string.Join(",", smallSubtract.Diagnostics.Select(d => d.Code))}.");
            return new(HoleRecoveryExecutionStatus.BooleanFailed, null, diagnostics);
        }

        diagnostics.Add("Stepped subtract small succeeded.");
        var mediumHeight = medium.ZMax - medium.ZMin;
        var mediumCenter = new Vector3D(plan.ToolTranslation.X, plan.ToolTranslation.Y, (medium.ZMin + medium.ZMax) * 0.5d);
        var mediumResult = BrepPrimitives.CreateCylinder(medium.RadiusStart, mediumHeight);
        if (!mediumResult.IsSuccess)
        {
            diagnostics.Add("Stepped medium-depth cylinder primitive construction failed.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        diagnostics.Add("Stepped subtract medium invoked.");
        var mediumSubtract = BrepBoolean.Subtract(smallSubtract.Value, TranslateBody(mediumResult.Value, mediumCenter));
        if (!mediumSubtract.IsSuccess || mediumSubtract.Value is null)
        {
            diagnostics.Add($"Stepped subtract medium failed: codes={string.Join(",", mediumSubtract.Diagnostics.Select(d => d.Code))}.");
            return new(HoleRecoveryExecutionStatus.BooleanFailed, null, diagnostics);
        }

        diagnostics.Add("Stepped subtract medium succeeded.");
        var largeHeight = large.ZMax - large.ZMin;
        var largeCenter = new Vector3D(plan.ToolTranslation.X, plan.ToolTranslation.Y, (large.ZMin + large.ZMax) * 0.5d);
        var largeResult = BrepPrimitives.CreateCylinder(large.RadiusStart, largeHeight);
        if (!largeResult.IsSuccess)
        {
            diagnostics.Add("Stepped large-shallow cylinder primitive construction failed.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        diagnostics.Add("Stepped subtract large invoked.");
        var largeSubtract = BrepBoolean.Subtract(mediumSubtract.Value, TranslateBody(largeResult.Value, largeCenter));
        if (!largeSubtract.IsSuccess || largeSubtract.Value is null)
        {
            diagnostics.Add($"Stepped subtract large failed: codes={string.Join(",", largeSubtract.Diagnostics.Select(d => d.Code))}.");
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

                var boxResult = BrepPrimitives.CreateBox(plan.HostSizeX, plan.HostSizeY, plan.HostSizeZ);
        if (!boxResult.IsSuccess)
        {
            diagnostics.Add("Blind-hole box primitive construction failed.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        var boxBody = TranslateBody(boxResult.Value, plan.HostTranslation);
        if (!TryBuildPlacementCylinderTool(plan, seg, "blind-cylinder", diagnostics, out var toolBody) || toolBody is null)
        {
            diagnostics.Add("Blind plan rejected before Boolean: placement-driven construction failed.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

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
        => ExecuteCountersinkLike(plan, diagnostics, "cone");

    private static HoleRecoveryExecutionResult ExecuteCountersinkLike(HoleRecoveryPlan plan, List<string> diagnostics, string coneLabel)
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
        if (!boxResult.IsSuccess)
        {
            diagnostics.Add("Countersink primitive construction failed for host.");
            return new(HoleRecoveryExecutionStatus.PrimitiveConstructionFailed, null, diagnostics);
        }

        var boxBody = TranslateBody(boxResult.Value, plan.HostTranslation);
        if (!TryBuildPlacementCylinderTool(plan, cylSeg, "entry-cylinder", diagnostics, out var cylBody) || cylBody is null)
        {
            diagnostics.Add("Countersink plan rejected before Boolean: placement-driven cylinder segment failed.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }
        diagnostics.Add("cylinder subtract invoked.");
        var firstSub = BrepBoolean.Subtract(boxBody, cylBody);
        if (!firstSub.IsSuccess || firstSub.Value is null)
        {
            diagnostics.Add("cylinder subtract failed.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }
        diagnostics.Add("cylinder subtract succeeded.");

        if (!TryBuildPlacementConeTool(plan, coneSeg, coneLabel, coneLabel, diagnostics, out var coneBody) || coneBody is null)
        {
            diagnostics.Add($"{coneLabel} plan rejected before Boolean: placement-driven cone segment failed.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }
        diagnostics.Add($"{coneLabel} subtract invoked.");
        var secondSub = BrepBoolean.Subtract(firstSub.Value, coneBody);
        if (!secondSub.IsSuccess || secondSub.Value is null)
        {
            diagnostics.Add($"{coneLabel} subtract failed.");
            return new(HoleRecoveryExecutionStatus.UnsupportedPlan, null, diagnostics);
        }

        diagnostics.Add($"{coneLabel} subtract succeeded.");
        diagnostics.Add("Result BRep body produced.");
        return new(HoleRecoveryExecutionStatus.Succeeded, secondSub.Value, diagnostics);
    }


        private static BrepBody TranslateBody(BrepBody body, Vector3D translation)
        => translation == Vector3D.Zero ? body : FirmamentPrimitiveExecutionTranslation.TranslateBody(body, translation);
}
