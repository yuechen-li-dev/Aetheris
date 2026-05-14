using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class MaterializationReadinessAnalyzerTests
{
    [Fact]
    public void Readiness_BoxMinusCylinder_IsEvidenceReadyOrSpecialReady()
    {
        var report = MaterializationReadinessAnalyzer.Analyze(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));

        Assert.True(report.SourceSurfaceCount > 0);
        Assert.True(report.CandidateCount > 0);
        Assert.True(report.PlannedFaceCount > 0);
        Assert.True(report.PlannedEdgeUseCount > 0);
        Assert.Contains(report.OverallReadiness, new[] { EmissionReadiness.EvidenceReadyForEmission, EmissionReadiness.SpecialCaseReady, EmissionReadiness.Deferred });
        Assert.False(report.TopologyEmissionImplemented);
        Assert.Contains(report.Diagnostics, d => d.Contains("topology-emission: not implemented", StringComparison.Ordinal));
    }

    [Fact]
    public void Readiness_BoxMinusSphere_NotGenericUnsupported()
    {
        var report = MaterializationReadinessAnalyzer.Analyze(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(3)));

        Assert.Contains(report.Diagnostics, d => d.Contains("planar/spherical => circle exact", StringComparison.Ordinal));
        Assert.NotEqual(EmissionReadiness.Unsupported, report.OverallReadiness);
        Assert.DoesNotContain(EmissionBlockingReason.UnsupportedSurfaceFamily, report.BlockingReasons);
    }

    [Fact]
    public void Readiness_BoxMinusTorus_BlockedByTrimCapability()
    {
        var report = MaterializationReadinessAnalyzer.Analyze(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirTorusNode(4, 1)));

        Assert.Contains(EmissionBlockingReason.TrimCapability, report.BlockingReasons);
        Assert.Contains(report.Diagnostics, d => d.Contains("planar/toroidal deferred", StringComparison.Ordinal));
        Assert.Contains(report.OverallReadiness, new[] { EmissionReadiness.Deferred, EmissionReadiness.Unsupported });
    }

    [Fact]
    public void Readiness_NonSubtract_NotApplicable()
    {
        var report = MaterializationReadinessAnalyzer.Analyze(new CirUnionNode(new CirBoxNode(10, 10, 10), new CirSphereNode(2)));

        Assert.Equal(EmissionReadiness.NotApplicable, report.OverallReadiness);
        Assert.Contains(EmissionBlockingReason.NonSubtractNotApplicable, report.BlockingReasons);
    }

    [Fact]
    public void Readiness_CountsAreDeterministic()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(3));
        var first = MaterializationReadinessAnalyzer.Analyze(root);
        var second = MaterializationReadinessAnalyzer.Analyze(root);

        Assert.Equal(first.SourceSurfaceCount, second.SourceSurfaceCount);
        Assert.Equal(first.CandidateCount, second.CandidateCount);
        Assert.Equal(first.PlannedFaceCount, second.PlannedFaceCount);
        Assert.Equal(first.PlannedLoopCount, second.PlannedLoopCount);
        Assert.Equal(first.PlannedEdgeUseCount, second.PlannedEdgeUseCount);
        Assert.Equal(first.CoedgePairingCount, second.CoedgePairingCount);
        Assert.Equal(first.ClosureEvidenceCount, second.ClosureEvidenceCount);
        Assert.Equal(first.OverallReadiness, second.OverallReadiness);
        Assert.Equal(first.BlockingReasons, second.BlockingReasons);
    }

    [Fact]
    public void Readiness_DoesNotEmitBRepTopology()
    {
        var report = MaterializationReadinessAnalyzer.Analyze(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));

        Assert.False(report.TopologyEmissionImplemented);
        Assert.Contains(report.LayerSummaries, l => l.LayerName == "pairing-evidence");
    }
}
