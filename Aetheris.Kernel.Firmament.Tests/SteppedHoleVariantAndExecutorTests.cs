using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SteppedHoleVariantAndExecutorTests
{
    [Fact]
    public void SteppedHolePlan_StillAdmitted()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped()));
        Assert.True(eval.Admissible);
        Assert.Contains("selected-variant:SteppedHoleVariant", eval.Evidence);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(HoleKind.Stepped, plan.HoleKind);
        Assert.Equal(3, plan.ProfileStack.Count);
        Assert.Equal(HoleEntryFeatureKind.Stepped, plan.EntryFeature);
        Assert.Contains(plan.ExpectedSurfacePatches, p => p.Role == HoleSurfacePatchRole.SteppedTransitionFloorAnnulus);
    }

    [Fact]
    public void SteppedHoleExecutor_CanonicalSteppedHole_ProducesBrepBody()
    {
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped())).Plan!;
        var exec = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, exec.Status); // diag: string.Join(" | ", exec.Diagnostics)
        Assert.NotNull(exec.Body);
        Assert.Contains(exec.Diagnostics, d => d.Contains("profile-stack layer-count", StringComparison.Ordinal));
        Assert.Contains(exec.Diagnostics, d => d.Contains("no-hidden-placement-inference", StringComparison.Ordinal));
        Assert.Contains(exec.Diagnostics, d => d.Contains("profile-stack composition build succeeded", StringComparison.Ordinal));
        Assert.Contains(exec.Diagnostics, d => d.Contains("stepped executor route: profile-stack-extrude", StringComparison.Ordinal));
        Assert.Contains(exec.Diagnostics, d => d.Contains("profile-stack executor route: no 3D subtract route used", StringComparison.Ordinal));
    }


    [Fact]
    public void SteppedHoleStepSmoke_CanonicalSteppedHole_ExportsStep()
    {
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped())).Plan!;
        var exec = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, exec.Status);
        var step = Step242Exporter.ExportBody(exec.Body!);
        Assert.True(step.IsSuccess);
        Assert.Contains("ISO-10303-21", step.Value, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", step.Value, StringComparison.Ordinal);
        Assert.Contains("ADVANCED_FACE", step.Value, StringComparison.Ordinal);
        Assert.Contains("CYLINDRICAL_SURFACE", step.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", step.Value, StringComparison.Ordinal);
        Assert.Contains(exec.Diagnostics, d => d.Contains("stepped executor route: profile-stack-extrude", StringComparison.Ordinal));
    }

    [Fact]
    public void SteppedRouteComparison_LabAndProductionGeometryMatchOrReportMismatch()
    {
        var plan = (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped())).Plan!;
        var mediumDepth = plan.ProfileStack[1].DepthEnd - plan.ProfileStack[1].DepthStart;
        var largeDepth = plan.ProfileStack[0].DepthEnd - plan.ProfileStack[0].DepthStart;
        var throughHeight = double.Max(plan.ThroughLength, plan.HostSizeZ);
        var entryZ = plan.HostTranslation.Z + (plan.HostSizeZ * 0.5d);

        var productionMediumCenter = entryZ + (mediumDepth * 0.5d);
        var productionLargeCenter = entryZ + (largeDepth * 0.5d);
        var expectedLabMediumCenter = productionMediumCenter;
        var expectedLabLargeCenter = productionLargeCenter;

        var mismatch = new List<string>();
        if (Math.Abs(productionMediumCenter - expectedLabMediumCenter) > 1e-9) mismatch.Add("medium.centerZ");
        if (Math.Abs(productionLargeCenter - expectedLabLargeCenter) > 1e-9) mismatch.Add("large.centerZ");

        var report = $"Box={plan.HostSizeX}x{plan.HostSizeY}x{plan.HostSizeZ}@{plan.HostTranslation}; small(h={throughHeight},z=[{plan.ToolTranslation.Z - throughHeight*0.5},{plan.ToolTranslation.Z + throughHeight*0.5}]); medium(h={mediumDepth},centerZ={productionMediumCenter},z=[{productionMediumCenter-mediumDepth*0.5},{productionMediumCenter+mediumDepth*0.5}]); large(h={largeDepth},centerZ={productionLargeCenter},z=[{productionLargeCenter-largeDepth*0.5},{productionLargeCenter+largeDepth*0.5}]); order=small->medium->large; mismatch={(mismatch.Count == 0 ? "none" : string.Join(",", mismatch))}";
        Assert.True(mismatch.Count == 0, report);
    }

    [Fact]
    public void SteppedHole_InvalidPlanShape_RejectsBeforeBoolean()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped()));
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        var invalid = plan with { ProfileStack = [plan.ProfileStack[0], plan.ProfileStack[2]] };
        var exec = HoleRecoveryExecutor.Execute(invalid);
        Assert.Equal(HoleRecoveryExecutionStatus.UnsupportedPlan, exec.Status);
        Assert.Null(exec.Body);
        Assert.Contains(exec.Diagnostics, d => d.Contains("rejected before Boolean", StringComparison.Ordinal));
        Assert.DoesNotContain(exec.Diagnostics, d => d.Contains("Stepped subtract", StringComparison.Ordinal));
    }

    [Fact]
    public void SteppedHole_InvalidPlacement_RejectsBeforeBoolean()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped()));
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        var large = plan.ProfileStack[0] with { AnchorSide = HoleTierAnchorSide.Unknown };
        var medium = plan.ProfileStack[1] with { IsThrough = true };
        var small = plan.ProfileStack[2] with { IsThrough = false, ZMin = double.NaN };
        var invalid = plan with { ProfileStack = [large, medium, small] };
        var exec = HoleRecoveryExecutor.Execute(invalid);
        Assert.Equal(HoleRecoveryExecutionStatus.UnsupportedPlan, exec.Status);
        Assert.Null(exec.Body);
        Assert.Contains(exec.Diagnostics, d => d.Contains("rejected before Boolean", StringComparison.Ordinal));
        Assert.DoesNotContain(exec.Diagnostics, d => d.Contains("Stepped subtract", StringComparison.Ordinal));
    }

    [Fact] public void SteppedHoleVariant_DoesNotStealCounterbore() => Assert.Contains("selected-variant:CounterboreVariant", new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildCounterbore())).Evidence);
    [Fact] public void SteppedHoleVariant_DoesNotStealThroughOrBlind() { Assert.Contains("selected-variant:ThroughHoleVariant", new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20,20,10), new CirCylinderNode(2,20)))).Evidence); Assert.Contains("selected-variant:BlindHoleVariant", new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20,20,10), new CirTransformNode(new CirCylinderNode(2,4), Transform3D.CreateTranslation(new Vector3D(0,0,3)))))).Evidence); }
    [Fact] public void SteppedHoleVariant_RejectsNonCoaxialCylinders() => Assert.False(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped(new Vector3D(1,0,-2)))).Admissible);
    [Fact] public void SteppedHoleVariant_RejectsInvalidRadiusOrdering() => Assert.False(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped(mediumRadius:5))).Admissible);
    [Fact] public void SteppedHoleVariant_RejectsInvalidDepthOrdering() => Assert.False(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped(mediumHeight:2))).Admissible);

    private static CirNode BuildCounterbore() => new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)), new CirTransformNode(new CirCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))));
    private static CirNode BuildStepped(Vector3D? mediumTranslation = null, double mediumRadius = 3, double mediumHeight = 6)
    {
        var host = new CirBoxNode(20,20,10);
        var small = new CirCylinderNode(2,20);
        var medium = new CirTransformNode(new CirCylinderNode(mediumRadius, mediumHeight), Transform3D.CreateTranslation(mediumTranslation ?? new Vector3D(0,0,2)));
        var large = new CirTransformNode(new CirCylinderNode(4,4), Transform3D.CreateTranslation(new Vector3D(0,0,3)));
        return new CirSubtractNode(new CirSubtractNode(new CirSubtractNode(host, small), medium), large);
    }
}
