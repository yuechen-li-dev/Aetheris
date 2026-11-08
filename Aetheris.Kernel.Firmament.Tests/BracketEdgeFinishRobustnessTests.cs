using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class BracketEdgeFinishRobustnessTests
{
    private static string Bracket => FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/OrientedComposeHoles/bracket-foot-hole.firmament");
    private static string Empty => Bracket[..Bracket.IndexOf("  Modify", StringComparison.Ordinal)] + "}";
    private static string Finish(string name, string corner, string kind, double size) => FormattableString.Invariant(
        $"EdgeFinish {name} {{ Face: Longitudinal Target: section.Outer.{corner}.Start Kind: {kind} {(kind == "Fillet" ? "Radius" : "Distance")}: {size}mm }}");
    private static string With(string source, params string[] finishes) => source[..source.LastIndexOf('}')]
        + " Modify bracket { " + string.Join("\n", finishes) + " } }";
    private static FirmamentStepExportResult Build(string source)
    {
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(result.Value.StepText);
        Assert.True(imported.IsSuccess);
        foreach (var body in new[] { result.Value.RuntimeBody!, imported.Value })
        {
            Assert.True(BrepExportPreflight.Validate(body).IsValid);
            var mass = BrepMassProperties.Evaluate(body);
            Assert.True(mass.IsEnclosed); Assert.True(mass.IsOrientationConsistent);
        }
        return result.Value;
    }

    [Theory]
    [InlineData("Fillet")] [InlineData("Chamfer")]
    public void MountingHoleAndInsideBend_CombineWithoutMovingTheHole(string kind)
    {
        var original = Build(Bracket);
        var finished = Build(With(Bracket, Finish("Bend", "Vertex3_Vertex4", kind, 2)));
        var cylinder = Assert.Single(original.RuntimeBody!.Geometry.Surfaces, s => s.Value.Cylinder is not null).Value.Cylinder!.Value;
        var retained = Assert.Single(finished.RuntimeBody!.Geometry.Surfaces, s => s.Value.Cylinder is { } c && Math.Abs(c.Radius - 3) < 1e-8).Value.Cylinder!.Value;
        Assert.Equal(cylinder.Origin, retained.Origin); Assert.Equal(cylinder.Axis, retained.Axis);
        Assert.Single(finished.Features!);
        Assert.Contains(finished.EngineeringFeatures!, f => f.Name == "Bend");
        Assert.Contains(finished.Features!, f => f.Name == "Hole");
        var side = Assert.Single(finished.RuntimeCorrespondence!.Descendants, d => d.Role == SemanticTopologyRole.ExtrusionSideFace
            && d.SourceStableId == "profile:section.Outer.Vertex2_Vertex3");
        var support = PlanarFaceThroughHole.Support(finished.RuntimeBody, side.Face!.Value, side.SupportOrigin)!;
        Assert.Equal(3, support.Origin[0], 8); // Trimmed face starts at x=5; authored frame stays x=3.
    }

    [Theory]
    [InlineData("Shaft", 3.01, true)]
    [InlineData("Shaft", 3.005, false)]
    [InlineData("Shaft", 3, false)]
    [InlineData("Shaft", 2.5, false)]
    [InlineData("Counterbore", 5.51, true)]
    [InlineData("Counterbore", 5.5, false)]
    [InlineData("Countersink", 5.51, true)]
    [InlineData("Countersink", 5.5, false)]
    public void FinishHoleAndRecessEnvelopes_RequirePositiveRemainingMaterial(string variant, double x, bool accepted)
    {
        // x is authored in the original foot frame. With a 2 mm bend, its
        // straight support starts at local x=2; a 2 mm shaft needs center>=3.01.
        var hole = Bracket.Replace("Point2(13mm, 10mm)", FormattableString.Invariant($"Point2({x}mm, 10mm)"), StringComparison.Ordinal)
            .Replace("Diameter: 6mm", "Diameter: 2mm", StringComparison.Ordinal)
            .Replace("Hole<Shaft>", "Hole<" + variant + ">", StringComparison.Ordinal);
        if (variant == "Counterbore") hole = hole.Replace("End: ThroughAll", "CounterboreDiameter: 7mm CounterboreDepth: 1mm End: ThroughAll", StringComparison.Ordinal);
        if (variant == "Countersink") hole = hole.Replace("End: ThroughAll", "CountersinkDiameter: 7mm CountersinkAngle: 120deg End: ThroughAll", StringComparison.Ordinal);
        var source = With(hole, Finish("Bend", "Vertex3_Vertex4", "Fillet", 2));
        if (accepted) Build(source);
        else
        {
            var result = FirmamentBuildAndExport.CompileSource(source);
            Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, d => d.Message.Contains("clearance", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("Fillet", "Fillet")] [InlineData("Fillet", "Chamfer")]
    [InlineData("Chamfer", "Fillet")] [InlineData("Chamfer", "Chamfer")]
    public void InsideBendAndTwoOutsideBreaks_CombineWithTheMountingHole(string inside, string outside)
    {
        var result = Build(With(Bracket, Finish("Bend", "Vertex3_Vertex4", inside, 2),
            Finish("FootTop", "Vertex2_Vertex3", outside, .5), Finish("FootBottom", "Vertex1_Vertex2", outside, .5)));
        Assert.Equal(3, result.EngineeringFeatures!.Count);
        Assert.Equal(3, result.MaterializedFeatures!.Count(f => f.Kind == "EdgeFinish"));
    }

    [Theory]
    [InlineData(1.494, true)] [InlineData(1.5, false)] [InlineData(1.6, false)]
    public void AdjacentCornerFinishes_CannotConsumeTheirSharedThreeMillimetreLeg(double size, bool accepted)
    {
        var source = With(Empty, Finish("Top", "Vertex2_Vertex3", "Fillet", size), Finish("Bottom", "Vertex1_Vertex2", "Fillet", size));
        if (accepted) Build(source);
        else Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Theory]
    [InlineData(0)] [InlineData(.005)] [InlineData(-1)]
    public void ZeroAndMicroscopicFinishesAreRefused(double size)
        => Assert.False(FirmamentBuildAndExport.CompileSource(With(Empty, Finish("Tiny", "Vertex2_Vertex3", "Fillet", size))).IsSuccess);

    [Fact]
    public void FinishingTheSameOriginalCornerTwiceIsRefused()
        => Assert.False(FirmamentBuildAndExport.CompileSource(With(Empty,
            Finish("First", "Vertex3_Vertex4", "Fillet", 2), Finish("Second", "Vertex3_Vertex4", "Chamfer", 1))).IsSuccess);

    [Fact]
    public void FixtureCombinesBothLegsMountingRecessesAndThreeCornerFinishes()
    {
        var result = Build(FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/OrientedComposeHoles/bracket-finished-mounts.firmament"));
        Assert.Equal(2, result.Features!.Count); Assert.Equal(3, result.EngineeringFeatures!.Count);
        var expected = (201 + 3.75 * (1 - Math.PI / 4) - .125) * 20 - (20.25 + 27 + 10d / 3) * Math.PI;
        foreach (var body in new[] { result.RuntimeBody!, Step242Importer.ImportBody(result.StepText).Value })
        {
            var mass = BrepMassProperties.Evaluate(body);
            Assert.InRange(Math.Abs(mass.AbsoluteVolume - expected), 0, Math.Max(mass.ErrorBound ?? 0, 1e-6));
        }
    }

    [Fact]
    public void AllSixOriginalCornersCanBeFinishedWithoutBecomingAChainSelection()
    {
        var finishes = Enumerable.Range(0, 6).Select(i => Finish("Corner" + i, "Vertex" + i + "_Vertex" + ((i + 1) % 6), "Fillet", i == 3 ? 2 : .5)).ToArray();
        var result = Build(With(Bracket, finishes));
        Assert.Equal(6, result.MaterializedFeatures!.Count(f => f.Kind == "EdgeFinish"));
        Assert.Equal(7, result.RuntimeBody!.Geometry.Surfaces.Count(s => s.Value.Cylinder is not null));
    }
}
