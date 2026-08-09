using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SteppedHoleCorpusHardeningTests
{
    [Fact]
    public void SteppedHole_TopEntry_ExecutesAndExports()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped(topEntry: true)));
        Assert.True(eval.Admissible);
        Assert.Contains("selected-variant:SteppedHoleVariant", eval.Evidence);
        Assert.Contains(eval.Diagnostics, d => d.Contains("entry side detected: top(+Z)", StringComparison.Ordinal));

        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        var exec = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, exec.Status);
        var step = Step242Exporter.ExportBody(Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(exec.Body));
        Assert.True(step.IsSuccess);
        Assert.Contains("MANIFOLD_SOLID_BREP", step.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", step.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void SteppedHole_BottomEntry_ExecutesAndExportsOrRejectsExplicitly()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped(topEntry: false)));
        if (eval.Admissible)
        {
            Assert.Contains("selected-variant:SteppedHoleVariant", eval.Evidence);
            Assert.Contains(eval.Diagnostics, d => d.Contains("entry side detected: bottom(-Z)", StringComparison.Ordinal));
            var exec = HoleRecoveryExecutor.Execute((HoleRecoveryPlan)eval.Plan!);
            if (exec.Status == HoleRecoveryExecutionStatus.Succeeded)
            {
                var step = Step242Exporter.ExportBody(Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(exec.Body));
                Assert.True(step.IsSuccess);
                Assert.Contains("MANIFOLD_SOLID_BREP", step.Value, StringComparison.Ordinal);
                Assert.DoesNotContain("BREP_WITH_VOIDS", step.Value, StringComparison.Ordinal);
            }
            else
            {
                Assert.True(exec.Status is HoleRecoveryExecutionStatus.BooleanFailed or HoleRecoveryExecutionStatus.UnsupportedPlan);
                Assert.Contains(exec.Diagnostics, d => d.Contains("profile-stack", StringComparison.OrdinalIgnoreCase) || d.Contains("rejected before", StringComparison.OrdinalIgnoreCase));
            }
            return;
        }

        Assert.Contains(eval.RejectionReasons, r => r.Contains("Unsupported", StringComparison.Ordinal));
        Assert.Contains(eval.Diagnostics, d => d.Contains("entry side", StringComparison.OrdinalIgnoreCase) || d.Contains("anchor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SteppedHole_TranslatedGeometry_ExecutesAndExports()
    {
        var eval = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped(topEntry: true, hostTranslation: new Vector3D(40, -30, 15), toolTranslation: new Vector3D(40, -30, 15))));
        Assert.True(eval.Admissible);
        var plan = (HoleRecoveryPlan)eval.Plan!;
        var large = plan.ProfileStack[0];
        var medium = plan.ProfileStack[1];
        Assert.True(large.ZMin > 0);
        Assert.True(medium.ZMin > 0);

        var exec = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, exec.Status);
        var step = Step242Exporter.ExportBody(Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(exec.Body));
        Assert.True(step.IsSuccess);
    }

    [Fact]
    public void SteppedHole_InvalidUnknownAnchor_RejectsBeforeBoolean()
    {
        var invalid = MutatePlan(s => s with { AnchorSide = HoleTierAnchorSide.Unknown }, idx: 0);
        AssertRejectsBeforeBoolean(invalid, "anchor side cannot be Unknown");
    }

    [Fact]
    public void SteppedHole_InvalidThroughFlag_RejectsBeforeBoolean()
    {
        var invalid = MutatePlan(s => s with { IsThrough = true }, idx: 1);
        AssertRejectsBeforeBoolean(invalid, "medium/large tiers must be blind tiers");
    }

    [Fact]
    public void SteppedHole_InvalidZSpan_RejectsBeforeBoolean()
    {
        var invalid = MutatePlan(s => s with { ZMin = s.ZMax }, idx: 0);
        AssertRejectsBeforeBoolean(invalid, "valid explicit z-span");
    }

    [Fact]
    public void SteppedHole_AnchorMismatch_RejectsBeforeBoolean()
    {
        var basePlan = GetPlan();
        var mutated = basePlan with
        {
            ProfileStack =
            [
                basePlan.ProfileStack[0] with { AnchorSide = HoleTierAnchorSide.Top },
                basePlan.ProfileStack[1] with { AnchorSide = HoleTierAnchorSide.Bottom },
                basePlan.ProfileStack[2]
            ]
        };
        AssertRejectsBeforeBoolean(mutated, "must share a concrete entry anchor side");
    }

    [Fact]
    public void SteppedHole_RejectsEqualRadiusOrDepthOrdering()
    {
        Assert.False(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped(true, mediumRadius: 2))).Admissible);
        Assert.False(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped(true, mediumHeight: 4))).Admissible);
    }

    [Fact]
    public void SteppedHole_RejectsTangentOrOversizedLargestRadius()
    {
        var tangent = new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped(true, largeRadius: 10)));
        Assert.False(tangent.Admissible);
        Assert.Contains(tangent.RejectionReasons, r => r.Contains("UnsupportedLargestRadiusClearance", StringComparison.Ordinal));
    }

    [Fact]
    public void SteppedHole_DoesNotStealCounterboreBlindThroughCountersink()
    {
        var policy = new HoleRecoveryPolicy();
        Assert.Contains("selected-variant:ThroughHoleVariant", policy.Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)))).Evidence);
        Assert.Contains("selected-variant:BlindHoleVariant", policy.Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirTransformNode(new CirCylinderNode(2, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, 3)))))).Evidence);
        Assert.Contains("selected-variant:CounterboreVariant", policy.Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)), new CirTransformNode(new CirCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3)))))).Evidence);
        Assert.Contains("selected-variant:CountersinkVariant", policy.Evaluate(new FrepMaterializerContext(new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)), new CirTransformNode(new CirConeNode(2, 4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, 3)))))).Evidence);
    }

    private static HoleRecoveryPlan GetPlan() => (HoleRecoveryPlan)new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(BuildStepped(true))).Plan!;

    private static HoleRecoveryPlan MutatePlan(Func<HoleProfileSegment, HoleProfileSegment> mutate, int idx)
    {
        var p = GetPlan();
        var stack = p.ProfileStack.ToArray();
        stack[idx] = mutate(stack[idx]);
        return p with { ProfileStack = stack };
    }

    private static void AssertRejectsBeforeBoolean(HoleRecoveryPlan plan, string contains)
    {
        var exec = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.UnsupportedPlan, exec.Status);
        Assert.Contains(exec.Diagnostics, d => d.Contains("rejected before Boolean", StringComparison.Ordinal));
        Assert.Contains(exec.Diagnostics, d => d.Contains(contains, StringComparison.Ordinal) || d.Contains("placement-invalid", StringComparison.Ordinal));
        Assert.DoesNotContain(exec.Diagnostics, d => d.Contains("Stepped subtract", StringComparison.Ordinal));
    }

    private static CirNode BuildStepped(bool topEntry, Vector3D? hostTranslation = null, Vector3D? toolTranslation = null, double mediumRadius = 3, double mediumHeight = 6, double largeRadius = 4, double largeHeight = 4)
    {
        var ht = hostTranslation ?? Vector3D.Zero;
        var tt = toolTranslation ?? ht;
        var host = new CirTransformNode(new CirBoxNode(20, 20, 10), Transform3D.CreateTranslation(ht));
        var small = new CirTransformNode(new CirCylinderNode(2, 20), Transform3D.CreateTranslation(tt));

        var sign = topEntry ? 1d : -1d;
        var mediumZ = tt.Z + sign * (10d - mediumHeight) * 0.5d;
        var largeZ = tt.Z + sign * (10d - largeHeight) * 0.5d;
        var medium = new CirTransformNode(new CirCylinderNode(mediumRadius, mediumHeight), Transform3D.CreateTranslation(new Vector3D(tt.X, tt.Y, mediumZ)));
        var large = new CirTransformNode(new CirCylinderNode(largeRadius, largeHeight), Transform3D.CreateTranslation(new Vector3D(tt.X, tt.Y, largeZ)));
        return new CirSubtractNode(new CirSubtractNode(new CirSubtractNode(host, small), medium), large);
    }
}
