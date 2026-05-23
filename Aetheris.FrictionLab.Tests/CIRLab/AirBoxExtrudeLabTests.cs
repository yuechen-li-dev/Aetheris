using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class AirBoxExtrudeLabTests
{
    [Theory]
    [InlineData("cube", 10d, 10d, 10d)]
    [InlineData("rect", 12d, 8d, 6d)]
    public void AirBoxExtrude_HappyCases_AreDeterministic(string name, double width, double depth, double height)
    {
        var @case = new AirBoxExtrudeCase(name, width, depth, height);
        var first = AirBoxExtrudeLab.Run(@case);
        var second = AirBoxExtrudeLab.Run(@case);

        Assert.Equal(first.Baseline, second.Baseline);
        Assert.Equal(first.Extrude, second.Extrude);
        Assert.Equal(first.BaselineStep.Exported, second.BaselineStep.Exported);
        Assert.Equal(string.Join("|", first.BaselineStep.PresentMarkers), string.Join("|", second.BaselineStep.PresentMarkers));
        Assert.Equal(string.Join("|", first.BaselineStep.MissingMarkers), string.Join("|", second.BaselineStep.MissingMarkers));
        Assert.Equal(first.BaselineStep.ContainsBrepWithVoids, second.BaselineStep.ContainsBrepWithVoids);
        Assert.Equal(first.ExtrudeStep.Exported, second.ExtrudeStep.Exported);
        Assert.Equal(string.Join("|", first.ExtrudeStep.PresentMarkers), string.Join("|", second.ExtrudeStep.PresentMarkers));
        Assert.Equal(string.Join("|", first.ExtrudeStep.MissingMarkers), string.Join("|", second.ExtrudeStep.MissingMarkers));
        Assert.Equal(first.ExtrudeStep.ContainsBrepWithVoids, second.ExtrudeStep.ContainsBrepWithVoids);
        Assert.Equal(first.FinalRecommendation, second.FinalRecommendation);
        Assert.Equal(first.TopologyParity, second.TopologyParity);
        Assert.Equal(first.StepSmokeParity, second.StepSmokeParity);
        Assert.Contains("air-x4-baseline-box-created", first.Diagnostics);
        Assert.Contains(first.Diagnostics, d => d.StartsWith("air-x4-extrude-box-", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("cube", 10d, 10d, 10d)]
    [InlineData("rect", 12d, 8d, 6d)]
    public void AirBoxExtrude_HappyCases_ReportParityOrExplicitBlocker(string name, double width, double depth, double height)
    {
        var result = AirBoxExtrudeLab.Run(new AirBoxExtrudeCase(name, width, depth, height));

        Assert.True(result.Baseline.BodyProduced);
        Assert.True(result.Extrude.BodyProduced);

        if (result.TopologyParity && result.StepSmokeParity)
        {
            Assert.Equal(8, result.Baseline.VertexCount);
            Assert.Equal(12, result.Baseline.EdgeCount);
            Assert.Equal(6, result.Baseline.FaceCount);
            Assert.Equal(6, result.Baseline.PlanarFaceCount);
            Assert.Equal(8, result.Extrude.VertexCount);
            Assert.Equal(12, result.Extrude.EdgeCount);
            Assert.Equal(6, result.Extrude.FaceCount);
            Assert.Equal(6, result.Extrude.PlanarFaceCount);
            Assert.Equal("box-air-extrude-ready-for-production-migration", result.FinalRecommendation);
        }
        else
        {
            Assert.Equal("box-air-extrude-needs-emitter-parity-work", result.FinalRecommendation);
            Assert.Contains(result.Diagnostics, d => d.StartsWith("air-x4-topology-parity-", StringComparison.Ordinal) || d == "air-x4-step-smoke-failed");
        }
    }

    [Theory]
    [InlineData(0d, 10d, 10d)]
    [InlineData(10d, -2d, 10d)]
    public void AirBoxExtrude_InvalidDimensions_ReportDeterministicValidation(double width, double depth, double height)
    {
        var result = AirBoxExtrudeLab.Run(new AirBoxExtrudeCase("invalid", width, depth, height));
        Assert.False(result.IsValid);
        Assert.Contains("air-x4-invalid-dimensions", result.Diagnostics);
        Assert.False(result.Baseline.BodyProduced);
        Assert.False(result.Extrude.BodyProduced);
        Assert.Equal("box-air-extrude-needs-emitter-parity-work", result.FinalRecommendation);
    }
}
