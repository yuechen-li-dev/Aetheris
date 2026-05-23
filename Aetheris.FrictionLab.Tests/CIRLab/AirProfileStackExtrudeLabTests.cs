using Aetheris.Firmament.FrictionLab.CIRLab;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class AirProfileStackExtrudeLabTests
{
    [Fact]
    public void AirProfileIr_RepresentsRectangleWithCircularHole()
    {
        var r = AirProfileStackExtrudeLab.Run().ThroughHole.Layers.Single().Region;
        Assert.Equal(30, r.OuterRectangle.Width);
        Assert.NotNull(r.InnerCircle);
        Assert.Equal(2, r.InnerCircle!.Radius);
    }

    [Fact]
    public void AirProfileStack_ThroughHole_EmitsStepOrReportsExactBlocker()
    {
        var result = AirProfileStackExtrudeLab.Run().ThroughHoleResult;
        Assert.True(result.Success || result.Status.StartsWith("blocker:emitter:", StringComparison.Ordinal));
        if (result.Success)
        {
            Assert.Contains("ISO-10303-21", result.StepMarkers);
            Assert.DoesNotContain("BREP_WITH_VOIDS", result.StepMarkers);
        }
    }

    [Fact]
    public void AirProfileStack_SteppedHole_EmitsStep()
    {
        var result = AirProfileStackExtrudeLab.Run().SteppedHoleResult;
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        Assert.Contains("ISO-10303-21", result.StepMarkers);
        Assert.Contains("MANIFOLD_SOLID_BREP", result.StepMarkers);
        Assert.DoesNotContain("BREP_WITH_VOIDS", result.StepMarkers);
    }

    [Fact]
    public void AirProfileStack_BlindHole_FeasibilityRecorded()
    {
        var result = AirProfileStackExtrudeLab.Run().BlindHoleResult;
        Assert.True(result.Success || result.Status.StartsWith("blocker:emitter:", StringComparison.Ordinal));
    }

    [Fact]
    public void AirProfileStack_Counterbore_FeasibilityRecorded()
    {
        var result = AirProfileStackExtrudeLab.Run().CounterboreResult;
        Assert.True(result.Success || result.Status.StartsWith("blocker:emitter:", StringComparison.Ordinal));
    }

    [Fact]
    public void AirProfileStack_ReportContainsDecisionGradeMapping()
    {
        var result = AirProfileStackExtrudeLab.Run();
        Assert.NotEmpty(result.MappingFindings);
        Assert.Contains("HoleRecoveryPlan", result.MappingRecommendation, StringComparison.Ordinal);
        Assert.Contains("ProfileStackExtrudeSpec", result.MappingRecommendation, StringComparison.Ordinal);
    }

    [Fact]
    public void AirProfileStack_DeterministicResults()
    {
        var a = AirProfileStackExtrudeLab.Run();
        var b = AirProfileStackExtrudeLab.Run();
        Assert.Equal(a.ThroughHoleResult.Status, b.ThroughHoleResult.Status);
        Assert.Equal(a.SteppedHoleResult.Status, b.SteppedHoleResult.Status);
        Assert.Equal(string.Join("|", a.SteppedHoleResult.LayerRoles), string.Join("|", b.SteppedHoleResult.LayerRoles));
    }

    [Fact]
    public void AirProfileStack_MapsFromKernelProfileStackSpec()
    {
        var spec = new Aetheris.Kernel.Firmament.Materializer.ProfileStackExtrudeSpec(20, 20, -5, 5,
            [new Aetheris.Kernel.Firmament.Materializer.ProfileStackLayer(-5, 5, 2, "through", [])], []);
        Assert.True(AirProfileStackExtrudeLab.TryMapFromProfileStackSpec(spec, out var air, out _));
        Assert.NotNull(air);
        Assert.Single(air!.Layers);
    }
}
