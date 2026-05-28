using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class TriangleHexPrismProfileParityLabTests
{
    private static string Stable(PrismProfileParityRow r)
        => $"{r.CaseName}|{r.Kind}|{r.IsValidInput}|{r.BaselineSucceeded}|{r.CandidateSucceeded}|{r.TopologyParityWithBaseline}|{r.BaselineTopology.FaceCount}|{r.CandidateTopology.FaceCount}|{r.BaselineTopology.PlanarFaceCount}|{r.CandidateTopology.PlanarFaceCount}|{r.StepSmoke.Exported}|{string.Join(',', r.Diagnostics)}|{r.Recommendation}";

    [Fact]
    public void PrismProfileParityLab_IsDeterministic()
    {
        var a = TriangleHexPrismProfileParityLab.RunAll();
        var b = TriangleHexPrismProfileParityLab.RunAll();
        Assert.Equal(a.Select(Stable), b.Select(Stable));
    }

    [Fact]
    public void TriangleAndHex_ValidRows_ProduceBodiesAndNo3DBoolean()
    {
        var rows = TriangleHexPrismProfileParityLab.RunAll().Where(r => r.IsValidInput).ToArray();
        Assert.All(rows, row =>
        {
            Assert.True(row.BaselineSucceeded);
            Assert.True(row.CandidateSucceeded);
            Assert.Contains("v2-x8-no-3d-boolean-used", row.Diagnostics);
            Assert.True(row.StepSmoke.Exported);
        });

        Assert.All(rows.Where(x => x.Kind == PrismKind.Triangle), row =>
        {
            Assert.Equal(5, row.CandidateTopology.PlanarFaceCount);
            Assert.Equal(0, row.CandidateTopology.CylindricalFaceCount);
        });

        Assert.All(rows.Where(x => x.Kind == PrismKind.Hex), row =>
        {
            Assert.Equal(8, row.CandidateTopology.PlanarFaceCount);
            Assert.Equal(0, row.CandidateTopology.CylindricalFaceCount);
        });
    }

    [Fact]
    public void ValidRows_ReportParityOrExplicitMismatch()
    {
        var rows = TriangleHexPrismProfileParityLab.RunAll().Where(r => r.IsValidInput).ToArray();
        Assert.All(rows, row =>
        {
            if (!row.TopologyParityWithBaseline)
            {
                Assert.Contains(row.Diagnostics, d => d.StartsWith("v2-x8-topology-parity-mismatch:", StringComparison.Ordinal));
            }
        });
    }

    [Fact]
    public void InvalidRows_AreRejectedDeterministically()
    {
        var rows = TriangleHexPrismProfileParityLab.RunAll().Where(r => !r.IsValidInput).ToArray();
        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Assert.Equal("prism-profile-invalid-rejected", row.Recommendation);
            Assert.Contains("v2-x8-invalid-input-rejected", row.Diagnostics);
        });
    }

    [Fact]
    public void Recommendations_AreFinite()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "prism-profile-ready-for-production-migration",
            "prism-profile-needs-emitter-parity-work",
            "prism-profile-invalid-rejected",
            "prism-profile-deferred-convention-mismatch"
        };
        Assert.All(TriangleHexPrismProfileParityLab.RunAll(), row => Assert.Contains(row.Recommendation, allowed));
    }
}
