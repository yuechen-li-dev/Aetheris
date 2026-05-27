using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class LineArcProfileExtrudeLabTests
{
    private static string Stable(LineArcProfileExtrudeRow r) => $"{r.CaseName}|{r.Status}|{r.Succeeded}|{r.Topology.FaceCount}|{r.Topology.PlanarFaceCount}|{r.Topology.CylindricalFaceCount}|{string.Join(",", r.Step.PresentMarkers.OrderBy(x=>x,StringComparer.Ordinal))}|{string.Join(",", r.Step.MissingMarkers.OrderBy(x=>x,StringComparer.Ordinal))}|{string.Join(",", r.Diagnostics.OrderBy(x=>x,StringComparer.Ordinal))}|{r.Recommendation}";
    [Fact]
    public void LineArcProfileExtrudeLab_Deterministic()
    {
        var a = LineArcProfileExtrudeLab.RunAll();
        var b = LineArcProfileExtrudeLab.RunAll();
        Assert.Equal(a.Select(Stable), b.Select(Stable));
    }

    [Theory]
    [InlineData("valid-rectangle-only", 6, 0)]
    [InlineData("valid-rectangle-circle-hole", 6, 1)]
    [InlineData("valid-rectangle-slot-centered", 8, 2)]
    public void LineArcProfileExtrudeLab_ExpectedTopology(string caseName, int planar, int cyl)
    {
        var row = LineArcProfileExtrudeLab.RunAll().Single(x => x.CaseName == caseName);
        Assert.True(row.Succeeded);
        Assert.Equal(planar, row.Topology.PlanarFaceCount);
        Assert.Equal(cyl, row.Topology.CylindricalFaceCount);
        Assert.Contains("v2-x7-no-3d-boolean-used", row.Diagnostics);
        Assert.Contains("v2-x7-step-smoke-succeeded", row.Diagnostics);
    }

    [Fact]
    public void LineArcProfileExtrudeLab_RecommendationsFinite()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "line-arc-profile-extrude-ready-for-production-evaluation",
            "line-arc-profile-extrude-needs-emitter-hardening",
            "line-arc-profile-extrude-invalid-rejected",
            "line-arc-profile-extrude-deferred-topology"
        };
        foreach (var row in LineArcProfileExtrudeLab.RunAll()) Assert.Contains(row.Recommendation, allowed);
    }
}
