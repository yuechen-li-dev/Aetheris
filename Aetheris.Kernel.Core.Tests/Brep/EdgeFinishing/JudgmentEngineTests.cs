using Aetheris.Kernel.Core.Brep.EdgeFinishing;

namespace Aetheris.Kernel.Core.Tests.Brep.EdgeFinishing;

public sealed class JudgmentEngineTests
{
    [Fact]
    public void Evaluate_SelectsOnlyAdmissibleCandidate()
    {
        var engine = new JudgmentEngine<int>();
        var candidates = new[]
        {
            new JudgmentCandidate<int>("non_positive", value => value <= 0, _ => 10d),
            new JudgmentCandidate<int>("positive", value => value > 0, _ => 1d)
        };

        var result = engine.Evaluate(3, candidates);

        Assert.True(result.IsSuccess);
        Assert.Equal("positive", result.Selection!.Value.Candidate.Name);
    }

    [Fact]
    public void Evaluate_SelectsHighestScoreAmongAdmissibleCandidates()
    {
        var engine = new JudgmentEngine<int>();
        var candidates = new[]
        {
            new JudgmentCandidate<int>("low", _ => true, _ => 2d),
            new JudgmentCandidate<int>("high", _ => true, _ => 5d)
        };

        var result = engine.Evaluate(0, candidates);

        Assert.True(result.IsSuccess);
        Assert.Equal("high", result.Selection!.Value.Candidate.Name);
        Assert.Equal(5d, result.Selection.Value.Score);
    }

    [Fact]
    public void Evaluate_UsesDeterministicTieBreakByPriorityThenName()
    {
        var engine = new JudgmentEngine<int>();
        var candidates = new[]
        {
            new JudgmentCandidate<int>("zeta", _ => true, _ => 1d, TieBreakerPriority: 1),
            new JudgmentCandidate<int>("alpha", _ => true, _ => 1d, TieBreakerPriority: 1),
            new JudgmentCandidate<int>("beta", _ => true, _ => 1d, TieBreakerPriority: 0)
        };

        var first = engine.Evaluate(0, candidates);
        var second = engine.Evaluate(0, candidates.Reverse().ToArray());

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("beta", first.Selection!.Value.Candidate.Name);
        Assert.Equal("beta", second.Selection!.Value.Candidate.Name);
    }

    [Fact]
    public void Evaluate_ReturnsExplicitFailureRejectionsWhenNoCandidateIsAdmissible()
    {
        var engine = new JudgmentEngine<int>();
        var candidates = new[]
        {
            new JudgmentCandidate<int>(
                "too_small",
                value => value > 10,
                _ => 1d,
                value => $"Expected > 10 but got {value}."),
            new JudgmentCandidate<int>(
                "even_only",
                value => value % 2 == 0,
                _ => 1d,
                value => $"Expected even value but got {value}.")
        };

        var result = engine.Evaluate(3, candidates);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Selection);
        Assert.Collection(
            result.Rejections,
            rejection =>
            {
                Assert.Equal("too_small", rejection.CandidateName);
                Assert.Contains("Expected > 10", rejection.Reason, StringComparison.Ordinal);
            },
            rejection =>
            {
                Assert.Equal("even_only", rejection.CandidateName);
                Assert.Contains("Expected even", rejection.Reason, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void When_Composition_AllAnyAndNot_BehaveAsExpected()
    {
        var positive = new Func<int, bool>(value => value > 0);
        var even = new Func<int, bool>(value => value % 2 == 0);

        var all = When.All(positive, even);
        var any = When.Any(positive, even);
        var not = When.Not(positive);

        Assert.True(all(2));
        Assert.False(all(3));

        Assert.True(any(2));
        Assert.True(any(3));
        Assert.False(any(-3));

        Assert.True(not(-1));
        Assert.False(not(1));
    }
}
