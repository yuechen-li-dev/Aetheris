using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Brep.Verification;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class EquationCurveTests
{
    private static string Source(string name) => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/EquationCurves/" + name + ".firmament"));

    [Fact]
    public void ApproximatePlanarLawExportsItsExplicitRepresentationWithBoundedError()
    {
        var parsed = FirmamentV2Parser.Parse(Source("sine-arch"));
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var curve = Assert.Single(parsed.Document!.StaticAuthoring!.EquationCurves!, c => c.Name == "Layout.Crown");
        Assert.InRange(curve.ErrorBound, double.Epsilon, .05);
        Assert.True(FirmamentBuildAndExport.CompileSource(Source("sine-arch")).IsSuccess);
        foreach (var segment in curve.Segments)
            for (var i = 0; i <= 10; i++)
            {
                var u = i / 10d; var t = segment.Start + (segment.End - segment.Start) * u;
                var y = segment.Controls[0][1] * (1 - u) + segment.Controls[1][1] * u;
                Assert.InRange(Math.Abs(20 * Math.Sin(t * Math.PI) - y), 0, curve.ErrorBound);
            }
    }

    [Fact]
    public void InvalidTrigCaptureAndToleranceInputsRemainRejected()
    {
        var length = Source("arch").Replace("span * t", "span * t * Cos(1mm)");
        var captured = "Static Captured: Length = 2mm;\n" + Source("arch").Replace("span * t", "Captured * t");
        var tolerated = Source("record-arch").Replace("span: 80mm; rise: 20mm", "span: 80mm tol 0.1mm; rise: 20mm");
        Assert.False(FirmamentV2Parser.Parse(length).IsSuccess);
        Assert.False(FirmamentV2Parser.Parse(captured).IsSuccess);
        Assert.False(FirmamentV2Parser.Parse(tolerated).IsSuccess);
    }

    [Theory]
    [InlineData("Evaluation { point: Point2(span * t, 4 * rise * t * (1 - t)); typo: 1 }.point")]
    [InlineData("Evaluation { point: Point2(span * t, 4 * rise * t * (1 - t)); point: Point2(0mm,0mm) }.point")]
    [InlineData("Point2(Point2(span * t, 1mm, 2mm).X, 4 * rise * t * (1 - t))")]
    [InlineData("Point2(Point2(span * t, 1mm).XY, 4 * rise * t * (1 - t))")]
    public void MemberProjectionCannotEraseUnknownFieldsOrMalformedPointArguments(string expression)
    {
        var source = "Record Evaluation { point: Point2 }\n" + Source("arch").Replace("Point2(span * t, 4 * rise * t * (1 - t))", expression);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        Assert.Contains(parsed.SyntaxDiagnostics, d => d.Code.Contains("function-body-invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void ScalarFunctionNormalizationCannotEraseManufacturingTolerance()
    {
        const string source = "Function Identity(x: Length) -> Length = x; Model Test { units: mm; Static D: Length = 10mm tol 0.1mm; Box Base { size: [Identity(D),20mm,5mm] } }";
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        Assert.Contains(parsed.SyntaxDiagnostics, d => d.Message.Contains("tolerance", StringComparison.Ordinal));
    }

    [Fact]
    public void RecordInputsPreserveExactGeometryAndLexicalParameterBinding()
    {
        var baseline = FirmamentV2Parser.Parse(Source("arch"));
        var parsed = FirmamentV2Parser.Parse(Source("record-arch"));
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.True(FirmamentBuildAndExport.CompileSource(Source("record-arch")).IsSuccess);
        Assert.Equal(baseline.Document!.StaticAuthoring!.EquationCurves![0].Segments[0].Controls.SelectMany(x => x), parsed.Document!.StaticAuthoring!.EquationCurves![0].Segments[0].Controls.SelectMany(x => x));
        var shadowed = FirmamentV2Parser.Parse("Static t: Float = 999;\n" + Source("record-arch"));
        Assert.True(shadowed.IsSuccess, string.Join("\n", shadowed.Diagnostics));
        Assert.Equal(parsed.Document.StaticAuthoring.EquationCurves[0].Segments[0].Controls.SelectMany(x => x), shadowed.Document!.StaticAuthoring!.EquationCurves![0].Segments[0].Controls.SelectMany(x => x));
        foreach (var unrelated in new[] { "Static t: Length = 999mm tol 0.1mm;", "Static t: ArchSpec { span: 999mm; rise: 999mm }" })
        {
            var actual = FirmamentV2Parser.Parse(unrelated + "\n" + Source("record-arch"));
            Assert.True(actual.IsSuccess, string.Join("\n", actual.Diagnostics));
            Assert.Equal(parsed.Document.StaticAuthoring.EquationCurves[0].Segments[0].Controls.SelectMany(x => x), actual.Document!.StaticAuthoring!.EquationCurves![0].Segments[0].Controls.SelectMany(x => x));
        }
    }

    [Fact]
    public void CircleRecognitionPreservesAnalyticGeometryAndPeriodicity()
    {
        var parsed = FirmamentV2Parser.Parse(Source("circle"));
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var curve = Assert.Single(parsed.Document!.StaticAuthoring!.EquationCurves!);
        Assert.Equal("Circle", curve.GeometryKind);
        Assert.True(curve.Periodic);
        Assert.True(FirmamentBuildAndExport.CompileSource(Source("circle")).IsSuccess);
    }

    [Fact]
    public void EllipseRequiresExplicitLoopConsumptionAndNeverCrashesTheFrontend()
    {
        var source = Source("circle").Replace("radius * Sin(t * 360deg)", "2 * radius * Sin(t * 360deg)");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.Equal("Ellipse", parsed.Document!.StaticAuthoring!.EquationCurves![0].GeometryKind);
        Assert.True(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        var invalid = FirmamentV2Parser.Parse(source.Replace("|> TraceLoop", "|> Close"));
        Assert.False(invalid.IsSuccess);
        Assert.Contains(invalid.SyntaxDiagnostics, d => d.Code.EndsWith("curve-consumer-invalid", StringComparison.Ordinal) && d.Message.Contains("TraceLoop", StringComparison.Ordinal));
    }

    [Fact]
    public void AlmostFullTurnIsNotSilentlyPromotedToAnExactClosedConic()
    {
        var source = Source("circle").Replace("domain: [0, 1]", "domain: [0, 0.99999999999999]").Replace("periodic: true", "periodic: false");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.SyntaxDiagnostics, d => d.Message.Contains("Exact lowering", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Layout.Coil.At(n)", "Layout.Coil.At(n + 5)")]
    [InlineData("maxDeviation: 0.01mm", "maxDeviation: 0mm")]
    [InlineData("on: World", "on: XZ")]
    [InlineData("representation: Approximate", "representation: Approximate; periodic: true")]
    public void SpatialCurvesRejectOutOfDomainQueriesAndUnprovenContracts(string from, string to)
    {
        var source = Source("helix").Replace(from, to);
        Assert.False(FirmamentV2Parser.Parse(source).IsSuccess);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void TypedRecordResultBundlesCanFeedCurvesAndDirectGeometryValues()
    {
        var source = Source("arch").Replace("Function Arch", "Record Evaluation { point: Point2; width: Length }\nFunction Bundle(t: Float, span: Length, rise: Length) -> Evaluation = Evaluation { point: Arch(t, span, rise); width: span };\nFunction Arch")
            .Replace("from: Arch(t, 80mm, 20mm)", "from: Bundle(t, 80mm, 20mm).point");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.True(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        var direct = Source("helix").Replace("Layout.Stations.Turn1.Z", "Layout.Coil.At(1).Z");
        Assert.True(FirmamentBuildAndExport.CompileSource(direct).IsSuccess);
    }

    [Theory]
    [InlineData("XY")]
    [InlineData("XZ")]
    public void ExactArchBuildsThroughNormalProfilePathAndRetainsFrame(string frame)
    {
        var source = Source("arch").Replace("on: XY", "on: " + frame);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var curve = Assert.Single(parsed.Document!.StaticAuthoring!.EquationCurves!, c => c.Name.EndsWith(".Crown", StringComparison.Ordinal));
        Assert.Equal("CubicBezier", curve.GeometryKind);
        Assert.Equal(frame, curve.Frame);
        Assert.Equal(0, curve.ErrorBound);
        Assert.NotNull(curve.SourceSpan);
        var compiled = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics));
        var imported = Step242Importer.ImportBody(compiled.Value!.StepText);
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics));
        var mass = BrepMassProperties.Evaluate(imported.Value!);
        Assert.True(mass.IsEnclosed);
        Assert.True(mass.IsOrientationConsistent);
        var crowns = imported.Value!.Geometry.Curves.Select(pair => pair.Value.BSpline3).Where(c => c.HasValue).Select(c => c!.Value).ToArray();
        Assert.Equal(2, crowns.Length);
        var midpoints = crowns.Select(c => c.Evaluate((c.DomainStart + c.DomainEnd) / 2)).ToArray();
        Assert.All(midpoints, p => Assert.Equal(40, p.X, 8));
        if (frame == "XY")
        {
            Assert.All(midpoints, p => Assert.Equal(20, p.Y, 8));
            Assert.Equal(new[] { 0d, 5d }, midpoints.Select(p => p.Z).Order());
        }
        else
        {
            Assert.All(midpoints, p => Assert.Equal(20, p.Z, 8));
            Assert.Equal(new[] { -5d, 0d }, midpoints.Select(p => p.Y).Order());
        }
        Assert.True(FirmamentV2Parser.TryExpandCanonicalStaticAuthoring(source, out var expanded, out var errors), string.Join("\n", errors));
        var profile = ProfileAuthoringParser.Parse(expanded);
        Assert.Empty(profile.Diagnostics);
        Assert.Equal(80 * 20 * 2d / 3, ResolvedProfile2DValidator.Validate(profile.Profile!).SignedArea, 7);
        var plane = profile.Profile!.EffectiveConstructionPlane;
        var world = plane.ToWorld((0, 20));
        Assert.Equal(frame == "XZ" ? 20 : 0, world.Z, 8);
        Assert.Equal(frame == "XY" ? 20 : 0, world.Y, 8);
    }

    [Fact]
    public void SpatialEquationDrivesKeyedPhysicalDimensionsWithoutPretendingToSweep()
    {
        var source = Source("helix");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var curve = Assert.Single(parsed.Document!.StaticAuthoring!.EquationCurves!);
        Assert.Equal("Polyline", curve.GeometryKind);
        Assert.InRange(curve.ErrorBound, double.Epsilon, .01);
        Assert.InRange(curve.Segments.Count, 2, 4096);
        var points = Assert.Single(parsed.Document.StaticAuthoring.ConceptPoints!);
        Assert.Equal(new[] { "Turn0", "Turn1", "Turn2" }, points.Points.Select(p => p.Key));
        Assert.Equal(12, points.Points[1].Coordinates[0], 8);
        Assert.Equal(8, points.Points[1].Coordinates[2], 8);
        Assert.True(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Theory]
    [InlineData("domain: [0, 1]", "domain: [1, 0]")]
    [InlineData("representation: Exact", "representation: Approximate")]
    [InlineData("representation: Exact", "representation: Exact; maxDeviation: 0.1mm")]
    [InlineData("on: XY", "on: Missing")]
    [InlineData("on: XY", "on: XY; heigth: 1mm")]
    [InlineData("representation: Exact", "representation: Exact; closed: true")]
    [InlineData("span * t", "span / (t - 0.5)")]
    public void InvalidIntentCannotBeAcceptedOrSilentlyReplaced(string from, string to)
    {
        var source = Source("arch").Replace(from, to);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        Assert.Contains(parsed.SyntaxDiagnostics, d => d.Message.Length > 15 && d.SourceSpan is not null);
    }

    [Theory]
    [InlineData("Cos(0)", 1)]
    [InlineData("Cos(180deg)", -1)]
    [InlineData("Cos(3.141592653589793)", -1)]
    [InlineData("Cos(3.141592653589793rad)", -1)]
    [InlineData("Sin(90deg)", 1)]
    public void TrigonometryDistinguishesBareRadiansFromDegrees(string expression, double expected)
    {
        var source = Source("arch").Replace("span * t", $"span * t * ({expression})");
        source = source[..source.IndexOf("    Profile Outline", StringComparison.Ordinal)] + "Box Base { size: [10mm,10mm,10mm] } }";
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var curve = Assert.Single(parsed.Document!.StaticAuthoring!.EquationCurves!, c => c.Name.EndsWith(".Crown", StringComparison.Ordinal));
        Assert.Equal(80 * expected, curve.Segments[0].Controls[^1][0], 8);
    }
}
