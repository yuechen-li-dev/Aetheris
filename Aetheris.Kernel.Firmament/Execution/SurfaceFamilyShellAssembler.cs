using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Cir;

namespace Aetheris.Kernel.Firmament.Execution;

internal sealed record SurfaceFamilyShellAssemblyResult(
    bool Success,
    BrepBody? Body,
    string Pattern,
    ShellClosureReadiness Readiness,
    bool FullShellAssembled,
    int PlanarPatchCount,
    bool CylindricalPatchConsumed,
    IReadOnlyList<string> Diagnostics);

internal static class SurfaceFamilyShellAssembler
{
    internal static SurfaceFamilyShellAssemblyResult TryAssembleBoxMinusCylinder(
        CirNode root,
        NativeGeometryReplayLog? replayLog = null)
    {
        _ = replayLog;
        var dryRun = ShellStitchingDryRunPlanner.Generate(root);
        var planarSet = new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root);
        var generation = FacePatchCandidateGenerator.Generate(root);
        var cylindricalCandidate = generation.Candidates.SingleOrDefault(c => c.SourceSurface.Family == SurfacePatchFamily.Cylindrical && c.RetentionRole == FacePatchRetentionRole.ToolBoundaryRetainedInsideBase);
        SurfaceMaterializationResult? cylindricalEmission = null;
        if (cylindricalCandidate is not null)
        {
            var synthReadiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 0, 0, 0, 0, 0, 0, 0, [], false);
            cylindricalEmission = new CylindricalSurfaceMaterializer().EmitRetainedWall(new CylindricalSurfaceMaterializer.RetainedWallEmissionRequest(cylindricalCandidate, synthReadiness));
        }
        var pairedTokens = TopologyPairingEvidenceGenerator.Generate(root).PlannedCoedgePairings
            .Where(p => p.PairingKind == PlannedCoedgePairingKind.SharedTrimIdentity)
            .Select(p => p.EdgeUseA.IdentityToken?.OrderingKey)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToHashSet(StringComparer.Ordinal);
        var diagnostics = new List<string>
        {
            "pattern: subtract(box,cylinder)",
            $"readiness-gate-state: {dryRun.Readiness}"
        };

        var planarPatchCount = dryRun.PlannedPatches.Count(p => p.SurfaceFamily == SurfacePatchFamily.Planar);
        var hasCylinderPatch = dryRun.PlannedPatches.Any(p => p.SurfaceFamily == SurfacePatchFamily.Cylindrical);
        diagnostics.Add($"planar-patches-consumed: {planarPatchCount}");
        diagnostics.Add($"cylindrical-patch-consumed: {hasCylinderPatch}");
        var emittedEntries = planarSet.Entries.Where(e => e.Emitted).SelectMany(e => e.IdentityMap?.Entries ?? []).Concat(cylindricalEmission?.IdentityMap?.Entries ?? []).ToArray();
        diagnostics.Add(emittedEntries.Any(e => e.Role == EmittedTopologyRole.InnerCircularTrim && e.TrimIdentityToken is not null)
            ? "emitted-identity-planar-inner-circle-token-attached: planar emitted topology exposes inner-circle token."
            : "emitted-identity-planar-inner-circle-token-missing: planar emitted topology did not expose an inner-circle token.");
        diagnostics.Add(emittedEntries.Any(e => e.Role == EmittedTopologyRole.CylindricalSeam)
            ? "emitted-identity-cylindrical-seam-role-tagged: cylindrical seam metadata is available."
            : "emitted-identity-cylindrical-seam-role-missing: cylindrical seam metadata not found.");
        var cylBoundaryEntries = emittedEntries.Where(e => e.Role is EmittedTopologyRole.CylindricalTopBoundary or EmittedTopologyRole.CylindricalBottomBoundary).ToArray();
        diagnostics.Add(cylBoundaryEntries.Any(e => e.TrimIdentityToken is not null)
            ? "emitted-identity-cylindrical-boundary-token-attached: cylindrical boundary token evidence is present."
            : "emitted-identity-cylindrical-boundary-token-missing: cylindrical boundary emitted without token evidence.");
        var matched = emittedEntries.Where(e => e.TrimIdentityToken is { } tok && pairedTokens.Contains(tok.OrderingKey)).ToArray();
        diagnostics.Add(matched.Length > 0
            ? $"emitted-identity-token-match-candidates: found {matched.Length} emitted topology candidate(s) matching dry-run shared trim identity tokens."
            : "emitted-identity-token-match-candidates-missing: no emitted topology entries matched dry-run shared trim identity tokens.");

        if (dryRun.Readiness != ShellClosureReadiness.ReadyForAssemblyEvidence)
        {
            diagnostics.Add("readiness-gate-rejected: no shell-readiness, no assembly.");
            diagnostics.AddRange(dryRun.Diagnostics);
            return new(false, null, CirBrepMaterializer.BoxMinusCylinderPattern, dryRun.Readiness, false, planarPatchCount, hasCylinderPatch, diagnostics);
        }

        diagnostics.Add("readiness-gate-accepted: shell evidence is ready.");
        diagnostics.Add("shell-assembly-blocked: identity metadata bridge is present, but topology merge/stitch execution is still intentionally unimplemented.");
        diagnostics.AddRange(dryRun.Diagnostics);

        return new(false, null, CirBrepMaterializer.BoxMinusCylinderPattern, dryRun.Readiness, false, planarPatchCount, hasCylinderPatch, diagnostics);
    }
}
