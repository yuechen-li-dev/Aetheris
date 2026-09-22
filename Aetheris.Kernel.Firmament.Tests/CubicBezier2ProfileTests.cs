using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CubicBezier2ProfileTests
{
    [Fact]
    public void LampBaseUsesTwoDirectPolynomialFlanksAndExportsWithoutRationalGeometry()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/ThreeDm/lamp-base-intent.firmament");
        var source = File.ReadAllText(path);
        var profile = ProfileAuthoringParser.ResolveNamedProfile(source, "PlateSection", out var diagnostics);
        Assert.Empty(diagnostics);
        Assert.NotNull(profile);
        Assert.Equal(2, profile.Loops.Single(loop => loop.IsOuter).Segments.Count(segment => segment.Geometry is LineArcCubicBezier2D));

        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join("\n", build.Diagnostics.Select(d => d.Message)));
        Assert.Empty(build.Diagnostics);
        Assert.Contains("B_SPLINE_CURVE_WITH_KNOTS", build.Value.StepText);
        Assert.DoesNotContain("RATIONAL_B_SPLINE_CURVE", build.Value.StepText);
        Assert.DoesNotContain("RATIONAL_B_SPLINE_SURFACE", build.Value.StepText);
    }

    [Fact]
    public void CubicGuideRequiresNamedControlsAndItsAuthoredEndpoints()
    {
        const string source = """
            Model InvalidCubic {
                Units: mm
                Concept Struct Sketch On XY {
                    Point2 A { Position: [0mm,0mm] }
                    Point2 B { Position: [10mm,0mm] }
                    Point2 C { Position: [10mm,10mm] }
                    CubicBezier2 Flank { From: A; Control1: B; Control2: Missing; To: C }
                }
                Profile Outline Using Sketch { Loop Outer {
                    Segment Curve { Trace: Flank; From: A; To: C }
                } }
            }
            """;
        _ = ProfileAuthoringParser.ResolveNamedProfile(source, "Outline", out var diagnostics);
        Assert.Contains(diagnostics, d => d.Contains("profile-layout-unresolved-cubic-bezier:Flank", StringComparison.Ordinal));
    }
}
