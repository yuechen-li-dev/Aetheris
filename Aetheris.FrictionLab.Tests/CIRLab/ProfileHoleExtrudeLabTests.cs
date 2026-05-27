using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class ProfileHoleExtrudeLabTests
{
    [Fact]
    public void DeterministicAcrossRuns()
    {
        var a = ProfileHoleExtrudeLab.RunAll();
        var b = ProfileHoleExtrudeLab.RunAll();
        Assert.Equal(string.Join(";", a.Select(x => $"{x.CaseName}:{x.Recommendation}:{string.Join(',', x.Diagnostics)}")), string.Join(";", b.Select(x => $"{x.CaseName}:{x.Recommendation}:{string.Join(',', x.Diagnostics)}")));
    }

    [Fact]
    public void CenterHoleProducesBodyAndNoBooleanSubtractDiagnostic()
    {
        var row = ProfileHoleExtrudeLab.RunAll().Single(x => x.CaseName == "valid-rect-center-hole");
        Assert.True(row.Topology.BodyProduced);
        Assert.Contains("v2-x3-no-3d-boolean-subtract-used", row.Diagnostics);
        Assert.Equal(1, row.Topology.CylindricalFaceCount);
        Assert.True(row.Step.Exported);
    }

    [Fact]
    public void TwoHolesProduceTwoCylinders()
    {
        var row = ProfileHoleExtrudeLab.RunAll().Single(x => x.CaseName == "valid-rect-two-holes");
        Assert.Equal(2, row.Topology.CylindricalFaceCount);
    }

    [Theory]
    [InlineData("invalid-hole-outside")]
    [InlineData("invalid-hole-touches-boundary")]
    [InlineData("invalid-hole-overlap")]
    [InlineData("invalid-height")]
    [InlineData("invalid-hole-radius")]
    [InlineData("invalid-open-outer")]
    public void InvalidCasesRejectedBeforeEmission(string caseName)
    {
        var row = ProfileHoleExtrudeLab.RunAll().Single(x => x.CaseName == caseName);
        Assert.False(row.Topology.BodyProduced);
        Assert.Contains("v2-x3-invalid-profile-rejected", row.Diagnostics);
    }

    [Fact]
    public void RecommendationsAreFiniteSet()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "profile-hole-extrude-ready-for-production-evaluation",
            "profile-hole-extrude-needs-emitter-parity-work",
            "profile-hole-extrude-invalid-profile-rejected",
            "profile-hole-extrude-deferred-topology"
        };

        foreach (var row in ProfileHoleExtrudeLab.RunAll()) Assert.Contains(row.Recommendation, allowed);
    }
}
