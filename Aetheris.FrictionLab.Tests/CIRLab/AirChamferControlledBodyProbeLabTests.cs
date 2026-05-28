using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class AirChamferControlledBodyProbeLabTests
{
    [Fact]
    public void Evaluate_ControlledCase_IsDeterministic()
    {
        var c = AirChamferControlledBodyProbeLab.Cases().Single(x => x.CaseName == "controlled-box-convex-planar-single-edge");
        var a = AirChamferControlledBodyProbeLab.Evaluate(c);
        var b = AirChamferControlledBodyProbeLab.Evaluate(c);

        Assert.Equal(a.Decision, b.Decision);
        Assert.Equal(a.Recommendation, b.Recommendation);
        Assert.Equal(a.Diagnostics, b.Diagnostics);
        Assert.Equal(a.ExpectedTopologyContract, b.ExpectedTopologyContract);
    }

    [Theory]
    [InlineData("controlled-box-convex-planar-single-edge")]
    [InlineData("controlled-wedge-nonorthogonal-convex-planar-single-edge")]
    public void Evaluate_AcceptedControlledCases_InvokeEdgeV1AndEmitDeferredBodyBlocker(string caseName)
    {
        var c = AirChamferControlledBodyProbeLab.Cases().Single(x => x.CaseName == caseName);
        var result = AirChamferControlledBodyProbeLab.Evaluate(c);

        Assert.True(result.ControlledBodyCreated);
        Assert.True(result.PrototypeInvoked);
        Assert.False(result.CandidateReplacementBodyCreated);
        Assert.Equal("air-chamfer-controlled-body-needs-body-mutation-hardening", result.Recommendation);
        Assert.Contains("edge-x7-candidate-replacement-body-deferred:body-mutation-not-implemented;using-closed-witness-artifact", result.Diagnostics);

        Assert.NotNull(result.CandidateTopology);
        Assert.NotNull(result.CandidateStep);
        Assert.Equal(result.CandidateTopology!.FaceCount, result.CandidateTopology.PlanarFaceCount);

        Assert.True(result.CandidateStep!.Succeeded);
        Assert.True(result.CandidateStep.HasIso);
        Assert.True(result.CandidateStep.HasManifoldSolidBrep);
        Assert.True(result.CandidateStep.HasAdvancedFace);
        Assert.True(result.CandidateStep.HasPlane);
        Assert.False(result.CandidateStep.HasCylindricalSurface);
        Assert.False(result.CandidateStep.HasBrepWithVoids);

        Assert.Contains("edge-x7-step-smoke-succeeded", result.Diagnostics);
        Assert.Contains("edge-x7-legacy-authority-preserved", result.Diagnostics);
        Assert.Contains("edge-x7-no-production-route-replacement", result.Diagnostics);
        Assert.Contains("edge-x7-no-3d-boolean-used", result.Diagnostics);
    }

    [Theory]
    [InlineData("controlled-body-invalid-distance", "air-chamfer-controlled-body-rejected-invalid")]
    [InlineData("controlled-body-invalid-target-edge", "air-chamfer-controlled-body-rejected-invalid")]
    [InlineData("controlled-body-missing-adjacent-face", "air-chamfer-controlled-body-rejected-invalid")]
    [InlineData("controlled-body-non-planar-adjacent-marker", "air-chamfer-controlled-body-rejected-invalid")]
    [InlineData("controlled-body-edge-chain", "air-chamfer-controlled-body-deferred-chain-or-corner")]
    [InlineData("controlled-body-corner-chain", "air-chamfer-controlled-body-deferred-chain-or-corner")]
    [InlineData("controlled-body-triangle-legacy-dependent", "air-chamfer-controlled-body-keep-legacy-route")]
    public void Evaluate_InvalidOrDeferredCases_StopBeforeCandidateBody(string caseName, string expectedRecommendation)
    {
        var c = AirChamferControlledBodyProbeLab.Cases().Single(x => x.CaseName == caseName);
        var result = AirChamferControlledBodyProbeLab.Evaluate(c);

        Assert.Equal(expectedRecommendation, result.Recommendation);
        Assert.False(result.CandidateReplacementBodyCreated);
        Assert.Null(result.CandidateTopology);
        Assert.Null(result.CandidateStep);
    }
}
