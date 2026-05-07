using Aetheris.Kernel.Core.Cir;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum ShellClosureReadiness
{
    NotApplicable,
    ReadyForAssemblyEvidence,
    Deferred,
    Unsupported
}

internal enum StitchingOrientationCompatibility
{
    Compatible,
    Incompatible,
    Deferred
}

internal sealed record PlannedShellPatch(
    string SourceCandidateKey,
    SurfacePatchFamily SurfaceFamily,
    string EmittedPatchKind,
    IReadOnlyList<string> LoopKeys,
    ShellClosureReadiness Readiness,
    IReadOnlyList<string> Diagnostics);

internal sealed record PlannedShellEdgePair(
    string EdgeToken,
    string PatchA,
    string PatchB,
    string LoopA,
    string LoopB,
    ShellClosureReadiness Readiness,
    StitchingOrientationCompatibility OrientationCompatibility,
    IReadOnlyList<string> Diagnostics);

internal sealed record UnpairedBoundaryEvidence(
    string Patch,
    string Loop,
    string Reason,
    ShellClosureReadiness Readiness);

internal sealed record ShellStitchingDryRunResult(
    bool Success,
    ShellClosureReadiness Readiness,
    IReadOnlyList<PlannedShellPatch> PlannedPatches,
    IReadOnlyList<PlannedShellEdgePair> PlannedPairs,
    IReadOnlyList<UnpairedBoundaryEvidence> UnpairedBoundaries,
    IReadOnlyList<string> Diagnostics,
    bool ShellAssemblyImplemented);

internal static class ShellStitchingDryRunPlanner
{
    internal static ShellStitchingDryRunResult Generate(CirNode root)
    {
        var planar = new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root);
        var generation = FacePatchCandidateGenerator.Generate(root);
        var cylindricalCandidate = generation.Candidates.SingleOrDefault(c =>
            c.SourceSurface.Family == SurfacePatchFamily.Cylindrical
            && c.RetentionRole == FacePatchRetentionRole.ToolBoundaryRetainedInsideBase);

        var synthReadiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 0, 0, 0, 0, 0, 0, 0, [], false);
        SurfaceMaterializationResult? cylindricalEmission = null;
        if (cylindricalCandidate is not null)
        {
            cylindricalEmission = new CylindricalSurfaceMaterializer().EmitRetainedWall(new CylindricalSurfaceMaterializer.RetainedWallEmissionRequest(cylindricalCandidate, synthReadiness));
        }

        var pairing = TopologyPairingEvidenceGenerator.Generate(root);
        var diagnostics = new List<string>
        {
            "scope-note: shell stitching dry-run only; shell/body assembly is not implemented.",
            "shell-assembly-not-implemented: dry-run emits patch/pairing evidence only."
        };

        var patches = BuildPatches(planar, cylindricalCandidate, cylindricalEmission, diagnostics);
        var pairs = BuildPairs(pairing, diagnostics);
        var unpaired = BuildUnpaired(pairing, pairs, diagnostics);

        var readiness = ResolveReadiness(patches, pairs, unpaired);
        return new ShellStitchingDryRunResult(
            Success: patches.Count > 0,
            Readiness: readiness,
            PlannedPatches: patches,
            PlannedPairs: pairs,
            UnpairedBoundaries: unpaired,
            Diagnostics: diagnostics.Distinct().ToArray(),
            ShellAssemblyImplemented: false);
    }

    private static List<PlannedShellPatch> BuildPatches(
        PlanarSurfaceMaterializer.PlanarPatchSetMaterializationResult planar,
        FacePatchCandidate? cylindricalCandidate,
        SurfaceMaterializationResult? cylindricalEmission,
        List<string> diagnostics)
    {
        var patches = new List<PlannedShellPatch>();
        foreach (var entry in planar.Entries.Where(e => e.Emitted).OrderBy(e => e.Candidate.SourceSurface.Provenance, StringComparer.Ordinal))
        {
            var loopKeys = entry.Candidate.RetainedRegionLoopGroups.Select(g => g.OrderingKey).DefaultIfEmpty("outer-boundary").OrderBy(k => k, StringComparer.Ordinal).ToArray();
            patches.Add(new PlannedShellPatch(
                SourceCandidateKey: entry.Candidate.SourceSurface.Provenance,
                SurfaceFamily: SurfacePatchFamily.Planar,
                EmittedPatchKind: "planar-retained-base-patch",
                LoopKeys: loopKeys,
                Readiness: ShellClosureReadiness.Deferred,
                Diagnostics: entry.Diagnostics));
            diagnostics.Add($"planar-patch-emitted: candidate={entry.Candidate.SourceSurface.Provenance}");
        }

        if (cylindricalCandidate is not null)
        {
            var loopKeys = cylindricalCandidate.RetainedRegionLoopGroups.Select(g => g.OrderingKey).OrderBy(k => k, StringComparer.Ordinal).ToArray();
            var emitted = cylindricalEmission?.Success == true;
            patches.Add(new PlannedShellPatch(
                SourceCandidateKey: cylindricalCandidate.SourceSurface.Provenance,
                SurfaceFamily: SurfacePatchFamily.Cylindrical,
                EmittedPatchKind: emitted ? "cylindrical-retained-wall-patch" : "cylindrical-retained-wall-patch-deferred",
                LoopKeys: loopKeys,
                Readiness: emitted ? ShellClosureReadiness.Deferred : ShellClosureReadiness.Unsupported,
                Diagnostics: cylindricalEmission?.Diagnostics ?? ["cylindrical-patch-emission-missing: no emitted cylindrical retained wall patch was available."]));
            diagnostics.Add(emitted
                ? $"cylindrical-patch-emitted: candidate={cylindricalCandidate.SourceSurface.Provenance}"
                : $"cylindrical-patch-deferred: candidate={cylindricalCandidate.SourceSurface.Provenance}");
        }

        return patches.OrderBy(p => p.SourceCandidateKey, StringComparer.Ordinal).ToList();
    }

    private static List<PlannedShellEdgePair> BuildPairs(TopologyPairingEvidenceResult pairing, List<string> diagnostics)
    {
        var pairs = new List<PlannedShellEdgePair>();
        foreach (var p in pairing.PlannedCoedgePairings.Where(p => p.PairingKind == PlannedCoedgePairingKind.SharedTrimIdentity).OrderBy(p => p.OrderingKey, StringComparer.Ordinal))
        {
            var orientation = p.EdgeUseA.OrientationPolicy == p.EdgeUseB.OrientationPolicy
                ? StitchingOrientationCompatibility.Compatible
                : StitchingOrientationCompatibility.Deferred;
            pairs.Add(new PlannedShellEdgePair(
                EdgeToken: p.EdgeUseA.IdentityToken?.OrderingKey ?? p.OrderingKey,
                PatchA: p.EdgeUseA.SourceFaceKey,
                PatchB: p.EdgeUseB.SourceFaceKey,
                LoopA: p.EdgeUseA.SourceLoopKey,
                LoopB: p.EdgeUseB.SourceLoopKey,
                Readiness: ShellClosureReadiness.Deferred,
                OrientationCompatibility: orientation,
                Diagnostics: p.Diagnostics));
            diagnostics.Add($"pairing-found-by-token: {p.Evidence}");
        }

        if (pairing.PlannedCoedgePairings.Any(p => p.PairingKind == PlannedCoedgePairingKind.Deferred))
        {
            diagnostics.Add("pairing-deferred: one or more boundaries are missing one-to-one identity token evidence.");
        }

        return pairs;
    }

    private static List<UnpairedBoundaryEvidence> BuildUnpaired(TopologyPairingEvidenceResult pairing, IReadOnlyList<PlannedShellEdgePair> pairs, List<string> diagnostics)
    {
        var pairedTokens = pairs.Select(p => p.EdgeToken).ToHashSet(StringComparer.Ordinal);
        var unpaired = new List<UnpairedBoundaryEvidence>();
        foreach (var edge in pairing.PlannedEdgeUses.OrderBy(e => e.OrderingKey, StringComparer.Ordinal))
        {
            var token = edge.IdentityToken?.OrderingKey;
            if (token is not null && pairedTokens.Contains(token))
            {
                continue;
            }

            var reason = edge.IdentityToken is null
                ? "unpaired-boundary-missing-identity-token"
                : "unpaired-boundary-token-without-match";
            unpaired.Add(new UnpairedBoundaryEvidence(edge.SourceFaceKey, edge.SourceLoopKey, reason, ShellClosureReadiness.Deferred));
        }

        if (unpaired.Count > 0) diagnostics.Add($"unpaired-boundary-remaining: count={unpaired.Count}");
        diagnostics.Add("seam-closure-deferred: cylindrical seam self-closure accounting is diagnostic-only in CIR-F12.");
        return unpaired;
    }

    private static ShellClosureReadiness ResolveReadiness(IReadOnlyList<PlannedShellPatch> patches, IReadOnlyList<PlannedShellEdgePair> pairs, IReadOnlyList<UnpairedBoundaryEvidence> unpaired)
    {
        if (patches.Count == 0) return ShellClosureReadiness.NotApplicable;
        if (patches.Any(p => p.Readiness == ShellClosureReadiness.Unsupported)) return ShellClosureReadiness.Unsupported;
        if (unpaired.Count > 0) return ShellClosureReadiness.Deferred;
        if (pairs.Count > 0 && pairs.All(p => p.OrientationCompatibility == StitchingOrientationCompatibility.Compatible))
        {
            return ShellClosureReadiness.ReadyForAssemblyEvidence;
        }

        return ShellClosureReadiness.Deferred;
    }
}
