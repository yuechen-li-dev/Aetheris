using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum DuplicateEdgeCleanupStatus
{
    CleanupCandidate,
    AlreadyClean,
    Deferred,
    Unsupported,
    NotRelatedToStitch
}

internal enum BoundaryEdgeClassificationKind
{
    ExpectedOpenBoundary,
    StitchDuplicateUnreferenced,
    ShellClosureBlocker,
    Ambiguous
}

internal sealed record DuplicateEdgeCleanupCandidate(
    string Token,
    string CanonicalEdgeId,
    string DuplicateEdgeId,
    string RewrittenCoedgeId,
    string EvidenceSource,
    bool SafeToRemoveLater,
    DuplicateEdgeCleanupStatus Status,
    IReadOnlyList<string> Diagnostics);

internal sealed record BoundaryEdgeClassification(
    string EdgeId,
    int CoedgeUseCount,
    BoundaryEdgeClassificationKind Classification,
    string? RelatedToken,
    IReadOnlyList<string> Diagnostics);

internal sealed record DuplicateEdgeCleanupPlanResult(
    bool Success,
    IReadOnlyList<DuplicateEdgeCleanupCandidate> Candidates,
    IReadOnlyList<BoundaryEdgeClassification> BoundaryClassifications,
    IReadOnlyList<string> Diagnostics,
    bool CleanupMutationImplemented);

internal static class DuplicateEdgeCleanupPlanner
{
    internal static DuplicateEdgeCleanupPlanResult Plan(SurfaceFamilyStitchExecutionResult execution, BrepBody? body)
    {
        var diagnostics = new List<string>
        {
            "duplicate-edge-cleanup-planning-started: evidence-only duplicate cleanup analysis after shared-edge rewrite.",
            "no-coordinate-coincidence-policy: duplicate classification uses stitch operation/remap identity evidence only."
        };

        if (execution is null || body is null)
        {
            diagnostics.Add("duplicate-edge-cleanup-blocked-missing-input: stitch execution result and body are both required.");
            return new(false, [], [], diagnostics, CleanupMutationImplemented: false);
        }

        var edgeUse = body.Topology.Edges.ToDictionary(e => e.Id, e => body.Topology.Coedges.Count(c => c.EdgeId == e.Id));
        var stitchDuplicates = new Dictionary<string, (EdgeId edgeId, string token, string canonicalEdge, string rewrittenCoedge)>(StringComparer.Ordinal);
        var candidates = new List<DuplicateEdgeCleanupCandidate>();

        foreach (var op in execution.Operations.OrderBy(o => o.CandidateId, StringComparer.Ordinal))
        {
            if (op.Status != SurfaceFamilyStitchExecutionStatus.AssembledPartialBody)
            {
                continue;
            }

            if (op.EdgeOrCoedgeIds.Count < 4)
            {
                diagnostics.Add($"duplicate-edge-cleanup-blocked-missing-operation-metadata:candidate={op.CandidateId}; expected canonical/duplicate/rewritten coedge ids from T7.");
                continue;
            }

            var canonicalToken = op.EdgeOrCoedgeIds[0];
            var duplicateToken = op.EdgeOrCoedgeIds[1];
            var rewrittenCoedgeToken = op.EdgeOrCoedgeIds[2];
            var canonicalEdge = ParseId<EdgeId>(canonicalToken, static i => new EdgeId(i));
            var duplicateEdge = ParseId<EdgeId>(duplicateToken, static i => new EdgeId(i));
            if (canonicalEdge is null || duplicateEdge is null)
            {
                diagnostics.Add($"duplicate-edge-cleanup-blocked-unparseable-operation-ids:candidate={op.CandidateId};canonical={canonicalToken};duplicate={duplicateToken}.");
                continue;
            }

            var duplicateEdgeIdText = duplicateEdge.Value.ToString()!;
            stitchDuplicates[duplicateEdgeIdText] = (duplicateEdge.Value, op.Token, canonicalToken, rewrittenCoedgeToken);
            diagnostics.Add($"applied-stitch-recorded-canonical-duplicate-edge:candidate={op.CandidateId};token={op.Token};canonical={canonicalToken};duplicate={duplicateToken};rewritten={rewrittenCoedgeToken}.");

            if (!edgeUse.TryGetValue(duplicateEdge.Value, out var uses))
            {
                diagnostics.Add($"duplicate-edge-cleanup-blocked-duplicate-edge-missing: duplicate edge {duplicateToken} not present in topology.");
                continue;
            }

            if (uses == 0)
            {
                candidates.Add(new DuplicateEdgeCleanupCandidate(op.Token, canonicalToken, duplicateToken, rewrittenCoedgeToken, "applied-stitch-operation", true, DuplicateEdgeCleanupStatus.CleanupCandidate,
                    ["duplicate-edge-has-zero-coedges-after-rewrite: safe candidate for future bounded removal.", "duplicate-edge-cleanup: candidate identified; mutation deferred."]));
                diagnostics.Add($"duplicate-edge-cleanup-candidate-identified: duplicate={duplicateToken};canonical={canonicalToken};token={op.Token}.");
            }
            else
            {
                candidates.Add(new DuplicateEdgeCleanupCandidate(op.Token, canonicalToken, duplicateToken, rewrittenCoedgeToken, "applied-stitch-operation", false, DuplicateEdgeCleanupStatus.Deferred,
                    [$"duplicate-edge-still-referenced: duplicate edge has {uses} coedge use(s); removal deferred."]));
                diagnostics.Add($"duplicate-edge-cleanup-deferred-still-referenced: duplicate={duplicateToken};uses={uses}.");
            }
        }

        if (execution.Operations.Any(o => o.Status == SurfaceFamilyStitchExecutionStatus.AssembledPartialBody) && stitchDuplicates.Count == 0)
        {
            diagnostics.Add("duplicate-edge-cleanup-blocked-no-canonical-duplicate-evidence: applied stitch operation did not yield parseable duplicate edge metadata.");
        }

        var boundaries = new List<BoundaryEdgeClassification>();
        foreach (var edge in body.Topology.Edges.OrderBy(e => e.Id.Value))
        {
            var idText = edge.Id.ToString()!;
            var uses = edgeUse[edge.Id];
            if (uses == 0)
            {
                if (stitchDuplicates.TryGetValue(idText, out var stitch))
                {
                    boundaries.Add(new(idText, uses, BoundaryEdgeClassificationKind.StitchDuplicateUnreferenced, stitch.token,
                        ["zero-coedge edge tied to applied stitch duplicate; expected unreferenced duplicate after T7 rewrite."]));
                }
                else
                {
                    boundaries.Add(new(idText, uses, BoundaryEdgeClassificationKind.Ambiguous, null,
                        ["zero-coedge edge not related to applied stitch duplicate evidence."]));
                    diagnostics.Add($"edge-not-related-to-stitch-zero-coedge:{idText}.");
                }
            }
            else if (uses == 1)
            {
                boundaries.Add(new(idText, uses, BoundaryEdgeClassificationKind.ShellClosureBlocker, stitchDuplicates.TryGetValue(idText, out var stitch) ? stitch.token : null,
                    ["one-coedge edge remains shell-closure blocker in bounded X1 planning."]));
            }
            else if (uses == 2)
            {
                boundaries.Add(new(idText, uses, BoundaryEdgeClassificationKind.ExpectedOpenBoundary, null,
                    ["two-coedge edge is not a shell-closure blocker under bounded edge-use policy."]));
            }
            else
            {
                boundaries.Add(new(idText, uses, BoundaryEdgeClassificationKind.Ambiguous, null,
                    ["edge has more than two coedges; bounded cleanup planner marks ambiguous/invalid."]));
                diagnostics.Add($"edge-use-ambiguous-more-than-two-coedges:{idText};uses={uses}.");
            }
        }

        diagnostics.Add($"duplicate-edge-cleanup-candidate-count:{candidates.Count(c => c.Status == DuplicateEdgeCleanupStatus.CleanupCandidate)}.");
        diagnostics.Add($"boundary-shell-blocker-count:{boundaries.Count(b => b.Classification == BoundaryEdgeClassificationKind.ShellClosureBlocker)}.");
        diagnostics.Add("cleanup-mutation-not-implemented: planner is metadata-only in CIR-BREP-X1.");

        return new(true, candidates, boundaries, diagnostics.Distinct().ToArray(), CleanupMutationImplemented: false);
    }

    private static TId? ParseId<TId>(string token, Func<int, TId> factory) where TId : struct
    {
        var parts = token.Split(':', StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[1], out var numeric) ? factory(numeric) : null;
    }
}
