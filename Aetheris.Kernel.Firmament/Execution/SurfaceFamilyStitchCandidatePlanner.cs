using Aetheris.Kernel.Core.Cir;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum SurfaceFamilyStitchCandidateReadiness { Ready, Deferred, Unsupported, Ambiguous }
internal enum SurfaceFamilyStitchCandidateKind { SharedTrimIdentity, CylindricalSelfSeam, Deferred, Unsupported }

internal sealed record SurfaceFamilyStitchCandidate(
    string CandidateId,
    SurfaceFamilyStitchCandidateKind Kind,
    SurfaceFamilyStitchCandidateReadiness Readiness,
    InternalTrimIdentityToken? Token,
    EmittedTopologyIdentityEntry? EntryA,
    EmittedTopologyIdentityEntry? EntryB,
    string OrientationPolicy,
    IReadOnlyList<string> Diagnostics,
    string OrderingKey);

internal sealed record SurfaceFamilyStitchPlanResult(
    bool Success,
    IReadOnlyList<SurfaceFamilyStitchCandidate> Candidates,
    IReadOnlyList<string> DeferredItems,
    IReadOnlyList<string> AmbiguousItems,
    IReadOnlyList<string> Diagnostics,
    bool StitchExecutionImplemented);

internal static class SurfaceFamilyStitchCandidatePlanner
{
    internal static SurfaceFamilyStitchPlanResult Plan(
        IEnumerable<EmittedTopologyIdentityMap> identityMaps,
        EmittedTokenPairingAnalysisResult tokenAnalysis,
        ShellStitchingDryRunResult? shellEvidence = null)
    {
        _ = identityMaps?.ToArray(); // explicit consumed input; planner does not rematerialize
        var diagnostics = new List<string>();
        var candidates = new List<SurfaceFamilyStitchCandidate>();
        var deferred = new List<string>();
        var ambiguous = new List<string>();

        foreach (var pair in tokenAnalysis.SafePairs.OrderBy(p => p.TokenOrderingKey, StringComparer.Ordinal))
        {
            var orientation = shellEvidence?.OrientationEvidence.FirstOrDefault(e => string.Equals(e.PairKey, pair.TokenOrderingKey, StringComparison.Ordinal));
            var readiness = SurfaceFamilyStitchCandidateReadiness.Deferred;
            var orientationPolicy = "orientation-missing";
            var entryRoles = new[] { pair.EntryA.Role, pair.EntryB.Role };

            if (orientation is not null)
            {
                if (orientation.OrientationStatus == OrientationCompatibilityStatus.Compatible)
                {
                    readiness = SurfaceFamilyStitchCandidateReadiness.Ready;
                    orientationPolicy = "orientation-compatible";
                    diagnostics.Add($"orientation-compatible: token={pair.TokenOrderingKey} supports ready stitch candidate.");
                }
                else
                {
                    readiness = SurfaceFamilyStitchCandidateReadiness.Deferred;
                    orientationPolicy = $"orientation-{orientation.OrientationStatus.ToString().ToLowerInvariant()}";
                    diagnostics.Add($"orientation-deferred: token={pair.TokenOrderingKey} has status {orientation.OrientationStatus}.");
                }
            }
            else if (entryRoles.Contains(EmittedTopologyRole.InnerCircularTrim)
                && (entryRoles.Contains(EmittedTopologyRole.CylindricalTopBoundary) || entryRoles.Contains(EmittedTopologyRole.CylindricalBottomBoundary)))
            {
                readiness = SurfaceFamilyStitchCandidateReadiness.Ready;
                orientationPolicy = "orientation-convention-safe";
                diagnostics.Add($"orientation-compatible: token={pair.TokenOrderingKey} accepted by planar/cylindrical role convention.");
            }
            else
            {
                diagnostics.Add($"orientation-missing-deferred: token={pair.TokenOrderingKey} has no orientation evidence.");
            }

            var orderingKey = $"token:{pair.TokenOrderingKey}|{pair.EntryA.LocalTopologyKey}|{pair.EntryB.LocalTopologyKey}|SharedTrimIdentity";
            candidates.Add(new SurfaceFamilyStitchCandidate(
                CandidateId: $"candidate:{pair.TokenOrderingKey}",
                Kind: SurfaceFamilyStitchCandidateKind.SharedTrimIdentity,
                Readiness: readiness,
                Token: pair.EntryA.TrimIdentityToken,
                EntryA: pair.EntryA,
                EntryB: pair.EntryB,
                OrientationPolicy: orientationPolicy,
                Diagnostics: pair.Diagnostics.Concat(["safe-pair-converted-to-stitch-candidate"]).ToArray(),
                OrderingKey: orderingKey));
            diagnostics.Add($"safe-pair-converted-to-stitch-candidate: token={pair.TokenOrderingKey}.");
        }

        foreach (var g in tokenAnalysis.MissingMateGroups)
        {
            deferred.Add($"missing-mate-deferred:{g.TokenOrderingKey}");
            diagnostics.Add($"missing-mate-ignored-deferred: token={g.TokenOrderingKey}.");
        }

        foreach (var g in tokenAnalysis.AmbiguousGroups)
        {
            ambiguous.Add($"ambiguous-multiplicity-deferred:{g.TokenOrderingKey}");
            diagnostics.Add($"ambiguous-multiplicity-ignored-deferred: token={g.TokenOrderingKey}.");
        }

        if (tokenAnalysis.Diagnostics.Any(d => d.Contains("incompatible-roles", StringComparison.OrdinalIgnoreCase)))
        {
            diagnostics.Add("incompatible-roles-ignored-deferred: no stitch candidate created for incompatible role groups.");
        }

        if (shellEvidence is not null)
        {
            foreach (var seam in shellEvidence.SeamClosureEvidence.Where(s => s.SeamKind is SeamKind.CylindricalSelfSeam or SeamKind.SeamDeferred))
            {
                diagnostics.Add(seam.SeamKind == SeamKind.CylindricalSelfSeam
                    ? $"seam-accounted: patch={seam.PatchKey} cylindrical self seam evidence exists."
                    : $"seam-deferred: patch={seam.PatchKey} cylindrical seam metadata missing/deferred.");
            }
        }

        diagnostics.Add("stitch-execution-not-implemented: planner emits candidates only; no topology mutation or merge executed.");

        return new SurfaceFamilyStitchPlanResult(
            Success: candidates.Count > 0,
            Candidates: candidates.OrderBy(c => c.OrderingKey, StringComparer.Ordinal).ToArray(),
            DeferredItems: deferred.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            AmbiguousItems: ambiguous.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            Diagnostics: diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            StitchExecutionImplemented: false);
    }

    internal static SurfaceFamilyStitchPlanResult PlanBoxCylinder(CirNode root)
    {
        var planar = new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root);
        var gen = FacePatchCandidateGenerator.Generate(root);
        var cylCandidate = gen.Candidates.SingleOrDefault(c =>
            c.SourceSurface.Family == SurfacePatchFamily.Cylindrical
            && c.RetentionRole == FacePatchRetentionRole.ToolBoundaryRetainedInsideBase);

        SurfaceMaterializationResult? cyl = null;
        if (cylCandidate is not null)
        {
            var ready = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 0, 0, 0, 0, 0, 0, 0, [], false);
            cyl = new CylindricalSurfaceMaterializer().EmitRetainedWall(new CylindricalSurfaceMaterializer.RetainedWallEmissionRequest(cylCandidate, ready));
        }

        var maps = planar.Entries.Where(e => e.Emitted).Select(e => e.IdentityMap ?? EmittedTopologyIdentityMap.Empty)
            .Concat(cyl?.IdentityMap is null ? [] : [cyl.IdentityMap]);
        var analysis = EmittedTokenPairingAnalyzer.Analyze(maps);
        var shell = ShellStitchingDryRunPlanner.Generate(root);
        return Plan(maps, analysis, shell);
    }
}
