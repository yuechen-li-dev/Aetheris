using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class AirPrimitiveMatrixLabTests
{
    [Fact]
    public void Matrix_IsDeterministic()
    {
        var a = AirPrimitiveMatrixLab.RunMatrix();
        var b = AirPrimitiveMatrixLab.RunMatrix();
        Assert.Equal(a.Select(x => $"{x.Case.Name}|{x.RowKind}|{x.CandidateKind}|{x.Recommendation}|{x.Topology}|{x.StepSmoke.Exported}|{x.StepSmoke.ContainsBrepWithVoids}|{string.Join(",", x.StepSmoke.MissingMarkers)}|{string.Join(",", x.Diagnostics)}"), b.Select(x => $"{x.Case.Name}|{x.RowKind}|{x.CandidateKind}|{x.Recommendation}|{x.Topology}|{x.StepSmoke.Exported}|{x.StepSmoke.ContainsBrepWithVoids}|{string.Join(",", x.StepSmoke.MissingMarkers)}|{string.Join(",", x.Diagnostics)}"));
    }

    [Fact]
    public void Matrix_HasBaselineForEachPrimitiveFamily()
    {
        var baselines = AirPrimitiveMatrixLab.RunMatrix().Where(r => r.RowKind == "baseline" && r.IsValidInput).Select(r => r.Case.Primitive).Distinct().ToArray();
        Assert.Contains(AirPrimitiveBaselineKind.Cylinder, baselines);
        Assert.Contains(AirPrimitiveBaselineKind.ConeFrustum, baselines);
        Assert.Contains(AirPrimitiveBaselineKind.Sphere, baselines);
        Assert.Contains(AirPrimitiveBaselineKind.Torus, baselines);
    }

    [Fact]
    public void BaselineRows_ProduceBodies_AndStepSmoke()
    {
        foreach (var row in AirPrimitiveMatrixLab.RunMatrix().Where(r => r.RowKind == "baseline" && r.IsValidInput))
        {
            Assert.True(row.Topology.BodyProduced);
            Assert.True(row.StepSmoke.Exported);
            Assert.Empty(row.StepSmoke.MissingMarkers);
            Assert.False(row.StepSmoke.ContainsBrepWithVoids);
        }
    }

    [Fact]
    public void CandidateRows_AreProducedOrExplicitlyUnavailable()
    {
        foreach (var row in AirPrimitiveMatrixLab.RunMatrix().Where(r => r.RowKind == "candidate"))
        {
            Assert.True(row.Topology.BodyProduced || row.Diagnostics.Any(d => d.StartsWith("air-x5-air-candidate-unavailable:", StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void InvalidCases_AreDeterministicAndBounded()
    {
        foreach (var row in AirPrimitiveMatrixLab.RunMatrix().Where(r => !r.IsValidInput && r.RowKind == "baseline"))
        {
            Assert.False(row.Topology.BodyProduced);
            Assert.Contains(row.Diagnostics, d => d.Contains("air-x5-baseline-created:false", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Recommendations_AreFromFiniteSet()
    {
        foreach (var row in AirPrimitiveMatrixLab.RunMatrix())
        {
            Assert.Contains(row.Recommendation, AirPrimitiveMatrixLab.AllowedRecommendations);
        }
    }

    [Fact]
    public void CylinderReadyOnlyWhenParityMatches()
    {
        foreach (var row in AirPrimitiveMatrixLab.RunMatrix().Where(r => r.Case.Primitive == AirPrimitiveBaselineKind.Cylinder && r.RowKind == "candidate"))
        {
            if (row.Recommendation == "ready-for-production-migration")
            {
                Assert.True(row.TopologyParityWithBaseline);
                Assert.True(row.StepParityWithBaseline);
            }
        }
    }

    [Fact]
    public void SphereAndTorus_NotReadyWithoutExplicitParityProof()
    {
        foreach (var row in AirPrimitiveMatrixLab.RunMatrix().Where(r => r.Case.Primitive is AirPrimitiveBaselineKind.Sphere or AirPrimitiveBaselineKind.Torus && r.RowKind == "candidate"))
        {
            Assert.NotEqual("ready-for-production-migration", row.Recommendation);
        }
    }
}
