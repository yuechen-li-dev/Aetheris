using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class AirRuledTransitionFrustumLabTests
{
    [Fact]
    public void Matrix_IsDeterministic_AcrossRepeatedRuns()
    {
        string Canon(AirRuledTransitionFrustumRow r)
            => $"{r.Case.Name}|{r.RowKind}|{r.Topology}|{string.Join(',', r.StepSmoke.PresentMarkers)}|{string.Join(',', r.StepSmoke.MissingMarkers)}|{r.StepSmoke.ContainsBrepWithVoids}|{string.Join('|', r.Diagnostics)}|{r.IsValidInput}|{r.IsApexDeferredToRevolve}|{r.TopologyParityWithBaseline}|{r.StepSmokePassed}|{r.Recommendation}";
        var a = AirRuledTransitionFrustumLab.RunMatrix().Select(Canon).ToArray();
        var b = AirRuledTransitionFrustumLab.RunMatrix().Select(Canon).ToArray();
        Assert.Equal(a, b);
    }

    [Fact]
    public void HappyFrustumCases_ProduceBaselineAndCandidateBodies()
    {
        var rows = AirRuledTransitionFrustumLab.RunMatrix();
        foreach (var name in new[] { "frustum-5-2-10", "frustum-3-1-12", "frustum-inverted-2-5-10", "frustum-cylinder-like-4-4-10" })
        {
            var pair = rows.Where(r => r.Case.Name == name).ToArray();
            Assert.Equal(2, pair.Length);
            Assert.All(pair, r => Assert.True(r.Topology.BodyProduced));
        }
    }

    [Fact]
    public void CandidateRows_TopologyParityAndStepSmoke_AreDeterministic()
    {
        foreach (var row in AirRuledTransitionFrustumLab.RunMatrix().Where(r => r.RowKind == "candidate" && r.IsValidInput && !r.IsApexDeferredToRevolve && r.Case.BottomRadius != r.Case.TopRadius))
        {
            Assert.True(row.TopologyParityWithBaseline);
            Assert.True(row.StepSmokePassed);
            Assert.Contains("air-x6-topology-parity-succeeded", row.Diagnostics);
            Assert.Contains("air-x6-step-smoke-succeeded", row.Diagnostics);
        }
    }

    [Fact]
    public void ApexCases_AreExplicitlyDeferredToRevolve()
    {
        var rows = AirRuledTransitionFrustumLab.RunMatrix().Where(r => r.RowKind == "candidate" && (r.Case.TopRadius == 0 || r.Case.BottomRadius == 0)).ToArray();
        Assert.NotEmpty(rows);
        foreach (var row in rows)
        {
            Assert.True(row.IsApexDeferredToRevolve);
            Assert.Contains("air-x6-apex-cone-deferred-to-revolve", row.Diagnostics);
            Assert.Equal("frustum-apex-cone-defer-to-revolve", row.Recommendation);
        }
    }

    [Fact]
    public void InvalidInputs_RejectDeterministically()
    {
        var invalid = AirRuledTransitionFrustumLab.RunMatrix().Where(r => !r.IsValidInput).ToArray();
        Assert.NotEmpty(invalid);
        foreach (var row in invalid)
        {
            Assert.Contains("air-x6-invalid-input-rejected", row.Diagnostics);
            Assert.Equal("frustum-invalid-input-rejected", row.Recommendation);
        }
    }

    [Fact]
    public void Recommendations_AreFromAllowedSet()
    {
        foreach (var row in AirRuledTransitionFrustumLab.RunMatrix())
        {
            Assert.Contains(row.Recommendation, AirRuledTransitionFrustumLab.AllowedRecommendations);
        }
    }
}
