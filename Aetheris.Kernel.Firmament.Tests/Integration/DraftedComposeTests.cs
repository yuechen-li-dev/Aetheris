using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

/// <summary>
/// <c>draft:</c> on a Compose: every operation's walls are exact drafted planes and right cones. Material narrows
/// upward from where it starts, a Pocket opens toward the pull, a hollow's cavity follows its exterior at
/// t / cos(draft), and anything that would need a curved meeting line, an overhang or a wall that changes shape
/// within a slab is refused.
/// </summary>
[Trait("Suite", "Profile")]
public sealed class DraftedComposeTests
{
    private static string Fixture(string name) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "Canonical", "Mold", name + ".firmament"));

    /// <summary>The part of a fixture before its Mold block.</summary>
    private static string Part(string name)
    {
        var text = File.ReadAllText(Fixture(name));
        return text[..text.IndexOf("\nMold ", StringComparison.Ordinal)];
    }

    private static FirmamentStepExportResult Build(string source)
    {
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics.Select(d => d.Message)));
        return built.Value;
    }

    private static double RoundedRect(double width, double depth, double radius, double erosion)
        => (width - 2 * erosion) * (depth - 2 * erosion) - (4 - double.Pi) * (radius - erosion) * (radius - erosion);

    private static double Simpson(Func<double, double> f, double a, double b, int n = 64)
    {
        var h = (b - a) / n; var sum = f(a) + f(b);
        for (var k = 1; k < n; k++) sum += (k % 2 == 1 ? 4 : 2) * f(a + k * h);
        return sum * h / 3;
    }

    /// <summary>Each non-horizontal face's draft: a plane's tilt from the axis, a cone's semi-angle.</summary>
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
            else if (surface.Cone is { } cone) drafts.Add((surface.Kind, cone.SemiAngleRadians * 180 / double.Pi));
            else if (surface.Cylinder is not null) drafts.Add((surface.Kind, 0d));
            else Assert.Fail($"unexpected side surface {surface.Kind}");
        }
        return drafts;
    }

    private static void AssertReimports(FirmamentStepExportResult result, double volume, double relative)
    {
        var imported = Step242Importer.ImportBody(result.StepText);
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics.Select(d => d.Message)));
        Assert.DoesNotContain(imported.Diagnostics, d => d.Message.StartsWith("step-orientation-source-mismatch", StringComparison.Ordinal));
        var mass = BrepMassProperties.Evaluate(imported.Value);
        Assert.True(mass.IsEnclosed && mass.IsOrientationConsistent && mass.SignedVolume > 0);
        Assert.True(double.Abs(mass.SignedVolume - volume) <= relative * volume, $"{mass.SignedVolume} vs {volume}");
    }

    [Fact]
    public void BaseBossAndPocket_DraftToExactPlanesAndCones()
    {
        var result = Build(Part("drafted-compose-block"));
        var drafts = SideDrafts(result);
        Assert.Equal(32, drafts.Count);
        Assert.Equal(16, drafts.Count(d => d.Kind == SurfaceGeometryKind.Cone));
        Assert.All(drafts, d => Assert.Equal(2d, d.DraftDegrees, 9));
        // The Base erodes from 0, the Boss from its seat at 12, the Pocket from its mouth at 12 downward.
        var t = double.Tan(2 * double.Pi / 180);
        var volume = Simpson(z => RoundedRect(80, 50, 6, z * t), 0, 12)
            - Simpson(z => RoundedRect(24, 18, 3, (12 - z) * t), 6, 12)
            + Simpson(z => RoundedRect(20, 20, 4, (z - 12) * t), 12, 20);
        AssertReimports(result, volume, 1e-4);
    }

    [Fact]
    public void OverlappingStraightBosses_MeetAlongTheLineOfTheirPlanes()
    {
        var result = Build("""
            Model T {
              units: mm
              Profile outline = Rect2 { center: [0mm, 0mm]; size: [80mm, 50mm] }
              Profile a = Rect2 { center: [-5mm, 0mm]; size: [20mm, 20mm] }
              Profile b = Rect2 { center: [8mm, 6mm]; size: [20mm, 20mm] }
              Compose body {
                draft: 1deg
                Base stock { profile: outline; from: 0mm; to: 10mm; role: Stock }
                Boss p { on: stock.Top; profile: a; height: 8mm }
                Boss q { on: stock.Top; profile: b; height: 8mm }
              }
            }
            """);
        Assert.All(SideDrafts(result), d => Assert.Equal(1d, d.DraftDegrees, 9));
        var t = double.Tan(double.Pi / 180);
        // Two squares of side s offset by (13, 6) overlap in (s − 13)(s − 6); each erodes by e = z·tan 1°.
        double Union(double e) { var s = 20 - 2 * e; return 2 * s * s - (s - 13) * (s - 6); }
        var volume = Simpson(z => (80 - 2 * z * t) * (50 - 2 * z * t), 0, 10) + Simpson(z => Union(z * t), 0, 8);
        AssertReimports(result, volume, 1e-9);
    }

    [Fact]
    public void AnUndraftedBossAndADraftedThroughPocket_KeepTheirOwnAngles()
    {
        var result = Build("""
            Model T {
              units: mm
              Profile outline = Rect2 { center: [0mm, 0mm]; size: [80mm, 50mm] }
              Profile a = Circle2 { center: [0mm, 0mm]; radius: 8mm }
              Profile w = Rect2 { center: [25mm, 0mm]; size: [10mm, 20mm] }
              Compose body {
                draft: 3deg
                Base stock { profile: outline; from: 0mm; to: 10mm; role: Stock }
                Boss p { on: Top; profile: a; height: 8mm; draft: 0deg }
                Pocket through { on: Top; profile: w; end: ThroughAll }
              }
            }
            """);
        var drafts = SideDrafts(result);
        Assert.Contains(drafts, d => d.Kind == SurfaceGeometryKind.Cylinder);
        Assert.All(drafts.Where(d => d.Kind != SurfaceGeometryKind.Cylinder), d => Assert.Equal(3d, d.DraftDegrees, 9));
        var t = double.Tan(3 * double.Pi / 180);
        // The through window is 10 × 20 at the top and narrows by 2·(10 − z)·tan 3° toward the bottom.
        var volume = Simpson(z => (80 - 2 * z * t) * (50 - 2 * z * t) - (10 - 2 * (10 - z) * t) * (20 - 2 * (10 - z) * t), 0, 10)
            + double.Pi * 64 * 8;
        AssertReimports(result, volume, 1e-3);
    }

    [Theory]
    [InlineData(1.5, "Bottom")]
    [InlineData(-1.5, "Top")]
    public void HollowWalls_AreParallelAndExactlyTheWallApart(double draft, string opening)
    {
        var result = Build($$"""
            Model Shell {
              units: mm
              Profile outline = RoundedRect2 { center: [0mm, 0mm]; size: [80mm, 50mm]; radius: 6mm }
              Compose<Hollow> body {
                wallThickness: 2mm
                openings: [{{opening}}]
                draft: {{draft}}deg
                Base stock { profile: outline; from: 0mm; to: 20mm; role: Stock }
              }
            }
            """);
        var body = result.RuntimeBody!;
        var planes = new List<(Vector3D Normal, double Offset)>();
        var cones = new List<double>();
        foreach (var face in body.Topology.Faces)
        {
            Assert.True(body.TryGetFaceSurfaceGeometry(face.Id, out var surface) && surface is not null);
            if (surface.Plane is { } plane && double.Abs(plane.Normal.ToVector().Z) < 1 - 1e-12)
            {
                var n = plane.Normal.ToVector();
                planes.Add((n, n.Dot(new Vector3D(plane.Origin.X, plane.Origin.Y, plane.Origin.Z))));
            }
            if (surface.Cone is { } cone) cones.Add(cone.SemiAngleRadians);
        }
        // The outer walls are split where the cavity ends; the inner walls are not.
        Assert.Equal(12, planes.Count);
        // Each outer plane has an inner plane with the same (or opposite) normal, 2 mm away along it.
        foreach (var (normal, offset) in planes)
            Assert.Contains(planes, other => (other.Normal - normal).Length < 1e-12 && double.Abs(double.Abs(other.Offset - offset) - 2) < 1e-9
                || (other.Normal + normal).Length < 1e-12 && double.Abs(double.Abs(other.Offset + offset) - 2) < 1e-9);
        Assert.Equal(12, cones.Count);
        Assert.All(cones, c => Assert.Equal(1.5 * double.Pi / 180, c, 12));
        var a = draft * double.Pi / 180; var t = double.Tan(a); var inset = 2 / double.Cos(a);
        var (low, high) = opening == "Bottom" ? (0d, 18d) : (2d, 20d);
        var volume = Simpson(z => RoundedRect(80, 50, 6, z * t), 0, 20) - Simpson(z => RoundedRect(80, 50, 6, inset + z * t), low, high);
        AssertReimports(result, volume, 1e-4);
    }

    [Theory]
    [InlineData("Profile a = Rect2 { center: [0mm, 0mm]; size: [20mm, 20mm] }", "Base stock { profile: outline; from: 0mm; to: 10mm; role: Stock }\n Hole<Countersink> h { on: +Z; center: [0mm, 0mm]; diameter: 6mm; countersinkDiameter: 12mm; countersinkAngle: 90deg; end: ThroughAll }", "compose-draft-unsupported")]
    [InlineData("Profile a = Circle2 { center: [-5mm, 0mm]; radius: 8mm }\n Profile b = Circle2 { center: [6mm, 0mm]; radius: 8mm }", "Base stock { profile: outline; from: 0mm; to: 10mm; role: Stock }\n Boss p { on: stock.Top; profile: a; height: 8mm }\n Boss q { on: stock.Top; profile: b; height: 8mm }", "compose-draft-curved-junction")]
    [InlineData("Profile w = Rect2 { center: [0mm, 0mm]; size: [79.5mm, 20mm] }", "Base stock { profile: outline; from: 0mm; to: 10mm; role: Stock }\n Pocket deep { on: Top; profile: w; depth: 8mm }", "compose-draft-walls-cross")]
    [InlineData("Profile a = Rect2 { center: [0mm, 0mm]; size: [20mm, 20mm] }", "Base stock { profile: outline; from: 0mm; to: 10mm; role: Stock }\n Boss p { on: Top; profile: a; height: 8mm }\n Boss q { on: p.Top; profile: a; height: 4mm }", "compose-draft-overhang")]
    [InlineData("Profile a = Rect2 { center: [0mm, 0mm]; size: [20mm, 20mm] }", "Base stock { profile: outline; from: 0mm; to: 10mm; role: Stock; draft: 50deg }", "draft-angle-invalid")]
    public void DraftsWithoutAnExactWall_AreRefused(string profiles, string operations, string code)
    {
        var built = FirmamentBuildAndExport.CompileSource($"Model T {{\n units: mm\n Profile outline = Rect2 {{ center: [0mm, 0mm]; size: [80mm, 50mm] }}\n {profiles}\n Compose body {{\n draft: 3deg\n {operations}\n }}\n}}");
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains(code, StringComparison.Ordinal));
    }

    [Fact]
    public void ADraftedHollowExtrude_IsTheDraftedHollowCompose()
    {
        var extruded = Build("""
            Model T {
              units: mm
              Profile outline = RoundedRect2 { center: [0mm, 0mm]; size: [80mm, 50mm]; radius: 6mm }
              Extrude<Hollow> body { profile: outline; from: 0mm; to: 20mm; draft: 1.5deg; wallThickness: 2mm; openings: [Bottom] }
            }
            """);
        Assert.All(SideDrafts(extruded), d => Assert.Equal(1.5, d.DraftDegrees, 9));
        var a = 1.5 * double.Pi / 180; var t = double.Tan(a); var inset = 2 / double.Cos(a);
        AssertReimports(extruded, Simpson(z => RoundedRect(80, 50, 6, z * t), 0, 20) - Simpson(z => RoundedRect(80, 50, 6, inset + z * t), 0, 18), 1e-4);
    }

    [Theory]
    [InlineData("Modify body { EdgeFinish<Chamfer> c { target: outline.Outer; on: Top; distance: 1mm } }")]
    [InlineData("Modify body { EdgeFinish<Fillet> f { where: p meets stock; radius: 1mm } }")]
    public void EdgeFinishesOnDraftedWalls_AreRefused(string modify)
    {
        var built = FirmamentBuildAndExport.CompileSource($$"""
            Model T {
              units: mm
              Profile outline = Rect2 { center: [0mm, 0mm]; size: [80mm, 50mm] }
              Profile a = Rect2 { center: [0mm, 0mm]; size: [20mm, 20mm] }
              Compose body {
                draft: 2deg
                Base stock { profile: outline; from: 0mm; to: 10mm; role: Stock }
                Boss p { on: Top; profile: a; height: 8mm }
              }
              {{modify}}
            }
            """);
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains("compose-draft-unsupported", StringComparison.Ordinal));
    }

    [Fact]
    public void HolesCounterboresAndSlots_AreDraftedFromTheirMouths()
    {
        var result = Build("""
            Model Holes {
              units: mm
              Profile outline = RoundedRect2 { center: [0mm, 0mm]; size: [80mm, 50mm]; radius: 6mm }
              Compose body {
                draft: 1deg
                Base stock { profile: outline; from: 0mm; to: 10mm; role: Stock }
                Hole<Shaft> h1 { on: +Z; center: [-25mm, 0mm]; diameter: 6mm; end: ThroughAll }
                Hole<Counterbore> h2 { on: +Z; center: [0mm, 0mm]; diameter: 5mm; counterboreDiameter: 10mm; counterboreDepth: 4mm; end: ThroughAll }
                Slot<Capsule> s { center: [25mm, 0mm]; direction: [0, 1]; length: 20mm; width: 6mm; extent: ThroughAll }
              }
            }
            """);
        Assert.All(SideDrafts(result), d => Assert.Equal(1d, d.DraftDegrees, 9));
        var t = double.Tan(double.Pi / 180);
        double Disk(double r) => double.Pi * r * r;
        // Each removal is its size at its mouth: the shaft at 10, the bore at 10, its shaft at the bore's floor (6),
        // the slot at 10; eroding a capsule keeps its straight length.
        double Area(double z)
        {
            var e = (10 - z) * t;
            var counterbore = z >= 6 ? Disk(5 - e) : Disk(2.5 - (6 - z) * t);
            return RoundedRect(80, 50, 6, z * t) - Disk(3 - e) - counterbore - ((6 - 2 * e) * 14 + Disk(3 - e));
        }
        AssertReimports(result, Simpson(Area, 0, 6) + Simpson(Area, 6, 10), 1e-4);
    }

    [Fact]
    public void AHollowHousingWithABossAndAPocket_KeepsEveryWallParallel()
    {
        var result = Build(Part("drafted-housing"));
        var a = 1.5 * double.Pi / 180; var t = double.Tan(a); var i = 2 / double.Cos(a);
        Assert.All(SideDrafts(result), d => Assert.Equal(1.5, d.DraftDegrees, 9));
        // The exterior, less the Base's cavity (with the pocket's liner left solid under it) and the boss's cavity.
        var exterior = Simpson(z => RoundedRect(100, 60, 8, z * t), 0, 25) + Simpson(z => RoundedRect(24, 24, 4, (z - 25) * t), 25, 31)
            - Simpson(z => RoundedRect(30, 20, 4, (25 - z) * t), 20, 25);
        var cavity = Simpson(z => RoundedRect(100, 60, 8, i + z * t), 0, 23) - Simpson(z => RoundedRect(30, 20, 4, -i + (25 - z) * t), 18, 23)
            + Simpson(z => RoundedRect(24, 24, 4, i + (z - 25) * t), 23, 29);
        AssertReimports(result, exterior - cavity, 1e-4);
    }

    [Theory]
    [InlineData("Pocket p { on: Top; profile: well; depth: 9mm }", "hollow-pocket-too-deep")]
    [InlineData("Pocket p { on: Top; profile: edge; depth: 3mm }", "hollow-feature-too-close")]
    [InlineData("Boss b { on: Top; profile: well; height: 5mm }\n Pocket p { on: Top; profile: edge; depth: 3mm }", "hollow-feature-too-close")]
    public void HollowFeaturesWithoutAnEvenWall_AreRefused(string feature, string code)
    {
        var built = FirmamentBuildAndExport.CompileSource($$"""
            Model T {
              units: mm
              Profile outline = Rect2 { center: [0mm, 0mm]; size: [80mm, 50mm] }
              Profile well = Rect2 { center: [0mm, 0mm]; size: [20mm, 20mm] }
              Profile edge = Rect2 { center: [28mm, 0mm]; size: [20mm, 20mm] }
              Compose<Hollow> body {
                wallThickness: 2mm
                openings: [Bottom]
                draft: 1deg
                Base stock { profile: outline; from: 0mm; to: 10mm; role: Stock }
                {{feature}}
              }
            }
            """);
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains(code, StringComparison.Ordinal));
    }

    [Fact]
    public void ASharpCornerOnAnArc_HasNoStraightDraftedEdge()
    {
        // A D: a line closed by a semicircle, meeting it at two sharp corners.
        LineArcProfileCurve2D[] curves = [new LineArcLineSegment2D((0, -10), (0, 10)), new LineArcCircularArc2D((0, 0), 10, double.Pi / 2, double.Pi)];
        Assert.False(ProfileDraftOffset.TryOffset([new LineArcProfileLoop2D(curves, false)], 0.1, out _, out var error));
        Assert.StartsWith("draft-offset-corner-curved", error);
    }
}
