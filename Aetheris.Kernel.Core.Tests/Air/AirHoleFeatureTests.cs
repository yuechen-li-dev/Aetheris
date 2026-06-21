using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Air;

public sealed class AirHoleFeatureTests
{
    [Fact]
    public void SimpleShaftHole_PreservesSemanticIntentBeforeLowering()
    {
        var feature = ValidFeature(new AirHoleEndCondition.ThroughAll());

        Assert.True(feature.IsValid);
        Assert.Equal("mount-hole", feature.Name);
        Assert.Equal("hole-001", feature.FeatureId);
        Assert.Equal("body-main", feature.TargetBodyId);
        Assert.Equal("top", feature.Placement.EntryFaceName);
        Assert.Equal(1.25, feature.Placement.U);
        Assert.Equal(-2.5, feature.Placement.V);
        Assert.True(feature.Axis.DefaultedFromEntryFaceNormal);
        Assert.Equal(1, feature.Axis.Direction.Z, 12);
        Assert.Equal(6, feature.Shaft.Diameter);
        Assert.Equal(3, feature.Shaft.Radius);
        Assert.IsType<AirHoleEndCondition.ThroughAll>(feature.EndCondition);
        Assert.Equal("HOLE-X1", feature.Provenance.Milestone);
        Assert.Equal("Semantic hole AIR scaffold", feature.Provenance.SourceKind);
    }

    [Fact]
    public void SimpleShaftHole_DepthEndConditionIsPreserved()
    {
        var feature = ValidFeature(new AirHoleEndCondition.Depth(4.5));

        var depth = Assert.IsType<AirHoleEndCondition.Depth>(feature.EndCondition);
        Assert.Equal(AirHoleEndConditionKind.Depth, depth.Kind);
        Assert.Equal(4.5, depth.Value);
        Assert.DoesNotContain(feature.Diagnostics, d => d.Severity == AirDiagnosticSeverity.Error);
    }

    [Fact]
    public void SimpleShaftHole_LoweringCandidateKeepsSemanticFeatureNotAnonymousCylinderCut()
    {
        var feature = ValidFeature(new AirHoleEndCondition.ThroughAll());

        var plan = feature.CreateSimpleShaftLoweringPlan();

        Assert.Same(feature, plan.Feature);
        Assert.Equal(AirHoleLoweringRouteKind.SimpleShaftProfileStackCandidate, plan.RouteKind);
        Assert.False(plan.Executed);
        Assert.Contains("semantic-intent", plan.Recommendation);
        Assert.DoesNotContain("CylinderCut", plan.RouteKind.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SimpleShaftHole_InvalidPlacementDiameterAndDepthEmitDeterministicDiagnostics()
    {
        var feature = AirHoleFeature.CreateSimpleShaft(
            "bad-hole",
            "hole-bad",
            "body-main",
            new AirFaceLocalHolePlacement("", double.NaN, 0, ""),
            new AirHoleAxis(Direction3D.Create(new Vector3D(0, 0, 1)), true),
            new AirHoleShaft(0),
            new AirHoleEndCondition.Depth(-1));

        Assert.False(feature.IsValid);
        Assert.Equal(new[]
        {
            "hole-x1-entry-face-required",
            "hole-x1-placement-frame-required",
            "hole-x1-placement-center-invalid",
            "hole-x1-diameter-invalid",
            "hole-x1-depth-invalid"
        }, feature.Diagnostics.Select(d => d.Code).ToArray());

        var plan = feature.CreateSimpleShaftLoweringPlan();
        Assert.Equal(AirHoleLoweringRouteKind.NotLowered, plan.RouteKind);
        Assert.False(plan.Executed);
    }

    private static AirHoleFeature ValidFeature(AirHoleEndCondition endCondition) => AirHoleFeature.CreateSimpleShaft(
        "mount-hole",
        "hole-001",
        "body-main",
        new AirFaceLocalHolePlacement("top", 1.25, -2.5, "face(top):u=+X,v=+Y", "face(top)"),
        new AirHoleAxis(Direction3D.Create(new Vector3D(0, 0, 1)), true),
        new AirHoleShaft(6),
        endCondition);
}
