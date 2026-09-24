using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

/// <summary>
/// A 50 x 30 x 12 plate with a 12 mm through hole slid toward the +X edge. The sweep walks through every state a boolean
/// kernel finds hard - a thin wall, a 0.1 mm sliver, exact tangency, a hairline breakout and a clean notch - using the
/// most ordinary edit in mechanical design. Both authoring routes must agree, and must agree with the analytic volume.
/// </summary>
public sealed class EdgeHoleBreakoutTests
{
    private const double HoleRadius = 6d;
    private const double HalfLength = 25d;

    public static TheoryData<double> NonDegenerateOffsets => new() { 0d, 12d, 18d, 18.9d, 19.1d, 22d };

    [Theory]
    [MemberData(nameof(NonDegenerateOffsets))]
    public void SemanticHole_BuildsAtEveryNonDegenerateOffset_AndMatchesTheAnalyticVolume(double x)
        => AssertBuildsWithAnalyticVolume(SemanticHoleSource(x), x, relativeTolerance: 2e-3);

    [Theory]
    [MemberData(nameof(NonDegenerateOffsets))]
    public void ComposeRemove_BuildsAtEveryNonDegenerateOffset_AndMatchesTheAnalyticVolume(double x)
        => AssertBuildsWithAnalyticVolume(ComposeSource(x), x, relativeTolerance: 1e-4);

    /// <summary>
    /// Tangency is rejected on purpose: the wall there has zero thickness along a line, which is neither machinable nor a
    /// manifold solid. What is tested is that the rejection says so in the author's terms and gives the threshold.
    /// </summary>
    [Fact]
    public void SemanticHole_ExactTangency_IsRejectedWithTheThresholdInTheAuthorsTerms()
    {
        var build = FirmamentBuildAndExport.CompileSource(SemanticHoleSource(19d));

        Assert.False(build.IsSuccess);
        var error = Assert.Single(build.Diagnostics, d => d.Severity == KernelDiagnosticSeverity.Error);
        Assert.Contains("exactly tangent to the +X face", error.Message, StringComparison.Ordinal);
        Assert.Contains("Put Center X below 19mm to keep a wall on that side, or above it to cut through the +X face.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeRemove_ExactTangency_NamesTheZeroWidthPointInsteadOfAnEmptyStack()
    {
        var build = FirmamentBuildAndExport.CompileSource(ComposeSource(19d));

        Assert.False(build.IsSuccess);
        Assert.Contains(build.Diagnostics, d => d.Message.Contains("two boundaries touch only at the point (25, 0)", StringComparison.Ordinal));
        Assert.DoesNotContain(build.Diagnostics, d => d.Message.Contains("compose-no-material-slabs", StringComparison.Ordinal));
    }

    /// <summary>
    /// The planners narrate their route before stating why they stopped. Only the cause is an error; the narration used
    /// to be reported as errors too, which put "planner started" above the reason. A countersunk hole breaking out is a
    /// deterministic narrate-then-fail case: the breakout route announces itself, then rejects the conical entry.
    /// </summary>
    [Fact]
    public void SemanticHoleFailure_ReportsOnlyTheCauseAsAnError()
    {
        var source = SemanticHoleSource(22d)
            .Replace("Hole<Shaft>", "Hole<Countersink>", StringComparison.Ordinal)
            .Replace("Diameter: 12mm", "Diameter: 12mm\n                    CountersinkDiameter: 16mm\n                    CountersinkAngle: 90deg", StringComparison.Ordinal);

        var build = FirmamentBuildAndExport.CompileSource(source);

        Assert.False(build.IsSuccess);
        var error = Assert.Single(build.Diagnostics, d => d.Severity == KernelDiagnosticSeverity.Error);
        Assert.Contains("countersink is conical", error.Message, StringComparison.Ordinal);
        Assert.Contains(build.Diagnostics, d => d.Severity == KernelDiagnosticSeverity.Info && d.Message.StartsWith("hole-breakout:", StringComparison.Ordinal));
    }

    /// <summary>
    /// The arrangement bug behind the breakout failure: a clockwise arc starting at or beyond pi reported points on its
    /// own lower half as off the arc, because the angle was wrapped in only one direction.
    /// </summary>
    [Fact]
    public void ClockwiseArcStartingPastPi_StillReportsItsOwnPoints()
    {
        // Lower-left quarter of a unit circle, traversed clockwise from 3pi/2 to pi.
        var arc = new LineArcCircularArc2D((0d, 0d), 1d, 3d * Math.PI / 2d, -Math.PI / 2d);
        var chord = new LineArcLineSegment2D((-2d, -Math.Sqrt(0.5d)), (0d, -Math.Sqrt(0.5d)));

        var hit = Assert.Single(ProfileArrangementBuilder.IntersectBounded(chord, arc).Intersections);

        Assert.Equal(-Math.Sqrt(0.5d), hit.Point.X, 12);
        Assert.Equal(0.5d, hit.SecondParameter, 12);
    }

    private static void AssertBuildsWithAnalyticVolume(string source, double x, double relativeTolerance)
    {
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join(" | ", build.Diagnostics.Select(d => d.Message)));

        // Measure the exported STEP, not the in-process body: the claim is about what leaves the kernel.
        var reimport = Step242Importer.ImportBody(build.Value.StepText);
        Assert.True(reimport.IsSuccess, string.Join(" | ", reimport.Diagnostics.Select(d => d.Message)));
        var mass = BrepMassProperties.Evaluate(reimport.Value);
        Assert.True(mass.IsEnclosed, string.Join("; ", mass.Diagnostics));
        Assert.True(mass.IsOrientationConsistent, string.Join("; ", mass.Diagnostics));

        var expected = AnalyticVolume(x);
        Assert.InRange(Math.Abs(mass.AbsoluteVolume - expected) / expected, 0d, relativeTolerance);
    }

    private static double AnalyticVolume(double x)
    {
        var wall = HalfLength - x;
        var area = Math.PI * HoleRadius * HoleRadius;
        if (wall < HoleRadius)
            area -= HoleRadius * HoleRadius * Math.Acos(wall / HoleRadius) - wall * Math.Sqrt(HoleRadius * HoleRadius - wall * wall);
        return 50d * 30d * 12d - area * 12d;
    }

    private static string Mm(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "mm";

    private static string SemanticHoleSource(double x) => $$"""
        Model EdgeClearancePlate {
            Units: mm

            Box Plate { Size: [50mm, 30mm, 12mm] }
            Modify Plate {
                Hole<Shaft> Bolt {
                    On: +Z
                    Center: Point2({{Mm(x)}}, 0mm)
                    Diameter: 12mm
                    End: ThroughAll
                }
            }
        }
        """;

    private static string ComposeSource(double x) => $$"""
        Model EdgeClearancePlate {
            Units: mm
            Concept Struct Layout On XY {
                Rect2 Stock { Center: [0mm, 0mm]; Size: [50mm, 30mm] }
                Point2 C { Position: [{{Mm(x)}}, 0mm] }
                Circle2 Bore { Center: C; Radius: 6mm }
            }
            Profile PlateProfile Using Layout { Loop Outer {
                Stock.Bottom
                |> Stock.Right
                |> Stock.Top
                |> Stock.Left
                |> Close
            } }
            Profile BoltProfile Using Layout { Loop Outer {
                Bore |> TraceLoop
            } }
            Struct Plate { Compose Body {
                Base Stock { Profile: PlateProfile; From: 0mm; To: 12mm; Role: Stock }
                Remove Bolt { Profile: BoltProfile; From: 0mm; To: 12mm; Role: Relief }
            } }
        }
        """;
}
