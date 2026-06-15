using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Air.BRepPlan;
using Aetheris.Kernel.Core.Brep.Prismatic;

namespace Aetheris.Kernel.Core.Tests.Air;

public sealed class AirBRepPlanTests
{
    [Fact]
    public void AirPrismaticSectionTransitionBRepPlanner_CanonicalStack_ProducesExpectedPlan()
    {
        var result = AirPrismaticSectionTransitionBRepPlanner.Plan(CanonicalRequest());
        Assert.True(result.Succeeded);
        var plan = Assert.IsType<AirBRepPlan>(result.Plan);
        Assert.Equal(AirBRepPlanKind.PrismaticSectionTransition, plan.PlanKind);
        Assert.Equal(12, plan.Summary.VertexCount);
        Assert.Equal(20, plan.Summary.EdgeCount);
        Assert.Equal(20, plan.Summary.CurveCount);
        Assert.Equal(10, plan.Summary.FaceCount);
        Assert.Equal(10, plan.Summary.SurfaceCount);
        Assert.Equal(10, plan.Summary.LoopCount);
        Assert.Equal(40, plan.Summary.CoedgeCount);
        Assert.Equal(2, plan.Summary.CapFaceCount);
        Assert.Equal(4, plan.Summary.SideFaceCount);
        Assert.Equal(4, plan.Summary.TransitionFaceCount);
        Assert.Equal(0, plan.Summary.ChamferFaceCount);
        Assert.Equal(1, plan.Summary.ShellCount);
        Assert.Equal(1, plan.Summary.BodyCount);
        Assert.Equal("[-5,-4,0]..[5,4,6]", plan.Summary.Bounds);
        Assert.Equal(AirPrismaticSectionTransitionBRepPlanner.PreserveSectionSplits, plan.Summary.SplitPolicy);
        foreach (var code in new[] { "air-x3-prismatic-plan-created", "air-x3-stable-planned-ids-created", "air-x3-section-vertices-planned", "air-x3-section-edges-planned", "air-x3-transition-edges-planned", "air-x3-cap-faces-planned", "air-x3-transition-faces-planned", "air-x3-shell-planned", "air-x3-body-planned", "air-x3-split-preserving-plan" })
            Assert.Contains(plan.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void AirPrismaticSectionTransitionBRepPlanner_StableIds_AreDeterministic()
    {
        var a = AirPrismaticSectionTransitionBRepPlanner.Plan(CanonicalRequest()).Plan!;
        var b = AirPrismaticSectionTransitionBRepPlanner.Plan(CanonicalRequest()).Plan!;
        Assert.Equal(a.Elements.Select(e => e.Id.Value), b.Elements.Select(e => e.Id.Value));
        Assert.Equal(a.Elements.Select(e => (e.Kind, e.Role)), b.Elements.Select(e => (e.Kind, e.Role)));
        Assert.Equal((a.Summary.PlanKind, a.Summary.SourceAirNodeId, a.Summary.VertexCount, a.Summary.CurveCount, a.Summary.EdgeCount, a.Summary.CoedgeCount, a.Summary.LoopCount, a.Summary.SurfaceCount, a.Summary.FaceCount, a.Summary.ShellCount, a.Summary.BodyCount, a.Summary.CapFaceCount, a.Summary.SideFaceCount, a.Summary.TransitionFaceCount, a.Summary.ChamferFaceCount, a.Summary.Bounds, a.Summary.SplitPolicy), (b.Summary.PlanKind, b.Summary.SourceAirNodeId, b.Summary.VertexCount, b.Summary.CurveCount, b.Summary.EdgeCount, b.Summary.CoedgeCount, b.Summary.LoopCount, b.Summary.SurfaceCount, b.Summary.FaceCount, b.Summary.ShellCount, b.Summary.BodyCount, b.Summary.CapFaceCount, b.Summary.SideFaceCount, b.Summary.TransitionFaceCount, b.Summary.ChamferFaceCount, b.Summary.Bounds, b.Summary.SplitPolicy));
        Assert.Equal(a.Summary.Diagnostics.Select(d => d.Code), b.Summary.Diagnostics.Select(d => d.Code));
        Assert.Equal(a.Summary.Guarantees, b.Summary.Guarantees);
        Assert.Equal(a.Diagnostics.Select(d => d.Code), b.Diagnostics.Select(d => d.Code));
    }

    [Fact]
    public void AirPrismaticSectionTransitionBRepPlanner_PlanMatchesExistingEmitterSummary()
    {
        var request = CanonicalRequest(runStepSmoke: true);
        var plan = AirPrismaticSectionTransitionBRepPlanner.Plan(request).Plan!;
        var emitted = PrismaticSectionTransitionEmitter.Emit(request);
        Assert.True(emitted.Succeeded);
        Assert.Equal(emitted.Topology.VertexCount, plan.Summary.VertexCount);
        Assert.Equal(emitted.Topology.EdgeCount, plan.Summary.EdgeCount);
        Assert.Equal(emitted.Topology.FaceCount, plan.Summary.FaceCount);
        Assert.Equal(emitted.Topology.PlanarFaceCount, plan.Summary.SurfaceCount);
        Assert.Equal(emitted.Topology.LoopCount, plan.Summary.LoopCount);
        Assert.Equal(emitted.Topology.CoedgeCount, plan.Summary.CoedgeCount);
        Assert.Equal(emitted.Topology.CapFaceCount, plan.Summary.CapFaceCount);
        Assert.Equal(emitted.Topology.StableIntervalFaceCount, plan.Summary.SideFaceCount);
        Assert.Equal(emitted.Topology.ChangedIntervalFaceCount, plan.Summary.TransitionFaceCount);
        Assert.Equal(emitted.Topology.Bounds, plan.Summary.Bounds);
        Assert.Equal(AirPrismaticSectionTransitionBRepPlanner.PreserveSectionSplits, plan.Summary.SplitPolicy);
        Assert.Contains(plan.Guarantees, g => g == "no production route replacement");
        Assert.Contains(plan.Guarantees, g => g == "no emitter rewrite");
        Assert.True(emitted.Step.Exported);
        Assert.Empty(emitted.Step.MissingRequiredMarkers);
        Assert.Empty(emitted.Step.UnexpectedPresentMarkers);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void AirPrismaticSectionTransitionBRepPlanner_InvalidCases_AreRejectedOrDeferred(object requestObject, string expectedCode)
    {
        var request = Assert.IsType<PrismaticSectionTransitionRequest>(requestObject);
        var result = AirPrismaticSectionTransitionBRepPlanner.Plan(request);
        Assert.False(result.Succeeded);
        Assert.Null(result.Plan);
        Assert.Contains(result.Validation.Diagnostics, d => d.Code == expectedCode);
        Assert.Equal(AirBRepPlanKind.Unsupported, result.Validation.ExpectedTopologySummary.PlanKind);
    }


    [Fact]
    public void AirTopFaceLoopChamferBRepPlanner_CanonicalCase_PreservesClassBProvenance()
    {
        var result = AirTopFaceLoopChamferBRepPlanner.Plan(CanonicalTopFaceLoopChamferRequest());
        Assert.True(result.Succeeded);
        var plan = result.Plan!;
        Assert.Equal(AirNodeKind.TopFaceLoopChamfer, plan.FeatureContext!.SourceNodeKind);
        Assert.Equal(AirRouteKind.TopFaceLoopChamferPrismatic, plan.FeatureContext.RouteKind);
        Assert.Equal(AirSelectionClass.FaceBoundaryLoop, plan.FeatureContext.SelectionClass);
        Assert.Equal(AirRuleKind.UniformChamfer, plan.FeatureContext.RuleKind);
        Assert.Equal("SwitchMatch", plan.FeatureContext.RouteSelectionMode);
        Assert.Contains(plan.Diagnostics, d => d.Code == "air-x4-class-b-face-boundary-loop-provenance");
        Assert.Contains(plan.Diagnostics, d => d.Code == "air-x4-uniform-chamfer-rule-provenance");
        Assert.Contains(plan.Diagnostics, d => d.Code == "air-x4-not-four-independent-single-edge-chamfers");
    }

    [Fact]
    public void AirTopFaceLoopChamferBRepPlanner_CanonicalCase_MarksUpperTransitionFacesAsChamferFaces()
    {
        var plan = AirTopFaceLoopChamferBRepPlanner.Plan(CanonicalTopFaceLoopChamferRequest()).Plan!;
        Assert.Equal(10, plan.Summary.FaceCount);
        Assert.Equal(4, plan.Summary.TransitionFaceCount);
        Assert.Equal(4, plan.Summary.ChamferFaceCount);
        Assert.Equal(2, plan.Summary.CapFaceCount);
        Assert.Equal(4, plan.Summary.SideFaceCount);
        Assert.Equal(10, plan.Summary.SurfaceCount);
        Assert.Equal(AirPrismaticSectionTransitionBRepPlanner.PreserveSectionSplits, plan.Summary.SplitPolicy);
        var upperTransitionFaces = plan.Elements.Where(e => e.Kind == AirBRepPlanElementKind.Face && e.IntervalIndex == 1).ToArray();
        Assert.Equal(4, upperTransitionFaces.Length);
        Assert.All(upperTransitionFaces, f => Assert.Contains(AirBRepPlanRole.PrismaticTransitionFace, f.SemanticRoles!));
        Assert.All(upperTransitionFaces, f => Assert.Contains(AirBRepPlanRole.ChamferFace, f.SemanticRoles!));
    }

    [Fact]
    public void AirTopFaceLoopChamferBRepPlanner_StableIdsAndRoles_AreDeterministic()
    {
        var a = AirTopFaceLoopChamferBRepPlanner.Plan(CanonicalTopFaceLoopChamferRequest()).Plan!;
        var b = AirTopFaceLoopChamferBRepPlanner.Plan(CanonicalTopFaceLoopChamferRequest()).Plan!;
        Assert.Equal(a.Elements.Select(e => e.Id.Value), b.Elements.Select(e => e.Id.Value));
        Assert.Equal(a.Elements.Select(e => (e.Kind, e.Role)), b.Elements.Select(e => (e.Kind, e.Role)));
        Assert.Equal(a.Elements.Select(e => string.Join(",", e.SemanticRoles ?? [])), b.Elements.Select(e => string.Join(",", e.SemanticRoles ?? [])));
        Assert.Equal((a.Summary.PlanKind, a.Summary.SourceAirNodeId, a.Summary.VertexCount, a.Summary.CurveCount, a.Summary.EdgeCount, a.Summary.CoedgeCount, a.Summary.LoopCount, a.Summary.SurfaceCount, a.Summary.FaceCount, a.Summary.ShellCount, a.Summary.BodyCount, a.Summary.CapFaceCount, a.Summary.SideFaceCount, a.Summary.TransitionFaceCount, a.Summary.ChamferFaceCount, a.Summary.Bounds, a.Summary.SplitPolicy), (b.Summary.PlanKind, b.Summary.SourceAirNodeId, b.Summary.VertexCount, b.Summary.CurveCount, b.Summary.EdgeCount, b.Summary.CoedgeCount, b.Summary.LoopCount, b.Summary.SurfaceCount, b.Summary.FaceCount, b.Summary.ShellCount, b.Summary.BodyCount, b.Summary.CapFaceCount, b.Summary.SideFaceCount, b.Summary.TransitionFaceCount, b.Summary.ChamferFaceCount, b.Summary.Bounds, b.Summary.SplitPolicy));
        Assert.Equal(a.Summary.Diagnostics.Select(d => d.Code), b.Summary.Diagnostics.Select(d => d.Code));
        Assert.Equal(a.Summary.Guarantees, b.Summary.Guarantees);
        Assert.Equal(a.Diagnostics.Select(d => d.Code), b.Diagnostics.Select(d => d.Code));
        Assert.Equal(a.Guarantees, b.Guarantees);
    }

    [Fact]
    public void AirTopFaceLoopChamferBRepPlanner_PlanMatchesExistingLoopChamferSummary()
    {
        var request = CanonicalTopFaceLoopChamferRequest(exportStep: true);
        var plan = AirTopFaceLoopChamferBRepPlanner.Plan(request).Plan!;
        var emitted = AirTopFaceLoopChamferWrapper.LowerCanonicalTopFaceLoopChamfer();
        Assert.True(emitted.Succeeded);
        Assert.Equal(emitted.TopologySummary.VertexCount, plan.Summary.VertexCount);
        Assert.Equal(emitted.TopologySummary.EdgeCount, plan.Summary.EdgeCount);
        Assert.Equal(emitted.TopologySummary.FaceCount, plan.Summary.FaceCount);
        Assert.Equal(emitted.TopologySummary.PlanarFaceCount, plan.Summary.SurfaceCount);
        Assert.Equal(emitted.TopologySummary.LoopCount, plan.Summary.LoopCount);
        Assert.Equal(emitted.TopologySummary.CoedgeCount, plan.Summary.CoedgeCount);
        Assert.Equal(emitted.TopologySummary.CapFaceCount, plan.Summary.CapFaceCount);
        Assert.Equal(emitted.TopologySummary.SideFaceCount, plan.Summary.SideFaceCount);
        Assert.Equal(emitted.TopologySummary.TransitionFaceCount, plan.Summary.TransitionFaceCount);
        Assert.Equal(emitted.TopologySummary.ChamferFaceCount, plan.Summary.ChamferFaceCount);
        Assert.Equal(emitted.TopologySummary.Bounds, plan.Summary.Bounds);
        Assert.True(emitted.StepSmokeSummary.Succeeded);
        foreach (var guarantee in new[] { "no production route replacement", "no emitter rewrite", "no STEP exporter change", "no BRep topology behavior change", "no coplanar merge", "no AirEdgeSweep", "no BrepBoundedChamfer", "no Boolean", "no topology graft", "not four independent single-edge chamfers" })
            Assert.Contains(plan.Guarantees, g => g == guarantee);
    }

    [Theory]
    [MemberData(nameof(InvalidTopFaceLoopChamferContexts))]
    public void AirTopFaceLoopChamferBRepPlanner_InvalidFeatureContext_RejectedOrDeferred(object contextObject, string expectedCode)
    {
        var context = Assert.IsType<AirBRepPlanFeatureContext>(contextObject);
        var result = AirTopFaceLoopChamferBRepPlanner.Plan(CanonicalTopFaceLoopChamferRequest(), context);
        Assert.False(result.Succeeded);
        Assert.Null(result.Plan);
        Assert.Contains(result.Validation.Diagnostics, d => d.Code == expectedCode);
    }

    [Fact]
    public void AirPrismaticSectionTransitionBRepPlanner_PurePrismatic_DoesNotClaimChamferFaces()
    {
        var plan = AirPrismaticSectionTransitionBRepPlanner.Plan(CanonicalRequest()).Plan!;
        Assert.Equal(0, plan.Summary.ChamferFaceCount);
        Assert.Equal(4, plan.Summary.TransitionFaceCount);
        Assert.DoesNotContain(plan.Elements, e => (e.SemanticRoles ?? []).Contains(AirBRepPlanRole.ChamferFace));
    }

    public static IEnumerable<object[]> InvalidTopFaceLoopChamferContexts()
    {
        var canonical = AirTopFaceLoopChamferBRepPlanner.CanonicalFeatureContext();
        yield return [canonical with { SelectionClass = AirSelectionClass.SingleEdge }, "air-x4-non-face-boundary-loop-rejected"];
        yield return [canonical with { RuleKind = AirRuleKind.ConstantRadiusFillet }, "air-x4-non-uniform-chamfer-rule-rejected"];
        yield return [canonical with { RouteKind = AirRouteKind.PrismaticSectionTransitionEmitter }, "air-x4-non-prismatic-lowering-deferred"];
        yield return [canonical with { SourceNodeKind = AirNodeKind.PrismaticSectionTransition }, "air-x4-missing-loop-chamfer-provenance-rejected"];
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return [new PrismaticSectionTransitionRequest([new(0, [(-1d, -1d), (1d, -1d), (1d, 1d)])], PrismaticCorrespondenceMap.Identity(3)), "air-x3-invalid-section-count-rejected"];
        yield return [new PrismaticSectionTransitionRequest([new(0, [(-1d, -1d), (1d, -1d), (1d, 1d)]), new(1, [(-1d, -1d), (1d, -1d), (1d, 1d), (-1d, 1d)]), new(2, [(-1d, -1d), (1d, -1d), (1d, 1d)])], PrismaticCorrespondenceMap.Identity(3)), "air-x3-mismatched-vertex-count-rejected"];
        yield return [new PrismaticSectionTransitionRequest([new(0, [(-1d, -1d), (1d, -1d), (1d, 1d)]), new(0, [(-1d, -1d), (1d, -1d), (1d, 1d)]), new(2, [(-1d, -1d), (1d, -1d), (1d, 1d)])], PrismaticCorrespondenceMap.Identity(3)), "air-x3-non-increasing-sections-rejected"];
        yield return [new PrismaticSectionTransitionRequest([new(0, [(-1d, -1d), (1d, -1d), (1d, 1d)]), new(1, [(double.NaN, -1d), (1d, -1d), (1d, 1d)]), new(2, [(-1d, -1d), (1d, -1d), (1d, 1d)])], PrismaticCorrespondenceMap.Identity(3)), "air-x3-non-finite-coordinate-rejected"];
        yield return [new PrismaticSectionTransitionRequest([new(0, [(-1d, -1d), (1d, -1d), (1d, 1d)], HasHoles: true), new(1, [(-1d, -1d), (1d, -1d), (1d, 1d)]), new(2, [(-1d, -1d), (1d, -1d), (1d, 1d)])], PrismaticCorrespondenceMap.Identity(3)), "air-x3-holes-deferred"];
        yield return [new PrismaticSectionTransitionRequest([new(0, [(-1d, -1d), (1d, -1d), (1d, 1d)]), new(1, [(-1d, -1d), (1d, -1d), (1d, 1d)]), new(2, [(-1d, -1d), (1d, -1d), (1d, 1d)])], new PrismaticCorrespondenceMap([1, 2, 0])), "air-x3-non-identity-correspondence-deferred"];
    }

    private static PrismaticTopFaceLoopChamferRequest CanonicalTopFaceLoopChamferRequest(bool exportStep = false) => new(10, 8, 6, 1, ExportStep: exportStep);

    private static PrismaticSectionTransitionRequest CanonicalRequest(bool runStepSmoke = false) => new(
        [
            new(0, [(-5, -4), (5, -4), (5, 4), (-5, 4)]),
            new(5, [(-5, -4), (5, -4), (5, 4), (-5, 4)]),
            new(6, [(-4, -3), (4, -3), (4, 3), (-4, 3)]),
        ],
        PrismaticCorrespondenceMap.Identity(4),
        new PrismaticSectionTransitionOptions(RunStepSmoke: runStepSmoke, TraceLabel: "air-x3-brep-plan-test"));
}
