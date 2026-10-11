using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Garment;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Cloth3D;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class GarmentCodeImportTests
{
    [Fact]
    public void KnownDressMeshHasNoSliversAndRetainsTheAnalyticCutAndSeamReferences()
    {
        var imported = GarmentCodeImport.Import(Fixture("dress.json"));
        var garment = GarmentCompiler.Compile(imported.Source!).Garment!;
        var quality = GarmentMeshInspection.Measure(garment);
        Assert.Equal(611, garment.Cloth.Definition.Positions.Length);
        Assert.Equal(992, garment.Cloth.Definition.Indices.Length / 3);
        Assert.Equal(0, quality.UnreferencedVertices);
        Assert.Equal(0, quality.TrianglesBelowTenDegrees);
        Assert.InRange(quality.MinimumTriangleAngleDegrees, 10, 11);
        Assert.InRange(quality.MinimumEdgeLength, .006, .007);
        Assert.InRange(quality.MaximumLumpedAreaRatio, 25, 27);
        Assert.InRange(quality.MaximumEdgeLength, 0, garment.Source.MeshSize * 1.001);
        foreach (var panel in garment.Source.Panels)
        {
            var mesh = garment.Panels.Single(candidate => candidate.Name == panel.Name);
            double area = 0;
            for (int face = mesh.IndexStart; face < mesh.IndexStart + mesh.IndexCount; face += 3)
            {
                var a = garment.Cloth.Definition.Positions[garment.Cloth.Definition.Indices[face]];
                var b = garment.Cloth.Definition.Positions[garment.Cloth.Definition.Indices[face + 1]];
                var c = garment.Cloth.Definition.Positions[garment.Cloth.Definition.Indices[face + 2]];
                Assert.True(Vector3.Cross(b - a, c - a).Z > 0);
                area += Vector3.Cross(b - a, c - a).Length() * .5;
            }
            double analyticArea = ResolvedProfile2DValidator.Validate(panel.Profile).SignedArea;
            // Curved edges are approximated with <=1mm geometric error, never replaced by a new cut.
            Assert.InRange(Math.Abs(area - analyticArea * 1e-6), 0, .00025);
            Assert.All(mesh.Edges.Values, boundary => Assert.True(boundary.Length >= 2));
        }
        Assert.Equal(6, GarmentPatternInspection.Inspect(garment.Source).Count);
    }

    [Fact]
    public void StaticReferenceBodyUsesSharedSewingAndReplayWithoutInventingARig()
    {
        const string source = """
            schema Garment
            Rect2 blank { center: [0mm, 0mm]; size: [100mm, 100mm] }
            Profile cut { from: blank; }
            Garment test {
              Fabric cotton { thickness: 2mm; arealDensity: 0.2; }
              Panel sheet { profile: cut; origin: [0mm, 0mm, 1000mm]; }
            }
            """;
        var garment = GarmentCompiler.Compile(source).Garment!;
        var drape = GarmentDraping.SettleOnSurface(garment, new GroundPlane(), "reference", "plane", 1);
        Assert.True(drape.ReplayExact);
        Assert.True(drape.Metrics.Finite);
        Assert.Equal("reference", drape.BodyId);
        Assert.Empty(drape.BodyPositions);
        Assert.Empty(drape.Frames);
        var bound = GarmentCompiler.Compile(source.Replace("Panel sheet", "Drape { figure: Anatolia; pose: Rest; } Panel sheet")).Garment!;
        var rejected = Assert.Throws<InvalidOperationException>(() =>
            GarmentDraping.SettleOnSurface(bound, new GroundPlane(), "reference", "plane", 1));
        Assert.StartsWith("garment-reference-surface-static", rejected.Message);
    }

    [Fact]
    public void KnownDressTemplateChangesItsHemWithoutGuessingNewArmholesOrNecklines()
    {
        string path = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(FixturePath("dress.json")))!, "garmentcode-dress-template.firmament");
        string source = File.ReadAllText(path);
        var baseline = GarmentCompiler.Compile(source);
        Assert.True(baseline.IsSuccess, string.Join("; ", baseline.Diagnostics));
        var upstream = GarmentCompiler.Compile(GarmentCodeImport.Import(Fixture("dress.json")).Source!);
        Assert.Equal(upstream.Garment!.Cloth.ContentKey, baseline.Garment!.Cloth.ContentKey);
        string modified = source.Replace("Pattern();", "Pattern(hemDrop: 100mm);");
        var longer = GarmentCompiler.Compile(modified);
        Assert.True(longer.IsSuccess, string.Join("; ", longer.Diagnostics));
        Assert.NotEqual(baseline.Garment.Cloth.ContentKey, longer.Garment!.Cloth.ContentKey);
        foreach (var panel in baseline.Garment.Source.Panels)
        {
            var original = GarmentPatternInspection.Boundaries(panel);
            var changedPanel = longer.Garment.Source.Panels.Single(candidate => candidate.Name == panel.Name);
            var changed = GarmentPatternInspection.Boundaries(changedPanel);
            foreach (string name in original.Keys.Where(name => name.Contains("armhole") || name.Contains("collar")))
            {
                Assert.Equal(original[name].Segments.Single().Geometry, changed[name].Segments.Single().Geometry);
            }
            Assert.Equal(panel.Origin, changedPanel.Origin);
        }
        Assert.Equal(baseline.Garment.Source.Stitches.Select(seam => (seam.A, seam.B, seam.Reversed)),
            longer.Garment.Source.Stitches.Select(seam => (seam.A, seam.B, seam.Reversed)));
        var referenceSeams = GarmentPatternInspection.Inspect(baseline.Garment.Source);
        foreach (var seam in GarmentPatternInspection.Inspect(longer.Garment.Source))
        {
            var reference = referenceSeams.Single(item => item.Name == seam.Name);
            Assert.InRange(Math.Abs((seam.LengthAMm - seam.LengthBMm)
                - (reference.LengthAMm - reference.LengthBMm)), 0, .00001);
        }
    }

    [Theory]
    [InlineData("dress", 4, 6)]
    [InlineData("shirt", 8, 16)]
    public void RealUpstreamPatternsMatchItsIndependentCurveReaderAndBuild(string name, int panels, int seams)
    {
        string json = Fixture(name + ".json");
        var imported = GarmentCodeImport.Import(json);
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics));
        Assert.Equal(panels, imported.Panels.Count);
        var parsed = GarmentAuthoring.Parse(imported.Source!);
        Assert.True(parsed.IsSuccess, string.Join("; ", parsed.Diagnostics));
        Assert.Equal(seams, parsed.Source!.Stitches.Length);
        Assert.Equal(panels, parsed.Source.Features.Count);
        if (name == "dress")
        {
            string svg = GarmentPatternPreview.Svg(parsed.Source);
            Assert.Contains("Envelope 250.299525", svg);
            Assert.DoesNotContain("1075.299813", svg);
        }
        using var oracle = JsonDocument.Parse(Fixture(name + "-curves.json"));
        foreach (var expected in oracle.RootElement.EnumerateArray())
        {
            var mapping = imported.Panels.Single(panel => panel.UpstreamName == expected.GetProperty("panel").GetString());
            string edgeName = mapping.Edges[expected.GetProperty("edge").GetInt32()];
            var panel = parsed.Source.Panels.Single(panel => panel.Name == mapping.PanelName);
            var boundary = GarmentPatternInspection.Boundaries(panel)[edgeName];
            Assert.InRange(Math.Abs(boundary.LengthMm - expected.GetProperty("lengthMm").GetDouble()), 0, .00001);
            var samples = expected.GetProperty("samplesMm").EnumerateArray().ToArray();
            for (int i = 0; i < samples.Length; i++)
            {
                double t = i / 4d;
                var point = At(boundary.Segments.Single().Geometry, mapping.BoundaryReversed ? 1 - t : t);
                Assert.InRange(Math.Abs(point.X - samples[i][0].GetDouble()), 0, .00001);
                Assert.InRange(Math.Abs(point.Y - samples[i][1].GetDouble()), 0, .00001);
            }
        }
        var compiled = GarmentCompiler.Compile(imported.Source!);
        Assert.True(compiled.IsSuccess, string.Join("; ", compiled.Diagnostics));
        var repeated = GarmentCompiler.Compile(GarmentCodeImport.Import(json).Source!);
        Assert.Equal(compiled.Garment!.Cloth.ContentKey, repeated.Garment!.Cloth.ContentKey);
        Assert.All(compiled.Garment.Cloth.Definition.Positions, point => Assert.Equal(0, point.Z));
        Assert.NotEmpty(compiled.Garment.Cloth.Definition.Stitches);
        string formatted = FirmamentLanguageAnalysisService.Format(imported.Source!, "import.firmament", "1").Text;
        Assert.Equal(compiled.Garment.Cloth.ContentKey, GarmentCompiler.Compile(formatted).Garment!.Cloth.ContentKey);
    }

    [Fact]
    public void FrameAndSeamDirectionSurviveClockwiseNormalizationAndRightWrongFlags()
    {
        var root = JsonNode.Parse(Fixture("dress.json"))!;
        var pattern = root["pattern"]!;
        pattern["panels"]!["left_btorso"]!["rotation"] = new JsonArray(13, 27, -19);
        pattern["stitches"]![0]!.AsArray().Add("right_wrong");
        var imported = GarmentCodeImport.Import(root.ToJsonString());
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics));
        var parsed = GarmentAuthoring.Parse(imported.Source!).Source!;
        var panel = parsed.Panels.Single(panel => panel.Name == "left_btorso");
        var rotation = Matrix4x4.CreateRotationX(13 * MathF.PI / 180)
            * Matrix4x4.CreateRotationY(27 * MathF.PI / 180)
            * Matrix4x4.CreateRotationZ(-19 * MathF.PI / 180);
        var expectedU = Vector3.Transform(Vector3.UnitX, rotation);
        var expectedV = Vector3.Transform(Vector3.UnitY, rotation);
        Assert.InRange(Vector3.Distance(panel.U, new(expectedU.X, expectedU.Z, expectedU.Y)), 0, 1e-6f);
        Assert.InRange(Vector3.Distance(panel.V, new(expectedV.X, expectedV.Z, expectedV.Y)), 0, 1e-6f);
        var translation = pattern["panels"]!["left_btorso"]!["translation"]!;
        Assert.InRange(Vector3.Distance(panel.Origin, new(translation[0]!.GetValue<float>() * .01f,
            translation[2]!.GetValue<float>() * .01f, translation[1]!.GetValue<float>() * .01f)), 0, 1e-6f);
        var original = GarmentAuthoring.Parse(GarmentCodeImport.Import(Fixture("dress.json")).Source!).Source!;
        Assert.NotEqual(original.Stitches[0].Reversed, parsed.Stitches[0].Reversed);
        Assert.Equal(original.Stitches.Skip(1).ToArray(), parsed.Stitches.Skip(1).ToArray());
    }

    [Theory]
    [InlineData("units", "garmentcode-units-invalid")]
    [InlineData("normalization", "garmentcode-normalization-required")]
    [InlineData("curve", "garmentcode-curve-unsupported")]
    [InlineData("endpoint", "garmentcode-endpoint-invalid")]
    [InlineData("seam", "garmentcode-stitch-reference")]
    [InlineData("reuse", "garment-stitch-edge-reused")]
    [InlineData("arc", "garmentcode-circle-invalid")]
    [InlineData("loop", "garmentcode-loop-invalid")]
    [InlineData("parameter", "garmentcode-parameters-unresolved")]
    [InlineData("control", "garmentcode-json-invalid")]
    public void InvalidInputStopsBeforePublishingAnEditableSource(string mutation, string code)
    {
        var root = JsonNode.Parse(Fixture("dress.json"))!;
        var panel = root["pattern"]!["panels"]!["left_btorso"]!;
        var edges = panel["edges"]!;
        switch (mutation)
        {
            case "units": root["properties"]!["units_in_meter"] = 0; break;
            case "normalization": root["properties"]!["normalize_panel_translation"] = true; break;
            case "curve": edges[3]!["curvature"]!["type"] = "spline"; break;
            case "endpoint": edges[0]!["endpoints"]![0] = 999; break;
            case "seam": root["pattern"]!["stitches"]![0]![0]!["panel"] = "missing"; break;
            case "reuse": root["pattern"]!["stitches"]!.AsArray().Add(root["pattern"]!["stitches"]![0]!.DeepClone()); break;
            case "arc": edges[1]!["curvature"]!["params"]![0] = .001; break;
            case "loop": edges[0]!["endpoints"]![1] = 2; break;
            case "parameter": root["parameters"]!["length"] = 2; break;
            case "control":
                edges[3]!["curvature"]!["type"] = "quadratic";
                edges[3]!["curvature"]!["params"] = new JsonArray();
                break;
        }
        var result = GarmentCodeImport.Import(root.ToJsonString(), "bad.json");
        Assert.False(result.IsSuccess);
        Assert.Null(result.Source);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public void QuadraticCurvesAreElevatedExactlyAndUnitsAreExplicit()
    {
        const string json = """
            {"properties":{"curvature_coords":"relative","normalize_panel_translation":false,"units_in_meter":1000},
             "pattern":{"panels":{"square":{"translation":[0,0,0],"rotation":[0,0,0],
             "vertices":[[0,0],[20,0],[20,20],[0,20]],
             "edges":[{"endpoints":[0,1],"curvature":{"type":"quadratic","params":[[0.5,-0.2]]}},
                      {"endpoints":[1,2]},{"endpoints":[2,3]},{"endpoints":[3,0]}]}},"stitches":[]}}
            """;
        var imported = GarmentCodeImport.Import(json);
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics));
        var panel = GarmentAuthoring.Parse(imported.Source!).Source!.Panels.Single();
        var cubic = Assert.IsType<LineArcCubicBezier2D>(panel.Profile.Loops.Single().Segments[0].Geometry);
        for (int i = 0; i <= 8; i++)
        {
            double t = i / 8d;
            var point = At(cubic, t);
            Assert.InRange(Math.Abs(point.X - 20 * t), 0, 1e-10);
            Assert.InRange(Math.Abs(point.Y - 2 * (1 - t) * t * -4), 0, 1e-10);
        }
    }

    private static (double X, double Y) At(LineArcProfileCurve2D curve, double t)
    {
        double u = 1 - t;
        return curve switch
        {
            LineArcLineSegment2D line => (line.Start.X * u + line.End.X * t, line.Start.Y * u + line.End.Y * t),
            LineArcCircularArc2D arc => (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians * t),
                arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians * t)),
            LineArcCubicBezier2D cubic => (u * u * u * cubic.Start.X + 3 * u * u * t * cubic.Control1.X
                + 3 * u * t * t * cubic.Control2.X + t * t * t * cubic.End.X,
                u * u * u * cubic.Start.Y + 3 * u * u * t * cubic.Control1.Y
                + 3 * u * t * t * cubic.Control2.Y + t * t * t * cubic.End.Y),
            _ => throw new NotSupportedException(),
        };
    }

    private static string Fixture(string name) => File.ReadAllText(FixturePath(name));

    private static string FixturePath(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "fixtures/Canonical/Garment/GarmentCode", name);
    }

    private sealed class GroundPlane : IClothBodySurface3D
    {
        public ClothBodySample3D Query(Vector3 position) => new(new(position.X, position.Y, 0), Vector3.UnitZ, position.Z);
    }
}
