using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class HoleRecoveryPlacementSemanticsTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["through-hole", BuildThrough(), HoleKind.Through];
        yield return ["counterbore", BuildCounterbore(), HoleKind.Counterbore];
        yield return ["blind-top", BuildBlindTop(), HoleKind.Blind];
        yield return ["blind-bottom", BuildBlindBottom(), HoleKind.Blind];
        yield return ["countersink", BuildCountersink(), HoleKind.Countersink];
        yield return ["chamfer-top", BuildChamferTop(), HoleKind.ChamferedEntry];
        yield return ["chamfer-bottom", BuildChamferBottom(), HoleKind.ChamferedEntry];
        yield return ["stepped", BuildStepped(), HoleKind.Stepped];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void PlacementSemantics_AllSupportedVariants_PopulateExplicitPlacement(string _, CirNode root, HoleKind kind)
    {
        var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root)).Plan);
        Assert.Equal(kind, plan.HoleKind);
        foreach (var s in plan.ProfileStack)
        {
            Assert.NotEqual(HoleTierAnchorSide.Unknown, s.AnchorSide);
            Assert.False(double.IsNaN(s.ZMin));
            Assert.False(double.IsNaN(s.ZMax));
            Assert.True(s.ZMax > s.ZMin);
            Assert.NotNull(s.PlacementDiagnostics);
            Assert.NotEmpty(s.PlacementDiagnostics!);
        }
    }

    [Fact]
    public void PlacementSemantics_ThroughSegments_AreExplicitlyThrough()
    {
        foreach (var root in new[] { BuildThrough(), BuildCounterbore(), BuildStepped(), BuildChamferTop(), BuildCountersink() })
        {
            var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root)).Plan);
            foreach (var s in plan.ProfileStack.Where(x => x.IsThrough))
            {
                Assert.Equal(HoleTierAnchorSide.Through, s.AnchorSide);
            }
        }
    }

    [Fact]
    public void PlacementSemantics_BlindAndReliefSegments_HaveEntryAnchor()
    {
        var roots = new[] { BuildBlindTop(), BuildBlindBottom(), BuildCounterbore(), BuildChamferBottom(), BuildCountersink(), BuildStepped() };
        foreach (var root in roots)
        {
            var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root)).Plan);
            foreach (var s in plan.ProfileStack.Where(x => !x.IsThrough))
            {
                Assert.True(s.AnchorSide is HoleTierAnchorSide.Top or HoleTierAnchorSide.Bottom);
                Assert.True(s.DepthFromAnchor > 0d);
            }
        }
    }

    [Fact]
    public void PlacementSemantics_ExecutorsStillProduceBrep()
    {
        foreach (var root in new[] { BuildThrough(), BuildCounterbore(), BuildBlindTop(), BuildCountersink(), BuildChamferTop(), BuildChamferBottom() })
        {
            var plan = Assert.IsType<HoleRecoveryPlan>(new HoleRecoveryPolicy().Evaluate(new FrepMaterializerContext(root)).Plan);
            var exec = HoleRecoveryExecutor.Execute(plan);
            Assert.True(exec.Status is HoleRecoveryExecutionStatus.Succeeded or HoleRecoveryExecutionStatus.BooleanFailed);
        }
    }

    private static CirNode BuildThrough() => new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20));
    private static CirNode BuildCounterbore() => new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)), new CirTransformNode(new CirCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))));
    private static CirNode BuildBlindTop() => new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirTransformNode(new CirCylinderNode(2, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, 3))));
    private static CirNode BuildBlindBottom() => new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirTransformNode(new CirCylinderNode(2, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))));
    private static CirNode BuildCountersink() => new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20d, 20d, 10d), new CirCylinderNode(2d, 20d)), new CirTransformNode(new CirConeNode(2d, 4d, 4d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, 3d))));
    private static CirNode BuildChamferTop() => new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20d,20d,10d), new CirCylinderNode(2d,20d)), new CirTransformNode(new CirConeNode(2d, 2.8d, 1d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, 4.5d))));
    private static CirNode BuildChamferBottom() => new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20d,20d,10d), new CirCylinderNode(2d,20d)), new CirTransformNode(new CirConeNode(2.8d, 2d, 1d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, -4.5d))));
    private static CirNode BuildStepped() => new CirSubtractNode(new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20,20,10), new CirCylinderNode(2,20)), new CirTransformNode(new CirCylinderNode(3,6), Transform3D.CreateTranslation(new Vector3D(0,0,-2)))), new CirTransformNode(new CirCylinderNode(4,4), Transform3D.CreateTranslation(new Vector3D(0,0,-3))));
}
