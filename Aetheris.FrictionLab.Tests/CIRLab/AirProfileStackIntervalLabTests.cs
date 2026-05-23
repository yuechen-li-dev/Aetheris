using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class AirProfileStackIntervalLabTests
{
    [Fact]
    public void AirIntervalLab_BlindMatrix_HasAllCandidateRows()
    {
        var rows = AirProfileStackIntervalLab.Run().Rows.Where(r => r.ScenarioName == "Blind-hole").Select(r => r.RepresentationName).ToHashSet();
        Assert.Subset(new HashSet<string>{"B1-NullInnerLoopSolidLayer","B2-ZeroRadiusInnerLoop","B3-ExplicitBlindPocketDescriptor","B4-SplitCapTransitionModel","B5-LegacyBooleanBaseline"}, rows);
    }

    [Fact]
    public void AirIntervalLab_CounterboreMatrix_HasAllCandidateRows()
    {
        var rows = AirProfileStackIntervalLab.Run().Rows.Where(r => r.ScenarioName == "Counterbore").Select(r => r.RepresentationName).ToHashSet();
        Assert.Subset(new HashSet<string>{"C1-ContiguousLayerRadii","C2-OverlappingToolIntervals","C3-NormalizedSteppedStack","C4-DirectSafeCompositionDescriptor","C5-LegacyBooleanBaseline"}, rows);
    }

    [Fact]
    public void AirIntervalLab_SuccessfulRows_HaveStepSmokeMarkers()
    {
        foreach (var row in AirProfileStackIntervalLab.Run().Rows.Where(r => r.Status == "succeeded"))
        {
            Assert.True(row.StepSmokeAttempted && row.StepSmokeSucceeded);
            Assert.Contains("ISO-10303-21", row.StepMarkers);
            Assert.Contains("MANIFOLD_SOLID_BREP", row.StepMarkers);
            Assert.Contains("ADVANCED_FACE", row.StepMarkers);
            Assert.Contains("CYLINDRICAL_SURFACE", row.StepMarkers);
            Assert.DoesNotContain("BREP_WITH_VOIDS", row.StepMarkers);
        }
    }

    [Fact]
    public void AirIntervalLab_FailedRows_HaveExactFailure()
    {
        foreach (var row in AirProfileStackIntervalLab.Run().Rows.Where(r => r.Status != "succeeded"))
        {
            Assert.False(string.IsNullOrWhiteSpace(row.FailureStage));
            Assert.False(string.IsNullOrWhiteSpace(row.FailureCode));
            Assert.NotEmpty(row.Diagnostics);
        }
    }

    [Fact]
    public void AirIntervalLab_RecommendationsAreExplicit()
    {
        var result = AirProfileStackIntervalLab.Run();
        Assert.Contains(result.BlindRecommendation, AirProfileStackIntervalLab.AllowedRecommendations);
        Assert.Contains(result.CounterboreRecommendation, AirProfileStackIntervalLab.AllowedRecommendations);
    }

    [Fact]
    public void AirIntervalLab_DeterministicResults()
    {
        var a = AirProfileStackIntervalLab.Run();
        var b = AirProfileStackIntervalLab.Run();
        Assert.Equal(string.Join('|', a.Rows.Select(r => $"{r.ScenarioName}:{r.RepresentationName}:{r.Status}:{r.FailureCode}")), string.Join('|', b.Rows.Select(r => $"{r.ScenarioName}:{r.RepresentationName}:{r.Status}:{r.FailureCode}")));
        Assert.Equal(a.BlindRecommendation, b.BlindRecommendation);
        Assert.Equal(a.CounterboreRecommendation, b.CounterboreRecommendation);
    }

    [Fact]
    public void AirIntervalLab_LegacyBaselinesRemainGreen()
    {
        var result = AirProfileStackIntervalLab.Run();
        Assert.Equal("succeeded", result.Rows.Single(r => r.RepresentationName == "B5-LegacyBooleanBaseline").Status);
        Assert.Equal("succeeded", result.Rows.Single(r => r.RepresentationName == "C5-LegacyBooleanBaseline").Status);
    }
}
