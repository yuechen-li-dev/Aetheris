using Aetheris.Kernel.Core.Cir;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum FacePatchCandidateReadiness
{
    ExactReady,
    RetentionDeferred,
    TrimDeferred,
    Unsupported
}

internal sealed record FacePatchCandidate(
    SourceSurfaceDescriptor SourceSurface,
    FacePatchDescriptor ProposedPatch,
    string CandidateRole,
    FacePatchCandidateReadiness Readiness,
    TrimCapabilityResult? TrimCapability,
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
            diagnostics.Add("unsupported-node-shape: face patch dry-run currently supports subtract-tree analysis only.");
            return new(false, candidates, extraction.Descriptors, trimSummaries, extraction.Diagnostics, deferred, diagnostics, TopologyAssemblyImplemented: false);
        }

        var left = SourceSurfaceExtractor.Extract(subtract.Left, replayLog);
        var right = SourceSurfaceExtractor.Extract(subtract.Right, replayLog);

        trimSummaries.AddRange(ComputeUniqueTrimSummaries(left.Descriptors, right.Descriptors));

        foreach (var source in extraction.Descriptors)
        {
            var role = IsFromNode(source, subtract.Left) ? "base-surface-candidate" : "tool-surface-candidate";
            var relevantOther = role == "base-surface-candidate" ? right.Descriptors : left.Descriptors;
            var trim = EvaluateBestTrim(source, relevantOther);

            var readiness = EvaluateReadiness(trim);
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

                if (readiness == FacePatchCandidateReadiness.ExactReady)
                {
                    candidateDiagnostics.Add("retention-classification-deferred: exact trim capability found, but retained/discarded patch boundaries are not classified in CIR-F8.3.");
                }
            }

            var patch = new FacePatchDescriptor(source, [], [], source.OrientationRole, role, []);
            candidates.Add(new FacePatchCandidate(source, patch, role, readiness, trim, candidateDiagnostics));
        }

        diagnostics.Add("topology-assembly-not-implemented: dry-run emits candidate descriptors only and does not emit BRep topology.");
        diagnostics.Add("retention-classification-deferred: boolean retention mapping is intentionally deferred in CIR-F8.3.");

        var success = extraction.IsSuccess && candidates.Count > 0;
        return new(success, candidates, extraction.Descriptors, trimSummaries, extraction.Diagnostics, deferred.Distinct().ToArray(), diagnostics, TopologyAssemblyImplemented: false);
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
