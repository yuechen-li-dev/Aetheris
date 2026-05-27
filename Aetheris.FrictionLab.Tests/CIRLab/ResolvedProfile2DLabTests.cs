using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class ResolvedProfile2DLabTests
{
    [Fact]
    public void ResolvedProfile2DLab_DeterministicAcrossRuns()
    {
        var a = ResolvedProfile2DLab.RunAll();
        var b = ResolvedProfile2DLab.RunAll();
        Assert.Equal(string.Join("|", a.Select(x => $"{x.CaseName}:{x.Status}:{string.Join(',', x.Diagnostics)}")),
            string.Join("|", b.Select(x => $"{x.CaseName}:{x.Status}:{string.Join(',', x.Diagnostics)}")));
    }

    [Fact]
    public void ResolvedProfile2DLab_ValidCasesSucceed()
    {
        var all = ResolvedProfile2DLab.RunAll().ToDictionary(x => x.CaseName, StringComparer.Ordinal);
        Assert.Equal(LabProfileStatus.Succeeded, all["valid-rectangle"].Status);
        Assert.Equal(LabProfileStatus.Succeeded, all["valid-circle"].Status);
        Assert.Equal(LabProfileStatus.Succeeded, all["valid-rectangle-one-hole"].Status);
        Assert.Equal(1, all["valid-rectangle-one-hole"].HoleCount);
        Assert.Equal(LabProfileStatus.Succeeded, all["valid-rectangle-two-holes"].Status);
        Assert.Equal(2, all["valid-rectangle-two-holes"].HoleCount);
    }

    [Fact]
    public void ResolvedProfile2DLab_OrientationNormalizationIsDeterministic()
    {
        var row = ResolvedProfile2DLab.RunAll().Single(x => x.CaseName == "valid-orientation-reversed-input");
        Assert.Equal(LabProfileStatus.Succeeded, row.Status);
        Assert.Contains("profile-normalized-orientation", row.Diagnostics);
    }

    [Theory]
    [InlineData("invalid-open-loop", "profile-loop-open")]
    [InlineData("invalid-endpoint-mismatch", "profile-loop-open")]
    [InlineData("invalid-zero-length-line", "profile-loop-zero-length-segment")]
    [InlineData("invalid-self-intersecting-bowtie", "profile-loop-self-intersection")]
    [InlineData("invalid-hole-outside", "profile-region-hole-outside-outer")]
    [InlineData("invalid-hole-touches-boundary", "profile-region-hole-touches-boundary")]
    [InlineData("invalid-hole-overlap", "profile-region-hole-overlaps-hole")]
    [InlineData("deferred-multiple-outers", "profile-region-multiple-outer-loops-deferred")]
    public void ResolvedProfile2DLab_ReportsExpectedDiagnostics(string caseName, string diagnostic)
    {
        var row = ResolvedProfile2DLab.RunAll().Single(x => x.CaseName == caseName);
        Assert.Contains(diagnostic, row.Diagnostics);
    }

    [Fact]
    public void ResolvedProfile2DLab_RecommendationsAreFiniteSet()
    {
        var allowed = Enum.GetValues<LabProfileRecommendation>().Cast<LabProfileRecommendation>().ToHashSet();
        var rows = ResolvedProfile2DLab.RunAll();
        Assert.All(rows, row => Assert.Contains(row.Recommendation, allowed));
    }
}
