using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Diagnostics;

internal enum SurfaceFamilyPressureSeverity { Info, Warning, Blocking, Fatal }
internal enum SurfaceFamilyPressureStageStatus { Succeeded, Skipped, Deferred, Failed }

internal sealed record SurfaceFamilyPressureTestBlocker(string Stage, SurfaceFamilyPressureSeverity Severity, string Code, string Message, IReadOnlyList<string> RelatedTopologyIds, string RecommendedFix, IReadOnlyList<string> Diagnostics);
internal sealed record SurfaceFamilyPressureStageResult(string Stage, SurfaceFamilyPressureStageStatus Status, IReadOnlyList<string> Diagnostics, IReadOnlyDictionary<string, int> Counts);
internal sealed record SurfaceFamilyBoxCylinderPressureTestOptions(bool AttemptStepExportSmoke = true);

internal sealed record SurfaceFamilyBoxCylinderPressureTestResult(
    bool Success,
    string StageReached,
    BrepBody? FinalBody,
    IReadOnlyList<SurfaceFamilyPressureTestBlocker> Blockers,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<SurfaceFamilyPressureStageResult> Stages,
    IReadOnlyDictionary<string, int> TopologyCounts,
    IReadOnlyDictionary<string, int> TokenAndStitchCounts,
    IReadOnlyDictionary<string, int> EdgeUseCounts,
    bool ShellClosureValidated,
    bool BrepBodyValidationPassed,
    bool StepExportAttempted,
    bool StepExportSucceeded,
    string StepExportDiagnostic,
    bool ProductionPathChanged,
    bool GeneratedTopologyNamesExposed);

internal static class SurfaceFamilyBoxCylinderPressureTest
{
    private static readonly string[] RequiredStages = ["InputValidation","PlanarPatchEmission","CylindricalWallEmission","TokenPairingAnalysis","StitchCandidatePlanning","CombinedBodyRemap","SharedEdgeRewrite","DuplicateEdgeCleanup","VertexMergePlanning","LoopClosureValidation","ShellClosureValidation","BrepBodyValidation","StepExportSmoke"];

    internal static SurfaceFamilyBoxCylinderPressureTestResult Run(CirNode root, SurfaceFamilyBoxCylinderPressureTestOptions? options = null)
    {
        options ??= new();
        var stages = new List<SurfaceFamilyPressureStageResult>();
        var blockers = new List<SurfaceFamilyPressureTestBlocker>();
        var warnings = new List<string>();
        var topoCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var tokenCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var edgeUse = new Dictionary<string, int>(StringComparer.Ordinal);
        var reached = "InputValidation";

        if (root is not CirSubtractNode { Left: CirBoxNode, Right: CirCylinderNode })
        {
            stages.Add(new("InputValidation", SurfaceFamilyPressureStageStatus.Failed, ["canonical-input-required: subtract(box,cylinder)"], new Dictionary<string, int>()));
            blockers.Add(Block("InputValidation", "unsupported-input-noncanonical", "Only canonical Subtract(Box,Cylinder) is supported.", "Provide a canonical subtract node with box base and cylinder tool."));
            AddRemainingDeferredStages(stages, "input validation failed; no further bounded diagnostics executed.");
            return Final(false, reached, null, blockers, warnings, stages, topoCounts, tokenCounts, edgeUse, false, false, false, false, "step-smoke-skipped-shell-not-closed");
        }

        stages.Add(new("InputValidation", SurfaceFamilyPressureStageStatus.Succeeded, ["canonical-input: subtract(box,cylinder)"], new Dictionary<string, int>()));

        var planar = new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root);
        reached = "PlanarPatchEmission";
        stages.Add(new("PlanarPatchEmission", planar.Success ? SurfaceFamilyPressureStageStatus.Succeeded : SurfaceFamilyPressureStageStatus.Failed, planar.Diagnostics, new Dictionary<string, int>
        {
            ["PlanarEmittedPatchCount"] = planar.Entries.Count(e => e.Emitted),
            ["RouteTieredOracleTrim"] = planar.Entries.Count(e => e.Diagnostics.Any(d => d.Contains("oracle", StringComparison.OrdinalIgnoreCase))),
            ["RouteBinderFallback"] = planar.Entries.Count(e => e.Diagnostics.Any(d => d.Contains("binder", StringComparison.OrdinalIgnoreCase))),
            ["RouteUntrimmedRectangle"] = planar.Entries.Count(e => e.Diagnostics.Any(d => d.Contains("untrimmed", StringComparison.OrdinalIgnoreCase))),
            ["RouteSkipped"] = planar.Entries.Count(e => !e.Emitted)
        }));

        var gen = FacePatchCandidateGenerator.Generate(root);
        var cylCandidate = gen.Candidates.SingleOrDefault(c => c.SourceSurface.Family == SurfacePatchFamily.Cylindrical && c.RetentionRole == FacePatchRetentionRole.ToolBoundaryRetainedInsideBase);
        SurfaceMaterializationResult? cyl = null;
        if (cylCandidate is not null)
        {
            var ready = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 0, 0, 0, 0, 0, 0, 0, [], false);
            cyl = new CylindricalSurfaceMaterializer().EmitRetainedWall(new(cylCandidate, ready));
        }
        reached = "CylindricalWallEmission";
        stages.Add(new("CylindricalWallEmission", cyl?.Success == true ? SurfaceFamilyPressureStageStatus.Succeeded : SurfaceFamilyPressureStageStatus.Failed, cyl?.Diagnostics ?? ["cylindrical-candidate-missing"], new Dictionary<string, int>{{"CylindricalEmittedPatchCount", cyl?.Success == true ? 1 : 0}}));

        var maps = planar.Entries.Where(e => e.IdentityMap is not null).Select(e => e.IdentityMap!).Concat(cyl?.IdentityMap is null ? [] : [cyl.IdentityMap]).ToArray();
        var token = EmittedTokenPairingAnalyzer.Analyze(maps);
        tokenCounts["SafeTokenPairCount"] = token.SafePairs.Count;
        tokenCounts["MissingMateTokenCount"] = token.MissingMateGroups.Count;
        tokenCounts["AmbiguousTokenCount"] = token.AmbiguousGroups.Count;
        tokenCounts["NullTokenEntryCount"] = token.NullTokenEntries.Count;
        reached = "TokenPairingAnalysis";
        stages.Add(new("TokenPairingAnalysis", SurfaceFamilyPressureStageStatus.Succeeded, token.Diagnostics, new Dictionary<string, int>(tokenCounts)));

        var stitchPlan = SurfaceFamilyStitchCandidatePlanner.Plan(maps, token, ShellStitchingDryRunPlanner.Generate(root));
        tokenCounts["StitchCandidateCount"] = stitchPlan.Candidates.Count;
        tokenCounts["ReadyStitchCandidateCount"] = stitchPlan.Candidates.Count(c => c.Readiness == SurfaceFamilyStitchCandidateReadiness.Ready);
        reached = "StitchCandidatePlanning";
        stages.Add(new("StitchCandidatePlanning", SurfaceFamilyPressureStageStatus.Succeeded, stitchPlan.Diagnostics, new Dictionary<string, int>
        {
            ["StitchCandidateCount"] = tokenCounts["StitchCandidateCount"],
            ["ReadyStitchCandidateCount"] = tokenCounts["ReadyStitchCandidateCount"]
        }));

        var patchResults = planar.Entries.Where(e => e.Emission is { Success: true, Body: not null }).Select(e => e.Emission!).Concat(cyl is { Success: true, Body: not null } ? [cyl] : []).ToArray();
        var remap = CombinedPatchBodyRemapper.TryCombine(patchResults, maps);
        reached = "CombinedBodyRemap";
        stages.Add(new("CombinedBodyRemap", remap.Success ? SurfaceFamilyPressureStageStatus.Succeeded : SurfaceFamilyPressureStageStatus.Failed, remap.Diagnostics, new Dictionary<string, int>{{"IdentityRemapCount", remap.ReferenceRemaps.Count}}));
        if (!remap.Success)
        {
            blockers.Add(Block("CombinedBodyRemap", "combined-remap-failed", "Failed to build combined patch body.", "Fix remap contracts so emitted patch bodies can be combined."));
            AddRemainingDeferredStages(stages, "combined body unavailable after remap failure.");
            return Final(false, reached, null, blockers, warnings, stages, topoCounts, tokenCounts, edgeUse, false, false, false, false, "step-smoke-skipped-shell-not-closed");
        }

        var exec = SurfaceFamilyStitchExecutor.TryExecute(stitchPlan, planar, cyl);
        tokenCounts["AppliedStitchCount"] = exec.AppliedCandidateCount;
        tokenCounts["DeferredStitchCandidateCount"] = exec.DeferredCandidateCount;
        reached = "SharedEdgeRewrite";
        stages.Add(new("SharedEdgeRewrite", exec.Success ? SurfaceFamilyPressureStageStatus.Succeeded : SurfaceFamilyPressureStageStatus.Deferred, exec.Diagnostics, new Dictionary<string, int>
        {
            ["AppliedStitchCount"] = exec.AppliedCandidateCount,
            ["DeferredStitchCandidateCount"] = exec.DeferredCandidateCount
        }));

        var cleanupPlan = DuplicateEdgeCleanupPlanner.Plan(exec, exec.Body ?? remap.CombinedBody);
        tokenCounts["DuplicateCleanupCandidateCount"] = cleanupPlan.Candidates.Count(c => c.Status == DuplicateEdgeCleanupStatus.CleanupCandidate);
        tokenCounts["DuplicateCleanupDeferredCount"] = cleanupPlan.Candidates.Count(c => c.Status == DuplicateEdgeCleanupStatus.Deferred);
        tokenCounts["BoundaryShellBlockerCount"] = cleanupPlan.BoundaryClassifications.Count(c => c.Classification == BoundaryEdgeClassificationKind.ShellClosureBlocker);
        tokenCounts["BoundaryAmbiguousCount"] = cleanupPlan.BoundaryClassifications.Count(c => c.Classification == BoundaryEdgeClassificationKind.Ambiguous);
        var cleanupStatus = cleanupPlan.Success ? SurfaceFamilyPressureStageStatus.Succeeded : SurfaceFamilyPressureStageStatus.Deferred;
        stages.Add(new("DuplicateEdgeCleanup", cleanupStatus, cleanupPlan.Diagnostics, new Dictionary<string, int>
        {
            ["DuplicateCleanupCandidateCount"] = tokenCounts["DuplicateCleanupCandidateCount"],
            ["DuplicateCleanupDeferredCount"] = tokenCounts["DuplicateCleanupDeferredCount"],
            ["BoundaryShellBlockerCount"] = tokenCounts["BoundaryShellBlockerCount"],
            ["BoundaryAmbiguousCount"] = tokenCounts["BoundaryAmbiguousCount"]
        }));
        if (!cleanupPlan.Success)
        {
            blockers.Add(Block("DuplicateEdgeCleanup", "duplicate-edge-cleanup-planning-blocked", "Duplicate-edge cleanup planning could not classify stitch duplicates safely.", "Provide canonical/duplicate stitch metadata and rerun bounded duplicate-edge planning."));
        }
        else
        {
            blockers.Add(Block("DuplicateEdgeCleanup", "duplicate-edge-cleanup-mutation-deferred", "Duplicate-edge cleanup candidates identified; mutation intentionally deferred in X1.", "Implement bounded remove-unused-duplicate-edge mutation only after topology/binding safety contract is validated."));
        }

        stages.Add(new("VertexMergePlanning", SurfaceFamilyPressureStageStatus.Deferred, ["vertex-merge-needed-analysis-only: no vertex merge mutation in X0.1."], new Dictionary<string, int>()));
        blockers.Add(Block("VertexMergePlanning", "vertex-merge-needed", "Vertex merge planning indicates deferred endpoint consolidation work.", "Define and validate bounded vertex merge contract before shell-closure claims."));

        var body = exec.Body ?? remap.CombinedBody;
        CollectTopologyCounts(body!, topoCounts);
        var validation = ValidateTopology(body!);
        foreach (var kv in validation.Counts) edgeUse[kv.Key] = kv.Value;

        stages.Add(new("LoopClosureValidation", validation.LoopsOk ? SurfaceFamilyPressureStageStatus.Succeeded : SurfaceFamilyPressureStageStatus.Failed, validation.Diagnostics.Where(d => d.StartsWith("loop", StringComparison.Ordinal) || d.StartsWith("face", StringComparison.Ordinal)).ToArray(), new Dictionary<string, int>{{"LoopsWithMissingCoedge", edgeUse["LoopsWithMissingCoedge"]},{"FacesWithMissingLoop", edgeUse["FacesWithMissingLoop"]}}));

        var shellClosed = validation.EdgesWithOneCoedge == 0 && validation.CoedgesWithMissingEdge == 0 && validation.LoopsOk && validation.FacesOk;
        stages.Add(new("ShellClosureValidation", shellClosed ? SurfaceFamilyPressureStageStatus.Succeeded : SurfaceFamilyPressureStageStatus.Failed, validation.Diagnostics, new Dictionary<string, int>(edgeUse)));
        if (!shellClosed)
        {
            blockers.Add(new("ShellClosureValidation", SurfaceFamilyPressureSeverity.Blocking, "shell-not-proven-closed", "Closed shell not proven by bounded edge/coedge validation.", validation.RelatedIds, "Implement bounded duplicate-edge cleanup + vertex merge planning/execution and re-validate shell closure.", validation.Diagnostics));
            if (validation.EdgesWithOneCoedge > 0)
                blockers.Add(new("ShellClosureValidation", SurfaceFamilyPressureSeverity.Blocking, "edges-with-one-coedge", $"Observed {validation.EdgesWithOneCoedge} edge(s) with single coedge use.", validation.EdgeIdsWithOneUse, "Resolve single-use edges via bounded stitching/cleanup before claiming closed shell.", validation.Diagnostics));
        }

        var brepValid = validation.CoedgesWithMissingEdge == 0 && validation.LoopsOk && validation.FacesOk;
        stages.Add(new("BrepBodyValidation", brepValid ? SurfaceFamilyPressureStageStatus.Succeeded : SurfaceFamilyPressureStageStatus.Failed, validation.Diagnostics, new Dictionary<string, int>{{"CoedgesWithMissingEdge", validation.CoedgesWithMissingEdge}}));

        var stepAttempted = false;
        var stepSucceeded = false;
        var stepDiag = "step-smoke-skipped-shell-not-closed";
        if (options.AttemptStepExportSmoke && shellClosed && brepValid)
        {
            stepAttempted = true;
            var export = Step242Exporter.ExportBody(body!);
            stepSucceeded = export.IsSuccess;
            stepDiag = export.IsSuccess ? "step-smoke-attempted-succeeded" : $"step-smoke-attempted-failed:{string.Join("|", export.Diagnostics.Select(d => d.Message))}";
        }
        stages.Add(new("StepExportSmoke", stepAttempted ? (stepSucceeded ? SurfaceFamilyPressureStageStatus.Succeeded : SurfaceFamilyPressureStageStatus.Failed) : SurfaceFamilyPressureStageStatus.Skipped, [stepDiag], new Dictionary<string, int>()));
        if (!stepAttempted) blockers.Add(Block("StepExportSmoke", "step-smoke-skipped-shell-not-closed", "STEP smoke was skipped by safety gate.", "Attempt STEP smoke only after shell closure and body validation both pass."));

        return Final(blockers.All(b => b.Severity is SurfaceFamilyPressureSeverity.Info or SurfaceFamilyPressureSeverity.Warning), "StepExportSmoke", body, blockers.OrderBy(b => b.Stage, StringComparer.Ordinal).ThenBy(b => b.Code, StringComparer.Ordinal).ToArray(), warnings, EnsureAllStages(stages), topoCounts, tokenCounts, edgeUse, shellClosed, brepValid, stepAttempted, stepSucceeded, stepDiag);
    }

    private static SurfaceFamilyPressureTestBlocker Block(string stage, string code, string message, string fix)
        => new(stage, SurfaceFamilyPressureSeverity.Blocking, code, message, [], fix, []);

    private static void AddRemainingDeferredStages(List<SurfaceFamilyPressureStageResult> stages, string reason)
    {
        foreach (var stage in RequiredStages.Where(s => stages.All(x => x.Stage != s))) stages.Add(new(stage, SurfaceFamilyPressureStageStatus.Deferred, [reason], new Dictionary<string, int>()));
    }

    private static IReadOnlyList<SurfaceFamilyPressureStageResult> EnsureAllStages(List<SurfaceFamilyPressureStageResult> stages)
    {
        AddRemainingDeferredStages(stages, "stage not reached in bounded run.");
        return stages.OrderBy(s => Array.IndexOf(RequiredStages, s.Stage)).ToArray();
    }

    private static void CollectTopologyCounts(BrepBody body, IDictionary<string, int> counts)
    {
        counts["FaceCount"] = body.Topology.Faces.Count();
        counts["LoopCount"] = body.Topology.Loops.Count();
        counts["EdgeCount"] = body.Topology.Edges.Count();
        counts["CoedgeCount"] = body.Topology.Coedges.Count();
        counts["VertexCount"] = body.Topology.Vertices.Count();
        counts["ShellCount"] = body.Topology.Shells.Count();
        counts["CurveBindingCount"] = body.Bindings.EdgeBindings.Count();
        counts["SurfaceBindingCount"] = body.Bindings.FaceBindings.Count();
    }

    private static (bool LoopsOk, bool FacesOk, int EdgesWithOneCoedge, int CoedgesWithMissingEdge, Dictionary<string,int> Counts, List<string> Diagnostics, List<string> RelatedIds, List<string> EdgeIdsWithOneUse) ValidateTopology(BrepBody body)
    {
        var d = new List<string>();
        var related = new List<string>();
        var edgeUse = body.Topology.Edges.OrderBy(e => e.Id.Value).Select(e => new { e.Id, Uses = body.Topology.Coedges.Count(c => c.EdgeId == e.Id) }).ToArray();
        var zero = edgeUse.Where(x => x.Uses == 0).Select(x => x.Id.ToString()!).ToArray();
        var one = edgeUse.Where(x => x.Uses == 1).Select(x => x.Id.ToString()!).ToArray();
        var two = edgeUse.Count(x => x.Uses == 2);
        var more = edgeUse.Where(x => x.Uses > 2).Select(x => x.Id.ToString()!).ToArray();

        var missingEdgeCoedges = body.Topology.Coedges.Where(c => !body.Topology.TryGetEdge(c.EdgeId, out _)).Select(c => c.Id.ToString()!).ToArray();
        var loopsMissing = body.Topology.Loops.Where(l => l.CoedgeIds.Any(cid => !body.Topology.TryGetCoedge(cid, out _))).Select(l => l.Id.ToString()!).ToArray();
        var facesMissing = body.Topology.Faces.Where(f => f.LoopIds.Any(lid => !body.Topology.TryGetLoop(lid, out _))).Select(f => f.Id.ToString()!).ToArray();

        if (one.Length > 0) d.Add($"edges-with-one-coedge:count={one.Length};sample={string.Join(",", one.Take(5))}");
        if (zero.Length > 0) d.Add($"edges-with-zero-coedges:count={zero.Length};sample={string.Join(",", zero.Take(5))}");
        if (more.Length > 0) d.Add($"edges-with-more-than-two-coedges:count={more.Length};sample={string.Join(",", more.Take(5))}");
        if (missingEdgeCoedges.Length > 0) d.Add($"coedges-with-missing-edge:count={missingEdgeCoedges.Length};sample={string.Join(",", missingEdgeCoedges.Take(5))}");
        if (loopsMissing.Length > 0) d.Add($"loops-with-missing-coedge:count={loopsMissing.Length};sample={string.Join(",", loopsMissing.Take(5))}");
        if (facesMissing.Length > 0) d.Add($"faces-with-missing-loop:count={facesMissing.Length};sample={string.Join(",", facesMissing.Take(5))}");

        related.AddRange(one.Take(5)); related.AddRange(zero.Take(5)); related.AddRange(missingEdgeCoedges.Take(5));
        var counts = new Dictionary<string, int>
        {
            ["EdgesWithZeroCoedges"] = zero.Length,
            ["EdgesWithOneCoedge"] = one.Length,
            ["EdgesWithTwoCoedges"] = two,
            ["EdgesWithMoreThanTwoCoedges"] = more.Length,
            ["CoedgesWithMissingEdge"] = missingEdgeCoedges.Length,
            ["LoopsWithMissingCoedge"] = loopsMissing.Length,
            ["FacesWithMissingLoop"] = facesMissing.Length
        };

        return (loopsMissing.Length == 0, facesMissing.Length == 0, one.Length, missingEdgeCoedges.Length, counts, d, related.Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToList(), one.Take(5).ToList());
    }

    private static SurfaceFamilyBoxCylinderPressureTestResult Final(bool success, string reached, BrepBody? body, IReadOnlyList<SurfaceFamilyPressureTestBlocker> blockers, IReadOnlyList<string> warnings, IReadOnlyList<SurfaceFamilyPressureStageResult> stages, IReadOnlyDictionary<string, int> topologyCounts, IReadOnlyDictionary<string, int> tokenCounts, IReadOnlyDictionary<string, int> edgeUseCounts, bool shellClosed, bool bodyValid, bool stepAttempted, bool stepSuccess, string stepDiag)
        => new(success, reached, body, blockers, warnings, stages, topologyCounts, tokenCounts, edgeUseCounts, shellClosed, bodyValid, stepAttempted, stepSuccess, stepDiag, false, false);
}
