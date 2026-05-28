using System.Text.Json;
using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class AirChamferTopologyPlanLabTests
{
    [Fact]
    public void RunAll_IsDeterministic()
    {
        var a = AirChamferTopologyPlanLab.RunAll();
        var b = AirChamferTopologyPlanLab.RunAll();
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
    }

    [Fact]
    public void Diagnostics_IncludeJudgmentEngineUsage()
    {
        var row = AirChamferTopologyPlanLab.Evaluate(AirChamferTopologyPlanLab.Cases().Single(x => x.CaseName == "canonical-orthogonal-convex-planar-single-edge"));
        Assert.Contains("edge-x4-judgment-engine-used", row.Diagnostics);
    }

    [Fact]
    public void CanonicalConvexPlanar_ProducesTopologyPlanWithExpectedCounts()
    {
        var result = AirChamferTopologyPlanLab.Evaluate(AirChamferTopologyPlanLab.Cases().Single(x => x.CaseName == "canonical-orthogonal-convex-planar-single-edge"));
        Assert.Equal("plan-convex-replacement-topology", result.Decision);
        Assert.NotNull(result.Plan);
        Assert.Equal(2, result.Plan!.OffsetCurveCount);
        Assert.Equal(1, result.Plan.NewChamferFaceCount);
        Assert.Equal(2, result.Plan.AdjacentFaceAffectedCount);
        Assert.True(result.Plan.OriginalEdgeMarkedForReplacement);
        Assert.Equal(2, result.Plan.NewTransitionEdgeCount);
        Assert.Equal(0, result.Plan.CornerPatchCount);
        Assert.True(result.Plan.CornerPatchesDeferred);
        Assert.False(result.Plan.GeometryEmissionPerformed);
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("3d-boolean", StringComparison.OrdinalIgnoreCase) && !d.Contains("no-3d-boolean-used", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("convex-planar-unsafe-envelope", "reject-unsafe-offset-envelope")]
    [InlineData("invalid-distance", "reject-invalid-distance")]
    [InlineData("invalid-edge", "reject-invalid-edge")]
    [InlineData("invalid-face-adjacency", "reject-invalid-face-adjacency")]
    [InlineData("ambiguous-classification", "reject-ambiguous-classification")]
    [InlineData("edge-chain", "defer-edge-chain-policy")]
    [InlineData("corner-chain", "defer-corner-policy")]
    [InlineData("triangle-legacy-dependent", "fallback-legacy-chamfer")]
    public void InvalidOrDeferredCases_StopBeforePlan(string caseName, string expectedDecision)
    {
        var result = AirChamferTopologyPlanLab.Evaluate(AirChamferTopologyPlanLab.Cases().Single(x => x.CaseName == caseName));
        Assert.Equal(expectedDecision, result.Decision);
        Assert.Null(result.Plan);
    }

    [Fact]
    public void ConcaveCasesRemainAcceptedByPolicy()
    {
        var concave = AirChamferPolicyLab.Evaluate(AirChamferPolicyLab.Cases().Single(x => x.CaseName == "canonical-concave-planar"));
        Assert.Equal("accept-air-chamfer-patch", concave.Decision.Decision);
    }

    [Fact]
    public void Recommendations_AreFiniteAndDeterministic()
    {
        var rows = AirChamferTopologyPlanLab.RunAll();
        foreach (var row in rows)
            Assert.Contains(row.Recommendation, AirChamferTopologyPlanLab.AllowedRecommendations);
    }
}
