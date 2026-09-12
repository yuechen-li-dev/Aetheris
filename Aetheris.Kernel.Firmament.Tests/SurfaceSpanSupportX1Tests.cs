using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfaceSpanSupportX1Tests
{
    [Fact]
    public void PlaneSpan_ResolvesValidBoundaryAreaFrameNormalAndConsumers()
    {
        var inspection = ProfileAuthoringParser.InspectGeometricSpans(Source(Holes(valid: true)));
        var span = Assert.Single(inspection.Spans, item => item.SpanId == "MountingArea");

        Assert.Empty(inspection.Diagnostics);
        Assert.Equal(2880d, span.Area!.Value, 8);
        Assert.Equal([0d, 0d, 1d], span.Normal!);
        Assert.Equal("construction:TopSupport", span.LocalFrame);
        Assert.Equal(["Hole:H1", "Hole:H2", "Hole:H3", "Hole:H4"], span.ConsumerReferences);
        Assert.True(PlanarSpanContainment.ContainsPoint(span, 0, 0));
        Assert.False(PlanarSpanContainment.ContainsPoint(span, 35, 25)); // inside AABB, outside diamond
        Assert.True(PlanarSpanContainment.ContainsCircle(span, 16, 9, 8, out var margin));
        Assert.True(margin > 0);
    }

    [Fact]
    public void CounterboreOnSpan_PreservesBoundedAndParentSupport_AndUsesExistingLowering()
    {
        var parsed = PrismaticProfileCompositionParser.Parse(Source(Holes(valid: true)));
        Assert.Empty(parsed.Diagnostics);
        var feature = Assert.IsType<PrismaticProfileCompositionFeature>(parsed.Feature);
        Assert.All(feature.CounterboreHoles!, hole =>
        {
            Assert.Equal("MountingArea", hole.SupportSpan);
            Assert.Equal("TopSupport", hole.ParentSupport);
            Assert.True(hole.SupportBoundaryMargin > 0);
        });
        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
        Assert.Empty(diagnostics);
        Assert.True(stack.AnalyticVolume > 0);
    }

    [Fact]
    public void FootprintCrossingSpanBoundary_FailsTypedAndProducesNoConstruction()
    {
        var parsed = PrismaticProfileCompositionParser.Parse(Source(Holes(valid: false)));
        Assert.Null(parsed.Feature);
        Assert.Contains(parsed.Diagnostics, item => item.StartsWith("firmament-feature-footprint-outside-span:H4:MountingArea", StringComparison.Ordinal));
        Assert.Null(PrismaticSectionStackCompiler.Normalize(parsed, out _));
    }

    [Fact]
    public void SameHoleOnWholeParentPasses_WhileOnSpanFails()
    {
        var bounded = PrismaticProfileCompositionParser.Parse(Source(SingleHole("MountingArea", 30, 20)));
        var whole = PrismaticProfileCompositionParser.Parse(Source(SingleHole("+Z", 30, 20)));
        Assert.Null(bounded.Feature);
        Assert.Contains(bounded.Diagnostics, item => item.StartsWith("firmament-feature-footprint-outside-span:Outside:MountingArea", StringComparison.Ordinal));
        Assert.NotNull(whole.Feature);
        Assert.DoesNotContain(whole.Diagnostics, item => item.StartsWith("firmament-feature-footprint-outside-span:", StringComparison.Ordinal));
    }

    [Fact]
    public void BoundaryEdit_ReevaluatesExistingHoleWithoutStaleFallback()
    {
        var wide = PrismaticProfileCompositionParser.Parse(Source(SingleHole("MountingArea", 12, 7)));
        var narrowSource = Source(SingleHole("MountingArea", 12, 7)).Replace("[0mm,32mm]", "[0mm,18mm]", StringComparison.Ordinal);
        var narrow = PrismaticProfileCompositionParser.Parse(narrowSource);
        Assert.NotNull(wide.Feature);
        Assert.Null(narrow.Feature);
        Assert.Contains(narrow.Diagnostics, item => item.StartsWith("firmament-feature-footprint-outside-span:Outside:MountingArea", StringComparison.Ordinal));
    }

    [Fact]
    public void CounterborePattern_ValidatesEveryExpandedInstanceAndIdentifiesFailure()
    {
        var validSource = File.ReadAllText(Fixture("Canonical", "Span", "plane-hole-support.firmament"));
        var invalidSource = File.ReadAllText(Fixture("Invalid", "Span", "plane-hole-footprint-crossing.firmament"));

        var valid = FirmamentBuildAndExport.CompileSource(validSource);
        Assert.True(valid.IsSuccess, string.Join(Environment.NewLine, valid.Diagnostics.Select(item => item.Message)));
        Assert.Equal(4, valid.Value.Features!.Count);
        Assert.All(valid.Value.Features, hole => Assert.Equal("MountingArea", hole.SupportSpan));
        var span = Assert.Single(ProfileAuthoringParser.InspectGeometricSpans(validSource).Spans, item => item.SpanId == "MountingArea");
        Assert.Equal(["Hole:MountPattern_0", "Hole:MountPattern_1", "Hole:MountPattern_2", "Hole:MountPattern_3"], span.ConsumerReferences);

        var invalid = FirmamentBuildAndExport.CompileSource(invalidSource);
        Assert.False(invalid.IsSuccess);
        Assert.Contains(invalid.Diagnostics, item => item.Message.StartsWith("firmament-feature-footprint-outside-span:MountPattern_3:MountingArea", StringComparison.Ordinal));
    }

    [Fact]
    public void BossAndPocketOnSpan_UseActualProfileFootprintsAndRetainSupportIdentity()
    {
        var parsed = PrismaticProfileCompositionParser.Parse(ProfileFeatureSource(outside: false));
        var feature = Assert.IsType<PrismaticProfileCompositionFeature>(parsed.Feature);
        var boss = Assert.Single(feature.Bosses!);
        var pocket = Assert.Single(feature.Pockets!);
        Assert.Equal(("MountingArea", "TopSupport"), (boss.SupportSpan, boss.ParentSupport));
        Assert.Equal(("MountingArea", "TopSupport"), (pocket.SupportSpan, pocket.ParentSupport));

        var invalid = PrismaticProfileCompositionParser.Parse(ProfileFeatureSource(outside: true));
        Assert.Null(invalid.Feature);
        Assert.Contains(invalid.Diagnostics, item => item == "firmament-feature-footprint-outside-span:Pad:MountingArea:profile=PadProfile");
    }

    [Fact]
    public void ArcBoundaryAndInnerLoop_UseAnalyticProfileContainment()
    {
        var circular = ProfileAuthoringParser.InspectGeometricSpans(CircularSpanSource());
        var disk = Assert.Single(circular.Spans);
        Assert.True(PlanarSpanContainment.ContainsCircle(disk, 0, 0, 8, out var insideMargin));
        Assert.Equal(12d, insideMargin, 8);
        Assert.False(PlanarSpanContainment.ContainsCircle(disk, 13, 0, 8, out var crossingMargin));
        Assert.True(crossingMargin < 0);

        var ring = ProfileAuthoringParser.InspectGeometricSpans(RingSpanSource());
        var ringSpan = Assert.Single(ring.Spans);
        Assert.Equal(1400d, ringSpan.Area!.Value, 8);
        Assert.False(PlanarSpanContainment.ContainsPoint(ringSpan, 0, 0));
        Assert.True(PlanarSpanContainment.ContainsPoint(ringSpan, 15, 0));
    }

    [Fact]
    public void ParentPlaneAndHostMoveTogether_ContainmentAndRelativeFeatureSupportRemainStable()
    {
        var original = PrismaticProfileCompositionParser.Parse(Source(SingleHole("MountingArea", 0, 0)));
        var movedSource = Source(SingleHole("MountingArea", 0, 0))
            .Replace("0mm,0mm,8mm", "0mm,0mm,18mm", StringComparison.Ordinal)
            .Replace("From: 0mm; To: 8mm", "From: 10mm; To: 18mm", StringComparison.Ordinal);
        var moved = PrismaticProfileCompositionParser.Parse(movedSource);

        var first = Assert.Single(original.Feature!.CounterboreHoles!);
        var second = Assert.Single(moved.Feature!.CounterboreHoles!);
        Assert.Equal((0d, 8d), (first.From, first.To));
        Assert.Equal((10d, 18d), (second.From, second.To));
        Assert.Equal(first.SupportBoundaryMargin, second.SupportBoundaryMargin);
        Assert.Equal("TopSupport", second.ParentSupport);
    }

    [Fact]
    public void SpanSupportAndEquivalentWholePlaneSupport_ExportIdenticalGeometry()
    {
        var boundedSource = File.ReadAllText(Fixture("Canonical", "Span", "plane-hole-support.firmament"));
        var wholePlaneSource = boundedSource.Replace("On: MountingArea", "On: +Z", StringComparison.Ordinal);
        var bounded = FirmamentBuildAndExport.CompileSource(boundedSource);
        var whole = FirmamentBuildAndExport.CompileSource(wholePlaneSource);

        Assert.True(bounded.IsSuccess, string.Join(Environment.NewLine, bounded.Diagnostics.Select(item => item.Message)));
        Assert.True(whole.IsSuccess, string.Join(Environment.NewLine, whole.Diagnostics.Select(item => item.Message)));
        Assert.Equal(bounded.Value.StepText, whole.Value.StepText);
        Assert.All(bounded.Value.Features!, feature => Assert.Equal("MountingArea", feature.SupportSpan));
        Assert.All(whole.Value.Features!, feature => Assert.Null(feature.SupportSpan));
    }

    private static string Fixture(params string[] parts) =>
        Path.GetFullPath(Path.Combine([AppContext.BaseDirectory, "../../../../fixtures", .. parts]));

    private static string ProfileFeatureSource(bool outside) => Source($$"""
        Rect2 PadShape { Center: [{{(outside ? 35 : -10)}}mm, 0mm]; Size: [10mm, 8mm] }
        Rect2 PocketShape { Center: [10mm, 0mm]; Size: [8mm, 6mm] }
        Profile PadProfile { Loop Outer { PadShape.Bottom |> PadShape.Right |> PadShape.Top |> PadShape.Left |> Close } }
        Profile PocketProfile { Loop Outer { PocketShape.Bottom |> PocketShape.Right |> PocketShape.Top |> PocketShape.Left |> Close } }
        Boss Pad { On: MountingArea; Profile: PadProfile; Height: 3mm }
        Pocket Recess { On: MountingArea; Profile: PocketProfile; Depth: 2mm; MinimumFloorThickness: 1mm }
        """);

    private static string CircularSpanSource() => """
        Concept Struct Supports { Top: Plane { Origin: [0mm,0mm,0mm]; Normal: [0,0,1]; Up: [0,1,0] } }
        Construction Plane TopSupport { Trace: Supports.Top }
        Point2 C { Position: [0mm,0mm] } Point2 E { Position: [20mm,0mm] }
        Point2 N { Position: [0mm,20mm] } Point2 W { Position: [-20mm,0mm] } Point2 S { Position: [0mm,-20mm] }
        Circle2 Rim { Center: C; Radius: 20mm }
        Profile Disk { Loop Outer {
            Segment Q1 { Trace: Rim; From: E; To: N; Sweep: CounterClockwise }
            Segment Q2 { Trace: Rim; From: N; To: W; Sweep: CounterClockwise }
            Segment Q3 { Trace: Rim; From: W; To: S; Sweep: CounterClockwise }
            Segment Q4 { Trace: Rim; From: S; To: E; Sweep: CounterClockwise }
        } }
        Span<Plane> WorkArea { On: TopSupport; Boundary: Disk }
        """;

    private static string RingSpanSource() => """
        Concept Struct Supports { Top: Plane { Origin: [0mm,0mm,0mm]; Normal: [0,0,1]; Up: [0,1,0] } }
        Construction Plane TopSupport { Trace: Supports.Top }
        Rect2 Outer { Center: [0mm,0mm]; Size: [40mm,40mm] }
        Rect2 Inner { Center: [0mm,0mm]; Size: [20mm,10mm] }
        Profile Ring { Loop Outer { Outer.Bottom |> Outer.Right |> Outer.Top |> Outer.Left |> Close }
            Loop Inner { Reverse Inner.Bottom |> Reverse Inner.Left |> Reverse Inner.Top |> Reverse Inner.Right |> Close } }
        Span<Plane> RingArea { On: TopSupport; Boundary: Ring }
        """;

    private static string Holes(bool valid) => string.Join('\n', new[]
    {
        SingleHole("MountingArea", -16, -9, "H1"), SingleHole("MountingArea", 16, -9, "H2"),
        SingleHole("MountingArea", -16, 9, "H3"), SingleHole("MountingArea", valid ? 16 : 28, 9, "H4")
    });

    private static string SingleHole(string support, double x, double y, string name = "Outside") => $$"""
        Hole<Counterbore> {{name}} { On: {{support}}; Center: [{{x}}mm, {{y}}mm]; Diameter: 8mm; CounterboreDiameter: 16mm; CounterboreDepth: 3mm; End: ThroughAll }
        """;

    private static string Source(string holes) => $$"""
        Concept Struct Supports { Top: Plane { Origin: [0mm,0mm,8mm]; Normal: [0,0,1]; Up: [0,1,0] } }
        Construction Plane TopSupport { Trace: Supports.Top }
        Rect2 Stock { Center: [0mm,0mm]; Size: [100mm,80mm] }
        Profile StockProfile { Loop Outer { Stock.Bottom |> Stock.Right |> Stock.Top |> Stock.Left |> Close } }
        Point2 South { Position: [0mm,-32mm] } Point2 East { Position: [45mm,0mm] }
        Point2 North { Position: [0mm,32mm] } Point2 West { Position: [-45mm,0mm] }
        Line2 SE { From: South; To: East } Line2 EN { From: East; To: North }
        Line2 NW { From: North; To: West } Line2 WS { From: West; To: South }
        Profile MountBoundary { Loop Outer { SE |> EN |> NW |> WS |> Close } }
        Span<Plane> MountingArea { On: TopSupport; Boundary: MountBoundary }
        Compose Plate {
            Base StockBody { Profile: StockProfile; From: 0mm; To: 8mm; Role: Stock }
            {{holes}}
        }
        """;
}
