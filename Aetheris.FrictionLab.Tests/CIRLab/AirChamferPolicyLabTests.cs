using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class AirChamferPolicyLabTests
{
    [Fact]
    public void PolicyRows_AreDeterministic()
    {
        var a = AirChamferPolicyLab.RunAll();
        var b = AirChamferPolicyLab.RunAll();
        Assert.Equal(a.Select(Serialize), b.Select(Serialize));
    }

    [Fact]
    public void Diagnostics_IncludeJudgmentEngineUsage()
    {
        var row = AirChamferPolicyLab.Evaluate(AirChamferPolicyLab.Cases().Single(x => x.CaseName == "canonical-concave-planar"));
        Assert.Contains("edge-x3-judgment-engine-used", row.Diagnostics);
    }

    [Fact]
    public void CanonicalConcavePlanar_AcceptsAndConstructsPatch()
    {
        var row = AirChamferPolicyLab.Evaluate(AirChamferPolicyLab.Cases().Single(x => x.CaseName == "canonical-concave-planar"));
        Assert.Equal("accept-air-chamfer-patch", row.Decision.Decision);
        Assert.NotNull(row.PatchRow);
        Assert.True(row.PatchRow!.Topology.PatchProduced);
    }

    [Theory]
    [InlineData("nonorthogonal-concave-planar-safe", "accept-air-chamfer-patch")]
    [InlineData("convex-planar", "defer-convex-replacement-geometry")]
    [InlineData("convex-planar-unsafe-envelope", "reject-unsafe-offset-envelope")]
    [InlineData("edge-chain", "defer-edge-chain-policy")]
    [InlineData("corner-chain", "defer-corner-policy")]
    [InlineData("triangle-legacy-dependent", "fallback-legacy-chamfer")]
    [InlineData("invalid-distance-zero", "reject-invalid-distance")]
    [InlineData("invalid-edge-zero-length", "reject-invalid-edge")]
    [InlineData("invalid-face-adjacency-missing", "reject-invalid-face-adjacency")]
    [InlineData("ambiguous-classification", "reject-ambiguous-classification")]
    public void FixtureCases_ProduceExpectedDecision(string caseName, string expected)
    {
        var result = AirChamferPolicyLab.Evaluate(AirChamferPolicyLab.Cases().Single(x => x.CaseName == caseName));
        Assert.Equal(expected, result.Decision.Decision);
    }

    [Fact]
    public void Decisions_AreFromFiniteAllowedSet_AndScoresPresent()
    {
        foreach (var row in AirChamferPolicyLab.RunAll())
        {
            Assert.Contains(row.Decision, AirChamferPolicyLab.AllowedDecisions);
            Assert.InRange(row.Score.GeometrySupportScore, 0, 100);
            Assert.InRange(row.Score.TopologyRiskScore, 0, 100);
            Assert.InRange(row.Score.OffsetStabilityScore, 0, 100);
            Assert.InRange(row.Score.CornerPolicyScore, 0, 100);
            Assert.InRange(row.Score.LegacyDependencyScore, 0, 100);
        }
    }

    [Fact]
    public void NoConvexReplacementGeometry_IsEmitted()
    {
        var row = AirChamferPolicyLab.Evaluate(AirChamferPolicyLab.Cases().Single(x => x.CaseName == "convex-planar"));
        Assert.Equal("defer-convex-replacement-geometry", row.Decision.Decision);
        Assert.Null(row.PatchRow);
        Assert.Contains("edge-x3-convex-replacement-deferred:no-topology-replacement-plan", row.Diagnostics);
    }

    private static string Serialize(AirChamferPolicyRow row)
        => $"{row.CaseName}|{row.Decision}|{row.Score.GeometrySupportScore}|{row.Score.TopologyRiskScore}|{row.Score.OffsetStabilityScore}|{row.Score.CornerPolicyScore}|{row.Score.LegacyDependencyScore}|{row.Score.OverallUtility}|{row.Recommendation}|{row.PatchConstructed}|{string.Join(',', row.Diagnostics)}";
}
