using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class ProfileBooleanExtrudePipelineLabTests
{
    [Fact]
    public void RunAll_is_deterministic()
    {
        var a = ProfileBooleanExtrudePipelineLab.RunAll().Select(x => (x.CaseName, x.Status, x.HoleCount, x.PlanarFaceCount, x.CylindricalFaceCount, string.Join("|", x.Diagnostics), x.Recommendation));
        var b = ProfileBooleanExtrudePipelineLab.RunAll().Select(x => (x.CaseName, x.Status, x.HoleCount, x.PlanarFaceCount, x.CylindricalFaceCount, string.Join("|", x.Diagnostics), x.Recommendation));
        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData("success-centered", 1)]
    [InlineData("success-offcenter", 1)]
    [InlineData("success-two-holes", 2)]
    public void Success_cases_emit_step_valid_body(string caseName, int expectedHoles)
    {
        var row = ProfileBooleanExtrudePipelineLab.RunAll().Single(x => x.CaseName == caseName);
        Assert.Equal(LabProfileStatus.Succeeded, row.Status);
        Assert.Equal(expectedHoles, row.HoleCount);
        Assert.Equal(6, row.PlanarFaceCount);
        Assert.Equal(expectedHoles, row.CylindricalFaceCount);
        Assert.True(row.StepSmokePassed);
        Assert.Contains("v2-x5-no-3d-boolean-used", row.Diagnostics);
        Assert.Contains("v2-v3-no-3d-boolean-used", row.Diagnostics);
        Assert.DoesNotContain(row.Diagnostics, d => d.Contains("BREP_WITH_VOIDS", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("invalid-circle-outside")]
    [InlineData("invalid-height")]
    [InlineData("deferred-capsule")]
    [InlineData("deferred-disjoint-union")]
    public void Invalid_or_deferred_stop_before_emission(string caseName)
    {
        var row = ProfileBooleanExtrudePipelineLab.RunAll().Single(x => x.CaseName == caseName);
        Assert.NotEqual(LabProfileStatus.Succeeded, row.Status);
        Assert.DoesNotContain("v2-x5-profile-hole-extrude-succeeded", row.Diagnostics);
    }

    [Fact]
    public void Recommendation_set_is_bounded()
    {
        var allowed = new[]
        {
            ProfileBooleanRecommendation.profile_boolean_extrude_ready_for_production_evaluation,
            ProfileBooleanRecommendation.profile_boolean_extrude_normalization_rejected,
            ProfileBooleanRecommendation.profile_boolean_extrude_deferred_topology,
            ProfileBooleanRecommendation.profile_boolean_extrude_emitter_blocked,
            ProfileBooleanRecommendation.profile_boolean_extrude_needs_production_profile_adapter
        }.ToHashSet();
        foreach (var row in ProfileBooleanExtrudePipelineLab.RunAll()) Assert.Contains(row.Recommendation, allowed);
    }
}
