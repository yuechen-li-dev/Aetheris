using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class TrianglePrismChamferAdjacencyParityLabTests
{
    private static string Stable(TrianglePrismChamferAdjacencyParityRow r)
        => $"{r.CaseName}|{r.LegacyProduced}|{r.CandidateProduced}|{r.TopologySummaryParity}|{r.AdjacencySummaryParity}|{r.FeatureRecognitionParity}|{r.FirstDivergence}|{r.BlockerClassification}|{r.LegacyAdjacency.FaceCount}|{r.CandidateAdjacency.FaceCount}|{r.LegacyChamfer.CornerAdmissibleCount}|{r.CandidateChamfer.CornerAdmissibleCount}|{string.Join(',', r.Diagnostics)}|{r.Recommendation}";

    [Fact]
    public void Lab_IsDeterministic()
    {
        var a = TrianglePrismChamferAdjacencyParityLab.RunAll();
        var b = TrianglePrismChamferAdjacencyParityLab.RunAll();
        Assert.Equal(a.Select(Stable), b.Select(Stable));
    }

    [Fact]
    public void Rows_ProduceLegacyAndCandidateAndCaptureDiagnostics()
    {
        var rows = TrianglePrismChamferAdjacencyParityLab.RunAll();
        Assert.All(rows, row =>
        {
            Assert.True(row.LegacyProduced);
            Assert.True(row.CandidateProduced);
            Assert.Contains("v2-x8-1-no-3d-boolean-used", row.Diagnostics);
            Assert.Contains("v2-x8-1-adjacency-summary-captured", row.Diagnostics);
            Assert.Contains("v2-x8-1-chamfer-candidates-captured", row.Diagnostics);
            Assert.NotEmpty(row.LegacyAdjacency.EdgeRows);
            Assert.NotEmpty(row.CandidateAdjacency.EdgeRows);
        });
    }

    [Fact]
    public void Rows_ReportParityOrMismatchWithRecommendation()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "triangle-feature-recognition-parity-ready",
            "triangle-feature-recognition-needs-emitter-ordering-parity",
            "triangle-feature-recognition-needs-adjacency-parity",
            "triangle-feature-recognition-needs-corner-resolution-contract",
            "triangle-feature-recognition-keep-legacy-route"
        };

        foreach (var row in TrianglePrismChamferAdjacencyParityLab.RunAll())
        {
            Assert.Contains(row.Recommendation, allowed);
            Assert.False(string.IsNullOrWhiteSpace(row.BlockerClassification));
            if (row.FeatureRecognitionParity)
            {
                Assert.Equal("triangle-feature-recognition-parity-ready", row.Recommendation);
            }
            else
            {
                Assert.NotNull(row.FirstDivergence);
                Assert.Contains(row.Diagnostics, d => d.StartsWith("v2-x8-1-feature-recognition-parity-mismatch:", StringComparison.Ordinal));
                Assert.DoesNotContain("triangle-feature-recognition-parity-ready", row.Recommendation);
            }
        }
    }
}
