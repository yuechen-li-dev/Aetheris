using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FacePatchCandidateDryRunTests
{
    [Fact]
    public void LoopScaffold_BoxMinusCylinder_ProducesTrimLoopDescriptors()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));

        var result = FacePatchCandidateGenerator.Generate(root);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Candidates, c => c.CandidateRole == "base-surface-candidate" && c.RetentionRole == FacePatchRetentionRole.BaseBoundaryRetainedOutsideTool);
        Assert.Contains(result.Candidates, c => c.SourceSurface.Family == SurfacePatchFamily.Cylindrical && c.RetentionRole == FacePatchRetentionRole.ToolBoundaryRetainedInsideBase);
        Assert.Contains(result.Candidates, c => c.RetentionStatus == FacePatchRetentionStatus.KnownTrimmedSurface);
        Assert.Contains(result.Candidates, c => c.CandidateRole == "base-surface-candidate" && c.RetainedRegionLoops.Any(l => l.SourceSurfaceFamily == SurfacePatchFamily.Planar));
        Assert.Contains(result.Candidates, c => c.SourceSurface.Family == SurfacePatchFamily.Cylindrical && c.RetainedRegionLoops.Any(l => l.OppositeSurfaceFamily == SurfacePatchFamily.Planar));
        Assert.Contains(result.Candidates.SelectMany(c => c.RetainedRegionLoops), l => l.TrimCurveFamily is TrimCurveFamily.Line or TrimCurveFamily.Circle or TrimCurveFamily.Ellipse);
        Assert.Contains(result.Candidates.SelectMany(c => c.RetainedRegionLoops), l => l.Status is RetainedRegionLoopStatus.ExactReady or RetainedRegionLoopStatus.SpecialCaseReady);
        Assert.False(result.TopologyAssemblyImplemented);
    }

    [Fact]
    public void LoopScaffold_BoxMinusSphere_ProducesCircularTrimLoopDescriptors()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(3));

        var result = FacePatchCandidateGenerator.Generate(root);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Candidates, c =>
            c.SourceSurface.Family == SurfacePatchFamily.Spherical
            && c.RetentionRole == FacePatchRetentionRole.ToolBoundaryRetainedInsideBase
            && c.RetentionStatus == FacePatchRetentionStatus.KnownTrimmedSurface);
        Assert.Contains(result.TrimCapabilitySummaries, t =>
            ((t.FamilyA == SurfacePatchFamily.Planar && t.FamilyB == SurfacePatchFamily.Spherical)
             || (t.FamilyA == SurfacePatchFamily.Spherical && t.FamilyB == SurfacePatchFamily.Planar))
            && t.Classification == TrimCapabilityClassification.ExactSupported
            && t.CurveFamilies.Contains(TrimCurveFamily.Circle));
        Assert.Contains(result.Candidates, c => c.SourceSurface.Family == SurfacePatchFamily.Spherical && c.RetainedRegionLoops.Any(l => l.TrimCurveFamily == TrimCurveFamily.Circle && l.Status == RetainedRegionLoopStatus.ExactReady));
        Assert.DoesNotContain(result.Candidates, c => c.Diagnostics.Any(d => d.Contains("generic unsupported", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void LoopScaffold_BoxMinusTorus_ProducesDeferredLoopDiagnostics()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirTorusNode(4, 1));

        var result = FacePatchCandidateGenerator.Generate(root);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Candidates, c =>
            c.SourceSurface.Family == SurfacePatchFamily.Toroidal
            && c.RetentionRole == FacePatchRetentionRole.ToolBoundaryRetainedInsideBase
            && c.Readiness == FacePatchCandidateReadiness.TrimDeferred
            && c.RetentionStatus == FacePatchRetentionStatus.Deferred);
        Assert.Contains(result.Candidates.SelectMany(c => c.Diagnostics), d => d.Contains("quartic/algebraic", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Candidates, c =>
            c.SourceSurface.Family == SurfacePatchFamily.Toroidal
            && c.RetainedRegionLoops.Any(l => l.Status == RetainedRegionLoopStatus.Deferred));
    }

    [Fact]
    public void LoopScaffold_NonSubtract_DoesNotInventLoops()
    {
        var root = new CirUnionNode(new CirBoxNode(10, 10, 10), new CirSphereNode(2));

        var result = FacePatchCandidateGenerator.Generate(root);

        Assert.False(result.IsSuccess);
        Assert.All(result.Candidates, c => Assert.Equal(FacePatchRetentionRole.NotApplicable, c.RetentionRole));
        Assert.All(result.Candidates, c => Assert.Equal(FacePatchRetentionStatus.Deferred, c.RetentionStatus));
        Assert.All(result.Candidates, c => Assert.Empty(c.RetainedRegionLoops));
    }

    [Fact]
    public void FacePatchDryRun_UsesSourceSurfaceExtractor()
    {
        var root = new CirBoxNode(3, 4, 5);
        var extracted = SourceSurfaceExtractor.Extract(root);

        var result = FacePatchCandidateGenerator.Generate(new CirSubtractNode(root, new CirSphereNode(1)));

        Assert.Equal(extracted.Descriptors.Count, result.SourceSurfaces.Count(d => d.OwningCirNodeKind == nameof(CirBoxNode)));
    }

    [Fact]
    public void FacePatchDryRun_DoesNotClaimTopologyAssembly()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));

        var result = FacePatchCandidateGenerator.Generate(root);

        Assert.False(result.TopologyAssemblyImplemented);
        Assert.Contains(result.Diagnostics, d => d.Contains("topology-assembly-not-implemented", StringComparison.Ordinal));
    }
}
