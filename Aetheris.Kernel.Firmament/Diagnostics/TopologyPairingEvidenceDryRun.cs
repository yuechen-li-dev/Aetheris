using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Diagnostics;

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
    SharedTrimIdentity,
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

internal readonly record struct InternalTrimIdentityToken(
    string OperationKey,
    string SurfaceAKey,
    string SurfaceBKey,
    TrimCurveFamily TrimFamily,
    string InteractionRole,
    string OrderingKey);

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
    InternalTrimIdentityToken? IdentityToken,
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

        diagnostics.Add("judgment-engine-not-used: CIR-F8.9 token pairing is a deterministic identity-group map/reduce; no competing bounded strategies required.");
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
                var token = BuildIdentityToken(face, loop, descriptor, out var tokenDiagnostic);
                list.Add(new PlannedEdgeUse(face.OrderingKey, loop.OrderingKey, loop.GroupKind, descriptor.SourceSurfaceFamily, descriptor.OppositeSurfaceFamily, descriptor.TrimCurveFamily, loop.OrientationPolicy, key, readiness, token,
                    [$"edge-use-created: trim={descriptor.TrimCurveFamily} source={descriptor.SourceSurfaceFamily} opposite={descriptor.OppositeSurfaceFamily} orientation={loop.OrientationPolicy}.", descriptor.Diagnostic, tokenDiagnostic]));
            }
        }

        return list.OrderBy(e => e.OrderingKey, StringComparer.Ordinal).ToList();
    }

    private static List<PlannedCoedgePairing> BuildPairings(IReadOnlyList<PlannedEdgeUse> edgeUses, IReadOnlyList<PlannedAdjacency> adjacencies, List<string> diagnostics)
    {
        var list = new List<PlannedCoedgePairing>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var tokenGroups = edgeUses.Where(e => e.IdentityToken is not null)
            .GroupBy(e => e.IdentityToken!.Value.OrderingKey, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in tokenGroups)
        {
            var matches = group.OrderBy(e => e.OrderingKey, StringComparer.Ordinal).ToArray();
            if (matches.Length > 2)
            {
                var reason = $"pairing-deferred-ambiguous-token: token={group.Key} has multiplicity={matches.Length}; arbitrary coedge pairing rejected.";
                diagnostics.Add(reason);
                foreach (var m in matches)
                {
                    used.Add(m.OrderingKey);
                    list.Add(new PlannedCoedgePairing(m, m, PlannedCoedgePairingKind.Deferred, TopologyPairingReadiness.Deferred, reason, $"{m.OrderingKey}<->deferred", [reason]));
                }
            }
            else if (matches.Length == 2)
            {
                var a = matches[0];
                var b = matches[1];
                used.Add(a.OrderingKey);
                used.Add(b.OrderingKey);
                var readiness = a.Readiness == TopologyPairingReadiness.ExactReady && b.Readiness == TopologyPairingReadiness.ExactReady
                    ? TopologyPairingReadiness.ExactReady
                    : TopologyPairingReadiness.SpecialCaseReady;
                var evidence = $"pair-ready-token-match: matching internal trim identity token {group.Key}.";
                diagnostics.Add(evidence);
                list.Add(new PlannedCoedgePairing(a, b, PlannedCoedgePairingKind.SharedTrimIdentity, readiness, evidence, $"{a.OrderingKey}<->{b.OrderingKey}", [evidence]));
            }
        }

        foreach (var a in edgeUses.Where(e => !used.Contains(e.OrderingKey)))
        {
            var reason = a.IdentityToken is null
                ? $"pairing-deferred-missing-identity: edge-use={a.OrderingKey} has no internal trim identity token."
                : $"pairing-token-mismatch: edge-use={a.OrderingKey} has internal trim identity token but no matching pair token.";
            diagnostics.Add(reason);
            list.Add(new PlannedCoedgePairing(a, a, PlannedCoedgePairingKind.Deferred, TopologyPairingReadiness.Deferred, reason, $"{a.OrderingKey}<->deferred", [reason]));
        }

        return list.OrderBy(p => p.OrderingKey, StringComparer.Ordinal).ToList();
    }

    private static InternalTrimIdentityToken? BuildIdentityToken(PlannedFacePatch face, PlannedLoop loop, RetainedRegionLoopDescriptor descriptor, out string diagnostic)
    {
        if (descriptor.TrimCapability is TrimCapabilityClassification.Deferred or TrimCapabilityClassification.Unsupported
            || descriptor.Status is RetainedRegionLoopStatus.Deferred or RetainedRegionLoopStatus.Unsupported)
        {
            diagnostic = "identity-token-missing-deferred-trim: trim capability/status is deferred or unsupported.";
            return null;
        }

        if (descriptor.TrimCurveFamily is TrimCurveFamily.Unsupported or TrimCurveFamily.AlgebraicImplicit)
        {
            diagnostic = "identity-token-missing-trim-family: trim curve family is deferred algebraic/unsupported.";
            return null;
        }

        var provenance = face.SourceCandidate.SourceSurface.Provenance;
        if (string.IsNullOrWhiteSpace(provenance))
        {
            diagnostic = "identity-token-missing-provenance: source surface provenance unavailable.";
            return null;
        }

        var surfaceA = $"{descriptor.SourceSurfaceFamily}:{provenance}";
        var surfaceB = $"{descriptor.OppositeSurfaceFamily}:{descriptor.LoopKind}";
        var canonical = new[] { surfaceA, surfaceB }.OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var operationKey = face.SourceCandidate.SourceSurface.ReplayOpIndex?.ToString() ?? "op:unknown";
        var interactionRole = face.SourceCandidate.RetentionRole.ToString();
        var orderingKey = $"{operationKey}|{canonical[0]}|{canonical[1]}|{descriptor.TrimCurveFamily}|{interactionRole}|{loop.GroupKind}";
        diagnostic = $"identity-token-created: {orderingKey}";
        return new InternalTrimIdentityToken(operationKey, canonical[0], canonical[1], descriptor.TrimCurveFamily, interactionRole, orderingKey);
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
