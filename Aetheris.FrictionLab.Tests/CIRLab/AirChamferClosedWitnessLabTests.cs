using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class AirChamferClosedWitnessLabTests
{
    [Fact]
    public void RunAll_IsDeterministicAcrossRepeatedRuns()
    {
        var a = AirChamferClosedWitnessLab.RunAll();
        var b = AirChamferClosedWitnessLab.RunAll();
        Assert.Equal(a.Select(r => $"{r.CaseName}|{r.Decision}|{r.Recommendation}|{r.WitnessProduced}|{string.Join(",", r.Diagnostics)}"), b.Select(r => $"{r.CaseName}|{r.Decision}|{r.Recommendation}|{r.WitnessProduced}|{string.Join(",", r.Diagnostics)}"));
    }

    [Fact]
    public void CanonicalConvexPlanar_CreatesClosedWitnessAndStepSmokeMarkers()
    {
        var result = AirChamferClosedWitnessLab.Evaluate(AirChamferClosedWitnessLab.Cases().Single(x => x.CaseName == "canonical-orthogonal-convex-planar-single-edge"));
        var witness = Assert.IsType<AirChamferClosedWitnessBody>(result.Witness);

        Assert.True(witness.TopologySummary.BodyProduced);
        Assert.True(witness.TopologySummary.IsClosedManifold);
        Assert.Equal(witness.TopologySummary.FaceCount, witness.TopologySummary.PlanarFaceCount);
        Assert.True(witness.TopologySummary.FaceCount >= 6);

        Assert.True(witness.StepSummary.Succeeded);
        Assert.True(witness.StepSummary.HasIso);
        Assert.True(witness.StepSummary.HasManifoldSolidBrep);
        Assert.True(witness.StepSummary.HasAdvancedFace);
        Assert.True(witness.StepSummary.HasPlane);
        Assert.False(witness.StepSummary.HasCylindricalSurface);
        Assert.False(witness.StepSummary.HasBrepWithVoids);

        Assert.Contains("edge-x6-judgment-engine-used", result.Diagnostics);
        Assert.Contains("edge-x6-topology-plan-created", result.Diagnostics);
        Assert.Contains("edge-x6-geometry-artifact-created", result.Diagnostics);
        Assert.Contains("edge-x6-step-smoke-succeeded", result.Diagnostics);
        Assert.DoesNotContain("edge-x6-step-smoke-failed", result.Diagnostics);
    }

    [Theory]
    [InlineData("convex-planar-unsafe-envelope")]
    [InlineData("invalid-distance")]
    [InlineData("invalid-edge")]
    [InlineData("invalid-face-adjacency")]
    [InlineData("ambiguous-classification")]
    [InlineData("edge-chain")]
    [InlineData("corner-chain")]
    [InlineData("triangle-legacy-dependent")]
    public void InvalidDeferredOrLegacyCases_StopBeforeWitness(string caseName)
    {
        var result = AirChamferClosedWitnessLab.Evaluate(AirChamferClosedWitnessLab.Cases().Single(x => x.CaseName == caseName));
        Assert.Null(result.Witness);
    }
}
