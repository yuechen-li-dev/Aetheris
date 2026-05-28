using System.Numerics;
using Aetheris.Firmament.FrictionLab.CIRLab;
using Aetheris.Kernel.Core.Brep;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class AirChamferShadowRouteTests
{
    [Fact]
    public void Run_CanonicalControlledCase_DeterministicNonAuthoritativeCandidateReport()
    {
        var a = AirChamferShadowRoute.Evaluate(Request("canonical-shadow", 1d));
        var b = AirChamferShadowRoute.Evaluate(Request("canonical-shadow", 1d));

        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(a with { ShadowCandidateBody = null }), System.Text.Json.JsonSerializer.Serialize(b with { ShadowCandidateBody = null }));
        AssertAccepted(a);
    }

    [Fact]
    public void Run_NonOrthogonalControlledCase_ProducesShadowCandidateWhenPrototypeSupportsIt()
    {
        var report = AirChamferShadowRoute.Evaluate(Request("safe-nonorthogonal-shadow", 1d, nonOrthogonal: true));
        AssertAccepted(report);
    }

    [Theory]
    [InlineData("invalid-distance", 0d, false, false, AirChamferFaceFamily.Planar, false, false, false, AirChamferShadowCandidateStatus.Rejected)]
    [InlineData("invalid-target-edge", 1d, true, false, AirChamferFaceFamily.Planar, false, false, false, AirChamferShadowCandidateStatus.Rejected)]
    [InlineData("missing-adjacent-face", 1d, false, true, AirChamferFaceFamily.Planar, false, false, false, AirChamferShadowCandidateStatus.Rejected)]
    [InlineData("non-planar-adjacent-marker", 1d, false, false, AirChamferFaceFamily.Cylindrical, false, false, false, AirChamferShadowCandidateStatus.Rejected)]
    [InlineData("edge-chain", 1d, false, false, AirChamferFaceFamily.Planar, true, false, false, AirChamferShadowCandidateStatus.Deferred)]
    [InlineData("corner-chain", 1d, false, false, AirChamferFaceFamily.Planar, false, true, false, AirChamferShadowCandidateStatus.Deferred)]
    [InlineData("legacy-triangle-dependent-fixture", 1d, false, false, AirChamferFaceFamily.Planar, false, false, true, AirChamferShadowCandidateStatus.FallbackLegacy)]
    public void Run_RejectedDeferredCases_DoNotProduceCandidateAndRemainNonAuthoritative(
        string caseName,
        double distance,
        bool invalidEdge,
        bool missingFace,
        AirChamferFaceFamily faceFamily,
        bool edgeChain,
        bool cornerChain,
        bool legacyDependent,
        AirChamferShadowCandidateStatus expectedStatus)
    {
        var a = AirChamferShadowRoute.Evaluate(Request(caseName, distance, invalidEdge, missingFace, faceFamily, edgeChain, cornerChain, legacyDependent));
        var b = AirChamferShadowRoute.Evaluate(Request(caseName, distance, invalidEdge, missingFace, faceFamily, edgeChain, cornerChain, legacyDependent));

        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(a with { ShadowCandidateBody = null }), System.Text.Json.JsonSerializer.Serialize(b with { ShadowCandidateBody = null }));
        Assert.True(a.LegacyAuthoritative);
        Assert.False(a.ProductionOutputChanged);
        Assert.False(a.ShadowCandidateProduced);
        Assert.Null(a.ShadowCandidateBody);
        Assert.Null(a.TopologySummary);
        Assert.Equal(expectedStatus, a.ShadowCandidateStatus);
        Assert.NotNull(a.FirstDivergence);
        Assert.Contains(a.Recommendation, AirChamferShadowRoute.AllowedRecommendations);
        Assert.Contains("edge-v3-shadow-route-started", a.Diagnostics);
        Assert.Contains("edge-v3-shadow-route-internal-only", a.Diagnostics);
        Assert.Contains("edge-v3-legacy-authority-preserved", a.Diagnostics);
        Assert.Contains("edge-v3-production-output-unchanged", a.Diagnostics);
        Assert.Contains("edge-v3-no-production-route-replacement", a.Diagnostics);
        Assert.Contains("edge-v3-no-3d-boolean-used", a.Diagnostics);
    }

    private static void AssertAccepted(AirChamferShadowReport report)
    {
        Assert.True(report.LegacyAuthoritative);
        Assert.False(report.ProductionOutputChanged);
        Assert.True(report.ShadowCandidateProduced);
        Assert.Equal(AirChamferShadowCandidateStatus.Succeeded, report.ShadowCandidateStatus);
        Assert.NotNull(report.ShadowCandidateBody);
        Assert.NotNull(report.TopologySummary);
        Assert.NotNull(report.StepSmokeSummary);
        Assert.NotNull(report.FeatureRecognitionSummary);
        Assert.Null(report.FirstDivergence);
        Assert.Equal("air-chamfer-shadow-ready-for-controlled-opt-in-route", report.Recommendation);
        Assert.True(report.StepSmokeSummary!.Succeeded);
        Assert.True(report.FeatureRecognitionSummary!.RecognitionContractSatisfied);
        Assert.Equal(1, report.TopologySummary!.ChamferFaceCount);
        Assert.Equal(2, report.TopologySummary.TrimmedAdjacentFaceCount);
        Assert.Equal(2, report.TopologySummary.TransitionEdgeCount);
        Assert.True(report.TopologySummary.OriginalEdgeReplaced);
        Assert.Equal(0, report.FeatureRecognitionSummary.CandidateSummary!.CylindricalFaceCount);

        Assert.Contains("edge-v3-shadow-route-started", report.Diagnostics);
        Assert.Contains("edge-v3-shadow-route-internal-only", report.Diagnostics);
        Assert.Contains("edge-v3-legacy-authority-preserved", report.Diagnostics);
        Assert.Contains("edge-v3-production-output-unchanged", report.Diagnostics);
        Assert.Contains("edge-v3-air-chamfer-real-body-prototype-invoked", report.Diagnostics);
        Assert.Contains("edge-v3-judgment-engine-used", report.Diagnostics);
        Assert.Contains("edge-v3-shadow-candidate-produced", report.Diagnostics);
        Assert.Contains("edge-v3-shadow-candidate-step-smoke-succeeded", report.Diagnostics);
        Assert.Contains("edge-v3-shadow-feature-recognition-captured", report.Diagnostics);
        Assert.Contains("edge-v3-shadow-feature-recognition-parity-succeeded", report.Diagnostics);
        Assert.Contains("edge-v3-no-production-route-replacement", report.Diagnostics);
        Assert.Contains("edge-v3-no-3d-boolean-used", report.Diagnostics);
    }

    private static AirChamferShadowRouteRequest Request(
        string caseName,
        double distance,
        bool invalidEdge = false,
        bool missingFace = false,
        AirChamferFaceFamily faceFamily = AirChamferFaceFamily.Planar,
        bool edgeChain = false,
        bool cornerChain = false,
        bool legacyDependent = false,
        bool nonOrthogonal = false)
    {
        var body = BrepPrimitives.CreateBox(10d, 8d, 6d).Value;
        var edgeStart = new Vector3(5f, 4f, -3f);
        var edgeEnd = invalidEdge ? edgeStart : new Vector3(5f, 4f, 3f);
        Vector3? faceA = new(1f, 0f, 0f);
        Vector3? faceB = missingFace ? null : (nonOrthogonal ? Vector3.Normalize(new Vector3(1f, 1f, 0f)) : new Vector3(0f, 1f, 0f));
        var classification = edgeChain || cornerChain || legacyDependent ? AirChamferClassificationExpectation.Concave : AirChamferClassificationExpectation.Convex;
        return new(caseName, body, edgeStart, edgeEnd, faceA, faceB, distance, faceFamily, edgeChain, cornerChain, legacyDependent, classification, !nonOrthogonal, 10d);
    }
}
