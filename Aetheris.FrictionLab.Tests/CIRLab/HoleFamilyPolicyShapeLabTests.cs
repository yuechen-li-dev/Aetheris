using Aetheris.FrictionLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class HoleFamilyPolicyShapeLabTests
{
    [Fact]
    public void HolePolicyShape_CurrentThroughHoleSupported()
    {
        var s = HoleFamilyPolicyShapeLab.BuildScenarioMatrix().Single(x => x.Id == "A1");
        var score = HoleFamilyPolicyShapeLab.Evaluate(HoleFamilyArchitectureKind.CompositionalHolePolicy, s);
        Assert.True(score.AdmitsScenario, "Recommended compositional architecture should admit current exact through-hole case A1.");
    }

    [Fact]
    public void HolePolicyShape_CounterboreFitsRecommendedShape()
    {
        var s = HoleFamilyPolicyShapeLab.BuildScenarioMatrix().Single(x => x.Id == "B2");
        var score = HoleFamilyPolicyShapeLab.Evaluate(HoleFamilyArchitectureKind.CompositionalHolePolicy, s);
        Assert.True(score.AdmitsScenario, "Counterbore should map to compositional hole-family architecture without adding a new top-level spaghetti branch.");
    }

    [Fact]
    public void HolePolicyShape_CountersinkFitsRecommendedShape()
    {
        var s = HoleFamilyPolicyShapeLab.BuildScenarioMatrix().Single(x => x.Id == "B3");
        var score = HoleFamilyPolicyShapeLab.Evaluate(HoleFamilyArchitectureKind.CompositionalHolePolicy, s);
        Assert.True(score.AdmitsScenario, "Countersink should map to compositional hole-family architecture via local profile/entry modules.");
    }

    [Fact]
    public void HolePolicyShape_SurfaceFeatureNotCaptured()
    {
        foreach (var id in new[] { "E1", "E2", "D1" })
        {
            var s = HoleFamilyPolicyShapeLab.BuildScenarioMatrix().Single(x => x.Id == id);
            var score = HoleFamilyPolicyShapeLab.Evaluate(HoleFamilyArchitectureKind.CompositionalHolePolicy, s);
            Assert.True(score.CorrectlyRejectsScenario, $"{id} should be rejected or deferred by hole exact policies; captured={score.AdmitsScenario}.");
        }
    }

    [Fact]
    public void HolePolicyShape_CylindricalToolOverAdmissionDetected()
    {
        var s = HoleFamilyPolicyShapeLab.BuildScenarioMatrix().Single(x => x.Id == "E3");
        var score = HoleFamilyPolicyShapeLab.Evaluate(HoleFamilyArchitectureKind.ProfileStackPolicy, s);
        Assert.True(score.AdmitsScenario && score.Diagnostics.Any(d => d.Contains("over-admission", StringComparison.OrdinalIgnoreCase)), "Profile stack policy should expose cylindrical-tool over-admission risk on side-notch scenario E3.");
    }

    [Fact]
    public void HolePolicyShape_MonolithicPolicyComplexityDetected()
    {
        var s = HoleFamilyPolicyShapeLab.BuildScenarioMatrix().Single(x => x.Id == "B5");
        var mono = HoleFamilyPolicyShapeLab.Evaluate(HoleFamilyArchitectureKind.MonolithicHolePolicy, s);
        var comp = HoleFamilyPolicyShapeLab.Evaluate(HoleFamilyArchitectureKind.CompositionalHolePolicy, s);
        Assert.True(mono.BranchComplexity > comp.BranchComplexity && mono.LocalityOfChangeScore < comp.LocalityOfChangeScore, "Monolithic policy should score worse on branch complexity/locality than compositional architecture.");
    }

    [Fact]
    public void HolePolicyShape_DeterministicRecommendation()
    {
        var s = HoleFamilyPolicyShapeLab.BuildScenarioMatrix().Single(x => x.Id == "B4");
        var run1 = HoleFamilyPolicyShapeLab.Recommend(s).Winner;
        var run2 = HoleFamilyPolicyShapeLab.Recommend(s).Winner;
        Assert.Equal(run1, run2);
        Assert.Equal("CompositionalHolePolicy", run1);
    }

    [Fact]
    public void HolePolicyShape_ReportCoverageComplete()
    {
        var scenarios = HoleFamilyPolicyShapeLab.BuildScenarioMatrix();
        foreach (var group in new[] { "A", "B", "C", "D", "E" })
        {
            Assert.Contains(scenarios, s => s.Group == group);
        }

        var s = scenarios.First();
        var score = HoleFamilyPolicyShapeLab.Evaluate(HoleFamilyArchitectureKind.CompositionalHolePolicy, s);
        Assert.True(score.GetType().GetProperties().Length >= 12, "Result shape should include required evaluation dimensions for report table completeness.");
    }
}
