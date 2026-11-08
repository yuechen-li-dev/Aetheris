using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class OrientedComposeShaftHoleTests
{
    private static string Source => FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/OrientedComposeHoles/simple-bracket-through-hole.firmament");

    [Theory]
    [InlineData("XY", 0d, 0d, 1d)]
    [InlineData("XZ", 0d, -1d, 0d)]
    [InlineData("YZ", 1d, 0d, 0d)]
    public void LocalTopThroughShaft_PreservesBodyFrameVolumeAndHoleCorrespondence(string plane, double x, double y, double z)
    {
        var result = FirmamentBuildAndExport.CompileSource(Source.Replace("on XZ", $"on {plane}", StringComparison.Ordinal));
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        var body = Assert.IsType<BrepBody>(result.Value.RuntimeBody);
        Check(body, new Vector3D(x, y, z));
        Assert.Contains(result.Value.RuntimeCorrespondence!.Descendants, d => d.Role == SemanticTopologyRole.HoleWallFace);
        Assert.Contains(result.Value.RuntimeCorrespondence.Descendants, d => d.Role == SemanticTopologyRole.HoleEntryLoop);
        Assert.Contains(result.Value.RuntimeCorrespondence.Descendants, d => d.Role == SemanticTopologyRole.HoleExitLoop);
        var imported = Step242Importer.ImportBody(result.Value.StepText);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(d => d.Message)));
        Check(imported.Value, new Vector3D(x, y, z));
    }

    [Fact]
    public void ALocalHoleThatSplitsTheThinLeg_IsRejected()
    {
        var result = FirmamentBuildAndExport.CompileSource(Source.Replace("diameter: 2mm", "diameter: 6mm", StringComparison.Ordinal));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("compose-rejected:disconnected-3d-solid", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("on: +Z", "on: -Z")]
    [InlineData("end: ThroughAll", "end: Blind 5mm")]
    [InlineData("Hole<Shaft>", "Hole<Counterbore>")]
    public void OtherSupportAdapters_RemainExplicitlyUnqualified(string before, string after)
    {
        var result = FirmamentBuildAndExport.CompileSource(Source.Replace(before, after, StringComparison.Ordinal));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("compose-oriented-feature-not-admitted", StringComparison.Ordinal));
    }

    private static void Check(BrepBody body, Vector3D expectedAxis)
    {
        var cylinders = body.Geometry.Surfaces.Select(s => s.Value).Where(s => s.Cylinder is not null).Select(s => s.Cylinder!.Value).ToArray();
        Assert.NotEmpty(cylinders);
        Assert.All(cylinders, cylinder => {
            Assert.Equal(1d, cylinder.Radius, 9);
            Assert.InRange((cylinder.Axis.ToVector() - expectedAxis).Length, 0d, 1e-9d);
        });
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed);
        Assert.True(mass.IsOrientationConsistent);
        var expectedVolume = 4020d - Math.PI * 20d;
        Assert.InRange(Math.Abs(mass.AbsoluteVolume - expectedVolume), 0d, Math.Max(mass.ErrorBound ?? 0d, 1e-6d));
    }
}
