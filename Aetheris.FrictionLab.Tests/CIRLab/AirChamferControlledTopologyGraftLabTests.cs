using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class AirChamferControlledTopologyGraftLabTests
{
    [Fact]
    public void Evaluate_ControlledCase_IsDeterministic()
    {
        var c = AirChamferControlledTopologyGraftLab.Cases().Single(x => x.CaseName == "controlled-box-convex-planar-single-edge-graft");
        var a = AirChamferControlledTopologyGraftLab.Evaluate(c);
        var b = AirChamferControlledTopologyGraftLab.Evaluate(c);

        Assert.Equal(a.Decision, b.Decision);
        Assert.Equal(a.Recommendation, b.Recommendation);
        Assert.Equal(a.Diagnostics, b.Diagnostics);
        Assert.Equal(a.ExpectedTopologyContract, b.ExpectedTopologyContract);
    }

    [Theory]
    [InlineData("controlled-box-convex-planar-single-edge-graft")]
    [InlineData("controlled-wedge-nonorthogonal-convex-planar-single-edge-graft")]
    public void Evaluate_AcceptedControlledCases_ProduceCandidateBody(string caseName)
    {
        var c = AirChamferControlledTopologyGraftLab.Cases().Single(x => x.CaseName == caseName);
        var result = AirChamferControlledTopologyGraftLab.Evaluate(c);

        Assert.True(result.ControlledBodyCreated);
        Assert.True(result.PrototypeInvoked);
        Assert.True(result.GraftAttempted);
        Assert.True(result.CandidateBodyCreated);
        Assert.Null(result.CandidateBodyBlocker);
        Assert.Equal("air-chamfer-topology-graft-ready-for-production-adjacent-prototype", result.Recommendation);

        Assert.NotNull(result.CandidateSummary);
        Assert.Equal(result.CandidateSummary!.FaceCount, result.CandidateSummary.PlanarFaceCount);
        Assert.True(result.CandidateSummary.OriginalEdgeReplaced);
        Assert.True(result.CandidateSummary.OrientationValidated);
        Assert.True(result.CandidateSummary.TopologyValidated);

        Assert.Equal(result.ExpectedTopologyContract!["faceCount"], result.CandidateSummary.FaceCount);
        Assert.Equal(result.ExpectedTopologyContract["planarFaceCount"], result.CandidateSummary.PlanarFaceCount);
        Assert.Equal(result.ExpectedTopologyContract["edgeCount"], result.CandidateSummary.EdgeCount);
        Assert.Equal(result.ExpectedTopologyContract["vertexCount"], result.CandidateSummary.VertexCount);
        Assert.Equal(result.ExpectedTopologyContract["chamferFaceCount"], result.CandidateSummary.ChamferFaceCount);
        Assert.Equal(result.ExpectedTopologyContract["trimmedAdjacentFaceCount"], result.CandidateSummary.TrimmedAdjacentFaceCount);
        Assert.Equal(result.ExpectedTopologyContract["transitionEdgeCount"], result.CandidateSummary.TransitionEdgeCount);

        Assert.NotNull(result.CandidateStep);
        Assert.True(result.CandidateStep!.Succeeded);
        Assert.True(result.CandidateStep.HasIso);
        Assert.True(result.CandidateStep.HasManifoldSolidBrep);
        Assert.True(result.CandidateStep.HasAdvancedFace);
        Assert.True(result.CandidateStep.HasPlane);
        Assert.False(result.CandidateStep.HasCylindricalSurface);
        Assert.False(result.CandidateStep.HasBrepWithVoids);

        Assert.Contains("edge-x8-step-smoke-succeeded", result.Diagnostics);
        Assert.Contains("edge-x8-legacy-authority-preserved", result.Diagnostics);
        Assert.Contains("edge-x8-no-production-route-replacement", result.Diagnostics);
        Assert.Contains("edge-x8-no-3d-boolean-used", result.Diagnostics);
    }

    [Theory]
    [InlineData("controlled-graft-invalid-distance", "air-chamfer-topology-graft-rejected-invalid")]
    [InlineData("controlled-graft-invalid-target-edge", "air-chamfer-topology-graft-rejected-invalid")]
    [InlineData("controlled-graft-missing-adjacent-face", "air-chamfer-topology-graft-rejected-invalid")]
    [InlineData("controlled-graft-non-planar-adjacent-marker", "air-chamfer-topology-graft-rejected-invalid")]
    [InlineData("controlled-graft-edge-chain", "air-chamfer-topology-graft-deferred-chain-or-corner")]
    [InlineData("controlled-graft-corner-chain", "air-chamfer-topology-graft-deferred-chain-or-corner")]
    [InlineData("controlled-graft-triangle-legacy-dependent", "air-chamfer-topology-graft-keep-legacy-route")]
    public void Evaluate_InvalidOrDeferredCases_StopBeforeGraft(string caseName, string expectedRecommendation)
    {
        var c = AirChamferControlledTopologyGraftLab.Cases().Single(x => x.CaseName == caseName);
        var result = AirChamferControlledTopologyGraftLab.Evaluate(c);

        Assert.Equal(expectedRecommendation, result.Recommendation);
        Assert.False(result.GraftAttempted);
        Assert.False(result.CandidateBodyCreated);
        Assert.NotNull(result.CandidateBodyBlocker);
        Assert.Null(result.CandidateSummary);
        Assert.Null(result.CandidateStep);
    }
}
