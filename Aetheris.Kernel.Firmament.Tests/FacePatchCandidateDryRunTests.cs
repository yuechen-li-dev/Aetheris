using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FacePatchCandidateDryRunTests
{
    [Fact]
    public void FacePatchDryRun_BoxMinusCylinder_ReportsExactTrimReady()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));

        var result = FacePatchCandidateGenerator.Generate(root);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.SourceSurfaces, d => d.Family == SurfacePatchFamily.Planar);
        Assert.Contains(result.SourceSurfaces, d => d.Family == SurfacePatchFamily.Cylindrical);
        Assert.Contains(result.TrimCapabilitySummaries, t =>
            (t.FamilyA == SurfacePatchFamily.Planar && t.FamilyB == SurfacePatchFamily.Cylindrical)
            || (t.FamilyA == SurfacePatchFamily.Cylindrical && t.FamilyB == SurfacePatchFamily.Planar));
        Assert.Contains(result.Candidates, c => c.Readiness == FacePatchCandidateReadiness.ExactReady);
        Assert.False(result.TopologyAssemblyImplemented);
    }

    [Fact]
    public void FacePatchDryRun_BoxMinusSphere_ReportsPlanarSphericalCircleExact()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(3));

        var result = FacePatchCandidateGenerator.Generate(root);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.SourceSurfaces, d => d.Family == SurfacePatchFamily.Spherical);
        Assert.Contains(result.TrimCapabilitySummaries, t =>
            ((t.FamilyA == SurfacePatchFamily.Planar && t.FamilyB == SurfacePatchFamily.Spherical)
             || (t.FamilyA == SurfacePatchFamily.Spherical && t.FamilyB == SurfacePatchFamily.Planar))
            && t.Classification == TrimCapabilityClassification.ExactSupported
            && t.CurveFamilies.Contains(TrimCurveFamily.Circle));
        Assert.DoesNotContain(result.Candidates, c => c.Readiness == FacePatchCandidateReadiness.Unsupported);
    }

    [Fact]
    public void FacePatchDryRun_BoxMinusTorus_ReportsDeferredTrim()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirTorusNode(4, 1));

        var result = FacePatchCandidateGenerator.Generate(root);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.SourceSurfaces, d => d.Family == SurfacePatchFamily.Toroidal);
        Assert.Contains(result.TrimCapabilitySummaries, t =>
            ((t.FamilyA == SurfacePatchFamily.Planar && t.FamilyB == SurfacePatchFamily.Toroidal)
             || (t.FamilyA == SurfacePatchFamily.Toroidal && t.FamilyB == SurfacePatchFamily.Planar))
            && t.Classification == TrimCapabilityClassification.Deferred);
        Assert.Contains(result.Candidates, c => c.Readiness == FacePatchCandidateReadiness.TrimDeferred);
        Assert.Contains(result.Candidates.SelectMany(c => c.Diagnostics), d => d.Contains("quartic/algebraic", StringComparison.OrdinalIgnoreCase));
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
