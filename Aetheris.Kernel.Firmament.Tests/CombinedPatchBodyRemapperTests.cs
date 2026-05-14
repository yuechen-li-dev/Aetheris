using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CombinedPatchBodyRemapperTests
{
    [Fact]
    public void CombinedRemap_BoxCylinder_CreatesPartialBody()
    {
        var (planar, cyl) = BuildRealPath();
        var patches = planar.Entries.Where(e => e.Emission is { Success: true, Body: not null }).Select(e => e.Emission!).Concat(cyl is null ? [] : [cyl]).ToArray();
        var maps = planar.Entries.Where(e => e.IdentityMap is not null).Select(e => e.IdentityMap!).Concat(cyl?.IdentityMap is null ? [] : [cyl.IdentityMap]).ToArray();
        var result = CombinedPatchBodyRemapper.TryCombine(patches, maps);
        Assert.True(result.Success);
        Assert.NotNull(result.CombinedBody);
        Assert.NotEmpty(result.CombinedBody!.Topology.Faces);
        Assert.False(result.SharedEdgeMutationApplied);
        Assert.False(result.FullShellClaimed);
        Assert.False(result.StepExportAttempted);
    }

    [Fact]
    public void CombinedRemap_PreservesAndRemapsIdentityRefs()
    {
        var (planar, cyl) = BuildRealPath();
        var patches = planar.Entries.Where(e => e.Emission is { Success: true, Body: not null }).Select(e => e.Emission!).Concat(cyl is null ? [] : [cyl]).ToArray();
        var maps = planar.Entries.Where(e => e.IdentityMap is not null).Select(e => e.IdentityMap!).Concat(cyl?.IdentityMap is null ? [] : [cyl.IdentityMap]).ToArray();
        var result = CombinedPatchBodyRemapper.TryCombine(patches, maps);
        var withRefs = result.RemappedIdentityMaps.SelectMany(m => m.Entries).Where(e => e.TopologyReference is not null).ToArray();
        Assert.NotEmpty(withRefs);
        Assert.NotEmpty(result.ReferenceRemaps);
    }

    [Fact]
    public void CombinedRemap_RejectsMissingRefsPrecisely()
    {
        var r = CombinedPatchBodyRemapper.TryCombine([], [new EmittedTopologyIdentityMap([new EmittedTopologyIdentityEntry("x", EmittedTopologyKind.Edge, null, EmittedTopologyRole.Unmapped, "orientation-compatible", [])])]);
        Assert.False(r.Success);
        Assert.Contains(r.Diagnostics, d => d.Contains("no successful emitted patch bodies", StringComparison.Ordinal));
    }

    [Fact]
    public void CombinedRemap_Deterministic()
    {
        var (planar, cyl) = BuildRealPath();
        var patches = planar.Entries.Where(e => e.Emission is { Success: true, Body: not null }).Select(e => e.Emission!).Concat(cyl is null ? [] : [cyl]).ToArray();
        var maps = planar.Entries.Where(e => e.IdentityMap is not null).Select(e => e.IdentityMap!).Concat(cyl?.IdentityMap is null ? [] : [cyl.IdentityMap]).ToArray();
        var a = CombinedPatchBodyRemapper.TryCombine(patches, maps);
        var b = CombinedPatchBodyRemapper.TryCombine(patches, maps);
        Assert.Equal(a.ReferenceRemaps.Count, b.ReferenceRemaps.Count);
        Assert.Equal(a.PatchSummaries.Select(x => x.PatchKey), b.PatchSummaries.Select(x => x.PatchKey));
    }

    private static (PlanarSurfaceMaterializer.PlanarPatchSetMaterializationResult planar, SurfaceMaterializationResult? cyl) BuildRealPath()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var planar = new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root);
        var gen = FacePatchCandidateGenerator.Generate(root);
        var c = gen.Candidates.SingleOrDefault(x => x.SourceSurface.Family == SurfacePatchFamily.Cylindrical && x.RetentionRole == FacePatchRetentionRole.ToolBoundaryRetainedInsideBase);
        SurfaceMaterializationResult? cyl = null;
        if (c is not null)
        {
            var ready = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 0, 0, 0, 0, 0, 0, 0, [], false);
            cyl = new CylindricalSurfaceMaterializer().EmitRetainedWall(new(c, ready));
        }
        return (planar, cyl);
    }
}
