using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Tests.Cir;

public sealed class CirRegionPlannerTests
{
    private static readonly ToleranceContext Tolerance = ToleranceContext.Default;

    [Fact]
    public void Planner_ClassifiesInsideRegion()
    {
        var planner = new CirRegionPlanner();
        var context = BuildMixedContext(new FieldInterval(-2d, -0.1d), CirRegionClassification.Inside, depth: 0, maxDepth: 4);

        var plan = planner.Plan(context);

        Assert.Equal(CirRegionPlanAction.ClassifyInside, plan.Action);
        Assert.Equal("classify_inside", plan.SelectedCandidate);
    }

    [Fact]
    public void Planner_ClassifiesOutsideRegion()
    {
        var planner = new CirRegionPlanner();
        var context = BuildMixedContext(new FieldInterval(0.1d, 2d), CirRegionClassification.Outside, depth: 0, maxDepth: 4);

        var plan = planner.Plan(context);

        Assert.Equal(CirRegionPlanAction.ClassifyOutside, plan.Action);
        Assert.Equal("classify_outside", plan.SelectedCandidate);
    }

    [Fact]
    public void Planner_SubdividesMixedRegion_WhenDepthAllows()
    {
        var planner = new CirRegionPlanner();
        var context = BuildMixedContext(new FieldInterval(-0.25d, 0.25d), CirRegionClassification.Mixed, depth: 1, maxDepth: 4);

        var plan = planner.Plan(context);

        Assert.Equal(CirRegionPlanAction.Subdivide, plan.Action);
        Assert.Equal("subdivide_mixed", plan.SelectedCandidate);
    }

    [Fact]
    public void Planner_SamplesMixedRegion_AtDepthLimit()
    {
        var planner = new CirRegionPlanner();
        var context = BuildMixedContext(new FieldInterval(-0.25d, 0.25d), CirRegionClassification.Mixed, depth: 3, maxDepth: 3);

        var plan = planner.Plan(context);

        Assert.Equal(CirRegionPlanAction.SampleDirectly, plan.Action);
        Assert.Equal("sample_directly", plan.SelectedCandidate);
    }

    [Fact]
    public void Planner_TraceIncludesRejectedCandidates()
    {
        var planner = new CirRegionPlanner();
        var context = BuildMixedContext(new FieldInterval(-0.25d, 0.25d), CirRegionClassification.Mixed, depth: 3, maxDepth: 3);

        var plan = planner.Plan(context);

        Assert.NotEmpty(plan.RejectedCandidates);
        Assert.Contains(plan.RejectedCandidates, rejection => rejection.CandidateName == "subdivide_mixed");
        Assert.Contains(plan.RejectedCandidates, rejection => !string.IsNullOrWhiteSpace(rejection.Reason));
    }

    [Fact]
    public void Planner_DeterministicSelection()
    {
        var planner = new CirRegionPlanner();
        var context = BuildMixedContext(new FieldInterval(-0.25d, 0.25d), CirRegionClassification.Mixed, depth: 0, maxDepth: 4);

        var first = planner.Plan(context);
        var second = planner.Plan(context);

        Assert.Equal(first.Action, second.Action);
        Assert.Equal(first.SelectedCandidate, second.SelectedCandidate);
        Assert.Equal(first.Score, second.Score);
    }

    [Fact]
    public void Planner_UsesRealTapeInterval()
    {
        var node = new CirSubtractNode(new CirBoxNode(8d, 8d, 8d), new CirCylinderNode(2d, 10d));
        var tape = CirTapeLowerer.Lower(node);
        var region = new CirBounds(new Point3D(1.8d, -0.3d, -1d), new Point3D(2.2d, 0.3d, 1d));
        var options = new CirRegionPlannerOptions(MaxDepth: 4, DirectSampleThreshold: 32, MinimumRegionExtent: 0.1d);
        var planner = new CirRegionPlanner();

        var plan = planner.Plan(tape, region, depth: 1, options, Tolerance);

        Assert.Equal(CirRegionPlanAction.Subdivide, plan.Action);
        Assert.Equal(CirRegionClassification.Mixed, plan.Classification);
        Assert.True(plan.Interval.MinValue <= 0d && plan.Interval.MaxValue >= 0d);
    }

    private static CirRegionPlanContext BuildMixedContext(FieldInterval interval, CirRegionClassification classification, int depth, int maxDepth)
    {
        var options = new CirRegionPlannerOptions(MaxDepth: maxDepth, DirectSampleThreshold: 32, MinimumRegionExtent: 0.1d);
        var region = new CirBounds(new Point3D(-1d, -1d, -1d), new Point3D(1d, 1d, 1d));
        return CirRegionPlanner.BuildContext(region, interval, classification, depth, options, Tolerance);
    }
}
