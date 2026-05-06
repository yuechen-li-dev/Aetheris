using Aetheris.Kernel.Core.Cir;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum FacePatchCandidateReadiness
{
    ExactReady,
    RetentionDeferred,
    TrimDeferred,
    Unsupported
}

internal enum FacePatchRetentionRole
{
    NotApplicable,
    BaseBoundaryRetainedOutsideTool,
    ToolBoundaryRetainedInsideBase,
    DiscardedInterior,
    RetentionDeferred,
    Unsupported
}

internal enum FacePatchRetentionStatus
{
    KnownWholeSurface,
    KnownTrimmedSurface,
    Deferred,
    Unsupported
}

internal sealed record FacePatchCandidate(
    SourceSurfaceDescriptor SourceSurface,
    FacePatchDescriptor ProposedPatch,
    string CandidateRole,
    FacePatchCandidateReadiness Readiness,
    TrimCapabilityResult? TrimCapability,
    FacePatchRetentionRole RetentionRole,
    FacePatchRetentionStatus RetentionStatus,
    string RetentionReason,
    IReadOnlyList<SurfacePatchFamily> OppositeFamilies,
    IReadOnlyList<string> Diagnostics);

internal sealed record FacePatchCandidateGenerationResult(
    bool IsSuccess,
    IReadOnlyList<FacePatchCandidate> Candidates,
    IReadOnlyList<SourceSurfaceDescriptor> SourceSurfaces,
    IReadOnlyList<TrimCapabilityResult> TrimCapabilitySummaries,
    IReadOnlyList<SourceSurfaceExtractionDiagnostic> ExtractionDiagnostics,
    IReadOnlyList<string> DeferredReasons,
    IReadOnlyList<string> Diagnostics,
    bool TopologyAssemblyImplemented);

internal static class FacePatchCandidateGenerator
{
    internal static FacePatchCandidateGenerationResult Generate(CirNode root, NativeGeometryReplayLog? replayLog = null)
    {
        var extraction = SourceSurfaceExtractor.Extract(root, replayLog);
        var candidates = new List<FacePatchCandidate>();
        var trimSummaries = new List<TrimCapabilityResult>();
        var deferred = new List<string>();
        var diagnostics = new List<string>();

        if (!extraction.IsSuccess)
        {
            diagnostics.Add("source-surface-extraction-unsupported: one or more CIR nodes cannot be inventoried into source surfaces.");
            diagnostics.AddRange(extraction.UnsupportedNodeReasons.Select(r => $"source-surface-extraction-unsupported: {r}"));
        }

        if (root is not CirSubtractNode subtract)
        {
            foreach (var source in extraction.Descriptors)
            {
                var patch = new FacePatchDescriptor(source, [], [], source.OrientationRole, "non-subtract-candidate", []);
                candidates.Add(new FacePatchCandidate(
                    source,
                    patch,
                    "non-subtract-candidate",
                    FacePatchCandidateReadiness.RetentionDeferred,
                    null,
                    FacePatchRetentionRole.NotApplicable,
                    FacePatchRetentionStatus.Deferred,
                    "retention-not-applicable: CIR-F8.4 retention classification applies to subtract roots only.",
                    [],
                    ["retention-not-applicable: non-subtract root leaves candidate retention unclassified."]));
            }

            diagnostics.Add("unsupported-node-shape: face patch dry-run currently supports subtract-tree retention classification only.");
            diagnostics.Add("topology-assembly-not-implemented: dry-run emits candidate descriptors only and does not emit BRep topology.");
            return new(false, candidates, extraction.Descriptors, trimSummaries, extraction.Diagnostics, deferred, diagnostics, TopologyAssemblyImplemented: false);
        }

        var left = SourceSurfaceExtractor.Extract(subtract.Left, replayLog);
        var right = SourceSurfaceExtractor.Extract(subtract.Right, replayLog);

        trimSummaries.AddRange(ComputeUniqueTrimSummaries(left.Descriptors, right.Descriptors));

        foreach (var source in extraction.Descriptors)
        {
            var isBase = IsFromNode(source, subtract.Left);
            var role = isBase ? "base-surface-candidate" : "tool-surface-candidate";
            var relevantOther = isBase ? right.Descriptors : left.Descriptors;
            var oppositeFamilies = relevantOther.Select(d => d.Family).Distinct().ToArray();
            var trim = EvaluateBestTrim(source, relevantOther);

            var readiness = EvaluateReadiness(trim);
            var (retentionRole, retentionStatus, retentionReason) = EvaluateRetention(isBase, trim, relevantOther.Count);
            var candidateDiagnostics = new List<string>();
            if (trim is null)
            {
                readiness = FacePatchCandidateReadiness.Unsupported;
                candidateDiagnostics.Add("no-relevant-trim-capability: no opposite-side source surfaces available for trim matrix evaluation.");
            }
            else
            {
                if (trim.Classification == TrimCapabilityClassification.Deferred)
                {
                    candidateDiagnostics.Add($"trim-capability-deferred: {trim.Reason}");
                    deferred.Add($"{source.Provenance}: {trim.Reason}");
                }
                else if (trim.Classification == TrimCapabilityClassification.Unsupported)
                {
                    candidateDiagnostics.Add($"trim-capability-unsupported: {trim.Reason}");
                }
                else if (trim.Classification == TrimCapabilityClassification.SpecialCaseOnly)
                {
                    candidateDiagnostics.Add($"trim-capability-special-case: {trim.Reason}");
                }

                if (retentionStatus == FacePatchRetentionStatus.KnownTrimmedSurface)
                {
                    candidateDiagnostics.Add("retention-region-deferred: retention role is known and trim capability is available, but retained-loop assembly is deferred.");
                }
            }

            candidateDiagnostics.Add($"retention-role: {retentionRole}");
            candidateDiagnostics.Add($"retention-status: {retentionStatus}");
            candidateDiagnostics.Add(retentionReason);

            var orientation = isBase ? source.OrientationRole : FacePatchOrientationRole.Reversed;
            var patchRole = isBase ? "base-boundary-retained-outside-tool" : "tool-boundary-retained-inside-base";
            var patch = new FacePatchDescriptor(source, [], [], orientation, patchRole, []);
            candidates.Add(new FacePatchCandidate(source, patch, role, readiness, trim, retentionRole, retentionStatus, retentionReason, oppositeFamilies, candidateDiagnostics));
        }

        diagnostics.Add("topology-assembly-not-implemented: dry-run emits candidate descriptors only and does not emit BRep topology.");
        diagnostics.Add("retention-region-deferred: subtract retention roles are classified, but retained trim loops and topology assembly are deferred in CIR-F8.4.");

        var success = extraction.IsSuccess && candidates.Count > 0;
        return new(success, candidates, extraction.Descriptors, trimSummaries, extraction.Diagnostics, deferred.Distinct().ToArray(), diagnostics, TopologyAssemblyImplemented: false);
    }

    private static (FacePatchRetentionRole Role, FacePatchRetentionStatus Status, string Reason) EvaluateRetention(bool isBaseSide, TrimCapabilityResult? trim, int oppositeCount)
    {
        if (oppositeCount == 0)
        {
            return (FacePatchRetentionRole.Unsupported, FacePatchRetentionStatus.Unsupported, "retention-unsupported: subtract side has no opposite source surfaces to classify against.");
        }

        var role = isBaseSide
            ? FacePatchRetentionRole.BaseBoundaryRetainedOutsideTool
            : FacePatchRetentionRole.ToolBoundaryRetainedInsideBase;

        if (trim is null)
        {
            return (role, FacePatchRetentionStatus.Deferred, "retention-deferred: subtract retention rule is known, but trim capability against opposite surfaces is unavailable.");
        }

        return trim.Classification switch
        {
            TrimCapabilityClassification.ExactSupported or TrimCapabilityClassification.SpecialCaseOnly
                => (role, FacePatchRetentionStatus.KnownTrimmedSurface, "retention-known-trimmed: subtract retention role is known and pair trim capability is available; retained region loops are not assembled yet."),
            TrimCapabilityClassification.Deferred
                => (role, FacePatchRetentionStatus.Deferred, $"retention-trim-deferred: retention role is known but trim policy is deferred ({trim.Reason})"),
            TrimCapabilityClassification.Unsupported
                => (FacePatchRetentionRole.Unsupported, FacePatchRetentionStatus.Unsupported, $"retention-unsupported: subtract retention role cannot be applied due to unsupported trim pairing ({trim.Reason})"),
            _ => (FacePatchRetentionRole.RetentionDeferred, FacePatchRetentionStatus.Deferred, "retention-deferred: subtract retention classification unresolved.")
        };
    }

    private static IReadOnlyList<TrimCapabilityResult> ComputeUniqueTrimSummaries(IReadOnlyList<SourceSurfaceDescriptor> left, IReadOnlyList<SourceSurfaceDescriptor> right)
    {
        var summaries = new Dictionary<(SurfacePatchFamily, SurfacePatchFamily), TrimCapabilityResult>();
        foreach (var l in left)
        foreach (var r in right)
        {
            var result = TrimCapabilityMatrix.Evaluate(l.Family, r.Family);
            var key = Normalize(l.Family, r.Family);
            summaries.TryAdd(key, result);
        }

        return summaries.Values.ToArray();
    }

    private static TrimCapabilityResult? EvaluateBestTrim(SourceSurfaceDescriptor source, IReadOnlyList<SourceSurfaceDescriptor> others)
    {
        TrimCapabilityResult? best = null;
        foreach (var other in others)
        {
            var result = TrimCapabilityMatrix.Evaluate(source.Family, other.Family);
            if (best is null || Score(result.Classification) > Score(best.Classification))
            {
                best = result;
            }
        }

        return best;
    }

    private static FacePatchCandidateReadiness EvaluateReadiness(TrimCapabilityResult? trim)
        => trim?.Classification switch
        {
            TrimCapabilityClassification.ExactSupported => FacePatchCandidateReadiness.ExactReady,
            TrimCapabilityClassification.SpecialCaseOnly => FacePatchCandidateReadiness.ExactReady,
            TrimCapabilityClassification.Deferred => FacePatchCandidateReadiness.TrimDeferred,
            TrimCapabilityClassification.Unsupported => FacePatchCandidateReadiness.Unsupported,
            _ => FacePatchCandidateReadiness.Unsupported
        };

    private static bool IsFromNode(SourceSurfaceDescriptor source, CirNode node)
        => source.OwningCirNodeKind == node.GetType().Name;

    private static int Score(TrimCapabilityClassification c)
        => c switch
        {
            TrimCapabilityClassification.ExactSupported => 4,
            TrimCapabilityClassification.SpecialCaseOnly => 3,
            TrimCapabilityClassification.Deferred => 2,
            _ => 1
        };

    private static (SurfacePatchFamily, SurfacePatchFamily) Normalize(SurfacePatchFamily a, SurfacePatchFamily b)
        => a <= b ? (a, b) : (b, a);
}
