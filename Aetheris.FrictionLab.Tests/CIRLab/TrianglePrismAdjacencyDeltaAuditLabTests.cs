using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class TrianglePrismAdjacencyDeltaAuditLabTests
{
    private static string Stable(TrianglePrismAdjacencyDeltaRow r)
        => $"{r.CaseName}|{r.LegacyProduced}|{r.CandidateProduced}|{r.FirstDeltaCategory}|{r.FirstDeltaPayload}|{r.LegacyChamfer.CornerAdmissibleCount}|{r.CandidateChamfer.CornerAdmissibleCount}|{string.Join(',', r.Diagnostics)}|{r.Recommendation}";

    [Fact]
    public void Lab_IsDeterministic()
    {
        var a = TrianglePrismAdjacencyDeltaAuditLab.RunAll();
        var b = TrianglePrismAdjacencyDeltaAuditLab.RunAll();
        Assert.Equal(a.Select(Stable), b.Select(Stable));
    }

    [Fact]
    public void Rows_CaptureBodiesLedgersDeltaChamferAndRecommendation()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "triangle-adjacency-delta-fix-emitter-ordering",
            "triangle-adjacency-delta-fix-cap-loop-convention",
            "triangle-adjacency-delta-fix-side-face-convention",
            "triangle-adjacency-delta-fix-edge-orientation",
            "triangle-adjacency-delta-fix-vertex-incidence",
            "triangle-adjacency-delta-update-chamfer-contract",
            "triangle-adjacency-delta-legacy-route-required",
            "triangle-adjacency-delta-no-action-parity-ready"
        };

        foreach (var row in TrianglePrismAdjacencyDeltaAuditLab.RunAll())
        {
            Assert.True(row.LegacyProduced);
            Assert.True(row.CandidateProduced);
            Assert.NotNull(row.LegacyLedger);
            Assert.NotNull(row.CandidateLedger);
            Assert.Contains("v2-x8-2-no-3d-boolean-used", row.Diagnostics);
            Assert.Contains(row.Recommendation, allowed);
            Assert.True(row.LegacyChamfer.CornerCandidateCount >= 1);
            Assert.True(row.CandidateChamfer.CornerCandidateCount >= 1);
            Assert.Contains(row.Diagnostics, d => d.StartsWith("v2-x8-2-chamfer-delta:", StringComparison.Ordinal));

            if (row.FirstDeltaCategory is null)
            {
                Assert.Equal("triangle-adjacency-delta-no-action-parity-ready", row.Recommendation);
                Assert.Contains("v2-x8-2-no-delta-detected", row.Diagnostics);
            }
            else
            {
                Assert.NotEqual("body-count-mismatch", row.FirstDeltaCategory);
                Assert.NotEqual("adjacency:topology-count-mismatch", row.FirstDeltaCategory);
                Assert.False(string.IsNullOrWhiteSpace(row.FirstDeltaPayload));
            }
        }
    }
}
