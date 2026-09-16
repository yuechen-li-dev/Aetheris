using Aetheris.Kernel.Core.Judgment;

namespace Aetheris.Kernel.Core.Tests.Judgment;

public sealed class JudgmentModelTests
{
    private sealed record Candidate(bool Allowed, double Quality);
    private static JudgmentModel<Candidate, int> Model(Func<Candidate, int, JudgmentScoreEvidence>? score = null) => new("test.v1",
        [new("allowed", (c, _) => new(c.Allowed, "test-reject", c.Allowed ? 0 : 1, 0, "Explicit hard constraint."))],
        [new("quality", 1, score ?? ((c, _) => new(c.Quality, c.Quality, "unitless", "Test quality.")))]);

    [Fact]
    public void HardRejectedCandidateCannotWinAndIsNeverScored()
    {
        var calls = 0;
        var result = Model((c, _) => { calls++; Assert.True(c.Allowed); return new(c.Quality, c.Quality, "unitless", "witness"); })
            .Evaluate(0, [new("invalid", new(false, 1)), new("valid", new(true, .2))]);
        Assert.Equal("valid", result.Winner!.Id); Assert.Equal(1, calls);
        Assert.Empty(result.Candidates.Single(c => c.Id == "invalid").Scores);
        Assert.False(result.Candidates.Single(c => c.Id == "invalid").Admissible);
    }
    [Fact]
    public void TraceContainsAllTermsRunnerUpAndStableTie()
    {
        var options = new JudgmentOption<Candidate>[] { new("z", new(true, .7)), new("a", new(true, .7)), new("bad", new(false, 1)) };
        var first = Model().Evaluate(0, options); var second = Model().Evaluate(0, options.Reverse().ToArray());
        Assert.Equal("a", first.Winner!.Id); Assert.Equal(first.Winner.Id, second.Winner!.Id);
        Assert.Equal("z", first.RunnerUp); Assert.True(first.TieBroken);
        Assert.Equal(.7, first.Candidates.Single(c => c.Id == "a").TotalScore);
    }
    [Fact]
    public void NoSurvivorMeansNoWinnerOrFallback()
    {
        var result = Model().Evaluate(0, [new("bad", new(false, 1))]);
        Assert.Null(result.Winner); Assert.Null(result.RunnerUp); Assert.Null(result.Candidates[0].TotalScore);
    }
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1.01)]
    [InlineData(-.01)]
    public void InvalidUtilityIsRejected(double utility)
    {
        var result = Model().Evaluate(0, [new("bad", new(true, utility)), new("valid", new(true, .5))]);
        Assert.Equal("valid", result.Winner!.Id);
        Assert.Contains(result.Candidates.Single(c => c.Id == "bad").Rules, r => r.Result.Code == "judgment-invalid-score");
    }
    [Fact]
    public void InvalidWeightsAndDuplicateIdsFailExplicitly()
    {
        var model = new JudgmentModel<Candidate, int>("test", [new("rule", (_,_) => new(true,"ok",0,0,"ok"))],
            [new("invalid", double.NaN, (_,_) => new(.5,.5,"unitless","test"))]);
        Assert.Throws<ArgumentException>(() => model.Evaluate(0, [new("a", new(true,.5))]));
        Assert.Throws<ArgumentException>(() => Model().Evaluate(0, [new("a", new(true,.5)), new("a", new(true,.5))]));
    }
    [Fact]
    public void SharedChooserAndPolicyAdapterSelectSameWinnerForEquivalentSemantics()
    {
        var options = new JudgmentOption<Candidate>[] { new("a",new(true,.3)), new("b",new(true,.8)), new("c",new(false,1)) };
        var old = new JudgmentEngine<int>().Evaluate(0, options.Select(o => new JudgmentCandidate<int>(o.Id, _ => o.Value.Allowed, _ => o.Value.Quality)).ToArray());
        var current = Model().Evaluate(0, options);
        Assert.Equal(old.Selection!.Value.Candidate.Name, current.Winner!.Id);
        Assert.Equal(old.Selection.Value.Score, current.Candidates.Single(t => t.Id == current.Winner.Id).TotalScore);
    }
}
