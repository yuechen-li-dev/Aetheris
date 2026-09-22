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
                Reverse Cutout.Bottom As CutoutBottom |> Reverse Cutout.Left As CutoutLeft |> Reverse Cutout.Top As CutoutTop |> Reverse Cutout.Right As CutoutRight |> Close
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
    public void TraceLoop_NormalizesInnerWindingAndPreservesLeafIdentity()
    {
        var parsed = ProfileAuthoringParser.Parse("""
            Concept Path OuterPath { Start: Point2(0mm, 0mm) Heading: 0deg Line Bottom { Length: 20mm } Line Right { Turn: 90deg; Length: 20mm } Line Top { Turn: 90deg; Length: 20mm } Close Left }
            Concept Path InnerPath { Start: Point2(5mm, 5mm) Heading: 0deg Line InnerBottom { Length: 10mm } Line InnerRight { Turn: 90deg; Length: 10mm } Line InnerTop { Turn: 90deg; Length: 10mm } Close InnerLeft }
            Profile Plate { Loop Outer { OuterPath |> TraceLoop } Loop Inner { InnerPath |> TraceLoop } }
            Extrude Solid { Profile: Plate; From: 0mm; To: 2mm }
            """);

        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        Assert.Empty(parsed.Diagnostics);
        Assert.All(profile.Loops.Single(loop => !loop.IsOuter).Segments, segment =>
        {
            Assert.StartsWith("Inner", segment.Name, StringComparison.Ordinal);
            Assert.True(segment.Provenance.Reversed);
        });
        Assert.True(ResolvedProfile2DValidator.Validate(profile).IsValid);
    }

    [Theory]
    [InlineData("Stock.Bottom |> Stock.Top |> Close", "firmament-profile-pipeline-disconnected:Stock.Top")]
    [InlineData("Stock.Bottom |> Stock.Right |> Stock.Top |> Stock.Left |> Stock.Bottom |> Close", "firmament-profile-pipeline-identity-collision:Bottom")]
    [InlineData("Stock.Bottom |> Material |> Close", "firmament-pipeline-unknown-guide:Material")]
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
    [InlineData("Stock.Bottom |> Pattern", "firmament-pipeline-unknown-guide:Pattern")]
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

    [Fact]
    public void SubspanPipeline_AuthorsCanonicalLBracketWithPreservedIdentity()
    {
        var parsed = ProfileAuthoringParser.Parse("""
            Rect2 Horizontal { Center: [0mm, -10mm]; Size: [80mm, 20mm] }
            Rect2 Vertical { Center: [-30mm, 10mm]; Size: [20mm, 60mm] }
            Point2 Notch { Position: [-20mm, 0mm] }
            Profile Bracket { Loop Outer {
                Horizontal.Bottom As South
                |> Horizontal.Right As East
                |> Horizontal.Top To Notch As Inner
                |> Vertical.Right To Vertical.TopRight As Upright
                |> Vertical.Top As North
                |> Vertical.Left To Horizontal.BottomLeft As West
                |> Close
            } }
            Extrude Solid { Profile: Bracket; From: 0mm; To: 12mm }
            """);

        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        Assert.Empty(parsed.Diagnostics);
        Assert.Equal(["South", "East", "Inner", "Upright", "North", "West"], profile.Loops.Single().Segments.Select(segment => segment.Name));
        Assert.Equal(new LineArcLineSegment2D((-40, -20), (40, -20)), profile.Loops.Single().Segments[0].Geometry);
        Assert.Equal(new LineArcLineSegment2D((40, 0), (-20, 0)), profile.Loops.Single().Segments[2].Geometry);
    }

    [Fact]
    public void SubspanMigrationOracle_LBracketAndSphereAreCanonicalDumpIdentical()
    {
        const string layout = """
            Rect2 Horizontal { Center: [0mm, -10mm]; Size: [80mm, 20mm] }
            Rect2 Vertical { Center: [-30mm, 10mm]; Size: [20mm, 60mm] }
            Point2 Notch { Position: [-20mm, 0mm] }
            """;
        var manualBracket = ProfileAuthoringParser.ResolveNamedProfile(layout + """
            Profile Bracket { Loop Outer {
                Segment South { Trace: Horizontal.Bottom; From: Horizontal.BottomLeft; To: Horizontal.BottomRight }
                Segment East { Trace: Horizontal.Right; From: Horizontal.BottomRight; To: Horizontal.TopRight }
                Segment Inner { Trace: Horizontal.Top; From: Horizontal.TopRight; To: Notch }
                Segment Upright { Trace: Vertical.Right; From: Notch; To: Vertical.TopRight }
                Segment North { Trace: Vertical.Top; From: Vertical.TopRight; To: Vertical.TopLeft }
                Segment West { Trace: Vertical.Left; From: Vertical.TopLeft; To: Horizontal.BottomLeft }
            } }
            """, "Bracket", out var manualBracketDiagnostics);
        var pipelineBracket = ProfileAuthoringParser.ResolveNamedProfile(layout + """
            Profile Bracket { Loop Outer {
                Horizontal.Bottom As South |> Horizontal.Right As East |> Horizontal.Top To Notch As Inner
                |> Vertical.Right To Vertical.TopRight As Upright |> Vertical.Top As North
                |> Vertical.Left To Horizontal.BottomLeft As West |> Close
            } }
            """, "Bracket", out var pipelineBracketDiagnostics);

        const string sphereLayout = """
            Point2 South { Position: [0mm, -20mm] } Point2 North { Position: [0mm, 20mm] }
            Point2 Center { Position: [0mm, 0mm] } Concept Circle2 Meridian { Center: Center; Radius: 20mm }
            Line2 Diameter { From: North; To: South }
            """;
        var manualSphere = ProfileAuthoringParser.ResolveNamedProfile(sphereLayout + """
            Profile SphereSection { Loop Outer {
                Segment Arc { Trace: Meridian; From: South; To: North; Sweep: CounterClockwise }
                Segment AxisClosure { Trace: Diameter; From: North; To: South }
            } }
            """, "SphereSection", out var manualSphereDiagnostics);
        var pipelineSphere = ProfileAuthoringParser.ResolveNamedProfile(sphereLayout + """
            Profile SphereSection { Loop Outer { Meridian From South To North As Arc |> Diameter As AxisClosure |> Close } }
            """, "SphereSection", out var pipelineSphereDiagnostics);

        Assert.Empty(manualBracketDiagnostics); Assert.Empty(pipelineBracketDiagnostics);
        Assert.Empty(manualSphereDiagnostics); Assert.Empty(pipelineSphereDiagnostics);
        Assert.Equal(CanonicalDump(Assert.IsType<ResolvedProfile2D>(manualBracket)), CanonicalDump(Assert.IsType<ResolvedProfile2D>(pipelineBracket)));
        Assert.Equal(CanonicalDump(Assert.IsType<ResolvedProfile2D>(manualSphere)), CanonicalDump(Assert.IsType<ResolvedProfile2D>(pipelineSphere)));
    }

    [Fact]
    public void OpeningSubspan_UsesExplicitFromAndReverseCanSelectNaturalEnd()
    {
        var forward = ProfileAuthoringParser.Parse("""
            Point2 A { Position: [0mm, 0mm] } Point2 B { Position: [10mm, 0mm] }
            Point2 M { Position: [5mm, 0mm] } Point2 C { Position: [5mm, 5mm] }
            Line2 Base { From: A; To: B } Line2 MC { From: M; To: C } Line2 CA { From: C; To: A }
            Profile P { Base From A To M As Partial |> MC |> CA |> Close }
            Extrude Solid { Profile: P; From: 0mm; To: 1mm }
            """);
        var reverse = ProfileAuthoringParser.Parse("""
            Point2 A { Position: [0mm, 0mm] } Point2 B { Position: [10mm, 0mm] } Point2 C { Position: [0mm, 5mm] }
            Line2 Base { From: A; To: B } Line2 AC { From: A; To: C } Line2 CB { From: C; To: B }
            Profile P { Reverse Base To A As Partial |> AC |> CB |> Close }
            Extrude Solid { Profile: P; From: 0mm; To: 1mm }
            """);

        Assert.Empty(forward.Diagnostics);
        Assert.Equal(new LineArcLineSegment2D((0, 0), (5, 0)), Assert.IsType<ResolvedProfile2D>(forward.Profile).Loops.Single().Segments[0].Geometry);
        Assert.Empty(reverse.Diagnostics);
        Assert.Equal(new LineArcLineSegment2D((10, 0), (0, 0)), Assert.IsType<ResolvedProfile2D>(reverse.Profile).Loops.Single().Segments[0].Geometry);
    }

    [Fact]
    public void CircleSubspan_ExpressesHalfCircleWithoutSweep()
    {
        var parsed = ProfileAuthoringParser.Parse("""
            Point2 South { Position: [0mm, -20mm] } Point2 North { Position: [0mm, 20mm] } Point2 Center { Position: [0mm, 0mm] }
            Concept Circle2 Meridian { Center: Center; Radius: 20mm }
            Line2 Diameter { From: North; To: South }
            Profile SphereSection { Meridian From South To North As Arc |> Diameter As AxisClosure |> Close }
            Extrude Solid { Profile: SphereSection; From: 0mm; To: 1mm }
            """);

        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        Assert.Empty(parsed.Diagnostics);
        var arc = Assert.IsType<LineArcCircularArc2D>(profile.Loops.Single().Segments[0].Geometry);
        Assert.Equal(Math.PI, arc.SweepAngleRadians, 12);
        Assert.Equal(["Arc", "AxisClosure"], profile.Loops.Single().Segments.Select(segment => segment.Name));
    }

    [Fact]
    public void InlineGuideDeclarations_CoexistWithOneImplicitPipeline()
    {
        var parsed = ProfileAuthoringParser.Parse("""
            Profile SideProfile {
                Rect2 Outline { Center: [0mm, 0mm]; Size: [20mm, 10mm] }
                Point2 Witness { Position: [0mm, 0mm] }
                Outline |> TraceLoop
            }
            Extrude Solid { Profile: SideProfile; From: -5mm; To: 5mm }
            """);

        Assert.Empty(parsed.Diagnostics);
        Assert.Equal(4, Assert.IsType<ResolvedProfile2D>(parsed.Profile).Loops.Single().Segments.Count);
    }

    [Fact]
    public void InlineDeclaration_WithSecondPipeline_IsRejectedAsAmbiguous()
    {
        var parsed = ProfileAuthoringParser.Parse("""
            Profile P {
                Rect2 A { Center: [0mm, 0mm]; Size: [10mm, 10mm] }
                Rect2 B { Center: [20mm, 0mm]; Size: [10mm, 10mm] }
                A |> TraceLoop
                B |> TraceLoop
            }
            Extrude Solid { Profile: P; From: 0mm; To: 1mm }
            """);

        Assert.Null(parsed.Profile);
        Assert.Contains("firmament-profile-pipeline-expression-required:P:Outer", parsed.Diagnostics);
    }

    [Theory]
    [InlineData("Rect2 Shape { Center: [0mm, 0mm]; Size: [10mm, 6mm] }")]
    [InlineData("Square2 Shape { Center: [0mm, 0mm]; Size: 10mm }")]
    [InlineData("Polygon2<Rhombus> Shape { Center: [0mm, 0mm]; Diagonals: [10mm, 6mm] }")]
    [InlineData("RegularPolygon2<5> Shape { Center: [0mm, 0mm]; Circumradius: 5mm }")]
    [InlineData("Circle2 Shape { Center: [0mm, 0mm]; Radius: 5mm }")]
    [InlineData("Ellipse2 Shape { Center: [0mm, 0mm]; AxisLengths: [10mm, 6mm] }")]
    public void TraceLoop_AdmitsClosedOrderedGuideFamilies(string declaration)
    {
        var parsed = ProfileAuthoringParser.Parse($$"""
            {{declaration}}
            Profile P { Shape |> TraceLoop }
            Extrude Solid { Profile: P; From: 0mm; To: 1mm }
            """);

        Assert.Empty(parsed.Diagnostics);
        Assert.NotEmpty(Assert.IsType<ResolvedProfile2D>(parsed.Profile).Loops.Single().Segments);
    }

    [Fact]
    public void ReverseMidChain_IsPreferenceAndFallsBackToForwardConnectivity()
    {
        var parsed = ProfileAuthoringParser.Parse("""
            Rect2 Stock { Center: [0mm, 0mm]; Size: [10mm, 5mm] }
            Profile Plate { Stock.Bottom |> Reverse Stock.Right |> Stock.Top |> Stock.Left |> Close }
            Extrude Solid { Profile: Plate; From: 0mm; To: 1mm }
            """);

        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        Assert.Empty(parsed.Diagnostics);
        Assert.False(profile.Loops.Single().Segments[1].Provenance.Reversed);
    }

    [Fact]
    public void CrossLoopLeafCollision_IsExplicitAndAliasesResolveIt()
    {
        var collision = ProfileAuthoringParser.Parse("""
            Rect2 OuterBox { Center: [0mm, 0mm]; Size: [20mm, 20mm] }
            Rect2 InnerBox { Center: [0mm, 0mm]; Size: [6mm, 6mm] }
            Profile Plate { Loop Outer { OuterBox |> TraceLoop } Loop Inner { InnerBox |> TraceLoop } }
            Extrude Solid { Profile: Plate; From: 0mm; To: 1mm }
            """);
        Assert.Null(collision.Profile);
        Assert.Contains(collision.Diagnostics, diagnostic => diagnostic.StartsWith("firmament-profile-segment-identity-collision:Bottom:loops=Inner,Outer", StringComparison.Ordinal));

        var aliased = ProfileAuthoringParser.Parse("""
            Rect2 OuterBox { Center: [0mm, 0mm]; Size: [20mm, 20mm] }
            Rect2 InnerBox { Center: [0mm, 0mm]; Size: [6mm, 6mm] }
            Profile Plate { Loop Outer { OuterBox |> TraceLoop } Loop Inner {
                Reverse InnerBox.Bottom As CutoutBottom |> Reverse InnerBox.Left As CutoutLeft |> Reverse InnerBox.Top As CutoutTop |> Reverse InnerBox.Right As CutoutRight |> Close
            } }
            Extrude Solid { Profile: Plate; From: 0mm; To: 1mm }
            """);
        Assert.Empty(aliased.Diagnostics);
        Assert.NotNull(aliased.Profile);
    }

    [Theory]
    [InlineData("Stock.Bottom To Missing |> Stock.Right |> Close", "firmament-pipeline-unknown-endpoint:Missing")]
    [InlineData("Missing |> Close", "firmament-pipeline-unknown-guide:Missing")]
    [InlineData("Stock.Bottom |> Close |> Stock.Right", "firmament-pipeline-stage-type:Close")]
    public void PipelineDiagnostics_DistinguishEndpointGuideAndStageErrors(string expression, string expected)
    {
        var parsed = ProfileAuthoringParser.Parse($$"""
            Rect2 Stock { Center: [0mm, 0mm]; Size: [10mm, 5mm] }
            Profile Plate { {{expression}} }
            Extrude Solid { Profile: Plate; From: 0mm; To: 1mm }
            """);
        Assert.Null(parsed.Profile);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.StartsWith(expected, StringComparison.Ordinal));
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
    private static string CanonicalDump(ResolvedProfile2D profile) => string.Join("\n", new[] { $"frame:{profile.PlaneFrame}" }.Concat(
        profile.Loops.SelectMany(loop => new[] { $"loop:{loop.Name}:{loop.IsOuter}" }.Concat(
            loop.Segments.Select(segment => $"segment:{segment.Name}:{segment.Geometry}")))));
    private static bool AreSame(LineArcProfileCurve2D left, LineArcProfileCurve2D right) => left == right;
}
