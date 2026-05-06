using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class TopologyPairingEvidenceTests
{
    [Fact]
    public void PairingEvidence_BoxMinusCylinder_CreatesEdgeUses()
    {
        var result = TopologyPairingEvidenceGenerator.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.PlannedEdgeUses);
        Assert.Contains(result.PlannedEdgeUses, e => e.SourceSurfaceFamily == SurfacePatchFamily.Planar && e.OppositeSurfaceFamily == SurfacePatchFamily.Cylindrical);
        Assert.Contains(result.PlannedEdgeUses, e => e.IdentityToken is not null);
        Assert.Contains(result.PlannedCoedgePairings, p => p.PairingKind == PlannedCoedgePairingKind.SharedTrimIdentity);
        Assert.False(result.TopologyEmissionImplemented);
    }

    [Fact]
    public void PairingEvidence_BoxMinusSphere_CircularLoopsHaveClosureEvidence()
    {
        var result = TopologyPairingEvidenceGenerator.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(3)));

        Assert.Contains(result.PlannedEdgeUses, e => e.TrimCurveFamily == TrimCurveFamily.Circle);
        Assert.Contains(result.PlannedEdgeUses, e => e.TrimCurveFamily == TrimCurveFamily.Circle && e.IdentityToken is not null);
        Assert.Contains(result.LoopClosureEvidence, c => c.ClosureStatus is LoopClosureStatus.ClosedByDescriptor or LoopClosureStatus.ClosureDeferred);
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("generic unsupported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PairingEvidence_BoxMinusTorus_DefersClosureAndPairing()
    {
        var result = TopologyPairingEvidenceGenerator.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirTorusNode(4, 1)));

        Assert.Equal(TopologyPairingReadiness.Deferred, result.Readiness);
        Assert.DoesNotContain(result.PlannedCoedgePairings, p => p.PairingKind == PlannedCoedgePairingKind.SharedTrimIdentity);
        Assert.Contains(result.PlannedCoedgePairings, p => p.Readiness == TopologyPairingReadiness.Deferred);
        Assert.Contains(result.Diagnostics, d => d.Contains("quartic/algebraic", StringComparison.OrdinalIgnoreCase) || d.Contains("deferred", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PairingEvidence_CoedgePairingDeferredWhenIdentityMissing()
    {
        var result = TopologyPairingEvidenceGenerator.Generate(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(3)));

        Assert.Contains(result.PlannedCoedgePairings, p => p.PairingKind == PlannedCoedgePairingKind.Deferred);
        Assert.Contains(result.Diagnostics, d => d.Contains("missing-identity", StringComparison.OrdinalIgnoreCase) || d.Contains("token-mismatch", StringComparison.OrdinalIgnoreCase) || d.Contains("ambiguous-token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PairingIdentity_TokensAreDeterministic()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(3));
        var first = TopologyPairingEvidenceGenerator.Generate(root);
        var second = TopologyPairingEvidenceGenerator.Generate(root);
        Assert.Equal(first.PlannedEdgeUses.Select(e => e.IdentityToken?.OrderingKey), second.PlannedEdgeUses.Select(e => e.IdentityToken?.OrderingKey));
    }

    [Fact]
    public void PairingEvidence_NonSubtract_NotApplicable()
    {
        var result = TopologyPairingEvidenceGenerator.Generate(new CirUnionNode(new CirBoxNode(10, 10, 10), new CirSphereNode(2)));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopologyPairingReadiness.NotApplicable, result.Readiness);
        Assert.Empty(result.PlannedEdgeUses);
    }

    [Fact]
    public void PairingEvidence_DeterministicOrdering()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var first = TopologyPairingEvidenceGenerator.Generate(root);
        var second = TopologyPairingEvidenceGenerator.Generate(root);

        Assert.Equal(first.PlannedEdgeUses.Select(x => x.OrderingKey), second.PlannedEdgeUses.Select(x => x.OrderingKey));
        Assert.Equal(first.PlannedCoedgePairings.Select(x => x.OrderingKey), second.PlannedCoedgePairings.Select(x => x.OrderingKey));
        Assert.Equal(first.LoopClosureEvidence.Select(x => x.OrderingKey), second.LoopClosureEvidence.Select(x => x.OrderingKey));
    }
}
