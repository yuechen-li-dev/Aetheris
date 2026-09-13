using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class GeomRevolveX1Tests
{
    [Theory]
    [InlineData("quarter", 90d)]
    [InlineData("90deg", 90d)]
    [InlineData("1.5707963267948966", 90d)]
    [InlineData("half", 180d)]
    [InlineData("full", 360d)]
    [InlineData("-90deg", -90d)]
    public void AngleFormsBindAndMaterializeThroughOneBoundedPlan(string angle, double expectedDegrees)
    {
        var result = FirmamentBuildAndExport.CompileSource(Rectangle(angle));
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(expectedDegrees, result.Value.Revolve!.SweepDegrees, 9);
        Assert.Equal(Math.Abs(expectedDegrees) == 360d ? "Full" : "Partial", result.Value.Revolve.Classification);
        Assert.True(result.Value.Revolve.StepReimportedManifold);
        Assert.Equal(result.Value.Revolve.Classification == "Full" ? 2 : 4, result.Value.Revolve.Planes);
    }

    [Fact]
    public void FullRectanglePreservesAnalyticCylinderAndPlaneCarriersDeterministically()
    {
        var first = FirmamentBuildAndExport.CompileSource(Rectangle("full"));
        var second = FirmamentBuildAndExport.CompileSource(Rectangle("360deg"));
        Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics.Select(d => d.Message)));
        Assert.True(second.IsSuccess, string.Join(Environment.NewLine, second.Diagnostics.Select(d => d.Message)));
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Equal(2, first.Value.Revolve!.Cylinders);
        Assert.Equal(2, first.Value.Revolve.Planes);
        Assert.DoesNotContain("B_SPLINE_SURFACE", first.Value.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void SemicircleAboutDiameterProducesExactSphereCarrierAndReimports()
    {
        var result = FirmamentBuildAndExport.CompileSource(Sphere);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(1, result.Value.Revolve!.Spheres);
        Assert.Contains("SPHERICAL_SURFACE", result.Value.StepText, StringComparison.Ordinal);
        var imported = Step242Importer.ImportBody(result.Value.StepText);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(d => d.Message)));
        Assert.Contains(imported.Value.Topology.Faces, face => imported.Value.GetFaceSurface(face.Id).Kind == SurfaceGeometryKind.Sphere);
    }

    [Fact]
    public void ConceptCircle2IsAReusableCurveWithoutClosedBoundaryBoilerplate()
    {
        var diagnostics = new List<string>();
        var expanded = ClosedBoundary2Authoring.Expand(Sphere, diagnostics);
        Assert.NotNull(expanded);
        Assert.Empty(diagnostics);
        Assert.DoesNotContain(expanded.Boundaries, boundary => boundary.Name == "Meridian");

        var profile = ProfileAuthoringParser.ResolveNamedProfile(expanded.Source, "SphereSection", out var profileDiagnostics);
        Assert.NotNull(profile);
        Assert.Empty(profileDiagnostics);
        Assert.IsType<LineArcCircularArc2D>(profile.Loops[0].Segments[0].Geometry);
    }

    [Fact]
    public void NonPrincipalCoplanarAxisIsAdmitted()
    {
        var result = FirmamentBuildAndExport.CompileSource(ArbitraryAxis);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.True(result.Value.Revolve!.StepReimportedManifold);
    }

    [Theory]
    [InlineData("0deg", "firmament-revolve-zero-angle:Ring")]
    [InlineData("361deg", "firmament-revolve-angle-out-of-range:Ring")]
    public void InvalidAnglesAreRejectedBeforeMaterialization(string angle, string diagnostic)
    {
        var result = FirmamentBuildAndExport.CompileSource(Rectangle(angle));
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, d => d.Message == diagnostic);
    }

    [Fact]
    public void ZeroSkewAndCrossingAxisCasesHaveTypedDiagnostics()
    {
        var zero = FirmamentBuildAndExport.CompileSource(Rectangle("full").Replace("Direction: [0, 1, 0]", "Direction: [0, 0, 0]", StringComparison.Ordinal));
        var skew = FirmamentBuildAndExport.CompileSource(Rectangle("full").Replace("Direction: [0, 1, 0]", "Direction: [0, 1, 1]", StringComparison.Ordinal));
        var crossing = FirmamentBuildAndExport.CompileSource(Rectangle("full").Replace("Center: [15mm, 0mm]", "Center: [0mm, 0mm]", StringComparison.Ordinal));
        Assert.Contains(zero.Diagnostics, d => d.Message == "firmament-revolve-axis-zero-direction:Ring");
        Assert.Contains(skew.Diagnostics, d => d.Message == "firmament-revolve-axis-not-in-profile-plane:Ring");
        Assert.Contains(crossing.Diagnostics, d => d.Message == "firmament-revolve-profile-crosses-axis:Ring");
    }

    private static string Rectangle(string angle) => $$"""
        Model TurnedRing {
            Units: mm
            Axis MainAxis { Origin: [0mm, 0mm, 0mm]; Direction: [0, 1, 0] }
            Concept Struct Layout On XY { Rect2 Section { Center: [15mm, 0mm]; Size: [10mm, 20mm] } }
            Profile RingSection Using Layout { Loop Outer { Section.Bottom |> Section.Right |> Section.Top |> Section.Left |> Close } }
            Revolve Ring { Profile: RingSection; About: MainAxis; Angle: {{angle}} }
        }
        """;

    private const string Sphere = """
        Model RevolvedSphere {
            Units: mm
            Axis MainAxis { Origin: [0mm, 0mm, 0mm]; Direction: [0, 1, 0] }
            Concept Struct Layout On XY {
                Point2 South { Position: [0mm, -20mm] }
                Point2 North { Position: [0mm, 20mm] }
                Point2 Center { Position: [0mm, 0mm] }
                Concept Circle2 Meridian { Center: Center; Radius: 20mm }
                Line2 Diameter { From: North; To: South }
            }
            Profile SphereSection Using Layout { Loop Outer {
                Segment Arc { Trace: Meridian; From: South; To: North; Sweep: CounterClockwise }
                Segment AxisClosure { Trace: Diameter; From: North; To: South }
            } }
            Revolve Ball { Profile: SphereSection; About: MainAxis; Angle: full }
        }
        """;

    private const string ArbitraryAxis = """
        Model ArbitraryAxisTurn {
            Units: mm
            Axis Tilted { Origin: [3mm, 4mm, 5mm]; Direction: [1, 1, 0] }
            Concept Struct Supports { Datum: Plane { Origin: [3mm,4mm,5mm]; Normal: [1,-1,0]; Up: [0,0,1] } }
            Construction Plane SectionFrame { Trace: Supports.Datum }
            Concept Struct Layout On SectionFrame { Rect2 Section { Center: [0mm, 15mm]; Size: [20mm, 10mm] } }
            Profile SectionProfile Using SectionFrame { Loop Outer { Section.Bottom |> Section.Right |> Section.Top |> Section.Left |> Close } }
            Revolve TiltedRing { Profile: SectionProfile; About: Tilted; Angle: quarter }
        }
        """;
}
