using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class AirChamferFeatureRecognitionParityLabTests
{
    [Fact]
    public void RunAll_Deterministic()
    {
        var a = AirChamferFeatureRecognitionParityLab.RunAll();
        var b = AirChamferFeatureRecognitionParityLab.RunAll();
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(a), System.Text.Json.JsonSerializer.Serialize(b));
    }

    [Theory]
    [InlineData("canonical-orthogonal-edge-v2-candidate")]
    [InlineData("safe-nonorthogonal-edge-v2-candidate")]
    public void ControlledCases_CaptureRecognitionLedger(string caseName)
    {
        var c = AirChamferFeatureRecognitionParityLab.Cases().Single(x => x.CaseName == caseName);
        var r = AirChamferFeatureRecognitionParityLab.Evaluate(c);

        Assert.True(r.PrototypeInvoked);
        Assert.True(r.CandidateProduced);
        Assert.NotNull(r.CandidateSummary);
        Assert.True(r.RecognitionContractSatisfied, r.FirstDivergence);

        Assert.Equal(1, r.ChamferFaceCount);
        Assert.Equal(2, r.TrimmedAdjacentFaceCount);
        Assert.Equal(2, r.TransitionEdgeCount);
        Assert.True(r.OriginalSharpEdgeAbsent);
        Assert.Equal(6, r.CandidateSummary!.PlanarFaceCount);
        Assert.Equal(0, r.CandidateSummary.CylindricalFaceCount);

        Assert.Contains("edge-x9-edge-v2-prototype-invoked", r.Diagnostics);
        Assert.Contains("edge-x9-candidate-adjacency-summary-captured", r.Diagnostics);
        Assert.Contains("edge-x9-recognition-contract-checked", r.Diagnostics);
        Assert.Contains("edge-x9-legacy-authority-preserved", r.Diagnostics);
        Assert.Contains("edge-x9-no-production-route-replacement", r.Diagnostics);
        Assert.Contains("edge-x9-no-3d-boolean-used", r.Diagnostics);
        Assert.StartsWith("edge-x9-legacy-comparison-unavailable:", r.LegacyComparisonStatus, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("invalid-distance-deferred")]
    [InlineData("legacy-triangle-dependent-fixture")]
    public void DeferredCases_StopBeforeRecognition(string caseName)
    {
        var c = AirChamferFeatureRecognitionParityLab.Cases().Single(x => x.CaseName == caseName);
        var r = AirChamferFeatureRecognitionParityLab.Evaluate(c);
        Assert.True(r.PrototypeInvoked);
        Assert.False(r.CandidateProduced);
        Assert.False(r.RecognitionContractSatisfied);
        Assert.NotNull(r.FirstDivergence);
        Assert.Equal(0, r.RecognizedCandidateCount);
        Assert.Equal(0, r.AdmissibleCandidateCount);
    }
}
