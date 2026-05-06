using Aetheris.Kernel.Core.Cir;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum TopologyPairingReadiness
{
    NotApplicable,
    ExactReady,
    SpecialCaseReady,
    Deferred,
    Unsupported
}

internal enum PlannedCoedgePairingKind
{
    SharedTrimCurve,
    SameSurfaceDeferred,
    UnpairedBoundary,
    Deferred,
    Unsupported,
    NotApplicable
}

internal enum LoopClosureStatus
{
    ClosedByDescriptor,
    ClosureDeferred,
    Unsupported,
    NotApplicable
}

internal sealed record PlannedEdgeUse(
    string SourceFaceKey,
    string SourceLoopKey,
    RetainedRegionLoopGroupKind SourceLoopKind,
    SurfacePatchFamily SourceSurfaceFamily,
    SurfacePatchFamily OppositeSurfaceFamily,
    TrimCurveFamily TrimCurveFamily,
    RetainedRegionLoopOrientationPolicy OrientationPolicy,
    string OrderingKey,
    TopologyPairingReadiness Readiness,
    IReadOnlyList<string> Diagnostics);

internal sealed record PlannedCoedgePairing(
    PlannedEdgeUse EdgeUseA,
    PlannedEdgeUse EdgeUseB,
    PlannedCoedgePairingKind PairingKind,
    TopologyPairingReadiness Readiness,
    string Evidence,
    string OrderingKey,
    IReadOnlyList<string> Diagnostics);

internal sealed record LoopClosureEvidence(
    string SourceFaceKey,
    string SourceLoopKey,
    LoopClosureStatus ClosureStatus,
    TopologyPairingReadiness Readiness,
    string Evidence,
    string OrderingKey,
    IReadOnlyList<string> Diagnostics);

internal sealed record TopologyPairingEvidenceResult(
    bool IsSuccess,
    TopologyPairingReadiness Readiness,
    IReadOnlyList<PlannedEdgeUse> PlannedEdgeUses,
    IReadOnlyList<PlannedCoedgePairing> PlannedCoedgePairings,
    IReadOnlyList<LoopClosureEvidence> LoopClosureEvidence,
    IReadOnlyList<string> Diagnostics,
    bool TopologyEmissionImplemented);

internal static class TopologyPairingEvidenceGenerator
{
    internal static TopologyPairingEvidenceResult Generate(CirNode root) => Generate(TopologyAssemblyDryRunPlanner.Generate(root));

    internal static TopologyPairingEvidenceResult Generate(TopologyAssemblyDryRunResult dryRun)
    {
        var diagnostics = new List<string>
        {
            "topology-emission-not-implemented: pairing evidence is dry-run only; no BRep edge/coedge/loop/face/vertex/shell entities are created."
        };

        if (!dryRun.PlannedFaces.Any() || dryRun.Readiness == TopologyAssemblyReadiness.NotApplicable)
        {
            diagnostics.Add("pairing-not-applicable: topology dry-run has no subtract planned faces for pairing/closure evidence.");
            return new(false, TopologyPairingReadiness.NotApplicable, [], [], [], diagnostics, TopologyEmissionImplemented: false);
        }

        var edgeUses = BuildEdgeUses(dryRun.PlannedFaces, diagnostics);
        var pairings = BuildPairings(edgeUses, dryRun.PlannedAdjacencies, diagnostics);
        var closure = BuildClosureEvidence(dryRun.PlannedFaces, diagnostics);
        var readiness = ReduceReadiness(edgeUses.Select(e => e.Readiness)
            .Concat(pairings.Select(p => p.Readiness))
            .Concat(closure.Select(c => c.Readiness))
            .ToArray());

        diagnostics.Add("judgment-engine-not-used: CIR-F8.8 pairing currently has deterministic single-candidate matching keyed by trim-family + complementary surface families + adjacency hints.");
        return new(edgeUses.Count > 0, readiness, edgeUses, pairings, closure, diagnostics.Distinct().ToArray(), TopologyEmissionImplemented: false);
    }

    private static List<PlannedEdgeUse> BuildEdgeUses(IReadOnlyList<PlannedFacePatch> faces, List<string> diagnostics)
    {
        var list = new List<PlannedEdgeUse>();
        foreach (var face in faces)
        foreach (var loop in face.LoopGroups)
        {
            if (!loop.LoopDescriptors.Any())
            {
                diagnostics.Add($"edge-use-deferred: face={face.OrderingKey} loop={loop.OrderingKey} has no loop descriptors.");
                continue;
            }

            foreach (var descriptor in loop.LoopDescriptors)
            {
                var readiness = MapStatus(descriptor.Status);
                var key = $"{face.OrderingKey}|{loop.OrderingKey}|{descriptor.TrimCurveFamily}|{descriptor.SourceSurfaceFamily}|{descriptor.OppositeSurfaceFamily}|{descriptor.LoopKind}";
                list.Add(new PlannedEdgeUse(face.OrderingKey, loop.OrderingKey, loop.GroupKind, descriptor.SourceSurfaceFamily, descriptor.OppositeSurfaceFamily, descriptor.TrimCurveFamily, loop.OrientationPolicy, key, readiness,
                    [$"edge-use-created: trim={descriptor.TrimCurveFamily} source={descriptor.SourceSurfaceFamily} opposite={descriptor.OppositeSurfaceFamily} orientation={loop.OrientationPolicy}.", descriptor.Diagnostic]));
            }
        }

        return list.OrderBy(e => e.OrderingKey, StringComparer.Ordinal).ToList();
    }

    private static List<PlannedCoedgePairing> BuildPairings(IReadOnlyList<PlannedEdgeUse> edgeUses, IReadOnlyList<PlannedAdjacency> adjacencies, List<string> diagnostics)
    {
        var list = new List<PlannedCoedgePairing>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < edgeUses.Count; i++)
        {
            var a = edgeUses[i];
            if (used.Contains(a.OrderingKey)) continue;

            var matches = edgeUses.Where((b, bi) => bi != i && !used.Contains(b.OrderingKey)
                && b.TrimCurveFamily == a.TrimCurveFamily
                && b.SourceSurfaceFamily == a.OppositeSurfaceFamily
                && b.OppositeSurfaceFamily == a.SourceSurfaceFamily
                && adjacencies.Any(adj => adj.TrimCurveFamily == a.TrimCurveFamily
                    && ((adj.SourceFaceA == a.SourceFaceKey && adj.SourceFaceB == b.SourceFaceKey) || (adj.SourceFaceA == b.SourceFaceKey && adj.SourceFaceB == a.SourceFaceKey))))
                .OrderBy(b => b.OrderingKey, StringComparer.Ordinal)
                .ToArray();

            if (matches.Length == 1)
            {
                var b = matches[0];
                used.Add(a.OrderingKey);
                used.Add(b.OrderingKey);
                var readiness = ReduceReadiness([a.Readiness, b.Readiness]);
                var key = $"{a.OrderingKey}<->{b.OrderingKey}";
                list.Add(new PlannedCoedgePairing(a, b, PlannedCoedgePairingKind.SharedTrimCurve, readiness,
                    "pair-ready: shared trim-family and complementary source/opposite families with adjacency hint.", key,
                    ["pair-ready: deterministic coedge pairing evidence produced."]));
            }
            else
            {
                var reason = matches.Length > 1
                    ? $"pairing-deferred-ambiguous: edge-use={a.OrderingKey} has {matches.Length} matching candidates; missing one-to-one identity/provenance key."
                    : $"pairing-deferred-missing-identity: edge-use={a.OrderingKey} has no deterministic complementary match with adjacency evidence.";
                diagnostics.Add(reason);
                var key = $"{a.OrderingKey}<->deferred";
                list.Add(new PlannedCoedgePairing(a, a, PlannedCoedgePairingKind.Deferred, TopologyPairingReadiness.Deferred, reason, key, [reason]));
                used.Add(a.OrderingKey);
            }
        }

        return list.OrderBy(p => p.OrderingKey, StringComparer.Ordinal).ToList();
    }

    private static List<LoopClosureEvidence> BuildClosureEvidence(IReadOnlyList<PlannedFacePatch> faces, List<string> diagnostics)
    {
        var list = new List<LoopClosureEvidence>();
        foreach (var face in faces)
        foreach (var loop in face.LoopGroups)
        {
            if (!loop.LoopDescriptors.Any())
            {
                var msg = $"closure-deferred-no-descriptors: face={face.OrderingKey} loop={loop.OrderingKey} has no loop descriptors.";
                diagnostics.Add(msg);
                list.Add(new LoopClosureEvidence(face.OrderingKey, loop.OrderingKey, LoopClosureStatus.ClosureDeferred, TopologyPairingReadiness.Deferred, msg,
                    $"{face.OrderingKey}|{loop.OrderingKey}|closure", [msg]));
                continue;
            }

            var families = loop.LoopDescriptors.Select(d => d.TrimCurveFamily).Distinct().ToArray();
            var hasUnsupported = loop.LoopDescriptors.Any(d => d.Status == RetainedRegionLoopStatus.Unsupported || d.TrimCurveFamily == TrimCurveFamily.Unsupported);
            var allCircleExact = loop.LoopDescriptors.All(d => d.TrimCurveFamily == TrimCurveFamily.Circle && d.Status == RetainedRegionLoopStatus.ExactReady);
            LoopClosureStatus status;
            TopologyPairingReadiness readiness;
            string evidence;
            if (hasUnsupported)
            {
                status = LoopClosureStatus.Unsupported;
                readiness = TopologyPairingReadiness.Unsupported;
                evidence = "closure-unsupported: loop contains unsupported trim family/status; exact closure cannot be claimed.";
            }
            else if (allCircleExact)
            {
                status = LoopClosureStatus.ClosedByDescriptor;
                readiness = TopologyPairingReadiness.ExactReady;
                evidence = "closure-ready-by-descriptor: exact circular trim descriptor implies closed loop boundary in dry-run evidence.";
            }
            else
            {
                status = LoopClosureStatus.ClosureDeferred;
                readiness = TopologyPairingReadiness.Deferred;
                evidence = $"closure-deferred: trim families={string.Join(',', families)} need explicit parameter/order solving beyond descriptor-only dry-run.";
            }

            list.Add(new LoopClosureEvidence(face.OrderingKey, loop.OrderingKey, status, readiness, evidence,
                $"{face.OrderingKey}|{loop.OrderingKey}|closure", [evidence]));
        }

        return list.OrderBy(c => c.OrderingKey, StringComparer.Ordinal).ToList();
    }

    private static TopologyPairingReadiness MapStatus(RetainedRegionLoopStatus status) => status switch
    {
        RetainedRegionLoopStatus.ExactReady => TopologyPairingReadiness.ExactReady,
        RetainedRegionLoopStatus.SpecialCaseReady => TopologyPairingReadiness.SpecialCaseReady,
        RetainedRegionLoopStatus.Deferred => TopologyPairingReadiness.Deferred,
        RetainedRegionLoopStatus.Unsupported => TopologyPairingReadiness.Unsupported,
        _ => TopologyPairingReadiness.NotApplicable
    };

    private static TopologyPairingReadiness ReduceReadiness(IReadOnlyList<TopologyPairingReadiness> readiness)
    {
        if (readiness.Count == 0) return TopologyPairingReadiness.NotApplicable;
        if (readiness.Any(r => r == TopologyPairingReadiness.Unsupported)) return TopologyPairingReadiness.Unsupported;
        if (readiness.Any(r => r == TopologyPairingReadiness.Deferred)) return TopologyPairingReadiness.Deferred;
        if (readiness.Any(r => r == TopologyPairingReadiness.SpecialCaseReady)) return TopologyPairingReadiness.SpecialCaseReady;
        if (readiness.All(r => r == TopologyPairingReadiness.ExactReady)) return TopologyPairingReadiness.ExactReady;
        return TopologyPairingReadiness.NotApplicable;
    }
}
