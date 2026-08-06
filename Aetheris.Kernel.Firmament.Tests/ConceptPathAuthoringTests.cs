using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ConceptPathAuthoringTests
{
    [Fact]
    public void ProfileFromPath_LowersOrderedGuidesIntoOrdinaryResolvedProfile()
    {
        var parsed = ProfileAuthoringParser.Parse(Rectangle("Profile Plate From Outline"));
        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        Assert.Equal(["South", "East", "North", "West"], profile.Loops.Single().Segments.Select(x => x.Name));
        Assert.All(profile.Loops.Single().Segments, x => Assert.StartsWith("concept-path:Outline.", x.Provenance.ConceptStableId, StringComparison.Ordinal));
        Assert.True(ResolvedProfile2DValidator.Validate(profile).IsValid);
        Assert.Equal(LineArcProfileExtrudeStatus.Succeeded, ResolvedProfile2DValidator.Extrude(profile, parsed.Height).Status);
    }

    [Fact]
    public void TurnBeforeAdvance_AndTangentArc_UpdateExactEndpointAndHeading()
    {
        var parsed = ProfileAuthoringParser.Parse("""
            Concept Path Outline {
                Start: Point2(0mm, 0mm)
                Heading: 0deg
                Line East { Length: 10mm }
                Arc Corner { Radius: 5mm; Turn: 90deg }
                Line West { Length: 10mm }
            }
            Profile Plate From Outline
            Extrude Solid { Profile: Plate; From: 0mm; To: 1mm }
            """);
        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        var arc = Assert.IsType<LineArcCircularArc2D>(profile.Loops.Single().Segments[1].Geometry);
        Assert.Equal(10, arc.Center.X, 9); Assert.Equal(5, arc.Center.Y, 9);
        var last = Assert.IsType<LineArcLineSegment2D>(profile.Loops.Single().Segments[2].Geometry);
        Assert.Equal(15, last.Start.X, 9); Assert.Equal(5, last.Start.Y, 9);
        Assert.Equal(15, last.End.X, 9); Assert.Equal(15, last.End.Y, 9);
    }

    [Theory]
    [InlineData("Line Bad { Turn: 90deg; Heading: 0deg; Length: 1mm }", "concept-path-turn-and-heading:Outline:Bad")]
    [InlineData("Line Bad { To: Start; Length: 1mm }", "concept-path-to-mixed-direction-or-length:Outline:Bad")]
    [InlineData("Line Bad { To: Unknown }", "concept-path-unknown-target:Outline:Bad:Unknown")]
    [InlineData("Arc Bad { Radius: 0mm; Turn: 90deg }", "concept-path-arc-invalid:Outline:Bad")]
    public void InvalidPathSteps_ProduceTypedDiagnostics(string step, string expected)
    {
        var parsed = ProfileAuthoringParser.Parse("Concept Path Outline { Start: Point2(0mm, 0mm) Heading: 0deg " + step + " }\nProfile Plate From Outline\nExtrude Solid { Profile: Plate; From: 0mm; To: 1mm }");
        Assert.Null(parsed.Profile);
        Assert.Contains(expected, parsed.Diagnostics);
    }

    [Fact]
    public void ExplicitLoopFromPath_AndLowLevelSegmentsConsumeSameEmittedGuides()
    {
        var source = Rectangle("""
            Profile Plate {
                Loop Outer {
                    Segment SouthEdge { Trace: Outline.South; From: Outline.Start; To: Outline.South.End }
                    Segment EastEdge { Trace: Outline.East; From: Outline.South.End; To: Outline.East.End }
                    Segment NorthEdge { Trace: Outline.North; From: Outline.East.End; To: Outline.North.End }
                    Segment WestEdge { Trace: Outline.West; From: Outline.North.End; To: Outline.West.End }
                }
            }
            """);
        var parsed = ProfileAuthoringParser.Parse(source);
        Assert.True(parsed.Profile is not null, string.Join(Environment.NewLine, parsed.Diagnostics));
        var profile = parsed.Profile;
        Assert.Equal(["SouthEdge", "EastEdge", "NorthEdge", "WestEdge"], profile.Loops.Single().Segments.Select(x => x.Name));
        Assert.True(ResolvedProfile2DValidator.Validate(profile).IsValid);
    }

    private static string Rectangle(string profile) => """
        Concept Path Outline {
            Start: Point2(0mm, 0mm)
            Heading: 0deg
            Line South { Length: 10mm }
            Line East { Turn: 90deg; Length: 5mm }
            Line North { Turn: 90deg; Length: 10mm }
            Close West
        }
        """ + profile + """

        Extrude Solid { Profile: Plate; From: 0mm; To: 2mm }
        """;
}
