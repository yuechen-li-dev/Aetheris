using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;

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

internal enum RetainedRegionLoopKind
{
    OuterBoundary,
    InnerTrim,
    MouthTrim,
    CapTrim,
    SeamTrim,
    Deferred,
    Unsupported
}

internal enum RetainedRegionLoopStatus
{
    ExactReady,
    SpecialCaseReady,
    Deferred,
    Unsupported
}

internal enum RetainedRegionLoopGroupKind
{
    NotApplicable,
    OuterBoundaryGroup,
    InnerTrimGroup,
    MouthTrimGroup,
    CapTrimGroup,
    SeamTrimGroup,
    DeferredGroup,
    UnsupportedGroup
}

internal enum RetainedRegionLoopGroupReadiness
{
    NotApplicable,
    ExactReady,
    SpecialCaseReady,
    Deferred,
    Unsupported
}

internal enum RetainedRegionLoopOrientationPolicy
{
    UseCandidateOrientation,
    ReverseForToolCavity,
    SurfaceNatural,
    Deferred,
    Unsupported
}

internal sealed record RetainedRegionLoopDescriptor(
    RetainedRegionLoopKind LoopKind,
    TrimCurveFamily TrimCurveFamily,
    TrimCapabilityClassification TrimCapability,
    SurfacePatchFamily SourceSurfaceFamily,
    SurfacePatchFamily OppositeSurfaceFamily,
    FacePatchOrientationRole OrientationHint,
    FacePatchRetentionRole RetentionRole,
    RetainedRegionLoopStatus Status,
    string Diagnostic,
    RetainedCircularLoopGeometry? CircularGeometry);

internal readonly record struct RetainedCircularLoopGeometry(
    Point3D Center,
    Vector3D Normal,
    double Radius,
    RetainedRegionLoopOrientationPolicy OrientationPolicy,
    string OrderingToken,
    string Diagnostic);

internal sealed record RetainedRegionLoopGroup(
    RetainedRegionLoopGroupKind GroupKind,
    RetainedRegionLoopGroupReadiness Readiness,
    RetainedRegionLoopOrientationPolicy OrientationPolicy,
    string OrderingKey,
    IReadOnlyList<RetainedRegionLoopDescriptor> Loops,
    IReadOnlyList<string> Diagnostics);

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
    IReadOnlyList<RetainedRegionLoopDescriptor> RetainedRegionLoops,
    IReadOnlyList<RetainedRegionLoopGroup> RetainedRegionLoopGroups,
    RetainedRegionLoopStatus LoopReadiness,
    string LoopDiagnostic,
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
                    [],
                    [],
                    RetainedRegionLoopStatus.Deferred,
                    "loop-retention-not-applicable: retained-region loop scaffolding applies to subtract roots with classified retention only.",
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
            var loopDescriptors = BuildRetainedRegionLoops(source, relevantOther, retentionRole, retentionStatus, isBase);
            var loopReadiness = EvaluateLoopReadiness(loopDescriptors);
            var loopDiagnostic = BuildLoopDiagnostic(loopDescriptors, retentionStatus);
            var loopGroups = BuildRetainedRegionLoopGroups(source, loopDescriptors, retentionRole, retentionStatus, isBase);
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
            candidateDiagnostics.Add($"loop-readiness: {loopReadiness}");
            candidateDiagnostics.Add(loopDiagnostic);
            candidateDiagnostics.AddRange(loopGroups.Select(g => $"loop-group: {g.GroupKind}/{g.Readiness}/{g.OrientationPolicy} key={g.OrderingKey}"));
            candidateDiagnostics.AddRange(loopDescriptors.Select(l => l.Diagnostic));

            var orientation = isBase ? source.OrientationRole : FacePatchOrientationRole.Reversed;
            var patchRole = isBase ? "base-boundary-retained-outside-tool" : "tool-boundary-retained-inside-base";
            var patch = new FacePatchDescriptor(source, [], [], orientation, patchRole, []);
            candidates.Add(new FacePatchCandidate(source, patch, role, readiness, trim, retentionRole, retentionStatus, retentionReason, oppositeFamilies, loopDescriptors, loopGroups, loopReadiness, loopDiagnostic, candidateDiagnostics));
        }

        diagnostics.Add("topology-assembly-not-implemented: dry-run emits candidate descriptors only and does not emit BRep topology.");
        diagnostics.Add("topology-assembly-not-implemented: retained-region loop scaffolding emits descriptor diagnostics only; no BRep loops/coedges/edges/vertices are created.");

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

    private static IReadOnlyList<RetainedRegionLoopDescriptor> BuildRetainedRegionLoops(
        SourceSurfaceDescriptor source,
        IReadOnlyList<SourceSurfaceDescriptor> opposite,
        FacePatchRetentionRole retentionRole,
        FacePatchRetentionStatus retentionStatus,
        bool isBase)
    {
        if (retentionRole is FacePatchRetentionRole.NotApplicable or FacePatchRetentionRole.RetentionDeferred)
        {
            return [];
        }

        if (retentionStatus != FacePatchRetentionStatus.KnownTrimmedSurface && retentionStatus != FacePatchRetentionStatus.Deferred)
        {
            return [];
        }

        var loops = new List<RetainedRegionLoopDescriptor>();
        foreach (var other in opposite)
        {
            var trim = TrimCapabilityMatrix.Evaluate(source.Family, other.Family);
            var loopKind = DetermineLoopKind(source.Family, isBase, trim.Classification);
            var status = trim.Classification switch
            {
                TrimCapabilityClassification.ExactSupported => RetainedRegionLoopStatus.ExactReady,
                TrimCapabilityClassification.SpecialCaseOnly => RetainedRegionLoopStatus.SpecialCaseReady,
                TrimCapabilityClassification.Deferred => RetainedRegionLoopStatus.Deferred,
                _ => RetainedRegionLoopStatus.Unsupported
            };

            var families = trim.CurveFamilies.Count == 0 ? [TrimCurveFamily.Unsupported] : trim.CurveFamilies;
            foreach (var curveFamily in families)
            {
                loops.Add(new RetainedRegionLoopDescriptor(
                    loopKind,
                    curveFamily,
                    trim.Classification,
                    source.Family,
                    other.Family,
                    isBase ? source.OrientationRole : FacePatchOrientationRole.Reversed,
                    retentionRole,
                    status,
                    BuildPerLoopDiagnostic(source.Family, other.Family, curveFamily, status, trim.Reason),
                    RetainedLoopGeometryBinder.TryBindCircularLoop(source, other, curveFamily, status, isBase, out var circularDiagnostic, out var circular)
                        ? circular
                        : null));
            }
        }

        return loops;
    }

    private static RetainedRegionLoopKind DetermineLoopKind(SurfacePatchFamily sourceFamily, bool isBase, TrimCapabilityClassification classification)
    {
        if (classification == TrimCapabilityClassification.Deferred) return RetainedRegionLoopKind.Deferred;
        if (classification == TrimCapabilityClassification.Unsupported) return RetainedRegionLoopKind.Unsupported;
        if (isBase) return RetainedRegionLoopKind.InnerTrim;
        return sourceFamily == SurfacePatchFamily.Cylindrical || sourceFamily == SurfacePatchFamily.Spherical
            ? RetainedRegionLoopKind.MouthTrim
            : RetainedRegionLoopKind.InnerTrim;
    }

    private static RetainedRegionLoopStatus EvaluateLoopReadiness(IReadOnlyList<RetainedRegionLoopDescriptor> loops)
    {
        if (loops.Count == 0) return RetainedRegionLoopStatus.Deferred;
        if (loops.Any(l => l.Status == RetainedRegionLoopStatus.ExactReady)) return RetainedRegionLoopStatus.ExactReady;
        if (loops.Any(l => l.Status == RetainedRegionLoopStatus.SpecialCaseReady)) return RetainedRegionLoopStatus.SpecialCaseReady;
        if (loops.Any(l => l.Status == RetainedRegionLoopStatus.Deferred)) return RetainedRegionLoopStatus.Deferred;
        return RetainedRegionLoopStatus.Unsupported;
    }

    private static string BuildLoopDiagnostic(IReadOnlyList<RetainedRegionLoopDescriptor> loops, FacePatchRetentionStatus retentionStatus)
    {
        if (loops.Count == 0)
        {
            return retentionStatus == FacePatchRetentionStatus.Deferred
                ? "loop-scaffold-deferred: retention is deferred, so retained-region loop descriptors are not emitted."
                : "loop-scaffold-empty: no retained-region loops were generated for this candidate.";
        }

        if (loops.All(l => l.Status == RetainedRegionLoopStatus.Deferred))
        {
            return "loop-scaffold-deferred: trim matrix marks all retained-region loops deferred.";
        }

        if (loops.Any(l => l.Status == RetainedRegionLoopStatus.SpecialCaseReady) && !loops.Any(l => l.Status == RetainedRegionLoopStatus.ExactReady))
        {
            return "loop-scaffold-special-case-ready: retained-region loop types are known with special-case restrictions; exact parameter solving remains deferred.";
        }

        return "loop-scaffold-ready: retained-region loop types are known; exact curve parameters and topology assembly remain deferred.";
    }

    private static string BuildPerLoopDiagnostic(SurfacePatchFamily source, SurfacePatchFamily opposite, TrimCurveFamily family, RetainedRegionLoopStatus status, string reason)
        => status switch
        {
            RetainedRegionLoopStatus.ExactReady => $"loop-exact-ready: {source}/{opposite} uses {family} trim family; parameters deferred ({reason})",
            RetainedRegionLoopStatus.SpecialCaseReady => $"loop-special-case-ready: {source}/{opposite} uses {family} trim family with restrictions ({reason})",
            RetainedRegionLoopStatus.Deferred => $"loop-deferred: {source}/{opposite} trim loop remains deferred ({reason})",
            _ => $"loop-unsupported: {source}/{opposite} trim loop unsupported ({reason})"
        };

    private static IReadOnlyList<RetainedRegionLoopGroup> BuildRetainedRegionLoopGroups(
        SourceSurfaceDescriptor source,
        IReadOnlyList<RetainedRegionLoopDescriptor> loops,
        FacePatchRetentionRole retentionRole,
        FacePatchRetentionStatus retentionStatus,
        bool isBase)
    {
        if (retentionRole == FacePatchRetentionRole.NotApplicable)
        {
            return [new RetainedRegionLoopGroup(
                RetainedRegionLoopGroupKind.NotApplicable,
                RetainedRegionLoopGroupReadiness.NotApplicable,
                RetainedRegionLoopOrientationPolicy.Deferred,
                $"{source.Family}|not-applicable|0",
                [],
                ["loop-group-not-applicable: retained-loop grouping only applies to subtract retention candidates."])];
        }

        if (loops.Count == 0)
        {
            return retentionStatus == FacePatchRetentionStatus.Deferred
                ? [new RetainedRegionLoopGroup(RetainedRegionLoopGroupKind.DeferredGroup, RetainedRegionLoopGroupReadiness.Deferred, RetainedRegionLoopOrientationPolicy.Deferred, $"{source.Family}|deferred-empty|0", [], ["loop-group-deferred: no loop descriptors emitted because retention is deferred."])]
                : [];
        }

        var orderedLoops = loops
            .OrderBy(l => l.SourceSurfaceFamily)
            .ThenBy(l => l.OppositeSurfaceFamily)
            .ThenBy(l => l.LoopKind)
            .ThenBy(l => l.Status)
            .ThenBy(l => l.TrimCurveFamily)
            .ToArray();

        var groups = orderedLoops
            .GroupBy(l => MapGroupKind(l.LoopKind))
            .Select(g =>
            {
                var groupLoops = g.ToArray();
                var readiness = EvaluateGroupReadiness(groupLoops);
                var orientation = EvaluateOrientationPolicy(isBase, readiness);
                var key = $"{source.Family}|{retentionRole}|{g.Key}|{readiness}|{orientation}|{groupLoops.Length}";
                return new RetainedRegionLoopGroup(g.Key, readiness, orientation, key, groupLoops,
                    [BuildGroupDiagnostic(g.Key, readiness, orientation)]);
            })
            .OrderBy(g => g.OrderingKey, StringComparer.Ordinal)
            .ToArray();

        return groups;
    }

    private static RetainedRegionLoopGroupKind MapGroupKind(RetainedRegionLoopKind kind)
        => kind switch
        {
            RetainedRegionLoopKind.OuterBoundary => RetainedRegionLoopGroupKind.OuterBoundaryGroup,
            RetainedRegionLoopKind.InnerTrim => RetainedRegionLoopGroupKind.InnerTrimGroup,
            RetainedRegionLoopKind.MouthTrim => RetainedRegionLoopGroupKind.MouthTrimGroup,
            RetainedRegionLoopKind.CapTrim => RetainedRegionLoopGroupKind.CapTrimGroup,
            RetainedRegionLoopKind.SeamTrim => RetainedRegionLoopGroupKind.SeamTrimGroup,
            RetainedRegionLoopKind.Deferred => RetainedRegionLoopGroupKind.DeferredGroup,
            _ => RetainedRegionLoopGroupKind.UnsupportedGroup
        };

    private static RetainedRegionLoopGroupReadiness EvaluateGroupReadiness(IReadOnlyList<RetainedRegionLoopDescriptor> loops)
    {
        if (loops.Any(l => l.Status == RetainedRegionLoopStatus.Unsupported)) return RetainedRegionLoopGroupReadiness.Unsupported;
        if (loops.Any(l => l.Status == RetainedRegionLoopStatus.Deferred)) return RetainedRegionLoopGroupReadiness.Deferred;
        if (loops.Any(l => l.Status == RetainedRegionLoopStatus.SpecialCaseReady)) return RetainedRegionLoopGroupReadiness.SpecialCaseReady;
        return RetainedRegionLoopGroupReadiness.ExactReady;
    }

    private static RetainedRegionLoopOrientationPolicy EvaluateOrientationPolicy(bool isBase, RetainedRegionLoopGroupReadiness readiness)
    {
        if (readiness == RetainedRegionLoopGroupReadiness.Unsupported) return RetainedRegionLoopOrientationPolicy.Unsupported;
        if (readiness == RetainedRegionLoopGroupReadiness.Deferred) return RetainedRegionLoopOrientationPolicy.Deferred;
        return isBase ? RetainedRegionLoopOrientationPolicy.UseCandidateOrientation : RetainedRegionLoopOrientationPolicy.ReverseForToolCavity;
    }

    private static string BuildGroupDiagnostic(
        RetainedRegionLoopGroupKind kind,
        RetainedRegionLoopGroupReadiness readiness,
        RetainedRegionLoopOrientationPolicy orientation)
        => $"loop-group-{kind.ToString().ToLowerInvariant()}: readiness={readiness}; orientation-policy={orientation}; topology assembly remains deferred.";
}

internal static class RetainedLoopGeometryBinder
{
    internal static bool TryBindCircularLoop(
        SourceSurfaceDescriptor source,
        SourceSurfaceDescriptor opposite,
        TrimCurveFamily trimFamily,
        RetainedRegionLoopStatus loopStatus,
        bool isBase,
        out string diagnostic,
        out RetainedCircularLoopGeometry? circular)
    {
        circular = null;
        if (trimFamily != TrimCurveFamily.Circle)
        {
            diagnostic = "loop-geometry-bind-skipped: trim family is not circle.";
            return false;
        }

        if (loopStatus is RetainedRegionLoopStatus.Deferred or RetainedRegionLoopStatus.Unsupported)
        {
            diagnostic = "loop-geometry-bind-skipped: loop status is deferred/unsupported.";
            return false;
        }

        if (source.Family != SurfacePatchFamily.Planar || opposite.Family != SurfacePatchFamily.Cylindrical)
        {
            diagnostic = "loop-geometry-bind-skipped: CIR-F10.5 supports only planar source with cylindrical opposite.";
            return false;
        }

        if (source.BoundedPlanarGeometry is not { } planar)
        {
            diagnostic = "loop-geometry-bind-deferred: planar source lacks bounded planar geometry for canonical normal/plane evidence.";
            return false;
        }

        var planeNormal = planar.Normal;
        var axisDirection = opposite.Transform.Apply(new Vector3D(0d, 0d, 1d));
        if (planeNormal.Length <= 1e-12 || axisDirection.Length <= 1e-12)
        {
            diagnostic = "loop-geometry-bind-deferred: degenerate plane normal or cylinder axis evidence.";
            return false;
        }

        var normal = Direction3D.Create(planeNormal).ToVector();
        var axis = Direction3D.Create(axisDirection).ToVector();
        var parallel = Math.Abs(normal.Dot(axis));
        if (Math.Abs(1d - parallel) > 1e-9)
        {
            diagnostic = "loop-geometry-bind-deferred: planar/cylindrical circular binding requires plane normal parallel to cylinder axis.";
            return false;
        }

        var axisPoint = opposite.Transform.Apply(Point3D.Origin);
        var planePoint = planar.Kind == BoundedPlanarPatchGeometryKind.Circle ? planar.Center : planar.Corner00;
        var denom = normal.Dot(axis);
        if (Math.Abs(denom) < 1e-12)
        {
            diagnostic = "loop-geometry-bind-deferred: plane/axis intersection numerically unstable.";
            return false;
        }

        var t = normal.Dot(planePoint - axisPoint) / denom;
        var center = axisPoint + (axis * t);
        if (!TryReadCylindricalRadiusEvidence(opposite, out var radius))
        {
            diagnostic = "loop-geometry-bind-deferred: cylindrical source descriptor does not yet carry canonical radius evidence.";
            return false;
        }

        if (radius <= 1e-12)
        {
            diagnostic = "loop-geometry-bind-deferred: cylindrical radius evidence is degenerate.";
            return false;
        }

        var orientationPolicy = isBase ? RetainedRegionLoopOrientationPolicy.ReverseForToolCavity : RetainedRegionLoopOrientationPolicy.UseCandidateOrientation;
        var token = $"{source.Provenance}|{opposite.Provenance}|circle|{orientationPolicy}";
        circular = new RetainedCircularLoopGeometry(center, normal, radius, orientationPolicy, token, "loop-geometry-bind-ready: canonical planar/cylindrical inner-circle geometry bound.");
        diagnostic = circular.Value.Diagnostic;
        return true;
    }

    private static bool TryReadCylindricalRadiusEvidence(SourceSurfaceDescriptor opposite, out double radius)
    {
        radius = 0d;
        if (opposite.ParameterPayloadReference is null) return false;
        if (!opposite.ParameterPayloadReference.StartsWith("radius:", StringComparison.Ordinal)) return false;
        return double.TryParse(opposite.ParameterPayloadReference[7..], out radius);
    }
}
