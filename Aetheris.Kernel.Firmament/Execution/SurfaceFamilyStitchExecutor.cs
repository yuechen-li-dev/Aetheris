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
            "full-shell-not-claimed: bounded stitch execution does not prove shell closure in CIR-BREP-T5."
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
        var ready = plan.Candidates.Where(c => c.Readiness == SurfaceFamilyStitchCandidateReadiness.Ready).OrderBy(c => c.OrderingKey, StringComparer.Ordinal).ToArray();
        if (ready.Length == 0)
        {
            diagnostics.Add("no-stitch-candidate-no-mutation: stitch plan has candidates, but none are readiness=Ready.");
            return Build(false, SurfaceFamilyStitchExecutionStatus.Deferred, null, 0, plan.Candidates.Count, operations, diagnostics);
        }

        var emitted = planarPatches.Entries.Where(e => e.Emitted).SelectMany(e => e.IdentityMap?.Entries ?? [])
            .Concat(cylindricalPatch?.IdentityMap?.Entries ?? [])
            .ToArray();
        var byKey = emitted.GroupBy(e => e.LocalTopologyKey, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var deferredCount = 0;
        var appliedCount = 0;
        var attemptedMutation = false;

        foreach (var candidate in ready)
        {
            var opDiagnostics = new List<string>();
            if (candidate.Kind != SurfaceFamilyStitchCandidateKind.SharedTrimIdentity || candidate.EntryA is null || candidate.EntryB is null)
            {
                deferredCount++;
                opDiagnostics.Add("candidate-deferred-shared-trim-identity-required: candidate kind and entries must be SharedTrimIdentity with both entries present.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA?.LocalTopologyKey ?? "null", candidate.EntryB?.LocalTopologyKey ?? "null", [], SurfaceFamilyStitchExecutionStatus.Deferred, opDiagnostics));
                continue;
            }

            if (!string.Equals(candidate.EntryA.TrimIdentityToken?.OrderingKey, candidate.EntryB.TrimIdentityToken?.OrderingKey, StringComparison.Ordinal))
            {
                deferredCount++;
                opDiagnostics.Add("candidate-deferred-token-mismatch: entry trim identity tokens do not match.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [], SurfaceFamilyStitchExecutionStatus.Deferred, opDiagnostics));
                continue;
            }

            var roles = new[] { candidate.EntryA.Role, candidate.EntryB.Role };
            var hasCompatibleRoles = roles.Contains(EmittedTopologyRole.InnerCircularTrim)
                && (roles.Contains(EmittedTopologyRole.CylindricalTopBoundary) || roles.Contains(EmittedTopologyRole.CylindricalBottomBoundary));
            if (!hasCompatibleRoles)
            {
                deferredCount++;
                opDiagnostics.Add("candidate-deferred-incompatible-roles: expected InnerCircularTrim paired with CylindricalTopBoundary or CylindricalBottomBoundary.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [], SurfaceFamilyStitchExecutionStatus.Deferred, opDiagnostics));
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

            if (!byKey.ContainsKey(candidate.EntryA.LocalTopologyKey) || !byKey.ContainsKey(candidate.EntryB.LocalTopologyKey))
            {
                deferredCount++;
                opDiagnostics.Add("candidate-deferred-missing-local-topology-key: emitted identity map does not contain one or both candidate local keys.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [], SurfaceFamilyStitchExecutionStatus.Deferred, opDiagnostics));
                continue;
            }

            var entryA = byKey[candidate.EntryA.LocalTopologyKey];
            var entryB = byKey[candidate.EntryB.LocalTopologyKey];
            if (entryA.TopologyReference is not { FaceId: not null, LoopId: not null, EdgeId: not null, CoedgeId: not null } aRef
                || entryB.TopologyReference is not { FaceId: not null, LoopId: not null, EdgeId: not null, CoedgeId: not null } bRef)
            {
                deferredCount++;
                opDiagnostics.Add("candidate-deferred-topology-contract-blocker: concrete face/loop/edge/coedge refs are required for mutation.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey], SurfaceFamilyStitchExecutionStatus.Deferred, opDiagnostics));
                continue;
            }

            attemptedMutation = true;
            opDiagnostics.Add("ready-candidate-selected: bounded T5 picked deterministic first ready candidate.");
            opDiagnostics.Add($"topology-refs-resolved: a(face={aRef.FaceId},loop={aRef.LoopId},edge={aRef.EdgeId},coedge={aRef.CoedgeId}) b(face={bRef.FaceId},loop={bRef.LoopId},edge={bRef.EdgeId},coedge={bRef.CoedgeId}).");
            opDiagnostics.Add("body-remap-started: evaluating whether emitted patch bodies can be safely copied/remapped into one combined body.");
            var remapInputs = planarPatches.Entries.Where(e => e.Emission is { Success: true, Body: not null }).Select(e => e.Emission!).ToList();
            if (cylindricalPatch is { Success: true, Body: not null }) remapInputs.Add(cylindricalPatch);
            var remapMaps = planarPatches.Entries.Where(e => e.IdentityMap is not null).Select(e => e.IdentityMap!).Concat(cylindricalPatch?.IdentityMap is null ? [] : [cylindricalPatch.IdentityMap]).ToArray();
            var remap = CombinedPatchBodyRemapper.TryCombine(remapInputs, remapMaps);
            if (remap.Success)
            {
                opDiagnostics.Add("stitch-executor: combined-body-remap-ready");
                opDiagnostics.Add("stitch-executor: shared-edge-mutation-not-implemented");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [aRef.EdgeId, bRef.EdgeId, aRef.CoedgeId, bRef.CoedgeId], SurfaceFamilyStitchExecutionStatus.Unsupported, opDiagnostics));
                deferredCount++;
                break;
            }

            opDiagnostics.AddRange(remap.Diagnostics);
            opDiagnostics.Add("mutation-unsupported-topology-model: combined-body remap not ready; shared-edge mutation remains blocked.");
            operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [aRef.EdgeId, bRef.EdgeId, aRef.CoedgeId, bRef.CoedgeId], SurfaceFamilyStitchExecutionStatus.Unsupported, opDiagnostics));
            deferredCount++;
            break;
        }

        diagnostics.Add($"ready-candidates-processed: {ready.Length}.");
        diagnostics.Add($"stitch-applied-count: {appliedCount}.");
        diagnostics.Add($"stitch-deferred-count: {deferredCount}.");
        if (ready.Length > 1) diagnostics.Add("bounded-scope-single-candidate: CIR-BREP-T5 processes at most one ready candidate deterministically.");

        if (attemptedMutation)
        {
            diagnostics.Add("shared-edge-remap-not-applied: topology mutation blocked by missing safe remap capability for emitted-body merge.");
            return Build(false, SurfaceFamilyStitchExecutionStatus.Unsupported, null, appliedCount, deferredCount, operations, diagnostics);
        }

        return Build(false, SurfaceFamilyStitchExecutionStatus.Deferred, null, appliedCount, deferredCount, operations, diagnostics);
    }

    private static SurfaceFamilyStitchExecutionResult Build(bool success, SurfaceFamilyStitchExecutionStatus status, BrepBody? body, int applied, int deferred, IReadOnlyList<AppliedStitchOperation> operations, IReadOnlyList<string> diagnostics)
        => new(success, status, body, applied, deferred, operations, diagnostics.Distinct().ToArray(), FullShellClaimed: false, StepExportAttempted: false);
}
