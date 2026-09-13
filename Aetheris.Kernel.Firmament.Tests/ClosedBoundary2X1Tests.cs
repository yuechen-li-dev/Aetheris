using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ClosedBoundary2X1Tests
{
    [Fact]
    public void TemplateLocalBoundariesAreLoweredOnlyAfterSpecialization()
    {
        var source = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Templates/local-closed-boundaries.firmament"));
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var boundary = Assert.Single(parsed.Document!.Boundaries!);
        Assert.Equal(20, boundary.Dimensions["Radius"]);
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join("\n", build.Diagnostics.Select(d => d.Message)));
        var revised = FirmamentBuildAndExport.CompileSource(source.Replace("Disc<R: 20mm>", "Disc<R: 24mm>", StringComparison.Ordinal));
        Assert.True(revised.IsSuccess, string.Join("\n", revised.Diagnostics.Select(d => d.Message)));
        Assert.NotEqual(build.Value.StepText, revised.Value.StepText);
    }

    public static TheoryData<string, string, int> LinearFamilies => new()
    {
        { "Rect2 Shape { Center: [0mm,0mm] Size: [40mm,20mm] }", "Rect2", 4 },
        { "Square2 Shape { Center: [0mm,0mm] Size: 20mm }", "Square2", 4 },
        { "Triangle2<Equilateral> Shape { Center: [0mm,0mm] Side: 20mm }", "Triangle2", 3 },
        { "Triangle2<Isosceles> Shape { Center: [0mm,0mm] Base: 20mm Height: 15mm }", "Triangle2", 3 },
        { "Triangle2<Right> Shape { Origin: [0mm,0mm] Legs: [20mm,15mm] }", "Triangle2", 3 },
        { "Triangle2<Explicit> Shape { A: [0mm,0mm] B: [20mm,0mm] C: [0mm,15mm] }", "Triangle2", 3 },
        { "RegularPolygon2<6> Shape { Center: [0mm,0mm] AcrossFlats: 32mm Rotation: 30deg }", "RegularPolygon2", 6 },
        { "Polygon2<Parallelogram> Shape { Center: [0mm,0mm] Base: 30mm Height: 20mm Shear: 8mm }", "Polygon2", 4 },
        { "Polygon2<Trapezoid> Shape { Center: [0mm,0mm] BottomWidth: 30mm TopWidth: 20mm Height: 15mm }", "Polygon2", 4 },
    };

    [Theory]
    [MemberData(nameof(LinearFamilies))]
    public void LinearFamilies_BindThroughTraceLoopWithStableMetadata(string shape, string type, int segments)
    {
        var source = Source(shape);
        var parse = FirmamentV2Parser.Parse(source);
        var profile = ProfileAuthoringParser.ResolveNamedProfile(source, "Boundary", out var diagnostics);

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var boundary = Assert.Single(parse.Document!.Boundaries!);
        Assert.Equal("ClosedBoundary2", boundary.Capability);
        Assert.Equal(type, boundary.ShapeType);
        Assert.True(boundary.Area > 0);
        Assert.True(boundary.Perimeter > 0);
        Assert.NotEmpty(boundary.GeneratedEdges);
        Assert.NotNull(profile);
        Assert.Empty(diagnostics);
        Assert.Equal(segments, profile.Loops.Single().Segments.Count);
    }

    [Theory]
    [InlineData("Slot2 Shape { Center: [0mm,0mm] Length: 50mm Width: 12mm }", 4)]
    [InlineData("RoundedRect2 Shape { Center: [0mm,0mm] Size: [50mm,30mm] Radius: 6mm }", 8)]
    public void LineArcFamilies_PreserveExactCircularSpans(string shape, int segments)
    {
        var source = Source(shape); var profile = ProfileAuthoringParser.ResolveNamedProfile(source, "Boundary", out var diagnostics);
        Assert.NotNull(profile); Assert.Empty(diagnostics); Assert.Equal(segments, profile.Loops.Single().Segments.Count);
        Assert.Contains(profile.Loops.Single().Segments, x => x.Geometry is LineArcCircularArc2D);
        var export = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(export.IsSuccess, string.Join(Environment.NewLine, export.Diagnostics.Select(x => x.Message)));
        Assert.Contains("CIRCLE", export.Value.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void CircleAndEllipse_AreSingleAnalyticBoundariesAndExportAnalytically()
    {
        var circleSource = Source("Circle2 Shape { Center: [0mm,0mm] Diameter: 20mm }");
        var ellipseSource = Source("Ellipse2 Shape { Center: [0mm,0mm] AxisLengths: [40mm,20mm] Rotation: 15deg }");
        var circle = ProfileAuthoringParser.ResolveNamedProfile(circleSource, "Boundary", out var circleDiagnostics);
        var ellipse = ProfileAuthoringParser.ResolveNamedProfile(ellipseSource, "Boundary", out var ellipseDiagnostics);
        Assert.Empty(circleDiagnostics); Assert.Empty(ellipseDiagnostics);
        Assert.IsType<LineArcFullCircle2D>(Assert.Single(circle!.Loops.Single().Segments).Geometry);
        Assert.IsType<LineArcFullEllipse2D>(Assert.Single(ellipse!.Loops.Single().Segments).Geometry);
        var circleExport = FirmamentBuildAndExport.CompileSource(circleSource);
        var ellipseExport = FirmamentBuildAndExport.CompileSource(ellipseSource);
        Assert.True(circleExport.IsSuccess, string.Join(Environment.NewLine, circleExport.Diagnostics.Select(x => x.Message)));
        Assert.True(ellipseExport.IsSuccess, string.Join(Environment.NewLine, ellipseExport.Diagnostics.Select(x => x.Message)));
        Assert.Contains("CIRCLE", circleExport.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("ELLIPSE", ellipseExport.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("SURFACE_OF_LINEAR_EXTRUSION", ellipseExport.Value.StepText, StringComparison.Ordinal);
        var reimport = Step242Importer.ImportBody(ellipseExport.Value.StepText);
        Assert.True(reimport.IsSuccess, string.Join(Environment.NewLine, reimport.Diagnostics.Select(x => x.Message)));
        Assert.Contains(reimport.Value.Geometry.Curves, x => x.Value.Kind == CurveGeometryKind.Ellipse3);
    }

    [Fact]
    public void ExplicitPolygon_PreservesSetNamesAndRejectsSelfIntersection()
    {
        const string set = "Static Vertices: Set<Point2> { A => Point2(0mm,0mm) B => Point2(30mm,0mm) Nose => Point2(35mm,12mm) C => Point2(20mm,25mm) D => Point2(0mm,18mm) }";
        var source = Source(set + " Polygon2<Explicit> Shape { Vertices: Vertices }");
        var parse = FirmamentV2Parser.Parse(source); var boundary = Assert.Single(parse.Document!.Boundaries!);
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        Assert.Equal(["A", "B", "Nose", "C", "D"], boundary.GeneratedPoints);
        Assert.Equal(["A_B", "B_Nose", "Nose_C", "C_D", "D_A"], boundary.GeneratedEdges);

        var invalid = FirmamentV2Parser.Parse(Source("Static V: Set<Point2> { A => Point2(0mm,0mm) B => Point2(10mm,10mm) C => Point2(0mm,10mm) D => Point2(10mm,0mm) } Polygon2<Explicit> Shape { Vertices: V }"));
        Assert.False(invalid.IsSuccess);
        Assert.Contains("firmament-boundary2-explicit-self-intersection:Shape", invalid.Diagnostics);
    }

    [Fact]
    public void ThreeFamilies_AuthorPlaneSpanBoundariesWithoutShapeSpecificBackend()
    {
        var source = """
            Concept Struct Supports { Top: Plane { Origin: [0mm,0mm,0mm] Normal: [0,0,1] Up: [0,1,0] } }
            Construction Plane TopSupport { Trace: Supports.Top }
            RoundedRect2 Rounded { Center: [0mm,0mm] Size: [60mm,40mm] Radius: 8mm }
            Circle2 Disk { Center: [0mm,0mm] Diameter: 30mm }
            RegularPolygon2<6> Hex { Center: [0mm,0mm] AcrossFlats: 32mm }
            Profile RoundedProfile { Loop Outer { Rounded |> TraceLoop } }
            Profile DiskProfile { Loop Outer { Disk |> TraceLoop } }
            Profile HexProfile { Loop Outer { Hex |> TraceLoop } }
            Span<Plane> RoundedArea { On: TopSupport Boundary: RoundedProfile }
            Span<Plane> DiskArea { On: TopSupport Boundary: DiskProfile }
            Span<Plane> HexArea { On: TopSupport Boundary: HexProfile }
            """;
        var inspection = ProfileAuthoringParser.InspectGeometricSpans(source);
        Assert.Empty(inspection.Diagnostics);
        Assert.Equal(["RoundedProfile", "DiskProfile", "HexProfile"], inspection.Spans.Select(x => x.BoundaryProfile));
    }

    [Fact]
    public void RoundedRectanglePlaneSpan_UsesCurvedCornerForPatternContainment()
    {
        var valid = FirmamentBuildAndExport.CompileSource(RoundedPattern((35, 17)));
        var invalid = FirmamentBuildAndExport.CompileSource(RoundedPattern((39, 21)));
        Assert.True(valid.IsSuccess, string.Join(Environment.NewLine, valid.Diagnostics.Select(x => x.Message)));
        Assert.False(invalid.IsSuccess);
        Assert.Contains(invalid.Diagnostics, x => x.Message.StartsWith("firmament-feature-footprint-outside-span:Mounts.NearCorner:MountingArea", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Rect2 Shape { Center: [0mm,0mm] Size: [-1mm,2mm] }", "firmament-boundary2-invalid-dimensions:Shape")]
    [InlineData("Square2 Shape { Center: [0mm,0mm] Size: 0mm }", "firmament-boundary2-invalid-dimensions:Shape")]
    [InlineData("Slot2 Shape { Center: [0mm,0mm] Length: 10mm Width: 12mm }", "firmament-boundary2-invalid-dimensions:Shape")]
    [InlineData("RoundedRect2 Shape { Center: [0mm,0mm] Size: [20mm,10mm] Radius: 6mm }", "firmament-boundary2-invalid-dimensions:Shape")]
    [InlineData("RegularPolygon2<2> Shape { Center: [0mm,0mm] Circumradius: 10mm }", "firmament-boundary2-regular-count-out-of-range:Shape")]
    public void InvalidFamilies_ReportTypedDiagnostics(string shape, string diagnostic)
    {
        var parse = FirmamentV2Parser.Parse(Source(shape));
        Assert.False(parse.IsSuccess); Assert.Contains(diagnostic, parse.Diagnostics);
    }

    [Theory]
    [InlineData("rect-negative-size.firmament")]
    [InlineData("square-zero-size.firmament")]
    [InlineData("triangle-degenerate.firmament")]
    [InlineData("slot-impossible.firmament")]
    [InlineData("rounded-radius-too-large.firmament")]
    [InlineData("explicit-too-few.firmament")]
    [InlineData("explicit-duplicate.firmament")]
    [InlineData("explicit-self-intersection.firmament")]
    [InlineData("regular-count-too-small.firmament")]
    [InlineData("regular-invalid-size.firmament")]
    public void InvalidFixtures_AreRecognizedAndRejected(string file)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Invalid/Boundary2", file));
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(path));
        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.Diagnostics, x => x.StartsWith("firmament-boundary2-", StringComparison.Ordinal));
    }

    private static string Source(string declarations) => $$"""
        Model BoundaryFamily {
            Units: mm
            {{declarations}}
            Profile Boundary { Loop Outer { Shape |> TraceLoop } }
            Struct Part { Extrude Solid { Profile: Boundary From: 0mm To: 5mm } }
        }
        """;

    private static string RoundedPattern((double X, double Y) nearCorner) => $$"""
        Model RoundedPattern {
            Units: mm
            Feature Drill(Center: Point2) -> Hole<Shaft> { return Hole<Shaft> { On: MountingArea Center: Center Diameter: 2mm End: ThroughAll } }
            Static Points: Set<Point2> { Center => Point2(0mm,0mm) NearCorner => Point2({{nearCorner.X}}mm,{{nearCorner.Y}}mm) }
            Concept Struct Supports { Top: Plane { Origin: [0mm,0mm,5mm] Normal: [0,0,1] Up: [0,1,0] } }
            Construction Plane TopSupport { Trace: Supports.Top }
            Rect2 Stock { Center: [0mm,0mm] Size: [100mm,70mm] }
            RoundedRect2 Region { Center: [0mm,0mm] Size: [80mm,44mm] Radius: 8mm }
            Profile StockProfile { Loop Outer { Stock |> TraceLoop } }
            Profile RegionProfile { Loop Outer { Region |> TraceLoop } }
            Span<Plane> MountingArea { On: TopSupport Boundary: RegionProfile }
            Struct Part { Compose Plate { Base StockBody { Profile: StockProfile From: 0mm To: 5mm Role: Stock } Pattern Mounts Over Points { point => Drill(Center: point) } } }
        }
        """;
}
