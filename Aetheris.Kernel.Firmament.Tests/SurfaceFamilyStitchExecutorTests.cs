using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfaceFamilyStitchExecutorTests
{
    [Fact]
    public void StitchExecutor_BoxCylinder_AppliesOneSharedEdgeRewriteOrPreciseBlocker()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var planar = new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root);
        var cylindrical = EmitCylinder(root);
        var maps = planar.Entries.Where(e => e.Emitted).Select(e => e.IdentityMap ?? EmittedTopologyIdentityMap.Empty)
            .Concat(cylindrical?.IdentityMap is null ? [] : [cylindrical.IdentityMap]);
        var analysis = EmittedTokenPairingAnalyzer.Analyze(maps);
        var plan = SurfaceFamilyStitchCandidatePlanner.Plan(maps, analysis, ShellStitchingDryRunPlanner.Generate(root));

        var result = SurfaceFamilyStitchExecutor.TryExecute(plan, planar, cylindrical);

        if (result.Success)
        {
            Assert.Equal(1, result.AppliedCandidateCount);
            Assert.NotNull(result.Body);
            Assert.Contains(result.Operations.SelectMany(o => o.Diagnostics), d => d.Contains("shared-edge-rewrite-applied", StringComparison.Ordinal));
            var applied = result.Operations.Single(o => o.Status == SurfaceFamilyStitchExecutionStatus.AssembledPartialBody);
            var canonical = applied.EdgeOrCoedgeIds.First(id => id.StartsWith("E", StringComparison.Ordinal));
            var canonicalEdgeId = new Aetheris.Kernel.Core.Topology.EdgeId(int.Parse(canonical.Split(':')[1]));
            var cA = new Aetheris.Kernel.Core.Topology.CoedgeId(int.Parse(applied.EdgeOrCoedgeIds[2].Split(':')[1]));
            var cB = new Aetheris.Kernel.Core.Topology.CoedgeId(int.Parse(applied.EdgeOrCoedgeIds[3].Split(':')[1]));
            Assert.Equal(canonicalEdgeId, result.Body!.Topology.GetCoedge(cA).EdgeId);
            Assert.Equal(canonicalEdgeId, result.Body!.Topology.GetCoedge(cB).EdgeId);
        }
        else
        {
            Assert.True(result.Operations.SelectMany(o => o.Diagnostics).Any(d => d.Contains("mutation-unsupported-topology-model", StringComparison.Ordinal)
                || d.Contains("candidate-deferred", StringComparison.Ordinal))
                || result.Diagnostics.Any(d => d.Contains("no-stitch-candidate-no-mutation", StringComparison.Ordinal)));
        }

        Assert.False(result.FullShellClaimed);
        Assert.False(result.StepExportAttempted);
    }

    [Fact]
    public void StitchExecutor_RequiresRemappedRefs()
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
    public void StitchExecutor_RejectsTokenMismatch()
    {
        var tokenA = new InternalTrimIdentityToken("a", "b", "tok:A", TrimCurveFamily.Circle, "role", "a");
        var tokenB = new InternalTrimIdentityToken("a", "b", "tok:B", TrimCurveFamily.Circle, "role", "b");
        var candidate = new SurfaceFamilyStitchCandidate("c", SurfaceFamilyStitchCandidateKind.SharedTrimIdentity, SurfaceFamilyStitchCandidateReadiness.Ready, tokenA,
            new EmittedTopologyIdentityEntry("ea", EmittedTopologyKind.Edge, tokenA, EmittedTopologyRole.InnerCircularTrim, "orientation-compatible", []),
            new EmittedTopologyIdentityEntry("eb", EmittedTopologyKind.Edge, tokenB, EmittedTopologyRole.CylindricalTopBoundary, "orientation-compatible", []),
            "orientation-compatible", [], "a");
        var plan = new SurfaceFamilyStitchPlanResult(true, [candidate], [], [], [], false);

        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var result = SurfaceFamilyStitchExecutor.TryExecute(plan, new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root), null);
        Assert.Contains(result.Operations.SelectMany(o => o.Diagnostics), d => d.Contains("token-mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public void StitchExecutor_DoesNotMergeVerticesYet()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var result = SurfaceFamilyStitchExecutor.TryExecute(
            SurfaceFamilyStitchCandidatePlanner.PlanBoxCylinder(root),
            new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root),
            EmitCylinder(root));

        Assert.Contains(result.Diagnostics, d => d.Contains("vertex-merge-deferred", StringComparison.Ordinal));
    }

    [Fact]
    public void StitchExecutor_NoFullShellClaim()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var result = SurfaceFamilyStitchExecutor.TryExecute(
            SurfaceFamilyStitchCandidatePlanner.PlanBoxCylinder(root),
            new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root),
            EmitCylinder(root));

        Assert.False(result.FullShellClaimed);
    }

    [Fact]
    public void StitchExecutor_NoStepExport()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var result = SurfaceFamilyStitchExecutor.TryExecute(
            SurfaceFamilyStitchCandidatePlanner.PlanBoxCylinder(root),
            new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root),
            EmitCylinder(root));

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
