using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfaceFamilyShellAssemblerTests
{
    [Fact]
    public void SurfaceFamilyShellAssembler_BoxMinusCylinder_RejectsWhenReadinessDeferred()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(2));

        var result = SurfaceFamilyShellAssembler.TryAssembleBoxMinusCylinder(root);

        Assert.False(result.Success);
        Assert.Null(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("readiness-gate-rejected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SurfaceFamilyShellAssembler_BoxMinusCylinder_ReportsReadinessBlockerForCanonicalCase()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 12));

        var result = SurfaceFamilyShellAssembler.TryAssembleBoxMinusCylinder(root);

        Assert.False(result.Success);
        Assert.Null(result.Body);
        Assert.True(result.PlanarPatchCount > 0);
        Assert.True(result.CylindricalPatchConsumed);
        Assert.Equal(ShellClosureReadiness.Deferred, result.Readiness);
        Assert.Contains(result.Diagnostics, d => d.Contains("readiness-gate-rejected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SurfaceFamilyShellAssembler_BoxMinusCylinder_SummaryIncludesPlanarAndCylindricalFamilies()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 12));

        var result = SurfaceFamilyShellAssembler.TryAssembleBoxMinusCylinder(root);

        Assert.True(result.PlanarPatchCount > 0);
        Assert.True(result.CylindricalPatchConsumed);
    }

    [Fact]
    public void ShellAssembler_SeesMatchingPlanarAndCylindricalEmittedTokens()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 12));
        var result = SurfaceFamilyShellAssembler.TryAssembleBoxMinusCylinder(root);
        Assert.Contains(result.Diagnostics, d => d.Contains("planar-inner-circle-token", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("cylindrical-seam-role-tagged", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, d => d.Contains("token-match-candidates", StringComparison.OrdinalIgnoreCase));
        Assert.False(result.FullShellAssembled);
    }

    [Fact]
    public void EmittedIdentity_DoesNotExposeUserFacingTopologyNames()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 12));
        var result = SurfaceFamilyShellAssembler.TryAssembleBoxMinusCylinder(root);
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("firmament selector", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("user-facing topology names", StringComparison.OrdinalIgnoreCase));
    }
}
