using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class WirePointRouteTests
{
    private static string Source => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/WireForm/point-route.firmament"));

    [Fact]
    public void ArbitraryPlaneRouteDerivesTangentFilletAndExportsExactManifold()
    {
        var parsed = WireFormAuthoring.Parse(Source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var feature = parsed.Value;
        Assert.Equal(new Point3D(0, 0, 0), feature.StartState.Position);
        Assert.Equal(new Point3D(20, 30, 20), feature.EndState.Position);
        var bend = Assert.IsType<WireBendAir>(feature.Operations[1]);
        Assert.Equal(Math.PI / 2, bend.AngleRadians, 12);
        Assert.Equal(26, bend.Input.Position.Y, 12);
        Assert.Equal(4 / Math.Sqrt(2), bend.Output.Position.X, 12);
        Assert.Equal(bend.Output.Position.X, bend.Output.Position.Z, 12);
        Assert.Empty(WireFormBRepMaterializer.Validate(feature));
        var built = FirmamentBuildAndExport.CompileSource(Source);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
        Assert.True(built.Value.WireForm!.StepReimportedManifold);
        Assert.Equal(0, built.Value.WireForm.NonRationalBSplineSurfaces);
        Assert.True(built.Value.WireForm.Cylinders > 0);
        Assert.True(built.Value.WireForm.Tori > 0);
    }

    [Fact]
    public void ExplicitPoint2LiftingUsesSharedFrameTranslationAndRotation()
    {
        var source = Source.Replace("At: Point3(20mm,30mm,20mm);", "At: Point2(20mm,20mm); On { From: World; TranslateLocal: [0mm,30mm,0mm]; RotateLocal: { Axis: X; Angle: 90deg } }");
        var parsed = WireFormAuthoring.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.True((parsed.Value.EndState.Position - new Point3D(20, 30, 20)).Length < 1e-12);
    }

    [Theory]
    [InlineData("Point3(0mm,30mm,0mm)", "Point3(0mm,0mm,0mm)", "point-invalid")]
    [InlineData("Point3(20mm,30mm,20mm)", "Point3(0mm,60mm,0mm)", "corner-degenerate")]
    [InlineData("Radius: 4mm", "Radius: 40mm", "corner-does-not-fit")]
    [InlineData("; Radius: 4mm", "; Radius: 3mm", "radius-invalid")]
    [InlineData("Diameter: 1mm", "Diameter: 9mm", "radius-invalid")]
    [InlineData("Point3(0mm,0mm,0mm)", "Point3(0,0mm,0mm)", "point-invalid")]
    [InlineData("Start {", "Via {", "path-unsupported")]
    [InlineData("Diameter: 1mm;", "Diameter: 1mm; Diameter: 2mm;", "dimensions-invalid")]
    [InlineData("At: Point3(20mm,30mm,20mm);", "At: Point2(20mm,20mm);", "point-invalid")]
    public void InvalidOrUnsupportedIntentProducesNamedDiagnostic(string before, string after, string code)
    {
        var parsed = WireFormAuthoring.Parse(Source.Replace(before, after));
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Message.Contains("wire-route-" + code));
    }

    [Theory]
    [InlineData(0, 58, 732.589494, 64.073073, 12.805738)]
    [InlineData(1, 94, 732.509486, 100.649868, 14.372058)]
    [InlineData(2, 130, 732.474127, 137.301981, 15.022472)]
    [InlineData(5, 58, 732.589494, 64.073073, 12.805738)]
    [InlineData(4, 94, 732.509486, 100.649868, 14.372058)]
    [InlineData(3, 130, 732.474127, 137.301981, 15.022472)]
    public void GuitarPointRoutesPreserveLegacyFilletsWithinAuthoredRounding(int index, double postY, double lead, double tail, double degrees)
    {
        // Previously Python-authored values had six decimal places. Endpoints are
        // now exact inputs; these checks quantify the expected rounding correction.
        var radians = -13 * Math.PI / 180;
        var end = new Point3D(index < 3 ? -27 : 27, 660 + postY * Math.Cos(radians) - 20 * Math.Sin(radians),
            48 + postY * Math.Sin(radians) + 20 * Math.Cos(radians));
        var result = WireRouteAuthoring.Lower(new("String", .28 + index * .08, "Standard.Materials.StainlessSteel.304_Annealed",
            new((index - 2.5) * 10.2, -75, 65), new((index - 2.5) * 7.2, 658, 65), end, 4, 4, "NutBreak"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal(end, result.Value.EndState.Position);
        Assert.InRange(Math.Abs(result.Value.Operations[0].LengthMm - lead), 0, .00000051);
        Assert.InRange(Math.Abs(result.Value.Operations[2].LengthMm - tail), 0, .00000051);
        var bend = Assert.IsType<WireBendAir>(result.Value.Operations[1]);
        Assert.InRange(Math.Abs(bend.AngleRadians * 180 / Math.PI - degrees), 0, .00000051);
        Assert.Empty(WireFormBRepMaterializer.Validate(result.Value));
    }
}
