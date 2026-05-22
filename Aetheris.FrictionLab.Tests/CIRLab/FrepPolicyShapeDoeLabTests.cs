using Aetheris.FrictionLab;
using System.Text;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class FrepPolicyShapeDoeLabTests
{
    [Fact]
    public void PolicyShapeDoe_CanonicalBoxCylinder_SelectsRecommendedSemanticPolicy()
    {
        var s = FrepPolicyShapeDoeLab.BuildScenarioMatrix().Single(x => x.Id == "A1");
        var d = FrepPolicyShapeDoeLab.Decide(s);
        Assert.Equal("ThroughHoleRecoveryPolicyShape", d.Winner);
        Assert.DoesNotContain(d.Ranking, e => e.PolicyName == "CirBooleanRecoveryPolicyShape" && e.PolicyName == d.Winner);
    }

    [Fact] public void PolicyShapeDoe_TranslatedBoxCylinder_SelectsRecommendedSemanticPolicy() => Assert.Equal("ThroughHoleRecoveryPolicyShape", FrepPolicyShapeDoeLab.Decide(FrepPolicyShapeDoeLab.BuildScenarioMatrix().Single(x => x.Id=="A3")).Winner);

    [Fact]
    public void PolicyShapeDoe_InvalidHoleRejectsSemanticPolicy()
    {
        foreach (var id in new[] { "B1", "B2", "B3" })
        {
            var e = FrepPolicyShapeDoeLab.Decide(FrepPolicyShapeDoeLab.BuildScenarioMatrix().Single(x => x.Id == id)).Ranking.Single(x => x.PolicyName == "ThroughHoleRecoveryPolicyShape");
            Assert.False(e.Admissible, $"{id} should reject semantic through-hole policy.");
        }
    }

    [Fact]
    public void PolicyShapeDoe_CylindricalToolCutOverAdmissionDetected()
    {
        var e = FrepPolicyShapeDoeLab.Decide(FrepPolicyShapeDoeLab.BuildScenarioMatrix().Single(x => x.Id == "C1")).Ranking.Single(x => x.PolicyName == "CylindricalToolCutPolicyShape");
        Assert.True(e.OverAdmits || e.Diagnostics.Any(d => d.Contains("over-admission", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void PolicyShapeDoe_GenericBooleanRiskDetected()
    {
        var e = FrepPolicyShapeDoeLab.Decide(FrepPolicyShapeDoeLab.BuildScenarioMatrix().Single(x => x.Id == "F4")).Ranking.Single(x => x.PolicyName == "CirBooleanRecoveryPolicyShape");
        Assert.True(e.OverAdmits);
        Assert.Contains(e.Diagnostics, d => d.Contains("dispatcher-risk", StringComparison.Ordinal));
    }

    [Fact]
    public void PolicyShapeDoe_SurfaceFeaturesNotCapturedByThroughHole()
    {
        foreach (var id in new[] { "E1", "E3" })
        {
            var winner = FrepPolicyShapeDoeLab.Decide(FrepPolicyShapeDoeLab.BuildScenarioMatrix().Single(x => x.Id == id)).Winner;
            Assert.NotEqual("ThroughHoleRecoveryPolicyShape", winner);
        }
    }

    [Fact]
    public void PolicyShapeDoe_FallbacksWork()
    {
        var d = FrepPolicyShapeDoeLab.Decide(FrepPolicyShapeDoeLab.BuildScenarioMatrix().Single(x => x.Id == "F1"));
        Assert.True(d.Winner is "GenericNumericalContourPolicy" or "CirOnlyFallbackPolicy", $"Unexpected fallback winner: {d.Winner}");
    }

    [Fact]
    public void PolicyShapeDoe_DeterministicDecisions()
    {
        var scenarios = FrepPolicyShapeDoeLab.BuildScenarioMatrix();
        var run1 = scenarios.ToDictionary(s => s.Id, s => FrepPolicyShapeDoeLab.Decide(s).Winner);
        var run2 = scenarios.ToDictionary(s => s.Id, s => FrepPolicyShapeDoeLab.Decide(s).Winner);
        Assert.Equal(run1, run2);
    }

    [Fact]
    public void PolicyShapeDoe_ReportShapeComplete()
    {
        var required = new[] { "PolicyName", "Admissible", "Score", "Capability", "PlanShape", "EvidenceUsed", "RejectedReasons", "OverAdmits", "UnderAdmits", "PairSpecificityRisk", "FutureScalabilityScore", "Diagnostics" };
        var e = FrepPolicyShapeDoeLab.Decide(FrepPolicyShapeDoeLab.BuildScenarioMatrix().First()).Ranking.First();
        var present = new HashSet<string> { nameof(e.PolicyName), nameof(e.Admissible), nameof(e.Score), nameof(e.Capability), nameof(e.PlanShape), nameof(e.EvidenceUsed), nameof(e.RejectedReasons), nameof(e.OverAdmits), nameof(e.UnderAdmits), nameof(e.PairSpecificityRisk), nameof(e.FutureScalabilityScore), nameof(e.Diagnostics) };
        var missing = required.Where(r => !present.Contains(r)).ToArray();
        Assert.True(missing.Length == 0, $"Missing columns: {string.Join(",", missing)}");
    }
}
