using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ProfilePipelineX1Tests
{
    [Fact]
    public void RectanglePipeline_InheritsIdentityOrientsAndLowersToExistingProfile()
    {
        var parsed = ProfileAuthoringParser.Parse(RectanglePipeline());

        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        Assert.Empty(parsed.Diagnostics);
        Assert.Equal(["Bottom", "Right", "Top", "Left"], profile.Loops.Single().Segments.Select(segment => segment.Name));
        Assert.Equal([false, false, false, false], profile.Loops.Single().Segments.Select(segment => segment.Provenance.Reversed));
        Assert.Equal([0, 1, 2, 3], profile.Loops.Single().Segments.Select(segment => segment.Provenance.PipelineIndex));
        Assert.All(profile.Loops.Single().Segments, segment => Assert.NotNull(segment.Provenance.TracedFrom));
        Assert.True(ResolvedProfile2DValidator.Validate(profile).IsValid);
        Assert.Equal(LineArcProfileExtrudeStatus.Succeeded, ResolvedProfile2DValidator.Extrude(profile, parsed.Height).Status);
    }

    [Fact]
    public void PipelineAndManualProfile_HaveIdenticalGeometryAndTopologyPlan()
    {
        var pipeline = Assert.IsType<ResolvedProfile2D>(ProfileAuthoringParser.Parse(RectanglePipeline()).Profile);
        var manual = Assert.IsType<ResolvedProfile2D>(ProfileAuthoringParser.Parse(RectangleManual()).Profile);

        Assert.Equal(Signature(manual), Signature(pipeline));
        var pipelineResult = ResolvedProfile2DValidator.Extrude(pipeline, 2);
        var manualResult = ResolvedProfile2DValidator.Extrude(manual, 2);
        Assert.Equal(LineArcProfileExtrudeStatus.Succeeded, pipelineResult.Status);
        Assert.Equal(manualResult.Body!.Topology.Faces.Count(), pipelineResult.Body!.Topology.Faces.Count());
        Assert.Equal(manualResult.Body.Topology.Edges.Count(), pipelineResult.Body.Topology.Edges.Count());
        Assert.Equal(manualResult.Body.Topology.Vertices.Count(), pipelineResult.Body.Topology.Vertices.Count());
    }

    [Fact]
    public void Reverse_IsReadableExplicitOrientationEscapeHatch()
    {
        var parsed = ProfileAuthoringParser.Parse("""
            Point2 A { Position: [0mm, 0mm] } Point2 B { Position: [4mm, 0mm] }
            Point2 C { Position: [4mm, 3mm] } Point2 D { Position: [0mm, 3mm] }
            Line2 AB { From: A; To: B } Line2 CB { From: C; To: B }
            Line2 DC { From: D; To: C } Line2 AD { From: A; To: D }
            Profile Plate { Loop Outer { AB |> Reverse CB |> Reverse DC |> Reverse AD |> Close } }
            Extrude Solid { Profile: Plate; From: 0mm; To: 1mm }
            """);

        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        Assert.Empty(parsed.Diagnostics);
        Assert.Equal([false, true, true, true], profile.Loops.Single().Segments.Select(segment => segment.Provenance.Reversed));
        Assert.True(ResolvedProfile2DValidator.Validate(profile).IsValid);
    }

    [Fact]
    public void Continuation_AutomaticallyReversesAUniquelyMatchingSpan()
    {
        var parsed = ProfileAuthoringParser.Parse("""
            Point2 A { Position: [0mm, 0mm] } Point2 B { Position: [4mm, 0mm] }
            Point2 C { Position: [4mm, 3mm] } Point2 D { Position: [0mm, 3mm] }
            Line2 AB { From: A; To: B } Line2 CB { From: C; To: B }
            Line2 DC { From: D; To: C } Line2 AD { From: A; To: D }
            Profile Plate { Loop Outer { AB |> CB |> DC |> AD |> Close } }
            Extrude Solid { Profile: Plate; From: 0mm; To: 1mm }
            """);

        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        Assert.Empty(parsed.Diagnostics);
        Assert.Equal([false, true, true, true], profile.Loops.Single().Segments.Select(segment => segment.Provenance.Reversed));
        Assert.True(ResolvedProfile2DValidator.Validate(profile).IsValid);
    }

    [Fact]
    public void LineArcPipeline_IsNotRectangleSpecific()
    {
        var parsed = ProfileAuthoringParser.Parse("""
            Concept Path Stadium {
                Start: Point2(0mm, 0mm) Heading: 0deg
                Line Bottom { Length: 10mm }
                Arc Right { Radius: 5mm; Turn: 180deg }
                Line Top { Length: 10mm }
                Arc Left { Radius: 5mm; Turn: 180deg }
            }
            Profile Plate { Loop Outer {
                Stadium.Bottom |> Stadium.Right |> Stadium.Top |> Stadium.Left |> Close
            } }
            Extrude Solid { Profile: Plate; From: 0mm; To: 2mm }
            """);

        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        Assert.Empty(parsed.Diagnostics);
        Assert.Equal(2, profile.Loops.Single().Segments.Count(segment => segment.Geometry is LineArcLineSegment2D));
        Assert.Equal(2, profile.Loops.Single().Segments.Count(segment => segment.Geometry is LineArcCircularArc2D));
        Assert.True(ResolvedProfile2DValidator.Validate(profile).IsValid);
    }

    [Fact]
    public void OuterAndInnerPipelines_PreserveHoleOrientationAndIdentity()
    {
        var parsed = ProfileAuthoringParser.Parse("""
            Rect2 Stock { Center: [0mm, 0mm]; Size: [20mm, 20mm] }
            Rect2 Cutout { Center: [0mm, 0mm]; Size: [6mm, 6mm] }
            Profile Plate { Loop Outer {
                Stock.Bottom |> Stock.Right |> Stock.Top |> Stock.Left |> Close
            } Loop Inner {
                Reverse Cutout.Bottom |> Reverse Cutout.Left |> Reverse Cutout.Top |> Reverse Cutout.Right |> Close
            } }
            Extrude Solid { Profile: Plate; From: 0mm; To: 2mm }
            """);

        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        Assert.Empty(parsed.Diagnostics);
        Assert.Equal(2, profile.Loops.Count);
        Assert.True(ResolvedProfile2DValidator.Validate(profile).IsValid);
        Assert.Equal(LineArcProfileExtrudeStatus.Succeeded, ResolvedProfile2DValidator.Extrude(profile, parsed.Height).Status);
    }

    [Fact]
    public void ConceptPathPipeline_MayRemainOpenAndRetainsProvenance()
    {
        var source = """
            Rect2 Stock { Center: [0mm, 0mm]; Size: [10mm, 5mm] }
            Concept Path Guide { Stock.Bottom |> Stock.Right |> Stock.Top As Return }
            """;

        var path = Assert.IsType<ResolvedConceptPath2D>(ProfileAuthoringParser.ResolveConceptPath(source, "Guide", out var diagnostics));
        Assert.Empty(diagnostics);
        Assert.Equal(["Bottom", "Right", "Return"], path.Segments.Select(segment => segment.Name));
        Assert.Equal(["Stock.Bottom", "Stock.Right", "Stock.Top"], path.Segments.Select(segment => segment.TracedFrom));
        Assert.False(AreSame(path.Segments[0].Geometry, path.Segments[^1].Geometry));
    }

    [Fact]
    public void TraceLoop_RequiresAndNormalizesARealClosedConceptPath()
    {
        var source = """
            Concept Path Outline {
                Start: Point2(0mm, 0mm) Heading: 0deg
                Line South { Length: 10mm } Line East { Turn: 90deg; Length: 5mm }
                Line North { Turn: 90deg; Length: 10mm } Close West
            }
            Profile Plate { Outline |> TraceLoop }
            Extrude Solid { Profile: Plate; From: 0mm; To: 2mm }
            """;

        var parsed = ProfileAuthoringParser.Parse(source);
        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        Assert.Empty(parsed.Diagnostics);
        Assert.Equal(["South", "East", "North", "West"], profile.Loops.Single().Segments.Select(segment => segment.Name));
        Assert.True(ResolvedProfile2DValidator.Validate(profile).IsValid);
    }

    [Fact]
    public void TraceLoop_NormalizesInnerWindingAndQualifiesInheritedIdentity()
    {
        var parsed = ProfileAuthoringParser.Parse("""
            Concept Path OuterPath { Start: Point2(0mm, 0mm) Heading: 0deg Line Bottom { Length: 20mm } Line Right { Turn: 90deg; Length: 20mm } Line Top { Turn: 90deg; Length: 20mm } Close Left }
            Concept Path InnerPath { Start: Point2(5mm, 5mm) Heading: 0deg Line Bottom { Length: 10mm } Line Right { Turn: 90deg; Length: 10mm } Line Top { Turn: 90deg; Length: 10mm } Close Left }
            Profile Plate { Loop Outer { OuterPath |> TraceLoop } Loop Inner { InnerPath |> TraceLoop } }
            Extrude Solid { Profile: Plate; From: 0mm; To: 2mm }
            """);

        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        Assert.Empty(parsed.Diagnostics);
        Assert.All(profile.Loops.Single(loop => !loop.IsOuter).Segments, segment =>
        {
            Assert.StartsWith("Inner.", segment.Name, StringComparison.Ordinal);
            Assert.True(segment.Provenance.Reversed);
        });
        Assert.True(ResolvedProfile2DValidator.Validate(profile).IsValid);
    }

    [Theory]
    [InlineData("Stock.Bottom |> Stock.Top |> Close", "firmament-profile-pipeline-disconnected:Stock.Top")]
    [InlineData("Stock.Bottom |> Stock.Right |> Stock.Top |> Stock.Left |> Stock.Bottom |> Close", "firmament-profile-pipeline-identity-collision:Bottom")]
    [InlineData("Stock.Bottom |> Material |> Close", "firmament-pipeline-stage-type:Material")]
    [InlineData("Stock.Bottom |> Stock.Right |> Close", "firmament-pipeline-close-open:Outer")]
    public void InvalidPipeline_ReportsTypedDiagnostic(string expression, string expected)
    {
        var parsed = ProfileAuthoringParser.Parse($$"""
            Rect2 Stock { Center: [0mm, 0mm]; Size: [10mm, 5mm] }
            Profile Plate { Loop Outer { {{expression}} } }
            Extrude Solid { Profile: Plate; From: 0mm; To: 1mm }
            """);

        Assert.Null(parsed.Profile);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.StartsWith(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void DegenerateAttachment_RejectsAmbiguousOrientation()
    {
        var parsed = ProfileAuthoringParser.Parse("""
            Point2 A { Position: [0mm, 0mm] } Point2 B { Position: [1mm, 0mm] }
            Line2 AB { From: A; To: B } Line2 Same { From: B; To: B }
            Profile Plate { Loop Outer { AB |> Same |> Close } }
            Extrude Solid { Profile: Plate; From: 0mm; To: 1mm }
            """);

        Assert.Null(parsed.Profile);
        Assert.Contains("firmament-profile-pipeline-orientation-ambiguous:Same", parsed.Diagnostics);
    }

    [Theory]
    [InlineData("Stock.Bottom + 1mm |> Stock.Right", "firmament-profile-pipeline-expression-required")]
    [InlineData("Stock.Bottom |> If(Stock.Right)", "firmament-profile-pipeline-expression-required")]
    [InlineData("Stock.Bottom |> Pattern", "firmament-pipeline-stage-type:Pattern")]
    public void PipelineGrammar_IsLowPrecedenceQualifiedGeometryOnly(string expression, string expected)
    {
        var parsed = ProfileAuthoringParser.Parse($$"""
            Rect2 Stock { Center: [0mm, 0mm]; Size: [10mm, 5mm] }
            Profile Plate { Loop Outer { {{expression}} } }
            Extrude Solid { Profile: Plate; From: 0mm; To: 1mm }
            """);

        Assert.Null(parsed.Profile);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.StartsWith(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void PipelineEliminatesManualTraceEndpointContradiction()
    {
        var manual = ProfileAuthoringParser.Parse("""
            Rect2 Stock { Center: [0mm, 0mm]; Size: [10mm, 5mm] }
            Profile Plate { Loop Outer {
                Segment Bottom { Trace: Stock.Bottom; From: Stock.TopLeft; To: Stock.TopRight }
            } }
            Extrude Solid { Profile: Plate; From: 0mm; To: 1mm }
            """);
        var pipeline = ProfileAuthoringParser.Parse(RectanglePipeline());

        Assert.Null(manual.Profile);
        Assert.Contains(manual.Diagnostics, diagnostic => diagnostic.StartsWith("profile-endpoint-not-on-guide:Bottom:Stock.Bottom", StringComparison.Ordinal));
        Assert.NotNull(pipeline.Profile);
    }

    [Fact]
    public void PipelineOutsideProfileOrPath_IsContextTypedError()
    {
        var parsed = FirmamentV2Parser.Parse("""
            Model InvalidPipe {
                Units: mm
                Box Body { Size: [10mm, 10mm, 10mm] }
                Material |> Close
            }
            """);

        Assert.Null(parsed.Document);
        Assert.Contains("firmament-pipeline-context-type:Material", parsed.Diagnostics);
    }

    private static string RectanglePipeline() => """
        Rect2 Stock { Center: [0mm, 0mm]; Size: [10mm, 5mm] }
        Profile Plate { Loop Outer {
            Stock.Bottom |> Stock.Right |> Stock.Top |> Stock.Left |> Close
        } }
        Extrude Solid { Profile: Plate; From: 0mm; To: 2mm }
        """;

    private static string RectangleManual() => """
        Rect2 Stock { Center: [0mm, 0mm]; Size: [10mm, 5mm] }
        Profile Plate { Loop Outer {
            Segment Bottom { Trace: Stock.Bottom; From: Stock.BottomLeft; To: Stock.BottomRight }
            Segment Right { Trace: Stock.Right; From: Stock.BottomRight; To: Stock.TopRight }
            Segment Top { Trace: Stock.Top; From: Stock.TopRight; To: Stock.TopLeft }
            Segment Left { Trace: Stock.Left; From: Stock.TopLeft; To: Stock.BottomLeft }
        } }
        Extrude Solid { Profile: Plate; From: 0mm; To: 2mm }
        """;

    private static string Signature(ResolvedProfile2D profile) => string.Join("|", profile.Loops.SelectMany(loop => loop.Segments).Select(segment => segment.Geometry.ToString()));
    private static bool AreSame(LineArcProfileCurve2D left, LineArcProfileCurve2D right) => left == right;
}
