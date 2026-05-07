using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ShellStitchingDryRunTests
{
    [Fact]
    public void ShellStitching_BoxMinusCylinder_ConsumesPlanarAndCylindricalPatches()
    {
        var result = ShellStitchingDryRunPlanner.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));

        Assert.Contains(result.PlannedPatches, p => p.SurfaceFamily == SurfacePatchFamily.Planar);
        Assert.Contains(result.PlannedPatches, p => p.SurfaceFamily == SurfacePatchFamily.Cylindrical);
        Assert.False(result.ShellAssemblyImplemented);
        Assert.Contains(result.Diagnostics, d => d.Contains("dry-run", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ShellStitching_BoxMinusCylinder_ReportsPairingEvidence()
    {
        var result = ShellStitchingDryRunPlanner.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));

        Assert.True(result.PlannedPairs.Count > 0
            || result.Diagnostics.Any(d => d.Contains("missing one-to-one identity", StringComparison.OrdinalIgnoreCase))
            || result.UnpairedBoundaries.Any(u => u.Reason.Contains("identity", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ShellStitching_BoxMinusCylinder_ReportsUnpairedOrDeferredBoundaries()
    {
        var result = ShellStitchingDryRunPlanner.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));

        Assert.True(result.UnpairedBoundaries.Count > 0 || result.Readiness == ShellClosureReadiness.ReadyForAssemblyEvidence);
    }

    [Fact]
    public void ShellStitching_BoxMinusCylinder_DoesNotClaimFullShell()
    {
        var result = ShellStitchingDryRunPlanner.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));

        Assert.False(result.ShellAssemblyImplemented);
        Assert.NotEqual(ShellClosureReadiness.ReadyForAssemblyEvidence, result.Readiness);
    }

    [Fact]
    public void ShellStitching_DeterministicOrdering()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var first = ShellStitchingDryRunPlanner.Generate(root);
        var second = ShellStitchingDryRunPlanner.Generate(root);

        Assert.Equal(first.PlannedPatches.Select(p => p.SourceCandidateKey), second.PlannedPatches.Select(p => p.SourceCandidateKey));
        Assert.Equal(first.PlannedPairs.Select(p => p.EdgeToken), second.PlannedPairs.Select(p => p.EdgeToken));
        Assert.Equal(first.UnpairedBoundaries.Select(u => $"{u.Patch}|{u.Loop}|{u.Reason}"), second.UnpairedBoundaries.Select(u => $"{u.Patch}|{u.Loop}|{u.Reason}"));
    }
}
