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
        Assert.Equal(a.Summary with { Diagnostics = [], Guarantees = [] }, b.Summary with { Diagnostics = [], Guarantees = [] });
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

    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return [new PrismaticSectionTransitionRequest([new(0, [(-1d, -1d), (1d, -1d), (1d, 1d)])], PrismaticCorrespondenceMap.Identity(3)), "air-x3-invalid-section-count-rejected"];
        yield return [new PrismaticSectionTransitionRequest([new(0, [(-1d, -1d), (1d, -1d), (1d, 1d)]), new(1, [(-1d, -1d), (1d, -1d), (1d, 1d), (-1d, 1d)]), new(2, [(-1d, -1d), (1d, -1d), (1d, 1d)])], PrismaticCorrespondenceMap.Identity(3)), "air-x3-mismatched-vertex-count-rejected"];
        yield return [new PrismaticSectionTransitionRequest([new(0, [(-1d, -1d), (1d, -1d), (1d, 1d)]), new(0, [(-1d, -1d), (1d, -1d), (1d, 1d)]), new(2, [(-1d, -1d), (1d, -1d), (1d, 1d)])], PrismaticCorrespondenceMap.Identity(3)), "air-x3-non-increasing-sections-rejected"];
        yield return [new PrismaticSectionTransitionRequest([new(0, [(-1d, -1d), (1d, -1d), (1d, 1d)]), new(1, [(double.NaN, -1d), (1d, -1d), (1d, 1d)]), new(2, [(-1d, -1d), (1d, -1d), (1d, 1d)])], PrismaticCorrespondenceMap.Identity(3)), "air-x3-non-finite-coordinate-rejected"];
        yield return [new PrismaticSectionTransitionRequest([new(0, [(-1d, -1d), (1d, -1d), (1d, 1d)], HasHoles: true), new(1, [(-1d, -1d), (1d, -1d), (1d, 1d)]), new(2, [(-1d, -1d), (1d, -1d), (1d, 1d)])], PrismaticCorrespondenceMap.Identity(3)), "air-x3-holes-deferred"];
        yield return [new PrismaticSectionTransitionRequest([new(0, [(-1d, -1d), (1d, -1d), (1d, 1d)]), new(1, [(-1d, -1d), (1d, -1d), (1d, 1d)]), new(2, [(-1d, -1d), (1d, -1d), (1d, 1d)])], new PrismaticCorrespondenceMap([1, 2, 0])), "air-x3-non-identity-correspondence-deferred"];
    }

    private static PrismaticSectionTransitionRequest CanonicalRequest(bool runStepSmoke = false) => new(
        [
            new(0, [(-5, -4), (5, -4), (5, 4), (-5, 4)]),
            new(5, [(-5, -4), (5, -4), (5, 4), (-5, 4)]),
            new(6, [(-4, -3), (4, -3), (4, 3), (-4, 3)]),
        ],
        PrismaticCorrespondenceMap.Identity(4),
        new PrismaticSectionTransitionOptions(RunStepSmoke: runStepSmoke, TraceLabel: "air-x3-brep-plan-test"));
}
