using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

/// <summary>
/// <c>draft:</c> on an Extrude: every wall is drafted exactly (lines become drafted planes, arcs right cones), the
/// section at the neutral plane is the Profile, and anything without an exact drafted wall is refused.
/// </summary>
[Trait("Suite", "Profile")]
public sealed class DraftedExtrudeTests
{
    private const string Bracket = """
        Model DraftBracket {
          units: mm
          Path Outline {
            start: Point2(5mm, 0mm)
            heading: 0deg
            Line Base { length: 55mm }
            Arc C1 { Radius: 5mm; Turn: 90deg }
            Line Flank { length: 20mm }
            Arc C2 { Radius: 5mm; Turn: 90deg }
            Line Shelf { length: 30mm }
            Arc Heel { Radius: 4mm; Turn: -90deg }
            Line Riser { length: 26mm }
            Arc C3 { Radius: 5mm; Turn: 90deg }
            Line Crown { length: 16mm }
            Arc C4 { Radius: 5mm; Turn: 90deg }
            Line Back { length: 55mm }
            Arc C5 { Radius: 5mm; Turn: 90deg }
          }
          Circle2 Bore { center: [40mm, 15mm]; radius: 4mm }
          Rect2 Window { center: [12mm, 40mm]; size: [8mm, 20mm] }
          Profile outline {
            Loop Outer {
              Outline.Base |> Outline.C1 |> Outline.Flank |> Outline.C2 |> Outline.Shelf |> Outline.Heel
              |> Outline.Riser |> Outline.C3 |> Outline.Crown |> Outline.C4 |> Outline.Back |> Outline.C5 |> Close
            }
            Loop Bore { Bore |> TraceLoop }
            Loop Window { Window |> TraceLoop }
          }
          Extrude body { profile: outline; from: 0mm; to: 10mm; draft: 1.5deg; neutral: 10mm }
        }
        """;

    private static FirmamentStepExportResult Build(string source)
    {
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics.Select(d => d.Message)));
        return built.Value;
    }

    /// <summary>The draft of each side wall: a plane's tilt from the extrusion axis, a cone's semi-angle about it.</summary>
    private static IReadOnlyList<(SurfaceGeometryKind Kind, double DraftDegrees)> SideDrafts(FirmamentStepExportResult result)
    {
        var body = result.RuntimeBody!;
        var drafts = new List<(SurfaceGeometryKind, double)>();
        foreach (var face in body.Topology.Faces)
        {
            Assert.True(body.TryGetFaceSurfaceGeometry(face.Id, out var surface) && surface is not null);
            if (surface.Plane is { } plane)
            {
                var z = double.Abs(plane.Normal.ToVector().Z);
                if (z < 1 - 1e-12) drafts.Add((surface.Kind, double.Asin(z) * 180 / double.Pi));
            }
            else if (surface.Cone is { } cone)
            {
                Assert.Equal(1d, double.Abs(cone.Axis.ToVector().Z), 12);
                drafts.Add((surface.Kind, cone.SemiAngleRadians * 180 / double.Pi));
            }
            else Assert.Fail($"unexpected side surface {surface.Kind}");
        }
        return drafts;
    }

    private static void AssertReimportsFacingOut(FirmamentStepExportResult result, double volume, double relative)
    {
        var imported = Step242Importer.ImportBody(result.StepText);
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics.Select(d => d.Message)));
        Assert.DoesNotContain(imported.Diagnostics, d => d.Message.StartsWith("step-orientation-source-mismatch", StringComparison.Ordinal));
        var mass = BrepMassProperties.Evaluate(imported.Value);
        Assert.True(mass.IsEnclosed && mass.IsOrientationConsistent && mass.SignedVolume > 0);
        Assert.True(double.Abs(mass.SignedVolume - volume) <= relative * volume, $"{mass.SignedVolume} vs {volume}");
    }

    [Fact]
    public void RoundedRectangle_DraftsToExactPlanesAndCones()
    {
        var result = Build("""
            Model Cover {
              units: mm
              Profile outline = RoundedRect2 { center: [0mm, 0mm]; size: [80mm, 50mm]; radius: 6mm }
              Extrude body { profile: outline; from: 0mm; to: 12mm; draft: 2deg }
            }
            """);
        var drafts = SideDrafts(result);
        Assert.Equal(4, drafts.Count(d => d.Kind == SurfaceGeometryKind.Plane));
        Assert.Equal(4, drafts.Count(d => d.Kind == SurfaceGeometryKind.Cone));
        Assert.All(drafts, d => Assert.Equal(2d, d.DraftDegrees, 9));
        // The section eroded by d = z tan 2°: A(d) = (80 − 2d)(50 − 2d) − (4 − π)(6 − d)², integrated over 12 mm.
        var t = double.Tan(2 * double.Pi / 180);
        double A(double d) => (80 - 2 * d) * (50 - 2 * d) - (4 - double.Pi) * (6 - d) * (6 - d);
        var volume = 12d / 6 * (A(0) + 4 * A(6 * t) + A(12 * t));
        AssertReimportsFacingOut(result, volume, 1e-4);
    }

    [Fact]
    public void ConcaveArcHolesAndNeutralAtTheTop_AllReleaseTheSameWay()
    {
        var result = Build(Bracket);
        var drafts = SideDrafts(result);
        Assert.Equal(17, drafts.Count);
        Assert.All(drafts, d => Assert.Equal(1.5, d.DraftDegrees, 9));
        // The neutral plane is the top: the top cap is the Profile itself, and the part grows downward. Hole walls
        // move with the material, so the bore and the window are smaller at the bottom.
        var t = double.Tan(1.5 * double.Pi / 180);
        const double area = 2626.3429085516277; // the Profile: outline less the bore and the window
        const double perimeter = 247.5530935 + 8 * double.Pi + 56;
        // Steiner: A(d) = A₀ − L d + c d²; the smooth outer loop and the bore cancel, the window's mitred corners give −4.
        double A(double d) => area - perimeter * d - 4 * d * d;
        var volume = 10d / 6 * (A(-10 * t) + 4 * A(-5 * t) + A(0));
        // The reimported volume is measured on a mesh; the bore's chords bound it to about 2e-4 here.
        AssertReimportsFacingOut(result, volume, 5e-4);
    }

    [Fact]
    public void NegativeDraftAboutAMidplane_WidensTowardTheTop()
    {
        var result = Build("""
            Model Block {
              units: mm
              Profile p = Rect2 { center: [0mm, 0mm]; size: [40mm, 30mm] }
              Extrude b { profile: p; from: -5mm; to: 5mm; draft: -3deg }
            }
            """);
        Assert.All(SideDrafts(result), d => Assert.Equal(3d, d.DraftDegrees, 9));
        var t = double.Tan(3 * double.Pi / 180);
        // The neutral plane is the Profile's own plane, z = 0, where the section is 40 × 30.
        double A(double z) => (40 + 2 * z * t) * (30 + 2 * z * t);
        AssertReimportsFacingOut(result, 10d / 6 * (A(-5) + 4 * A(0) + A(5)), 1e-9);
        var top = result.RuntimeBody!.Topology.Vertices.Select(v => { result.RuntimeBody.TryGetVertexPoint(v.Id, out var p); return p; }).Where(p => p.Z > 4).ToArray();
        Assert.Equal(20 + 5 * t, top.Max(p => p.X), 9);
    }

    [Theory]
    [InlineData("Profile p = Ellipse2 { center: [0mm, 0mm]; axisLengths: [40mm, 20mm] }", "Extrude b { profile: p; from: 0mm; to: 10mm; draft: 1deg }", "", "draft-unsupported-curve")]
    [InlineData("Profile p = RoundedRect2 { center: [0mm, 0mm]; size: [40mm, 30mm]; radius: 1mm }", "Extrude b { profile: p; from: 0mm; to: 20mm; draft: 5deg }", "", "draft-offset-collapse")]
    [InlineData("Profile p = Rect2 { center: [0mm, 0mm]; size: [4mm, 30mm] }", "Extrude b { profile: p; from: 0mm; to: 30mm; draft: 5deg }", "", "draft-offset-collapse")]
    [InlineData("Profile p = Rect2 { center: [0mm, 0mm]; size: [40mm, 30mm] }", "Extrude b { profile: p; from: 0mm; to: 10mm; draft: 2deg }", "Modify b { Hole<Shaft> h { on: +Z; center: [0mm, 0mm]; diameter: 6mm; end: ThroughAll } }", "profile-extrusion-draft-unsupported")]
    [InlineData("Profile p = Rect2 { center: [0mm, 0mm]; size: [40mm, 30mm] }", "Extrude b { profile: p; from: 0mm; to: 10mm; draft: 2deg }", "Modify b { EdgeFinish<Chamfer> c { edges: top; distance: 1mm } }", "profile-extrusion-draft-unsupported")]
    [InlineData("Profile p = Rect2 { center: [0mm, 0mm]; size: [40mm, 30mm] }", "Extrude b { profile: p; from: 0mm; to: 10mm; neutral: 5mm }", "", "profile-extrusion-neutral-without-draft")]
    [InlineData("Profile p = Rect2 { center: [0mm, 0mm]; size: [40mm, 30mm] }", "Extrude b { profile: p; from: 0mm; to: 10mm; draft: 50deg }", "", "draft-angle-invalid")]
    public void WallsWithoutAnExactDraft_AreRefused(string profile, string extrude, string modify, string code)
    {
        var built = FirmamentBuildAndExport.CompileSource($"Model T {{\n  units: mm\n  {profile}\n  {extrude}\n  {modify}\n}}");
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains(code, StringComparison.Ordinal));
    }
}
