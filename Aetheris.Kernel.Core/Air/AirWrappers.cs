using Aetheris.Kernel.Core.Brep.Prismatic;

namespace Aetheris.Kernel.Core.Air;

internal static class AirPrismaticSectionTransitionWrapper
{
    public static AirLoweringSummary LowerCanonicalRectangleInset()
    {
        var diagnostics = new List<AirDiagnostic> { D("air-x1-prismatic-wrapper-created") };
        var sections = CanonicalSections();
        diagnostics.Add(D("air-x1-prismatic-existing-emitter-invoked"));
        var result = PrismaticSectionTransitionEmitter.Emit(new PrismaticSectionTransitionRequest(sections, PrismaticCorrespondenceMap.Identity(4), new PrismaticSectionTransitionOptions(RunStepSmoke: true, TraceLabel: "air-x1-prismatic-section-transition")));
        diagnostics.Add(D("air-x1-prismatic-split-preserving"));
        diagnostics.Add(D("air-x1-prismatic-summary-created"));
        diagnostics.Add(D("air-x1-no-production-route-replacement"));
        diagnostics.AddRange(result.Diagnostics.Select(code => D(code)));
        return new AirLoweringSummary(AirNodeKind.PrismaticSectionTransition, AirRouteKind.PrismaticSectionTransitionEmitter, result.Succeeded, result.Recommendation, Provenance(), Map(result.Topology), Map(result.Step), Stable(diagnostics), ["no production route replacement", "split-preserving prismatic section topology", "no coplanar merge"]);
    }

    internal static IReadOnlyList<PrismaticSection> CanonicalSections() =>
    [
        new(0, [(-5, -4), (5, -4), (5, 4), (-5, 4)]),
        new(5, [(-5, -4), (5, -4), (5, 4), (-5, 4)]),
        new(6, [(-4, -3), (4, -3), (4, 3), (-4, 3)]),
    ];

    private static AirProvenance Provenance() => new("AIR-X1", "Constructive AIR wrapper", "canonical-rectangle-inset-section-transition", "air-x1-prismatic-canonical", nameof(PrismaticSectionTransitionEmitter), AirSelectionClass.None, AirRuleKind.None, "generated/constructive; split policy: preserve section splits", false, ["Wraps an existing internal emitter; AIR is not a production route replacement."]);

    internal static AirTopologySummary Map(PrismaticTransitionTopologySummary t) => new(t.VertexCount, t.EdgeCount, t.FaceCount, t.PlanarFaceCount, t.CylindricalFaceCount, t.LoopCount, t.CoedgeCount, t.CapFaceCount, SideFaceCount: t.StableIntervalFaceCount, TransitionFaceCount: t.ChangedIntervalFaceCount, Bounds: t.Bounds, SectionCount: t.SectionCount);
    internal static AirStepSmokeSummary Map(PrismaticSectionTransitionStepSummary s) => new(s.Exported, s.Exported && s.MissingRequiredMarkers.Count == 0 && s.UnexpectedPresentMarkers.Count == 0, s.Exported && s.MissingRequiredMarkers.Count == 0, s.Exported && s.UnexpectedPresentMarkers.Count == 0, s.MissingRequiredMarkers.Concat(s.UnexpectedPresentMarkers).Select(x => D($"air-x1-step-smoke-marker:{x}")).ToArray());
    internal static AirDiagnostic D(string code) => new(code, AirDiagnosticSeverity.Info, code);
    internal static IReadOnlyList<AirDiagnostic> Stable(IEnumerable<AirDiagnostic> d) => d.GroupBy(x => x.Code).Select(g => g.First()).OrderBy(x => x.Code, StringComparer.Ordinal).ToArray();
}

internal static class AirTopFaceLoopChamferWrapper
{
    public static AirLoweringSummary LowerCanonicalTopFaceLoopChamfer()
    {
        var diagnostics = new List<AirDiagnostic> { D("air-x1-top-face-loop-chamfer-wrapper-created"), D("air-x1-class-b-face-boundary-loop"), D("air-x1-uniform-chamfer-rule"), D("air-x1-top-face-loop-chamfer-existing-prototype-invoked") };
        var result = PrismaticTopFaceLoopChamferPrototype.Emit(new PrismaticTopFaceLoopChamferRequest(10, 8, 6, 1, ExportStep: true));
        diagnostics.AddRange([D("air-x1-top-face-loop-chamfer-summary-created"), D("air-x1-not-four-independent-single-edge-chamfers"), D("air-x1-no-production-route-replacement"), D("air-x1-no-air-edge-sweep-used"), D("air-x1-no-brep-bounded-chamfer-used"), D("air-x1-no-topology-graft-used"), D("air-x1-no-3d-boolean-used"), D("air-x1-no-coplanar-merge-used")]);
        diagnostics.AddRange(result.Diagnostics.Select(code => D(code)));
        return new AirLoweringSummary(AirNodeKind.TopFaceLoopChamfer, AirRouteKind.TopFaceLoopChamferPrismatic, result.Succeeded, result.Recommendation, Provenance(), Map(result.Topology), Map(result.Step), AirPrismaticSectionTransitionWrapper.Stable(diagnostics), ["no production route replacement", "no AirEdgeSweep", "no BrepBoundedChamfer", "no topology graft", "no 3D Boolean", "no coplanar merge", "not four independent single-edge chamfers"]);
    }
    private static AirProvenance Provenance() => new("AIR-X1", "Constructive AIR wrapper", "canonical-top-face-loop-chamfer", "air-x1-top-face-loop-chamfer-canonical", "TopFaceLoopChamferPrismatic", AirSelectionClass.FaceBoundaryLoop, AirRuleKind.UniformChamfer, "generated/history-known", false, ["Class B face-boundary loop operation lowered through PrismaticSectionTransitionEmitter."]);
    private static AirTopologySummary Map(FaceLoopChamferTopologySummary t) => new(t.VertexCount, t.EdgeCount, t.FaceCount, t.PlanarFaceCount, t.CylindricalFaceCount, t.LoopCount, t.CoedgeCount, t.CapFaceCount, t.LowerPrismSideFaceCount, t.TransitionFaceCount, t.ChamferTransitionFaceCount, t.Bounds, t.SectionCount);
    private static AirStepSmokeSummary Map(FaceLoopChamferStepSummary s) => new(s.Exported, s.Exported && s.MissingRequiredMarkers.Count == 0 && s.UnexpectedPresentMarkers.Count == 0, s.Exported && s.MissingRequiredMarkers.Count == 0, s.Exported && s.UnexpectedPresentMarkers.Count == 0, s.MissingRequiredMarkers.Concat(s.UnexpectedPresentMarkers).Select(x => D($"air-x1-step-smoke-marker:{x}")).ToArray());
    private static AirDiagnostic D(string code) => AirPrismaticSectionTransitionWrapper.D(code);
}
