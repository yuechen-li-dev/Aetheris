using Aetheris.Kernel.Core.Cir;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum TopologyAssemblyReadiness
{
    NotApplicable,
    ExactPlanReady,
    SpecialCasePlanReady,
    Deferred,
    Unsupported
}

internal sealed record PlannedLoop(
    RetainedRegionLoopGroupKind GroupKind,
    TopologyAssemblyReadiness Readiness,
    RetainedRegionLoopOrientationPolicy OrientationPolicy,
    string OrderingKey,
    IReadOnlyList<RetainedRegionLoopDescriptor> LoopDescriptors,
    IReadOnlyList<string> Diagnostics);

internal sealed record PlannedFacePatch(
    FacePatchCandidate SourceCandidate,
    SurfacePatchFamily SurfaceFamily,
    FacePatchRetentionRole RetentionRole,
    RetainedRegionLoopOrientationPolicy OrientationPolicy,
    IReadOnlyList<PlannedLoop> LoopGroups,
    TopologyAssemblyReadiness Readiness,
    string OrderingKey,
    IReadOnlyList<string> Diagnostics);

internal sealed record PlannedAdjacency(
    string SourceFaceA,
    string SourceFaceB,
    TrimCurveFamily TrimCurveFamily,
    TopologyAssemblyReadiness Readiness,
    string Diagnostic);

internal sealed record TopologyAssemblyDryRunResult(
    bool IsSuccess,
    TopologyAssemblyReadiness Readiness,
    IReadOnlyList<PlannedFacePatch> PlannedFaces,
    IReadOnlyList<PlannedAdjacency> PlannedAdjacencies,
    IReadOnlyList<string> Diagnostics,
    bool TopologyEmissionImplemented);

internal static class TopologyAssemblyDryRunPlanner
{
    internal static TopologyAssemblyDryRunResult Generate(CirNode root)
        => Generate(FacePatchCandidateGenerator.Generate(root));

    internal static TopologyAssemblyDryRunResult Generate(FacePatchCandidateGenerationResult candidateResult)
    {
        var diagnostics = new List<string>
        {
            "topology-emission-not-implemented: this dry-run only plans descriptors; no BRep face/loop/edge/coedge/vertex/shell entities are created."
        };

        if (!candidateResult.Candidates.Any(c => c.RetentionRole != FacePatchRetentionRole.NotApplicable))
        {
            diagnostics.Add("topology-plan-not-applicable: non-subtract candidate set does not produce subtract topology planning contracts.");
            return new TopologyAssemblyDryRunResult(false, TopologyAssemblyReadiness.NotApplicable, [], [], diagnostics, TopologyEmissionImplemented: false);
        }

        var plannedFaces = candidateResult.Candidates
            .Where(c => c.RetentionRole is FacePatchRetentionRole.BaseBoundaryRetainedOutsideTool or FacePatchRetentionRole.ToolBoundaryRetainedInsideBase)
            .Select(PlanFace)
            .OrderBy(p => p.OrderingKey, StringComparer.Ordinal)
            .ToArray();

        var plannedAdjacencies = BuildAdjacencyHints(plannedFaces, diagnostics);
        diagnostics.AddRange(plannedFaces.SelectMany(f => f.SourceCandidate.Diagnostics));
        diagnostics.AddRange(plannedFaces.SelectMany(f => f.Diagnostics));

        var readiness = ReduceReadiness(plannedFaces.Select(f => f.Readiness).Concat(plannedAdjacencies.Select(a => a.Readiness)).ToArray());
        return new TopologyAssemblyDryRunResult(plannedFaces.Length > 0, readiness, plannedFaces, plannedAdjacencies, diagnostics.Distinct().ToArray(), TopologyEmissionImplemented: false);
    }

    private static PlannedFacePatch PlanFace(FacePatchCandidate candidate)
    {
        var loops = candidate.RetainedRegionLoopGroups
            .Select(g => new PlannedLoop(g.GroupKind, MapLoopReadiness(g.Readiness), g.OrientationPolicy, g.OrderingKey, g.Loops, g.Diagnostics))
            .OrderBy(g => g.OrderingKey, StringComparer.Ordinal)
            .ToArray();

        var faceReadiness = loops.Length == 0
            ? TopologyAssemblyReadiness.Deferred
            : ReduceReadiness(loops.Select(l => l.Readiness).ToArray());

        var diags = new List<string>
        {
            faceReadiness switch
            {
                TopologyAssemblyReadiness.ExactPlanReady => "planned-face-exact-ready: loop groups support exact dry-run face planning; loop closure and parameter solving remain deferred.",
                TopologyAssemblyReadiness.SpecialCasePlanReady => "planned-face-special-case-ready: loop groups support special-case dry-run planning with constrained trim policies.",
                TopologyAssemblyReadiness.Deferred => "planned-face-deferred: one or more loop groups are deferred; loop closure has not been proven.",
                TopologyAssemblyReadiness.Unsupported => "planned-face-unsupported: one or more loop groups are unsupported for topology planning.",
                _ => "planned-face-not-applicable"
            }
        };
        diags.Add("loop-closure-not-proven: dry-run planning carries loop grouping/orientation only.");

        var orientation = loops.FirstOrDefault()?.OrientationPolicy ?? RetainedRegionLoopOrientationPolicy.Deferred;
        var key = $"{candidate.SourceSurface.Family}|{candidate.RetentionRole}|{faceReadiness}|{orientation}|{loops.Length}";
        return new PlannedFacePatch(candidate, candidate.SourceSurface.Family, candidate.RetentionRole, orientation, loops, faceReadiness, key, diags);
    }

    private static PlannedAdjacency[] BuildAdjacencyHints(IReadOnlyList<PlannedFacePatch> faces, List<string> diagnostics)
    {
        var list = new List<PlannedAdjacency>();
        for (var i = 0; i < faces.Count; i++)
        for (var j = i + 1; j < faces.Count; j++)
        {
            var shared = faces[i].LoopGroups.SelectMany(l => l.LoopDescriptors)
                .Select(l => l.TrimCurveFamily)
                .Intersect(faces[j].LoopGroups.SelectMany(l => l.LoopDescriptors).Select(l => l.TrimCurveFamily))
                .Where(f => f != TrimCurveFamily.Unsupported)
                .Distinct()
                .ToArray();

            if (shared.Length == 0)
            {
                diagnostics.Add($"adjacency-inference-deferred: {faces[i].SurfaceFamily} and {faces[j].SurfaceFamily} do not share a trim curve family hint.");
                continue;
            }

            foreach (var family in shared)
            {
                list.Add(new PlannedAdjacency(
                    faces[i].OrderingKey,
                    faces[j].OrderingKey,
                    family,
                    ReduceReadiness([faces[i].Readiness, faces[j].Readiness]),
                    "adjacency-hint-ready: shared trim curve family found; edge/coedge and closure solving remain deferred."));
            }
        }

        if (list.Count == 0)
        {
            diagnostics.Add("adjacency-inference-deferred: no deterministic shared trim-family pairing was available for planned faces.");
        }

        return list.OrderBy(a => a.SourceFaceA, StringComparer.Ordinal).ThenBy(a => a.SourceFaceB, StringComparer.Ordinal).ThenBy(a => a.TrimCurveFamily).ToArray();
    }

    private static TopologyAssemblyReadiness MapLoopReadiness(RetainedRegionLoopGroupReadiness readiness)
        => readiness switch
        {
            RetainedRegionLoopGroupReadiness.ExactReady => TopologyAssemblyReadiness.ExactPlanReady,
            RetainedRegionLoopGroupReadiness.SpecialCaseReady => TopologyAssemblyReadiness.SpecialCasePlanReady,
            RetainedRegionLoopGroupReadiness.Deferred => TopologyAssemblyReadiness.Deferred,
            RetainedRegionLoopGroupReadiness.Unsupported => TopologyAssemblyReadiness.Unsupported,
            _ => TopologyAssemblyReadiness.NotApplicable
        };

    private static TopologyAssemblyReadiness ReduceReadiness(IReadOnlyList<TopologyAssemblyReadiness> readiness)
    {
        if (readiness.Count == 0) return TopologyAssemblyReadiness.NotApplicable;
        if (readiness.Any(r => r == TopologyAssemblyReadiness.Unsupported)) return TopologyAssemblyReadiness.Unsupported;
        if (readiness.Any(r => r == TopologyAssemblyReadiness.Deferred)) return TopologyAssemblyReadiness.Deferred;
        if (readiness.Any(r => r == TopologyAssemblyReadiness.SpecialCasePlanReady)) return TopologyAssemblyReadiness.SpecialCasePlanReady;
        if (readiness.All(r => r == TopologyAssemblyReadiness.ExactPlanReady)) return TopologyAssemblyReadiness.ExactPlanReady;
        return TopologyAssemblyReadiness.NotApplicable;
    }
}
