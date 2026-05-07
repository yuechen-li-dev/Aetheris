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
    Deferred,
    NotApplicable
}

internal enum SeamKind
{
    CylindricalSelfSeam,
    NoSeamRequired,
    SeamDeferred,
    Unsupported
}

internal enum OrientationCompatibilityStatus
{
    Compatible,
    Deferred,
    Incompatible,
    NotApplicable
}

internal sealed record SeamClosureEvidence(
    string PatchKey,
    SeamKind SeamKind,
    ShellClosureReadiness Readiness,
    string Evidence,
    IReadOnlyList<string> Diagnostics);

internal sealed record OrientationCompatibilityEvidence(
    string PairKey,
    OrientationCompatibilityStatus OrientationStatus,
    ShellClosureReadiness Readiness,
    string Evidence,
    IReadOnlyList<string> Diagnostics);

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
    IReadOnlyList<SeamClosureEvidence> SeamClosureEvidence,
    IReadOnlyList<OrientationCompatibilityEvidence> OrientationEvidence,
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
        var seamEvidence = BuildSeamEvidence(patches, diagnostics);
        var pairs = BuildPairs(pairing, diagnostics);
        var orientationEvidence = BuildOrientationEvidence(pairs, diagnostics);
        var unpaired = BuildUnpaired(pairing, pairs, seamEvidence, diagnostics);

        var readiness = ResolveReadiness(patches, pairs, orientationEvidence, seamEvidence, unpaired);
        return new ShellStitchingDryRunResult(
            Success: patches.Count > 0,
            Readiness: readiness,
            PlannedPatches: patches,
            PlannedPairs: pairs,
            SeamClosureEvidence: seamEvidence,
            OrientationEvidence: orientationEvidence,
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


    private static List<SeamClosureEvidence> BuildSeamEvidence(IReadOnlyList<PlannedShellPatch> patches, List<string> diagnostics)
    {
        var seam = new List<SeamClosureEvidence>();
        foreach (var patch in patches.OrderBy(p => p.SourceCandidateKey, StringComparer.Ordinal))
        {
            if (patch.SurfaceFamily != SurfacePatchFamily.Cylindrical)
            {
                seam.Add(new SeamClosureEvidence(patch.SourceCandidateKey, SeamKind.NoSeamRequired, ShellClosureReadiness.NotApplicable,
                    "seam-not-applicable: planar patches do not require cylindrical self-seam closure evidence.", []));
                continue;
            }

            var hasConvention = patch.Diagnostics.Any(d => d.Contains("seam-convention-applied", StringComparison.OrdinalIgnoreCase));
            if (hasConvention)
            {
                var evidence = "seam-self-closed: cylindrical patch reports side seam forward/reversed coedge convention.";
                seam.Add(new SeamClosureEvidence(patch.SourceCandidateKey, SeamKind.CylindricalSelfSeam, ShellClosureReadiness.ReadyForAssemblyEvidence, evidence, [evidence]));
                diagnostics.Add($"seam-accounted-self-closure: patch={patch.SourceCandidateKey}");
            }
            else
            {
                var evidence = "seam-closure-deferred-missing-metadata: cylindrical patch diagnostics did not expose seam forward/reversed convention evidence.";
                seam.Add(new SeamClosureEvidence(patch.SourceCandidateKey, SeamKind.SeamDeferred, ShellClosureReadiness.Deferred, evidence, [evidence]));
                diagnostics.Add($"seam-closure-deferred: patch={patch.SourceCandidateKey} missing seam convention evidence");
            }
        }

        return seam;
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


    private static List<OrientationCompatibilityEvidence> BuildOrientationEvidence(IReadOnlyList<PlannedShellEdgePair> pairs, List<string> diagnostics)
    {
        var evidence = new List<OrientationCompatibilityEvidence>();
        foreach (var pair in pairs.OrderBy(p => p.EdgeToken, StringComparer.Ordinal))
        {
            var status = pair.OrientationCompatibility switch
            {
                StitchingOrientationCompatibility.Compatible => OrientationCompatibilityStatus.Compatible,
                StitchingOrientationCompatibility.Incompatible => OrientationCompatibilityStatus.Incompatible,
                StitchingOrientationCompatibility.Deferred => OrientationCompatibilityStatus.Deferred,
                _ => OrientationCompatibilityStatus.NotApplicable
            };

            var msg = status switch
            {
                OrientationCompatibilityStatus.Compatible => "orientation-compatible: paired planar/cylindrical circular boundaries expose complementary loop orientation policies.",
                OrientationCompatibilityStatus.Incompatible => "orientation-incompatible: paired boundaries expose non-complementary orientation policies.",
                OrientationCompatibilityStatus.Deferred => "orientation-deferred: pairing exists but orientation compatibility cannot be proven from current policy evidence.",
                _ => "orientation-not-applicable"
            };
            evidence.Add(new OrientationCompatibilityEvidence(pair.EdgeToken, status,
                status == OrientationCompatibilityStatus.Compatible ? ShellClosureReadiness.ReadyForAssemblyEvidence : ShellClosureReadiness.Deferred,
                msg, [msg]));
            diagnostics.Add($"orientation-{status.ToString().ToLowerInvariant()}: pair={pair.EdgeToken}");
        }

        return evidence;
    }

    private static List<UnpairedBoundaryEvidence> BuildUnpaired(TopologyPairingEvidenceResult pairing, IReadOnlyList<PlannedShellEdgePair> pairs, IReadOnlyList<SeamClosureEvidence> seamEvidence, List<string> diagnostics)
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

            var isCylindrical = edge.SourceSurfaceFamily == SurfacePatchFamily.Cylindrical;
            var hasAccountedSeam = seamEvidence.Any(s => s.SeamKind == SeamKind.CylindricalSelfSeam);
            if (isCylindrical && hasAccountedSeam)
            {
                diagnostics.Add($"unpaired-reclassified-expected-seam: patch={edge.SourceFaceKey} loop={edge.SourceLoopKey}");
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

    private static ShellClosureReadiness ResolveReadiness(IReadOnlyList<PlannedShellPatch> patches, IReadOnlyList<PlannedShellEdgePair> pairs, IReadOnlyList<OrientationCompatibilityEvidence> orientation, IReadOnlyList<SeamClosureEvidence> seamEvidence, IReadOnlyList<UnpairedBoundaryEvidence> unpaired)
    {
        if (patches.Count == 0) return ShellClosureReadiness.NotApplicable;
        if (patches.Any(p => p.Readiness == ShellClosureReadiness.Unsupported)) return ShellClosureReadiness.Unsupported;
        if (unpaired.Count > 0) return ShellClosureReadiness.Deferred;
        if (seamEvidence.Any(s => s.Readiness is ShellClosureReadiness.Deferred or ShellClosureReadiness.Unsupported)) return ShellClosureReadiness.Deferred;
        if (orientation.Any(o => o.OrientationStatus is OrientationCompatibilityStatus.Deferred or OrientationCompatibilityStatus.Incompatible)) return ShellClosureReadiness.Deferred;
        if (pairs.Count > 0 && orientation.Count > 0 && orientation.All(o => o.OrientationStatus == OrientationCompatibilityStatus.Compatible))
        {
            return ShellClosureReadiness.ReadyForAssemblyEvidence;
        }

        return ShellClosureReadiness.Deferred;
    }
}
