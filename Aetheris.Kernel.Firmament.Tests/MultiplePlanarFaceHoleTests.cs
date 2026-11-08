using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class MultiplePlanarFaceHoleTests
{
    private static string Source => FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/OrientedComposeHoles/bracket-multiple-face-holes.firmament");
    [Fact]
    public void CrossingShaftAxes_RejectBeforeAnySurfaceIntersection()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/OrientedComposeHoles/crossing-face-holes-rejected.firmament");
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains("existing-hole-envelope-clearance", StringComparison.Ordinal));
    }
    [Fact]
    public void MixedRecessesOnUpright_AreAnalyticClosedAndHaveTheSameRemovedVolume()
    {
        var source = Source.Replace("Vertex2_Vertex3", "Vertex3_Vertex4", StringComparison.Ordinal).Replace("Point2(21mm, 15mm)", "Point2(13mm, 25mm)", StringComparison.Ordinal);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join(";", built.Diagnostics.Select(d => d.Message)));
        CheckVolume(built.Value.RuntimeBody!, 4020 - 49.875 * Math.PI);
        var imported = Step242Importer.ImportBody(built.Value.StepText);
        Assert.True(imported.IsSuccess); CheckVolume(imported.Value, 4020 - 49.875 * Math.PI);
    }
    [Fact]
    public void MultipleHolesOnBothBracketLegs_KeepIndependentSupportExclusions()
    {
        var source = Source[..Source.LastIndexOf("  }", StringComparison.Ordinal)] + "Hole<Shaft> UprightA { On: face(bracket.Side(\"profile:section.Outer.Vertex3_Vertex4\")); Center: Point2(10mm,10mm); Diameter: 6mm; End: ThroughAll } Hole<Shaft> UprightB { On: face(bracket.Side(\"profile:section.Outer.Vertex3_Vertex4\")); Center: Point2(10mm,25mm); Diameter: 6mm; End: ThroughAll } } }";
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join(";", built.Diagnostics.Select(d => d.Message)));
        CheckVolume(built.Value.RuntimeBody!, 4020 - (49.875 + 54) * Math.PI);
        var upright = built.Value.RuntimeCorrespondence!.Descendants.Single(d => d.Face is not null && d.FirmamentSelector?.Contains("Vertex3_Vertex4", StringComparison.Ordinal) == true);
        Assert.Equal(2, PlanarFaceThroughHole.Support(built.Value.RuntimeBody!, upright.Face!.Value)!.ExcludedDisks.Count);
    }
    [Fact]
    public void ThreeSeparatedVariants_RetainTheSupportFrameAndInnerLoops_AndRoundTrip()
    {
        var built = FirmamentBuildAndExport.CompileSource(Source);
        Assert.True(built.IsSuccess, string.Join(";", built.Diagnostics.Select(d => d.Message)));
        var body = built.Value.RuntimeBody!;
        var map = new GeometrySourceMap(built.Value.RuntimeCorrespondence!);
        var frame = built.Value.RuntimeCorrespondence!.Descendants.Where(d => d.Face is not null && d.FirmamentSelector?.Contains("Vertex2_Vertex3", StringComparison.Ordinal) == true)
            .Select(d => PlanarFaceThroughHole.Support(body, d.Face!.Value)).Single();
        Assert.NotNull(frame); Assert.Equal(new double[] { 3, -20, 3 }, frame.Origin);
        Assert.Equal(new double[] { 1, 0, 0 }, frame.X); Assert.Equal(new double[] { 0, 1, 0 }, frame.Y);
        Assert.Equal(new double[] { 2, 3.5, 3.5 }, frame.ExcludedDisks.Select(d => d.Radius).ToArray());
        Assert.Equal(3, built.Value.RuntimeCorrespondence.Descendants.Count(d => d.Role == SemanticTopologyRole.HoleEntryLoop));
        var imported = Step242Importer.ImportBody(built.Value.StepText);
        Assert.True(imported.IsSuccess, string.Join(";", imported.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { body, imported.Value }) CheckVolume(result, 4020 - 49.875 * Math.PI);
    }

    [Theory]
    [InlineData(0d, false)]
    [InlineData(4.9d, false)]
    [InlineData(5d, false)]
    [InlineData(5.005d, false)]
    [InlineData(5.02d, true)]
    public void RepeatedShafts_RejectOverlapTangencyAndSlivers(double distance, bool admitted)
    {
        var start = Source[..Source.IndexOf("  Modify", StringComparison.Ordinal)];
        var source = start + FormattableString.Invariant($"Modify bracket {{ Hole<Shaft> A {{ On: face(bracket.Side(\"profile:section.Outer.Vertex2_Vertex3\")); Center: Point2(8mm,8mm); Diameter: 5mm; End: ThroughAll }} Hole<Shaft> B {{ On: face(bracket.Side(\"profile:section.Outer.Vertex2_Vertex3\")); Center: Point2({8 + distance}mm,8mm); Diameter: 5mm; End: ThroughAll }} }} }}");
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.Equal(admitted, built.IsSuccess);
        if (admitted) CheckVolume(built.Value.RuntimeBody!, 4020 - 37.5 * Math.PI);
        else Assert.Contains(built.Diagnostics, d => d.Message.Contains("clearance", StringComparison.Ordinal));
    }

    [Fact]
    public void DiagonalDisks_AreAdmittedEvenWhenTheirAxisAlignedBoundsOverlap()
    {
        var start = Source[..Source.IndexOf("  Modify", StringComparison.Ordinal)];
        var source = start + "Modify bracket { Hole<Shaft> A { On: face(bracket.Side(\"profile:section.Outer.Vertex2_Vertex3\")); Center: Point2(8mm,8mm); Diameter: 5mm; End: ThroughAll } Hole<Shaft> B { On: face(bracket.Side(\"profile:section.Outer.Vertex2_Vertex3\")); Center: Point2(12mm,12mm); Diameter: 5mm; End: ThroughAll } } }";
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join(";", built.Diagnostics.Select(d => d.Message)));
        CheckVolume(built.Value.RuntimeBody!, 4020 - 37.5 * Math.PI);
    }

    [Theory]
    [InlineData("Point2(13mm, 10mm)", "Point2(10mm, 5mm)")]
    [InlineData("Point2(21mm, 15mm)", "Point2(18mm, 10mm)")]
    public void RecessFootprints_RejectEvenWhenTheShaftsWouldFit(string before, string after)
    {
        var built = FirmamentBuildAndExport.CompileSource(Source.Replace(before, after, StringComparison.Ordinal));
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains("clearance", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("CounterboreDepth: 1mm", "CounterboreDepth: 0mm")]
    [InlineData("CounterboreDepth: 1mm", "CounterboreDepth: 3mm")]
    [InlineData("CounterboreDepth: 1mm", "CounterboreDepth: 2.995mm")]
    [InlineData("CounterboreDiameter: 7mm", "CounterboreDiameter: 4mm")]
    [InlineData("CountersinkAngle: 90deg", "CountersinkAngle: 0deg")]
    [InlineData("CountersinkDiameter: 7mm", "CountersinkDiameter: 12mm")]
    public void DegenerateRecesses_RejectBeforeTopologyConstruction(string before, string after)
    {
        var built = FirmamentBuildAndExport.CompileSource(Source.Replace(before, after, StringComparison.Ordinal));
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains("compose-face-hole:", StringComparison.Ordinal));
    }

    private static void CheckVolume(BrepBody body, double expected)
    {
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed); Assert.True(mass.IsOrientationConsistent);
        Assert.InRange(Math.Abs(mass.AbsoluteVolume - expected), 0, Math.Max(mass.ErrorBound ?? 0, 1e-6));
    }
}
