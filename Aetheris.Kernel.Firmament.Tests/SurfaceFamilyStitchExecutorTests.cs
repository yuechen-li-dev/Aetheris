using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfaceFamilyStitchExecutorTests
{
    [Fact]
    public void StitchExecutor_BoxCylinder_ConsumesReadyCandidateOrReportsPreciseBlocker()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var planar = new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root);
        var cylindrical = EmitCylinder(root);
        var maps = planar.Entries.Where(e => e.Emitted).Select(e => e.IdentityMap ?? EmittedTopologyIdentityMap.Empty)
            .Concat(cylindrical?.IdentityMap is null ? [] : [cylindrical.IdentityMap]);
        var analysis = EmittedTokenPairingAnalyzer.Analyze(maps);
        var plan = SurfaceFamilyStitchCandidatePlanner.Plan(maps, analysis, ShellStitchingDryRunPlanner.Generate(root));

        var result = SurfaceFamilyStitchExecutor.TryExecute(plan, planar, cylindrical);

        Assert.True(result.AppliedCandidateCount > 0 || result.Status is SurfaceFamilyStitchExecutionStatus.Deferred or SurfaceFamilyStitchExecutionStatus.Unsupported);
        if (result.AppliedCandidateCount == 0)
        {
            Assert.True(
                result.Operations.SelectMany(o => o.Diagnostics).Any(d => d.Contains("candidate topology refs ready", StringComparison.Ordinal))
                || result.Diagnostics.Any(d => d.Contains("shared-edge-merge-unsupported", StringComparison.Ordinal))
                || result.Diagnostics.Any(d => d.Contains("no-stitch-candidate-no-mutation", StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void EmittedTopologyRefs_CylindricalSeam_RefsOrDiagnosticsPresent()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var cylindrical = EmitCylinder(root);
        var seam = cylindrical!.IdentityMap!.Entries.Single(e => e.Role == EmittedTopologyRole.CylindricalSeam);
        Assert.True(seam.TopologyReference is not null || seam.Diagnostics.Any(d => d.Contains("missing", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void StitchExecutor_NoCandidate_NoMutation()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var planar = new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root);
        var emptyPlan = new SurfaceFamilyStitchPlanResult(false, [], [], [], ["none"], false);

        var result = SurfaceFamilyStitchExecutor.TryExecute(emptyPlan, planar, null);

        Assert.Null(result.Body);
        Assert.Equal(0, result.AppliedCandidateCount);
        Assert.Contains(result.Diagnostics, d => d.Contains("no-stitch-candidate-no-mutation", StringComparison.Ordinal));
    }

    [Fact]
    public void StitchExecutor_MissingTopologyIds_Defers()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var planar = new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root);
        var candidate = new SurfaceFamilyStitchCandidate(
            "c1",
            SurfaceFamilyStitchCandidateKind.SharedTrimIdentity,
            SurfaceFamilyStitchCandidateReadiness.Ready,
            new InternalTrimIdentityToken("a", "b", "tok:1", TrimCurveFamily.Circle, "role", "key"),
            new EmittedTopologyIdentityEntry("edge:missingA", EmittedTopologyKind.Edge, null, EmittedTopologyRole.InnerCircularTrim, "orientation-compatible", []),
            new EmittedTopologyIdentityEntry("edge:missingB", EmittedTopologyKind.Edge, null, EmittedTopologyRole.CylindricalTopBoundary, "orientation-compatible", []),
            "orientation-compatible",
            [],
            "o");
        var plan = new SurfaceFamilyStitchPlanResult(true, [candidate], [], [], [], false);

        var result = SurfaceFamilyStitchExecutor.TryExecute(plan, planar, null);

        Assert.Equal(0, result.AppliedCandidateCount);
        Assert.Contains(result.Operations.SelectMany(o => o.Diagnostics), d => d.Contains("missing-local-topology-key", StringComparison.Ordinal));
    }

    [Fact]
    public void StitchExecutor_DoesNotClaimFullShell_OrExportStep()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var result = SurfaceFamilyStitchExecutor.TryExecute(
            SurfaceFamilyStitchCandidatePlanner.PlanBoxCylinder(root),
            new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root),
            EmitCylinder(root));

        Assert.False(result.FullShellClaimed);
        Assert.False(result.StepExportAttempted);
    }

    private static SurfaceMaterializationResult? EmitCylinder(CirNode root)
    {
        var gen = FacePatchCandidateGenerator.Generate(root);
        var candidate = gen.Candidates.SingleOrDefault(c => c.SourceSurface.Family == SurfacePatchFamily.Cylindrical
            && c.RetentionRole == FacePatchRetentionRole.ToolBoundaryRetainedInsideBase);
        if (candidate is null)
        {
            return null;
        }

        var ready = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 0, 0, 0, 0, 0, 0, 0, [], false);
        return new CylindricalSurfaceMaterializer().EmitRetainedWall(new CylindricalSurfaceMaterializer.RetainedWallEmissionRequest(candidate, ready));
    }
}
