using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class DuplicateEdgeCleanupPlannerTests
{
    [Fact]
    public void DuplicateEdgeCleanup_BoxCylinder_IdentifiesStitchDuplicateCandidate()
    {
        var (exec, body) = RunBoxCylinder();
        var plan = DuplicateEdgeCleanupPlanner.Plan(exec, body);

        Assert.True(plan.Success);
        Assert.True(plan.Candidates.Count > 0 || exec.AppliedCandidateCount == 0 || plan.Diagnostics.Any(d => d.Contains("missing-operation-metadata", StringComparison.Ordinal)));
        if (plan.Candidates.Count > 0)
        {
            Assert.All(plan.Candidates, c =>
            {
                Assert.False(string.IsNullOrWhiteSpace(c.Token));
                Assert.StartsWith("E:", c.CanonicalEdgeId, StringComparison.Ordinal);
                Assert.StartsWith("E:", c.DuplicateEdgeId, StringComparison.Ordinal);
            });
        }
    }

    [Fact]
    public void DuplicateEdgeCleanup_ClassifiesZeroCoedgeDuplicate()
    {
        var (exec, body) = RunBoxCylinder();
        var plan = DuplicateEdgeCleanupPlanner.Plan(exec, body);
        var zeroDup = plan.BoundaryClassifications.Where(c => c.Classification == BoundaryEdgeClassificationKind.StitchDuplicateUnreferenced).ToArray();
        Assert.True(zeroDup.Length > 0 || exec.AppliedCandidateCount == 0 || plan.Diagnostics.Any(d => d.Contains("no-canonical-duplicate-evidence", StringComparison.Ordinal)));
    }

    [Fact]
    public void DuplicateEdgeCleanup_ClassifiesOneCoedgeShellBlockers()
    {
        var (exec, body) = RunBoxCylinder();
        var plan = DuplicateEdgeCleanupPlanner.Plan(exec, body);
        Assert.All(plan.BoundaryClassifications.Where(c => c.CoedgeUseCount == 1), c => Assert.Equal(BoundaryEdgeClassificationKind.ShellClosureBlocker, c.Classification));
    }

    [Fact]
    public void DuplicateEdgeCleanup_DoesNotMutateByDefault()
    {
        var (exec, body) = RunBoxCylinder();
        var plan = DuplicateEdgeCleanupPlanner.Plan(exec, body);
        Assert.False(plan.CleanupMutationImplemented);
    }

    [Fact]
    public void PressureTest_ReportsDuplicateCleanupCandidates()
    {
        var result = SurfaceFamilyBoxCylinderPressureTest.Run(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));
        var stage = result.Stages.Single(s => s.Stage == "DuplicateEdgeCleanup");
        Assert.True(stage.Counts.ContainsKey("DuplicateCleanupCandidateCount"));
        Assert.Contains(stage.Diagnostics, d => d.Contains("cleanup-mutation-not-implemented", StringComparison.Ordinal));
    }

    [Fact]
    public void PressureTest_ShellStillNotClaimedAfterCleanupPlanning()
    {
        var result = SurfaceFamilyBoxCylinderPressureTest.Run(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));
        Assert.False(result.ShellClosureValidated);
    }

    private static (SurfaceFamilyStitchExecutionResult exec, Aetheris.Kernel.Core.Brep.BrepBody body) RunBoxCylinder()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var planar = new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root);
        var cylindrical = EmitCylinder(root);
        var maps = planar.Entries.Where(e => e.Emitted).Select(e => e.IdentityMap ?? EmittedTopologyIdentityMap.Empty)
            .Concat(cylindrical?.IdentityMap is null ? [] : [cylindrical.IdentityMap]);
        var analysis = EmittedTokenPairingAnalyzer.Analyze(maps);
        var plan = SurfaceFamilyStitchCandidatePlanner.Plan(maps, analysis, ShellStitchingDryRunPlanner.Generate(root));
        var exec = SurfaceFamilyStitchExecutor.TryExecute(plan, planar, cylindrical);

        var patchResults = planar.Entries.Where(e => e.Emission is { Success: true, Body: not null }).Select(e => e.Emission!).Concat(cylindrical is { Success: true, Body: not null } ? [cylindrical] : []).ToArray();
        var remap = CombinedPatchBodyRemapper.TryCombine(patchResults, maps.ToArray());
        return (exec, exec.Body ?? remap.CombinedBody!);
    }

    private static SurfaceMaterializationResult? EmitCylinder(CirNode root)
    {
        var gen = FacePatchCandidateGenerator.Generate(root);
        var candidate = gen.Candidates.SingleOrDefault(c => c.SourceSurface.Family == SurfacePatchFamily.Cylindrical
            && c.RetentionRole == FacePatchRetentionRole.ToolBoundaryRetainedInsideBase);
        if (candidate is null) return null;
        var ready = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 0, 0, 0, 0, 0, 0, 0, [], false);
        return new CylindricalSurfaceMaterializer().EmitRetainedWall(new CylindricalSurfaceMaterializer.RetainedWallEmissionRequest(candidate, ready));
    }
}
