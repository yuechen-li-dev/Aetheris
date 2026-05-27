using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class ProfileBooleanNormalizationLabTests
{
    [Fact]
    public void DeterministicAcrossRuns()
    {
        var a = ProfileBooleanNormalizationLab.RunAll().Select(x => (x.CaseName, x.Status, x.OuterLoopCount, x.HoleCount, x.CurveCount, x.BoundingBox, Diagnostics: string.Join("|", x.Diagnostics), x.Recommendation));
        var b = ProfileBooleanNormalizationLab.RunAll().Select(x => (x.CaseName, x.Status, x.OuterLoopCount, x.HoleCount, x.CurveCount, x.BoundingBox, Diagnostics: string.Join("|", x.Diagnostics), x.Recommendation));
        Assert.Equal(a, b);
    }

    [Fact]
    public void RectangleMinusCircle_NormalizesToOneHole_AndValidates()
    {
        var row = ProfileBooleanNormalizationLab.RunAll().Single(x => x.CaseName == "success-rect-minus-circle");
        Assert.Equal(LabProfileStatus.Succeeded, row.Status);
        Assert.Equal(1, row.OuterLoopCount);
        Assert.Equal(1, row.HoleCount);
        Assert.Contains("profile-boolean-no-3d-boolean-used", row.Diagnostics);
        var validation = ResolvedProfile2DLab.Evaluate(row.CaseName, row.NormalizedProfile!);
        Assert.Equal(LabProfileStatus.Succeeded, validation.Status);
    }

    [Fact]
    public void RectangleMinusTwoCircles_NormalizesToTwoHoles()
    {
        var row = ProfileBooleanNormalizationLab.RunAll().Single(x => x.CaseName == "success-rect-minus-two-circles");
        Assert.Equal(LabProfileStatus.Succeeded, row.Status);
        Assert.Equal(2, row.HoleCount);
    }

    [Theory]
    [InlineData("invalid-circle-outside", "profile-boolean-circle-outside-rectangle")]
    [InlineData("invalid-circle-touches-boundary", "profile-boolean-circle-touches-boundary")]
    [InlineData("invalid-circles-overlap", "profile-boolean-circles-overlap")]
    [InlineData("invalid-circle-radius", "profile-boolean-invalid-primitive")]
    [InlineData("invalid-rectangle-dimensions", "profile-boolean-invalid-primitive")]
    [InlineData("invalid-unsupported-primitive", "profile-boolean-invalid-primitive")]
    public void InvalidCasesRejected(string caseName, string diagnostic)
    {
        var row = ProfileBooleanNormalizationLab.RunAll().Single(x => x.CaseName == caseName);
        Assert.Equal(LabProfileStatus.Failed, row.Status);
        Assert.Contains(diagnostic, row.Diagnostics);
    }

    [Theory]
    [InlineData("deferred-multiple-islands", "profile-boolean-multiple-islands-deferred")]
    [InlineData("deferred-disjoint-union", "profile-boolean-union-normalization-deferred")]
    [InlineData("deferred-partial-overlap-union", "profile-boolean-union-normalization-deferred")]
    [InlineData("deferred-partial-overlap-intersection", "profile-boolean-intersection-normalization-deferred")]
    [InlineData("deferred-nested-topology-expression", "profile-boolean-nested-topology-deferred")]
    [InlineData("deferred-capsule", "profile-boolean-capsule-deferred")]
    public void DeferredCasesExplicit(string caseName, string diagnostic)
    {
        var row = ProfileBooleanNormalizationLab.RunAll().Single(x => x.CaseName == caseName);
        Assert.Equal(LabProfileStatus.Deferred, row.Status);
        Assert.Contains(diagnostic, row.Diagnostics);
    }

    [Fact]
    public void Recommendations_AreFiniteAllowedSet()
    {
        var allowed = Enum.GetValues<ProfileBooleanRecommendation>().ToHashSet();
        foreach (var row in ProfileBooleanNormalizationLab.RunAll())
            Assert.Contains(row.Recommendation, allowed);
    }
}
