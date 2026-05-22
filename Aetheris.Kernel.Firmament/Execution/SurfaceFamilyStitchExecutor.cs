using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Topology;

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
            "full-shell-not-claimed: bounded stitch execution does not prove shell closure in CIR-BREP-T7.",
            "vertex-merge-deferred: CIR-BREP-T7 rewrites coedge edge references only."
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

            var remapInputs = planarPatches.Entries.Where(e => e.Emission is { Success: true, Body: not null }).Select(e => e.Emission!).ToList();
            if (cylindricalPatch is { Success: true, Body: not null }) remapInputs.Add(cylindricalPatch);
            var remapMaps = planarPatches.Entries.Where(e => e.IdentityMap is not null).Select(e => e.IdentityMap!).Concat(cylindricalPatch?.IdentityMap is null ? [] : [cylindricalPatch.IdentityMap]).ToArray();
            var remap = CombinedPatchBodyRemapper.TryCombine(remapInputs, remapMaps);
            if (!remap.Success)
            {
                deferredCount++;
                opDiagnostics.AddRange(remap.Diagnostics);
                opDiagnostics.Add("mutation-unsupported-topology-model: combined-body remap not ready; shared-edge mutation remains blocked.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [], SurfaceFamilyStitchExecutionStatus.Unsupported, opDiagnostics));
                break;
            }

            var remapByKey = remap.RemappedIdentityMaps.SelectMany(m => m.Entries).GroupBy(e => e.LocalTopologyKey, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            if (!remapByKey.TryGetValue(candidate.EntryA.LocalTopologyKey, out var remappedA)
                || !remapByKey.TryGetValue(candidate.EntryB.LocalTopologyKey, out var remappedB))
            {
                deferredCount++;
                opDiagnostics.Add("candidate-deferred-remapped-refs-missing: combined body remap succeeded but one or both candidate entries have no remapped identity entry.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [], SurfaceFamilyStitchExecutionStatus.Deferred, opDiagnostics));
                break;
            }

            if (remappedA.TopologyReference is not { EdgeId: not null, CoedgeId: not null } aRef
                || remappedB.TopologyReference is not { EdgeId: not null, CoedgeId: not null } bRef)
            {
                deferredCount++;
                opDiagnostics.Add("candidate-deferred-topology-contract-blocker: remapped edge/coedge refs are required for mutation.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [], SurfaceFamilyStitchExecutionStatus.Deferred, opDiagnostics));
                break;
            }

            var aEdgeId = ParseId<EdgeId>(aRef.EdgeId, static n => new EdgeId(n));
            var bEdgeId = ParseId<EdgeId>(bRef.EdgeId, static n => new EdgeId(n));
            var aCoedgeId = ParseId<CoedgeId>(aRef.CoedgeId, static n => new CoedgeId(n));
            var bCoedgeId = ParseId<CoedgeId>(bRef.CoedgeId, static n => new CoedgeId(n));
            if (aEdgeId is null || bEdgeId is null || aCoedgeId is null || bCoedgeId is null)
            {
                deferredCount++;
                opDiagnostics.Add("candidate-deferred-id-parse-failed: remapped edge/coedge ids are not parseable.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [aRef.EdgeId!, bRef.EdgeId!, aRef.CoedgeId!, bRef.CoedgeId!], SurfaceFamilyStitchExecutionStatus.Deferred, opDiagnostics));
                break;
            }

            var combined = remap.CombinedBody!;
            if (!combined.Topology.TryGetCoedge(aCoedgeId.Value, out _) || !combined.Topology.TryGetCoedge(bCoedgeId.Value, out _)
                || !combined.Topology.TryGetEdge(aEdgeId.Value, out _) || !combined.Topology.TryGetEdge(bEdgeId.Value, out _))
            {
                deferredCount++;
                opDiagnostics.Add("candidate-deferred-remapped-topology-missing: remapped coedge/edge ids do not resolve in combined body topology.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [aRef.EdgeId!, bRef.EdgeId!, aRef.CoedgeId!, bRef.CoedgeId!], SurfaceFamilyStitchExecutionStatus.Deferred, opDiagnostics));
                break;
            }

            var canonicalEdge = aEdgeId.Value;
            var duplicateEdge = bEdgeId.Value;
            var rewritten = RewriteCoedgesWithCanonicalEdge(combined, aCoedgeId.Value, bCoedgeId.Value, canonicalEdge);
            if (!rewritten.success)
            {
                deferredCount++;
                opDiagnostics.Add(rewritten.diagnostic);
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [aRef.EdgeId!, bRef.EdgeId!, aRef.CoedgeId!, bRef.CoedgeId!], SurfaceFamilyStitchExecutionStatus.Unsupported, opDiagnostics));
                break;
            }

            var validation = ValidateTopologyConsistency(rewritten.body!);
            if (!validation.success)
            {
                deferredCount++;
                opDiagnostics.AddRange(validation.diagnostics);
                opDiagnostics.Add("invariant-validation-failed: stitched topology failed bounded consistency checks.");
                operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [canonicalEdge.ToString(), duplicateEdge.ToString(), aCoedgeId.ToString()!, bCoedgeId.ToString()!], SurfaceFamilyStitchExecutionStatus.Failed, opDiagnostics));
                break;
            }

            opDiagnostics.Add("ready-candidate-selected: bounded T7 picked deterministic first ready candidate.");
            opDiagnostics.Add($"canonical-edge-selected: canonical={canonicalEdge} duplicate={duplicateEdge} rewritten-coedges={aCoedgeId},{bCoedgeId}.");
            opDiagnostics.Add("shared-edge-rewrite-applied: two boundary coedges now reference one canonical edge in combined body.");
            opDiagnostics.Add("duplicate-edge-retained-unreferenced: duplicate edge is intentionally retained for bounded T7; no coedge references it after rewrite.");
            opDiagnostics.Add("geometry-binding-policy: canonical edge binding retained; duplicate edge binding retained for deferred cleanup.");
            opDiagnostics.Add("invariant-validation-passed: coedge/loop/face/binding references resolve after rewrite.");
            opDiagnostics.Add("full-shell-not-claimed: bounded shared-edge rewrite does not claim closed-shell completion.");
            opDiagnostics.Add("step-export-not-attempted: bounded shared-edge rewrite does not invoke STEP export.");

            operations.Add(new AppliedStitchOperation(candidate.CandidateId, candidate.Token?.OrderingKey ?? "null", candidate.EntryA.LocalTopologyKey, candidate.EntryB.LocalTopologyKey, [canonicalEdge.ToString(), duplicateEdge.ToString(), aCoedgeId.ToString()!, bCoedgeId.ToString()!], SurfaceFamilyStitchExecutionStatus.AssembledPartialBody, opDiagnostics));
            appliedCount = 1;
            diagnostics.Add("bounded-scope-single-candidate: CIR-BREP-T7 applies at most one deterministic ready candidate.");
            diagnostics.Add("shared-edge-rewrite-applied: one bounded coedge edge-reference rewrite completed.");
            return Build(true, SurfaceFamilyStitchExecutionStatus.AssembledPartialBody, rewritten.body, appliedCount, deferredCount, operations, diagnostics);
        }

        diagnostics.Add($"ready-candidates-processed: {ready.Length}.");
        diagnostics.Add($"stitch-applied-count: {appliedCount}.");
        diagnostics.Add($"stitch-deferred-count: {deferredCount}.");
        if (ready.Length > 1) diagnostics.Add("bounded-scope-single-candidate: CIR-BREP-T7 processes at most one ready candidate deterministically.");
        return Build(false, SurfaceFamilyStitchExecutionStatus.Deferred, null, appliedCount, deferredCount, operations, diagnostics);
    }

    private static (bool success, BrepBody? body, string diagnostic) RewriteCoedgesWithCanonicalEdge(BrepBody source, CoedgeId aCoedgeId, CoedgeId bCoedgeId, EdgeId canonicalEdge)
    {
        var topology = new TopologyModel();
        foreach (var v in source.Topology.Vertices.OrderBy(v => v.Id.Value)) topology.AddVertex(v);
        foreach (var e in source.Topology.Edges.OrderBy(e => e.Id.Value)) topology.AddEdge(e);
        foreach (var c in source.Topology.Coedges.OrderBy(c => c.Id.Value))
        {
            if (c.Id == aCoedgeId || c.Id == bCoedgeId) topology.AddCoedge(c with { EdgeId = canonicalEdge });
            else topology.AddCoedge(c);
        }
        foreach (var l in source.Topology.Loops.OrderBy(l => l.Id.Value)) topology.AddLoop(l);
        foreach (var f in source.Topology.Faces.OrderBy(f => f.Id.Value)) topology.AddFace(f);
        foreach (var s in source.Topology.Shells.OrderBy(s => s.Id.Value)) topology.AddShell(s);
        foreach (var b in source.Topology.Bodies.OrderBy(b => b.Id.Value)) topology.AddBody(b);
        return (true, new BrepBody(topology, source.Geometry, source.Bindings), string.Empty);
    }

    private static (bool success, IReadOnlyList<string> diagnostics) ValidateTopologyConsistency(BrepBody body)
    {
        var diagnostics = new List<string>();
        foreach (var c in body.Topology.Coedges)
            if (!body.Topology.TryGetEdge(c.EdgeId, out _)) diagnostics.Add($"missing-edge-for-coedge:{c.Id}->{c.EdgeId}");
        foreach (var l in body.Topology.Loops)
            foreach (var cid in l.CoedgeIds)
                if (!body.Topology.TryGetCoedge(cid, out _)) diagnostics.Add($"missing-coedge-for-loop:{l.Id}->{cid}");
        foreach (var f in body.Topology.Faces)
            foreach (var lid in f.LoopIds)
                if (!body.Topology.TryGetLoop(lid, out _)) diagnostics.Add($"missing-loop-for-face:{f.Id}->{lid}");
        foreach (var binding in body.Bindings.EdgeBindings)
            if (!body.Topology.TryGetEdge(binding.EdgeId, out _)) diagnostics.Add($"missing-edge-for-binding:{binding.EdgeId}");
        foreach (var binding in body.Bindings.FaceBindings)
            if (!body.Topology.TryGetFace(binding.FaceId, out _)) diagnostics.Add($"missing-face-for-binding:{binding.FaceId}");
        return (diagnostics.Count == 0, diagnostics);
    }

    private static TId? ParseId<TId>(string token, Func<int, TId> factory) where TId : struct
    {
        var parts = token.Split(':', StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[1], out var numeric) ? factory(numeric) : null;
    }

    private static SurfaceFamilyStitchExecutionResult Build(bool success, SurfaceFamilyStitchExecutionStatus status, BrepBody? body, int applied, int deferred, IReadOnlyList<AppliedStitchOperation> operations, IReadOnlyList<string> diagnostics)
        => new(success, status, body, applied, deferred, operations, diagnostics.Distinct().ToArray(), FullShellClaimed: false, StepExportAttempted: false);
}
