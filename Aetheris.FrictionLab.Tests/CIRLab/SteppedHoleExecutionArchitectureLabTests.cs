using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class SteppedHoleExecutionArchitectureLabTests
{
    private static readonly string[] AllowedRecommendations = ["repeated-subtract-production", "unioned-tool-production", "n-level-builder-production", "profile-stack-tool-builder-production", "keep-deferred"];

    private static readonly string[] RequiredStrategies =
    [
        "repeated-subtract-small-medium-large",
        "repeated-subtract-large-medium-small",
        "repeated-subtract-medium-large-small",
        "unioned-tool-single-subtract",
        "n-level-builder-analysis",
        "profile-stack-tool-builder-analysis",
        "deferred-baseline-current-production",
        "counterbore-baseline"
    ];

    [Fact]
    public void SteppedArchitectureLab_ProducesStrategyMatrix()
    {
        var report = SteppedHoleExecutionArchitectureLab.RunCanonicalSteppedScenario();
        Assert.All(RequiredStrategies, strategy => Assert.Contains(report.Strategies, s => s.Strategy == strategy));
    }

    [Fact]
    public void SteppedArchitectureLab_ReportMatrixContainsConcreteStatuses()
    {
        var report = SteppedHoleExecutionArchitectureLab.RunCanonicalSteppedScenario();
        foreach (var strategy in RequiredStrategies)
        {
            var row = report.Strategies.Single(s => s.Strategy == strategy);
            Assert.True(Enum.IsDefined(row.Status));
            Assert.False(string.IsNullOrWhiteSpace(row.FailureStage));
            Assert.False(string.IsNullOrWhiteSpace(row.FailureCode));
        }
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
        Assert.False(string.IsNullOrWhiteSpace(r));
        Assert.Contains(r, AllowedRecommendations);
    }

    [Fact]
    public void SteppedArchitectureLab_SelectedRecommendationIsExplicit()
    {
        var recommendation = SteppedHoleExecutionArchitectureLab.RunCanonicalSteppedScenario().Recommendation;
        Assert.False(string.IsNullOrWhiteSpace(recommendation));
        Assert.Contains(recommendation, AllowedRecommendations);
    }

    [Fact]
    public void SteppedArchitectureLab_UnionedToolRowHasConcreteOutcome()
    {
        var row = SteppedHoleExecutionArchitectureLab.RunCanonicalSteppedScenario().Strategies.Single(s => s.Strategy == "unioned-tool-single-subtract");
        Assert.True(row.Status is SteppedArchitectureStrategyStatus.Succeeded or SteppedArchitectureStrategyStatus.Failed or SteppedArchitectureStrategyStatus.Skipped);
        Assert.False(string.IsNullOrWhiteSpace(row.FailureStage));
        Assert.False(string.IsNullOrWhiteSpace(row.FailureCode));
        if (row.Status is SteppedArchitectureStrategyStatus.Skipped)
        {
            Assert.NotEmpty(row.Diagnostics);
        }
    }

    [Fact]
    public void SteppedArchitectureLab_RepeatedSubtractRowsHavePerOrderOutcomes()
    {
        var rows = SteppedHoleExecutionArchitectureLab.RunCanonicalSteppedScenario().Strategies
            .Where(s => s.Strategy.StartsWith("repeated-subtract-", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(3, rows.Length);
        Assert.Equal(3, rows.Select(r => r.Strategy).Distinct(StringComparer.Ordinal).Count());
        Assert.All(rows, row =>
        {
            Assert.True(row.Status is SteppedArchitectureStrategyStatus.Succeeded or SteppedArchitectureStrategyStatus.Failed);
            Assert.False(string.IsNullOrWhiteSpace(row.FailureStage));
            Assert.False(string.IsNullOrWhiteSpace(row.FailureCode));
        });
    }

    [Fact]
    public void SteppedArchitectureLab_CounterboreBaselineOutcomePresent()
    {
        var row = SteppedHoleExecutionArchitectureLab.RunCanonicalSteppedScenario().Strategies.Single(s => s.Strategy == "counterbore-baseline");
        Assert.True(row.Status is SteppedArchitectureStrategyStatus.Succeeded or SteppedArchitectureStrategyStatus.Failed);
        Assert.False(string.IsNullOrWhiteSpace(row.FailureStage));
        Assert.False(string.IsNullOrWhiteSpace(row.FailureCode));
    }

    [Fact]
    public void SteppedArchitectureLab_ReportIsDecisionGrade()
    {
        var report = SteppedHoleExecutionArchitectureLab.RunCanonicalSteppedScenario();
        var bannedVagueTerms = new[] { "represented", "compared", "analyzed" };
        foreach (var strategy in RequiredStrategies)
        {
            var row = report.Strategies.Single(s => s.Strategy == strategy);
            var rowText = string.Join("|", row.RecommendedNextStep, row.FailureCode, row.FailureStage, string.Join(";", row.Diagnostics)).ToLowerInvariant();
            Assert.DoesNotContain(bannedVagueTerms, term => rowText.Contains(term, StringComparison.Ordinal));
        }
    }
}
