using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class WireLineGuideTests
{
    private static string Source => FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/WireForm/line-guide.firmament");
    [Fact]
    public void LockedGuideSurvivesAutomaticEntryAndExactStepRoundtrip()
    {
        var parsed = WireFormAuthoring.Parse(Source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var locked = Assert.IsType<WireStraightAir>(parsed.Value.Operations[3]);
        Assert.Equal("MainGroove", locked.Name);
        Assert.Equal(new Point3D(20,0,0), locked.Input.Position);
        Assert.Equal(new Point3D(80,0,0), locked.Output.Position);
        Assert.Equal(60, locked.LengthMm);
        var bend = Assert.IsType<WireBendAir>(parsed.Value.Operations[1]);
        Assert.Equal(10, bend.RadiusMm);
        Assert.Equal(Math.PI / 2, bend.AngleRadians, 12);
        Assert.Empty(WireFormBRepMaterializer.Validate(parsed.Value));
        var built = FirmamentBuildAndExport.CompileSource(Source);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
        Assert.True(built.Value.WireForm!.StepReimportedManifold);
        Assert.Equal(0, built.Value.WireForm.NonRationalBSplineSurfaces);
    }
    [Theory]
    [InlineData("From: 20mm", "From: 5mm", "guide-connector-space-insufficient")]
    [InlineData("Distance: 60mm", "Distance: 100mm", "guide-interval-invalid")]
    [InlineData("Point3(120mm,0mm,0mm)", "Point3(120mm,20mm,0mm)", "guide-exit-not-qualified")]
    [InlineData("HarnessLayout.Groove", "HarnessLayout.Missing", "guide-unresolved")]
    public void UnsupportedGuideIntentHasNamedFailure(string from, string to, string diagnostic)
    {
        var result = WireFormAuthoring.Parse(Source.Replace(from, to));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains(diagnostic));
    }

    [Fact]
    public void ReverseGuideKeepsTheRequestedIntervalAndTangentExit()
    {
        var source = Source.Replace("Point3(0mm,-40mm,0mm)", "Point3(100mm,-40mm,0mm)")
            .Replace("From: 20mm", "From: 80mm").Replace("Direction: Forward", "Direction: Reverse")
            .Replace("Point3(120mm,0mm,0mm)", "Point3(-20mm,0mm,0mm)");
        var parsed = WireFormAuthoring.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var locked = Assert.IsType<WireStraightAir>(parsed.Value.Operations[3]);
        Assert.Equal(new Point3D(80,0,0), locked.Input.Position);
        Assert.Equal(new Point3D(20,0,0), locked.Output.Position);
        Assert.Empty(WireFormBRepMaterializer.Validate(parsed.Value));
    }
}
