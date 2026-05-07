using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ShellStitchingDryRunTests
{
    [Fact]
    public void ShellStitching_BoxMinusCylinder_AccountsForCylindricalSeam()
    {
        var result = ShellStitchingDryRunPlanner.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));

        Assert.Contains(result.SeamClosureEvidence, e => e.SeamKind == SeamKind.CylindricalSelfSeam || e.SeamKind == SeamKind.SeamDeferred);
        Assert.True(result.SeamClosureEvidence.Any(e => e.SeamKind == SeamKind.CylindricalSelfSeam)
            || result.SeamClosureEvidence.Any(e => e.Diagnostics.Any(d => d.Contains("missing-metadata", StringComparison.OrdinalIgnoreCase))));
    }

    [Fact]
    public void ShellStitching_BoxMinusCylinder_ChecksOrientationCompatibility()
    {
        var result = ShellStitchingDryRunPlanner.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));

        Assert.NotEmpty(result.OrientationEvidence);
        Assert.All(result.OrientationEvidence, e => Assert.True(
            e.OrientationStatus is OrientationCompatibilityStatus.Compatible or OrientationCompatibilityStatus.Deferred));
    }

    [Fact]
    public void ShellStitching_BoxMinusCylinder_ReclassifiesExpectedSeamBoundaries()
    {
        var result = ShellStitchingDryRunPlanner.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));

        if (result.SeamClosureEvidence.Any(e => e.SeamKind == SeamKind.CylindricalSelfSeam))
        {
            Assert.DoesNotContain(result.UnpairedBoundaries, u => u.Patch.Contains("Cylindrical", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void ShellStitching_BoxMinusCylinder_ReadinessPromotionIsConservative()
    {
        var result = ShellStitchingDryRunPlanner.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));

        if (result.UnpairedBoundaries.Count == 0
            && result.SeamClosureEvidence.All(e => e.Readiness != ShellClosureReadiness.Deferred)
            && result.OrientationEvidence.All(e => e.OrientationStatus == OrientationCompatibilityStatus.Compatible))
        {
            Assert.Equal(ShellClosureReadiness.ReadyForAssemblyEvidence, result.Readiness);
        }
        else
        {
            Assert.Equal(ShellClosureReadiness.Deferred, result.Readiness);
        }

        Assert.False(result.ShellAssemblyImplemented);
    }

    [Fact]
    public void ShellStitching_DeterministicEvidenceOrdering()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var first = ShellStitchingDryRunPlanner.Generate(root);
        var second = ShellStitchingDryRunPlanner.Generate(root);

        Assert.Equal(first.SeamClosureEvidence.Select(e => e.PatchKey), second.SeamClosureEvidence.Select(e => e.PatchKey));
        Assert.Equal(first.OrientationEvidence.Select(e => e.PairKey), second.OrientationEvidence.Select(e => e.PairKey));
        Assert.Equal(first.UnpairedBoundaries.Select(u => $"{u.Patch}|{u.Loop}|{u.Reason}"), second.UnpairedBoundaries.Select(u => $"{u.Patch}|{u.Loop}|{u.Reason}"));
    }
}
