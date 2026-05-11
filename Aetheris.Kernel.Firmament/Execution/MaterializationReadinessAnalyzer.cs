using Aetheris.Kernel.Core.Cir;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum EmissionReadiness
{
    NotApplicable,
    EvidenceReadyForEmission,
    SpecialCaseReady,
    Deferred,
    Unsupported
}

internal enum EmissionBlockingReason
{
    None,
    SourceSurfaceExtraction,
    TrimCapability,
    RetentionClassification,
    LoopScaffolding,
    LoopGrouping,
    TopologyPlanning,
    LoopClosure,
    CoedgePairing,
    Adjacency,
    UnsupportedSurfaceFamily,
    NonSubtractNotApplicable,
    TopologyEmissionNotImplemented
}

internal sealed record ReadinessLayerSummary(
    string LayerName,
    EmissionReadiness Readiness,
    IReadOnlyList<EmissionBlockingReason> BlockingReasons,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyDictionary<string, int> Counts);

internal sealed record MaterializationReadinessReport(
    bool Success,
    EmissionReadiness OverallReadiness,
    IReadOnlyList<EmissionBlockingReason> BlockingReasons,
    IReadOnlyList<ReadinessLayerSummary> LayerSummaries,
    int SourceSurfaceCount,
    int CandidateCount,
    int PlannedFaceCount,
    int PlannedLoopCount,
    int PlannedEdgeUseCount,
    int CoedgePairingCount,
    int ClosureEvidenceCount,
    IReadOnlyList<string> Diagnostics,
    bool TopologyEmissionImplemented);

internal static class MaterializationReadinessAnalyzer
{
    internal static MaterializationReadinessReport Analyze(CirNode root, NativeGeometryReplayLog? replayLog = null)
    {
        var source = SourceSurfaceExtractor.Extract(root, replayLog);
        var nonSubtract = root is not CirSubtractNode;

        var candidates = FacePatchCandidateGenerator.Generate(root, replayLog);
        var topology = TopologyAssemblyDryRunPlanner.Generate(candidates);
        var pairing = TopologyPairingEvidenceGenerator.Generate(topology);

        var layers = new List<ReadinessLayerSummary>
        {
            SummarizeSource(source),
            SummarizeCandidates(candidates),
            SummarizeTrimOracle(candidates),
            SummarizeTopology(topology),
            SummarizePairing(pairing)
        };

        var overall = nonSubtract ? EmissionReadiness.NotApplicable : Reduce(layers.Select(l => l.Readiness));
        var blocking = layers.SelectMany(l => l.BlockingReasons)
            .Where(r => r != EmissionBlockingReason.None)
            .Distinct()
            .OrderBy(r => r)
            .ToList();
        if (nonSubtract && !blocking.Contains(EmissionBlockingReason.NonSubtractNotApplicable))
        {
            blocking.Add(EmissionBlockingReason.NonSubtractNotApplicable);
        }
        var blockingReasons = blocking.Distinct().OrderBy(r => r).ToArray();

        var diagnostics = new List<string>
        {
            $"source-surface: extracted {source.Descriptors.Count} surfaces",
            $"face-patch: generated {candidates.Candidates.Count} candidates",
            $"topology-dry-run: planned {topology.PlannedFaces.Count} faces and {topology.PlannedFaces.SelectMany(f => f.LoopGroups).Count()} loops",
            $"pairing-evidence: edge-uses={pairing.PlannedEdgeUses.Count} coedge-pairings={pairing.PlannedCoedgePairings.Count} closure={pairing.LoopClosureEvidence.Count}",
            "topology-emission: not implemented",
            "judgment-engine-not-used: readiness gate is a conservative deterministic reduction with no competing bounded strategies."
        };
        diagnostics.AddRange(candidates.TrimCapabilitySummaries
            .Where(t => (t.FamilyA == SurfacePatchFamily.Planar && t.FamilyB == SurfacePatchFamily.Spherical) || (t.FamilyA == SurfacePatchFamily.Spherical && t.FamilyB == SurfacePatchFamily.Planar))
            .Select(_ => "trim-capability: planar/spherical => circle exact"));
        diagnostics.AddRange(pairing.LoopClosureEvidence.Where(c => c.ClosureStatus == LoopClosureStatus.ClosedByDescriptor)
            .Select(_ => "loop-closure: closed by exact circle descriptor"));
        diagnostics.AddRange(pairing.PlannedCoedgePairings.Where(p => p.PairingKind == PlannedCoedgePairingKind.SharedTrimIdentity)
            .Select(_ => "coedge-pairing: promoted by internal trim identity token"));
        diagnostics.AddRange(pairing.PlannedCoedgePairings.Where(p => p.Readiness == TopologyPairingReadiness.Deferred)
            .Select(_ => "coedge-pairing: deferred; missing one-to-one identity"));
        diagnostics.AddRange(candidates.TrimCapabilitySummaries
            .Where(t => (t.FamilyA == SurfacePatchFamily.Planar && t.FamilyB == SurfacePatchFamily.Toroidal) || (t.FamilyA == SurfacePatchFamily.Toroidal && t.FamilyB == SurfacePatchFamily.Planar))
            .Select(t => $"trim-capability: planar/toroidal deferred; {t.Reason}"));

        return new MaterializationReadinessReport(
            Success: source.IsSuccess && candidates.Candidates.Count > 0,
            OverallReadiness: overall,
            BlockingReasons: blockingReasons,
            LayerSummaries: layers,
            SourceSurfaceCount: source.Descriptors.Count,
            CandidateCount: candidates.Candidates.Count,
            PlannedFaceCount: topology.PlannedFaces.Count,
            PlannedLoopCount: topology.PlannedFaces.SelectMany(f => f.LoopGroups).Count(),
            PlannedEdgeUseCount: pairing.PlannedEdgeUses.Count,
            CoedgePairingCount: pairing.PlannedCoedgePairings.Count,
            ClosureEvidenceCount: pairing.LoopClosureEvidence.Count,
            Diagnostics: diagnostics.Distinct().ToArray(),
            TopologyEmissionImplemented: false);
    }

    private static ReadinessLayerSummary SummarizeSource(SourceSurfaceExtractionResult source)
    {
        var readiness = source.IsSuccess ? EmissionReadiness.EvidenceReadyForEmission : EmissionReadiness.Unsupported;
        var reason = source.IsSuccess ? EmissionBlockingReason.None : EmissionBlockingReason.SourceSurfaceExtraction;
        return new("source-surface-extraction", readiness, [reason], source.Diagnostics.Select(d => $"{d.Code}: {d.Message}").Concat(source.UnsupportedNodeReasons).ToArray(),
            new Dictionary<string, int> { ["source-surfaces"] = source.Descriptors.Count, ["unsupported-nodes"] = source.UnsupportedNodeReasons.Count });
    }

    private static ReadinessLayerSummary SummarizeCandidates(FacePatchCandidateGenerationResult candidates)
    {
        var readiness = MapCandidate(candidates);
        var reasons = new List<EmissionBlockingReason>();
        if (readiness == EmissionReadiness.Deferred) reasons.Add(EmissionBlockingReason.TrimCapability);
        if (readiness == EmissionReadiness.Unsupported) reasons.Add(EmissionBlockingReason.UnsupportedSurfaceFamily);
        if (candidates.Candidates.All(c => c.RetentionRole == FacePatchRetentionRole.NotApplicable)) reasons.Add(EmissionBlockingReason.NonSubtractNotApplicable);
        return new("face-patch-candidates", readiness, reasons, candidates.Diagnostics, new Dictionary<string, int>
        {
            ["candidates"] = candidates.Candidates.Count,
            ["retained-loops"] = candidates.Candidates.SelectMany(c => c.RetainedRegionLoops).Count(),
            ["loop-groups"] = candidates.Candidates.SelectMany(c => c.RetainedRegionLoopGroups).Count(),
            ["deferred-reasons"] = candidates.DeferredReasons.Count
        });
    }

    private static EmissionReadiness MapCandidate(FacePatchCandidateGenerationResult candidates)
    {
        if (candidates.Candidates.All(c => c.RetentionRole == FacePatchRetentionRole.NotApplicable)) return EmissionReadiness.NotApplicable;
        if (candidates.Candidates.Any(c => c.Readiness == FacePatchCandidateReadiness.Unsupported)) return EmissionReadiness.Unsupported;
        if (candidates.Candidates.Any(c => c.Readiness is FacePatchCandidateReadiness.TrimDeferred or FacePatchCandidateReadiness.RetentionDeferred)) return EmissionReadiness.Deferred;
        return candidates.Candidates.Any(c => c.TrimCapability?.Classification == TrimCapabilityClassification.SpecialCaseOnly)
            ? EmissionReadiness.SpecialCaseReady
            : EmissionReadiness.EvidenceReadyForEmission;
    }

    private static ReadinessLayerSummary SummarizeTopology(TopologyAssemblyDryRunResult topology)
    {
        var readiness = topology.Readiness switch
        {
            TopologyAssemblyReadiness.ExactPlanReady => EmissionReadiness.EvidenceReadyForEmission,
            TopologyAssemblyReadiness.SpecialCasePlanReady => EmissionReadiness.SpecialCaseReady,
            TopologyAssemblyReadiness.Deferred => EmissionReadiness.Deferred,
            TopologyAssemblyReadiness.Unsupported => EmissionReadiness.Unsupported,
            _ => EmissionReadiness.NotApplicable
        };
        var reasons = new List<EmissionBlockingReason>();
        if (readiness == EmissionReadiness.Deferred) reasons.Add(EmissionBlockingReason.TopologyPlanning);
        if (readiness == EmissionReadiness.Unsupported) reasons.Add(EmissionBlockingReason.TopologyPlanning);
        return new("topology-dry-run", readiness, reasons, topology.Diagnostics, new Dictionary<string, int>
        {
            ["planned-faces"] = topology.PlannedFaces.Count,
            ["planned-loops"] = topology.PlannedFaces.SelectMany(f => f.LoopGroups).Count(),
            ["adjacency-hints"] = topology.PlannedAdjacencies.Count
        });
    }

    private static ReadinessLayerSummary SummarizePairing(TopologyPairingEvidenceResult pairing)
    {
        var readiness = pairing.Readiness switch
        {
            TopologyPairingReadiness.ExactReady => EmissionReadiness.EvidenceReadyForEmission,
            TopologyPairingReadiness.SpecialCaseReady => EmissionReadiness.SpecialCaseReady,
            TopologyPairingReadiness.Deferred => EmissionReadiness.Deferred,
            TopologyPairingReadiness.Unsupported => EmissionReadiness.Unsupported,
            _ => EmissionReadiness.NotApplicable
        };
        var reasons = new List<EmissionBlockingReason>();
        if (pairing.PlannedCoedgePairings.Any(p => p.Readiness == TopologyPairingReadiness.Deferred)) reasons.Add(EmissionBlockingReason.CoedgePairing);
        if (pairing.LoopClosureEvidence.Any(c => c.Readiness == TopologyPairingReadiness.Deferred)) reasons.Add(EmissionBlockingReason.LoopClosure);
        if (pairing.LoopClosureEvidence.Any(c => c.Readiness == TopologyPairingReadiness.Unsupported)) reasons.Add(EmissionBlockingReason.LoopClosure);
        return new("pairing-evidence", readiness, reasons.Distinct().ToArray(), pairing.Diagnostics, new Dictionary<string, int>
        {
            ["planned-edge-uses"] = pairing.PlannedEdgeUses.Count,
            ["planned-coedge-pairings"] = pairing.PlannedCoedgePairings.Count,
            ["loop-closure-evidence"] = pairing.LoopClosureEvidence.Count
        });
    }


    private static ReadinessLayerSummary SummarizeTrimOracle(FacePatchCandidateGenerationResult candidates)
    {
        var reps = candidates.Candidates.SelectMany(c => c.RetainedRegionLoops).Select(l => l.OracleTrimRepresentation).Where(r => r is not null).ToArray();
        var diagnostics = new List<string>();
        diagnostics.Add(reps.Length == 0 ? "oracle-trim: no-tiered-trim-representations-attached" : $"oracle-trim: attached={reps.Length}");
        diagnostics.AddRange(reps.SelectMany(r => r!.Diagnostics).Select(d => d.StartsWith("oracle-trim:", StringComparison.Ordinal) ? d : $"oracle-trim: {d}"));
        diagnostics.AddRange(reps.Where(r => r!.Circle is not null).Select(_ => "oracle-trim: analytic-circle-candidate"));
        diagnostics.AddRange(reps.Where(r => r!.NumericalContour is not null).Select(_ => "oracle-trim: numerical-contour-present"));
        diagnostics.AddRange(reps.Where(r => r!.ExportCapability == TieredTrimExportCapability.ElementaryCurveCandidate).Select(_ => "oracle-trim: elementary-export-candidate-not-exported"));
        diagnostics.AddRange(reps.Select(_ => "oracle-trim: b-rep-topology-not-emitted"));
        diagnostics.AddRange(reps.Select(_ => "oracle-trim: exact-step-export-false"));

        return new ReadinessLayerSummary("trim-oracle-evidence",
            reps.Length == 0 ? EmissionReadiness.Deferred : EmissionReadiness.EvidenceReadyForEmission,
            reps.Length == 0 ? [EmissionBlockingReason.TrimCapability] : [EmissionBlockingReason.None],
            diagnostics.Distinct().ToArray(),
            new Dictionary<string, int>
            {
                ["trim-oracle-representations"] = reps.Length,
                ["strong-trim-oracle"] = candidates.Candidates.SelectMany(c => c.RetainedRegionLoops).Count(l => l.OracleTrimStrongEvidence),
                ["broad-or-deferred-trim-oracle"] = candidates.Candidates.SelectMany(c => c.RetainedRegionLoops).Count(l => !l.OracleTrimStrongEvidence && l.OracleTrimRepresentation is not null),
                ["analytic-circle"] = reps.Count(r => r!.Circle is not null),
                ["numerical-contours"] = reps.Count(r => r!.NumericalContour is not null)
            });
    }
    private static EmissionReadiness Reduce(IEnumerable<EmissionReadiness> values)
    {
        var arr = values.ToArray();
        if (arr.Length == 0) return EmissionReadiness.NotApplicable;
        if (arr.Any(v => v == EmissionReadiness.Unsupported)) return EmissionReadiness.Unsupported;
        if (arr.Any(v => v == EmissionReadiness.Deferred)) return EmissionReadiness.Deferred;
        if (arr.Any(v => v == EmissionReadiness.SpecialCaseReady)) return EmissionReadiness.SpecialCaseReady;
        if (arr.Any(v => v == EmissionReadiness.EvidenceReadyForEmission)) return EmissionReadiness.EvidenceReadyForEmission;
        return EmissionReadiness.NotApplicable;
    }
}
