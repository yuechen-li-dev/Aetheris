using Aetheris.Kernel.Core.Brep;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum SurfaceFamilyStitchExecutionStatus
{
    AssembledPartialBody,
    Deferred,
    Unsupported,
    Failed
}

internal sealed record AppliedStitchOperation(
    string CandidateId,
    string Token,
    string EntryAKey,
    string EntryBKey,
    IReadOnlyList<string> EdgeOrCoedgeIds,
    SurfaceFamilyStitchExecutionStatus Status,
    IReadOnlyList<string> Diagnostics);

internal sealed record SurfaceFamilyStitchExecutionResult(
    bool Success,
    SurfaceFamilyStitchExecutionStatus Status,
    BrepBody? Body,
    int AppliedCandidateCount,
    int DeferredCandidateCount,
    IReadOnlyList<AppliedStitchOperation> Operations,
    IReadOnlyList<string> Diagnostics,
    bool FullShellClaimed,
    bool StepExportAttempted);

internal static class SurfaceFamilyStitchExecutor
{
    internal static SurfaceFamilyStitchExecutionResult TryExecute(
        SurfaceFamilyStitchPlanResult plan,
        PlanarSurfaceMaterializer.PlanarPatchSetMaterializationResult planarPatches,
        SurfaceMaterializationResult? cylindricalPatch)
    {
        var diagnostics = new List<string>
        {
            "stitch-execution-started: bounded stitch executor entered.",
            "step-export-not-attempted: bounded stitch execution does not invoke STEP export.",
            "full-shell-not-claimed: bounded stitch execution does not prove shell closure in CIR-BREP-T3."
        };

        if (plan is null)
        {
            diagnostics.Add("stitch-plan-missing: no stitch plan result was supplied.");
            return Build(false, SurfaceFamilyStitchExecutionStatus.Failed, null, 0, 0, [], diagnostics);
        }

        if (!plan.Candidates.Any())
        {
            diagnostics.Add("no-stitch-candidate-no-mutation: stitch plan contains no candidates.");
            return Build(false, SurfaceFamilyStitchExecutionStatus.Deferred, null, 0, 0, [], diagnostics);
        }

        var operations = new List<AppliedStitchOperation>();
        var ready = plan.Candidates.Where(c => c.Readiness == SurfaceFamilyStitchCandidateReadiness.Ready).ToArray();
        if (ready.Length == 0)
        {
            diagnostics.Add("no-stitch-candidate-no-mutation: stitch plan has candidates, but none are readiness=Ready.");
            return Build(false, SurfaceFamilyStitchExecutionStatus.Deferred, null, 0, plan.Candidates.Count, operations, diagnostics);
        }

        var emitted = planarPatches.Entries.Where(e => e.Emitted).SelectMany(e => e.IdentityMap?.Entries ?? [])
            .Concat(cylindricalPatch?.IdentityMap?.Entries ?? [])
            .ToArray();
        var byKey = emitted.GroupBy(e => e.LocalTopologyKey, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var unsupported = false;
        var deferredCount = 0;
        var appliedCount = 0;

        foreach (var candidate in ready)
        {
            var opDiagnostics = new List<string>();
            if (candidate.Kind != SurfaceFamilyStitchCandidateKind.SharedTrimIdentity)
            {
                deferredCount++;
                opDiagnostics.Add($"candidate-deferred-unsupported-kind: {candidate.Kind}.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA?.LocalTopologyKey ?? "null", candidate.EntryB?.LocalTopologyKey ?? "null", [], SurfaceFamilyStitchExecutionStatus.Deferred, opDiagnostics));
                continue;
            }

            if (candidate.EntryA is null || candidate.EntryB is null)
            {
                deferredCount++;
                opDiagnostics.Add("candidate-deferred-missing-topology-entry: stitch candidate missing EntryA/EntryB.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA?.LocalTopologyKey ?? "null", candidate.EntryB?.LocalTopologyKey ?? "null", [], SurfaceFamilyStitchExecutionStatus.Deferred, opDiagnostics));
                continue;
            }

            if (!string.Equals(candidate.OrientationPolicy, "orientation-compatible", StringComparison.Ordinal)
                && !string.Equals(candidate.OrientationPolicy, "orientation-convention-safe", StringComparison.Ordinal))
            {
                deferredCount++;
                opDiagnostics.Add($"candidate-deferred-orientation-policy: {candidate.OrientationPolicy}.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [], SurfaceFamilyStitchExecutionStatus.Deferred, opDiagnostics));
                continue;
            }

            if (plan.AmbiguousItems.Any(a => a.Contains(candidate.Token?.OrderingKey ?? string.Empty, StringComparison.Ordinal))
                || plan.DeferredItems.Any(a => a.Contains(candidate.Token?.OrderingKey ?? string.Empty, StringComparison.Ordinal)))
            {
                deferredCount++;
                opDiagnostics.Add("candidate-deferred-ambiguous-or-missing-mate: associated token has deferred/ambiguous pairing diagnostics.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [], SurfaceFamilyStitchExecutionStatus.Deferred, opDiagnostics));
                continue;
            }

            if (!byKey.ContainsKey(candidate.EntryA.LocalTopologyKey) || !byKey.ContainsKey(candidate.EntryB.LocalTopologyKey))
            {
                deferredCount++;
                opDiagnostics.Add("candidate-deferred-missing-local-topology-key: emitted identity map does not contain one or both candidate local keys.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [], SurfaceFamilyStitchExecutionStatus.Deferred, opDiagnostics));
                continue;
            }

            var entryA = byKey[candidate.EntryA.LocalTopologyKey];
            var entryB = byKey[candidate.EntryB.LocalTopologyKey];
            if (entryA.TopologyReference is not { EdgeId: not null } aRef || entryB.TopologyReference is not { EdgeId: not null } bRef)
            {
                deferredCount++;
                opDiagnostics.Add("candidate-deferred-topology-contract-blocker: emitted identity entries are missing concrete topology references required for stitch mutation safety.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey], SurfaceFamilyStitchExecutionStatus.Deferred, opDiagnostics));
                continue;
            }

            opDiagnostics.Add("stitch-executor: candidate topology refs ready");
            opDiagnostics.Add($"stitch-executor: candidate edge refs ready a={aRef.EdgeId} b={bRef.EdgeId}");
            unsupported = true;
            deferredCount++;
            opDiagnostics.Add("candidate-deferred-mutation-not-implemented: topology refs ready but CIR-BREP-T4 does not mutate/merge topology.");
            opDiagnostics.Add("candidate-deferred-topology-remap-blocker: bounded patch bodies are independent Brep bodies and stitch remap/mutation implementation is deferred.");
            operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey], SurfaceFamilyStitchExecutionStatus.Unsupported, opDiagnostics));
        }

        diagnostics.Add($"ready-candidates-processed: {ready.Length}.");
        diagnostics.Add($"stitch-applied-count: {appliedCount}.");
        diagnostics.Add($"stitch-deferred-count: {deferredCount}.");

        if (unsupported)
        {
            diagnostics.Add("shared-edge-merge-unsupported: topology refs can be ready, but stitch mutation/remap remains deferred.");
            return Build(false, SurfaceFamilyStitchExecutionStatus.Unsupported, null, appliedCount, deferredCount, operations, diagnostics);
        }

        return Build(appliedCount > 0, appliedCount > 0 ? SurfaceFamilyStitchExecutionStatus.AssembledPartialBody : SurfaceFamilyStitchExecutionStatus.Deferred, null, appliedCount, deferredCount, operations, diagnostics);
    }

    private static SurfaceFamilyStitchExecutionResult Build(bool success, SurfaceFamilyStitchExecutionStatus status, BrepBody? body, int applied, int deferred, IReadOnlyList<AppliedStitchOperation> operations, IReadOnlyList<string> diagnostics)
        => new(success, status, body, applied, deferred, operations, diagnostics.Distinct().ToArray(), FullShellClaimed: false, StepExportAttempted: false);
}
