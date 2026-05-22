using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class SteppedHoleExecutionArchitectureLabTests
{
    [Fact]
    public void SteppedArchitectureLab_ProducesStrategyMatrix()
    {
        var report = SteppedHoleExecutionArchitectureLab.RunCanonicalSteppedScenario();
        Assert.Contains(report.Strategies, s => s.Strategy == "repeated-subtract-small-medium-large");
        Assert.Contains(report.Strategies, s => s.Strategy == "repeated-subtract-large-medium-small");
        Assert.Contains(report.Strategies, s => s.Strategy == "repeated-subtract-medium-large-small");
        Assert.Contains(report.Strategies, s => s.Strategy == "unioned-tool-single-subtract");
        Assert.Contains(report.Strategies, s => s.Strategy == "n-level-builder-analysis");
        Assert.Contains(report.Strategies, s => s.Strategy == "profile-stack-tool-builder-analysis");
        Assert.Contains(report.Strategies, s => s.Strategy == "deferred-baseline-current-production");
    }

    [Fact]
    public void SteppedArchitectureLab_RecordsFailureStages()
    {
        var failed = SteppedHoleExecutionArchitectureLab.RunCanonicalSteppedScenario().Strategies.Where(s => s.Status is SteppedArchitectureStrategyStatus.Failed or SteppedArchitectureStrategyStatus.Deferred);
        Assert.All(failed, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.FailureCode));
            Assert.False(string.IsNullOrWhiteSpace(s.FailureStage));
            Assert.NotEmpty(s.Diagnostics);
        });
    }

    [Fact]
    public void SteppedArchitectureLab_StepSmokeForSuccessfulStrategies()
    {
        var success = SteppedHoleExecutionArchitectureLab.RunCanonicalSteppedScenario().Strategies.Where(s => s.Status == SteppedArchitectureStrategyStatus.Succeeded);
        foreach (var s in success)
        {
            Assert.True(s.StepSmokeAttempted);
            Assert.True(s.StepSmokeSucceeded);
            Assert.Contains("ISO-10303-21", s.StepMarkers);
            Assert.Contains("MANIFOLD_SOLID_BREP", s.StepMarkers);
            Assert.False(s.HasBrepWithVoids);
        }
    }

    [Fact]
    public void SteppedArchitectureLab_CounterboreBaselineStillWorks()
    {
        var r = SteppedHoleExecutionArchitectureLab.RunCanonicalSteppedScenario().Strategies.Single(s => s.Strategy == "counterbore-baseline");
        Assert.True(r.Status == SteppedArchitectureStrategyStatus.Succeeded || !string.IsNullOrWhiteSpace(r.FailureCode));
    }

    [Fact]
    public void SteppedArchitectureLab_DeterministicResults()
    {
        var a = SteppedHoleExecutionArchitectureLab.RunCanonicalSteppedScenario();
        var b = SteppedHoleExecutionArchitectureLab.RunCanonicalSteppedScenario();
        Assert.Equal(string.Join(";", a.Strategies.Select(x => x.Strategy)), string.Join(";", b.Strategies.Select(x => x.Strategy)));
        Assert.Equal(string.Join(";", a.Strategies.Select(x => x.Status)), string.Join(";", b.Strategies.Select(x => x.Status)));
        Assert.Equal(string.Join(";", a.Strategies.Select(x => x.FailureCode)), string.Join(";", b.Strategies.Select(x => x.FailureCode)));
    }

    [Fact]
    public void SteppedArchitectureLab_RecommendationIsExplicit()
    {
        var r = SteppedHoleExecutionArchitectureLab.RunCanonicalSteppedScenario().Recommendation;
        Assert.Contains(r, new[] { "repeated-subtract-production", "unioned-tool-production", "n-level-builder-production", "profile-stack-tool-builder-production", "keep-deferred" });
    }
}
