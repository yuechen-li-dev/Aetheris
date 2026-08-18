using System.Text.Json;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class CliBaselineTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Build_Command_Builds_Firmament_To_Step()
    {
        var outputPath = Path.Combine(RepoRoot, "testdata", "step242", "golden", "firmament-v1", "cli-build-probe.step");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["build", Path.Combine(RepoRoot, "fixtures/LegacyV1/Examples/box_basic.firmament"), "--out", outputPath],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath), stderr.ToString());
        Assert.Contains("Built box_basic.firmament", stdout.ToString());

        File.Delete(outputPath);
    }

    [Fact]
    public void Asm_Exec_Command_Executes_OcctNutBolt_And_Returns_Analysis()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var manifestPath = Path.Combine(RepoRoot, "fixtures/Assembly/LegacyImports/examples/occt-nut-bolt/nut-bolt-assembly.firmasm");

        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["asm", "exec", manifestPath, "--json"],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));

        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(2, root.GetProperty("partCount").GetInt32());
        Assert.Equal(2, root.GetProperty("instanceCount").GetInt32());
        Assert.Equal(2, root.GetProperty("analysis").GetProperty("summary").GetProperty("bodyCount").GetInt32());
    }

    [Fact]
    public void Asm_Exec_Command_Executes_As1_Full_Assembly()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var manifestPath = Path.Combine(RepoRoot, "fixtures/Assembly/LegacyImports/examples/occt-as1/as1-assembly.firmasm");

        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["asm", "exec", manifestPath, "--json"],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));

        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(18, root.GetProperty("instanceCount").GetInt32());
        Assert.Equal(18, root.GetProperty("analysis").GetProperty("summary").GetProperty("bodyCount").GetInt32());
        Assert.True(root.GetProperty("analysis").GetProperty("summary").GetProperty("faceCount").GetInt32() > 0);
    }

    [Fact]
    public void Asm_Export_Command_Exports_OcctNutBolt_RoundtripPackage()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var manifestPath = Path.Combine(RepoRoot, "fixtures/Assembly/LegacyImports/examples/occt-nut-bolt/nut-bolt-assembly.firmasm");
        var outputDirectory = CreateTempDirectory();

        try
        {
            var exitCode = Aetheris.CLI.CliRunner.Run(
                ["asm", "export", manifestPath, "--out", outputDirectory, "--json"],
                stdout,
                stderr);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));

            using var doc = JsonDocument.Parse(stdout.ToString());
            var root = doc.RootElement;
            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal(".firmasm", root.GetProperty("nativeAuthority").GetString());
            Assert.Equal("step-instance-package", root.GetProperty("exportShape").GetString());
            Assert.Equal(2, root.GetProperty("instanceCount").GetInt32());
            Assert.Equal(2, root.GetProperty("exportedInstanceStepCount").GetInt32());

            var packageManifestPath = root.GetProperty("packageManifestPath").GetString();
            Assert.False(string.IsNullOrWhiteSpace(packageManifestPath));
            Assert.True(File.Exists(packageManifestPath));
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void Asm_Export_Command_Exports_As1_RoundtripPackage()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var manifestPath = Path.Combine(RepoRoot, "fixtures/Assembly/LegacyImports/examples/occt-as1/as1-assembly.firmasm");
        var outputDirectory = CreateTempDirectory();

        try
        {
            var exitCode = Aetheris.CLI.CliRunner.Run(
                ["asm", "export", manifestPath, "--out", outputDirectory, "--json"],
                stdout,
                stderr);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));

            using var doc = JsonDocument.Parse(stdout.ToString());
            var root = doc.RootElement;
            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal(18, root.GetProperty("instanceCount").GetInt32());
            Assert.Equal(18, root.GetProperty("composedBodyCount").GetInt32());
            Assert.Equal(18, root.GetProperty("exportedInstanceStepCount").GetInt32());
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }


    [Fact]
    public void Analyze_Command_Reports_LinearExtrusion_SurfaceFamily()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/generated/ruled-a2/ellipse-linear-extrusion-production.step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", stepPath, "--json"], stdout, stderr);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var families = doc.RootElement.GetProperty("summary").GetProperty("surfaceFamilies");
        Assert.True(families.GetProperty("linear-extrusion").GetInt32() >= 1);
    }

    [Fact]
    public void AnalyzeVolume_LinearExtrusion_ReturnsFriendlyUnsupportedDiagnostic()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/generated/ruled-a2/ellipse-linear-extrusion-production.step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "volume", stepPath, "--json"], stdout, stderr);

        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        var error = doc.RootElement.GetProperty("error").GetString();
        Assert.Contains("Exact volume is not supported", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("linear-extrusion", error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("given key", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_Command_Reports_SurfaceOfRevolution_SurfaceFamily()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/probes/surface-of-revolution-line.step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", stepPath, "--json"], stdout, stderr);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var families = doc.RootElement.GetProperty("summary").GetProperty("surfaceFamilies");
        Assert.True(families.GetProperty("surface-of-revolution").GetInt32() >= 1);
    }

    [Fact]
    public void AnalyzeVolume_SurfaceOfRevolution_ReturnsFriendlyUnsupportedDiagnostic()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/probes/surface-of-revolution-line.step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "volume", stepPath, "--json"], stdout, stderr);

        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        var error = doc.RootElement.GetProperty("error").GetString();
        Assert.Contains("Exact volume is not supported", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("surface-of-revolution", error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("given key", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_Command_Accepts_DegreeOneOneBsplineBilinearProbe()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/probes/bspline-degree-1-1-bilinear.step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", stepPath, "--json"], stdout, stderr);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var families = doc.RootElement.GetProperty("summary").GetProperty("surfaceFamilies");
        Assert.True(families.GetProperty("bspline").GetInt32() >= 1);
    }

    [Fact]
    public void Analyze_Command_Reports_Summary_Facts_And_Discoverability()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/box_basic.step"), "--json"],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var summary = doc.RootElement.GetProperty("summary");
        Assert.Equal(1, summary.GetProperty("bodyCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("shellCount").GetInt32());
        Assert.Equal(6, summary.GetProperty("faceCount").GetInt32());
        Assert.Equal(12, summary.GetProperty("edgeCount").GetInt32());
        Assert.Equal(8, summary.GetProperty("vertexCount").GetInt32());
        Assert.Equal("enclosed-manifold", summary.GetProperty("structuralAssessment").GetString());
        Assert.Equal(0, summary.GetProperty("surfaceFamilies").GetProperty("bspline").GetInt32());

        Assert.Equal("mm", summary.GetProperty("lengthUnit").GetString());
        Assert.Contains("assumed", summary.GetProperty("lengthUnitBasis").GetString(), StringComparison.OrdinalIgnoreCase);

        var faceIds = summary.GetProperty("faceIds");
        Assert.Equal(1, faceIds.GetProperty("min").GetInt32());
        Assert.Equal(6, faceIds.GetProperty("max").GetInt32());
        Assert.Equal(6, faceIds.GetProperty("count").GetInt32());
        Assert.True(faceIds.GetProperty("contiguous").GetBoolean());

        var edgeIds = summary.GetProperty("edgeIds");
        Assert.Equal(1, edgeIds.GetProperty("min").GetInt32());
        Assert.Equal(12, edgeIds.GetProperty("max").GetInt32());
        Assert.Equal(12, edgeIds.GetProperty("count").GetInt32());
        Assert.True(edgeIds.GetProperty("contiguous").GetBoolean());

        var vertexIds = summary.GetProperty("vertexIds");
        Assert.Equal(1, vertexIds.GetProperty("min").GetInt32());
        Assert.Equal(8, vertexIds.GetProperty("max").GetInt32());
        Assert.Equal(8, vertexIds.GetProperty("count").GetInt32());
        Assert.True(vertexIds.GetProperty("contiguous").GetBoolean());
    }

    [Theory]
    [InlineData("air-v5-1-box-10-10-10", "box", 10d, 10d, 10d, 6, 0, 0, true, false)]
    [InlineData("air-v5-1-box-12-8-6", "box", 12d, 8d, 6d, 6, 0, 0, true, false)]
    [InlineData("air-v5-1-cylinder-5-10", "cylinder", 5d, 0d, 10d, 2, 1, 0, true, false)]
    [InlineData("air-v5-1-cylinder-3-12", "cylinder", 3d, 0d, 12d, 2, 1, 0, true, false)]
    [InlineData("air-v5-1-frustum-5-2-10", "cone", 5d, 2d, 10d, 2, 0, 1, true, false)]
    [InlineData("air-v5-1-frustum-3-1-12", "cone", 3d, 1d, 12d, 2, 0, 1, true, false)]
    [InlineData("air-v5-1-frustum-inverted-2-5-10", "cone", 2d, 5d, 10d, 2, 0, 1, true, false)]
    [InlineData("air-v5-1-apex-bottom-5", "cone", 5d, 0d, 10d, 1, 0, 1, true, false)]
    [InlineData("air-v5-1-apex-top-5", "cone", 0d, 5d, 10d, 1, 0, 1, true, false)]
    public void Build_And_Analyze_Primitives_Preserve_AIR_V5_1_Visible_Contracts(
        string name,
        string op,
        double primary,
        double secondary,
        double heightOrDepth,
        int expectedPlanarFaces,
        int expectedCylindricalFaces,
        int expectedConicalFaces,
        bool expectManifoldSolid,
        bool expectBrepWithVoids)
    {
        var sourcePath = WriteSingleOpFirmamentFixture(name, op, primary, secondary, heightOrDepth);
        var outputPath = Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}.step");

        try
        {
            var buildStdout = new StringWriter();
            var buildStderr = new StringWriter();
            var buildExitCode = Aetheris.CLI.CliRunner.Run(["build", sourcePath, "--out", outputPath], buildStdout, buildStderr);
            Assert.Equal(0, buildExitCode);
            Assert.True(File.Exists(outputPath), buildStderr.ToString());

            var summary = AnalyzeSummary(outputPath);
            Assert.Equal(1, summary.GetProperty("bodyCount").GetInt32());
            Assert.Equal("enclosed-manifold", summary.GetProperty("structuralAssessment").GetString());
            Assert.Equal(expectedPlanarFaces, summary.GetProperty("surfaceFamilies").GetProperty("plane").GetInt32());
            Assert.Equal(expectedCylindricalFaces, summary.GetProperty("surfaceFamilies").GetProperty("cylinder").GetInt32());
            Assert.Equal(expectedConicalFaces, summary.GetProperty("surfaceFamilies").GetProperty("cone").GetInt32());

            var stepText = File.ReadAllText(outputPath);
            Assert.Contains("ISO-10303-21", stepText, StringComparison.Ordinal);
            Assert.Equal(expectManifoldSolid, stepText.Contains("MANIFOLD_SOLID_BREP", StringComparison.Ordinal));
            Assert.Contains("ADVANCED_FACE", stepText, StringComparison.Ordinal);
            Assert.Equal(expectedPlanarFaces > 0, stepText.Contains("PLANE", StringComparison.Ordinal));
            Assert.Equal(expectedCylindricalFaces > 0, stepText.Contains("CYLINDRICAL_SURFACE", StringComparison.Ordinal));
            Assert.Equal(expectedConicalFaces > 0, stepText.Contains("CONICAL_SURFACE", StringComparison.Ordinal));
            Assert.Equal(expectBrepWithVoids, stepText.Contains("BREP_WITH_VOIDS", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(sourcePath);
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Theory]
    [InlineData("box", 0d, 5d, 6d)]
    [InlineData("box", -1d, 5d, 6d)]
    [InlineData("cylinder", -2d, 0d, 10d)]
    [InlineData("cylinder", 3d, 0d, 0d)]
    [InlineData("cone", 5d, 2d, -8d)]
    [InlineData("cone", 4d, 4d, 10d)]
    public void Build_Command_Invalid_Primitive_Inputs_Fail_Deterministically(string op, double primary, double secondary, double heightOrDepth)
    {
        var sourcePath = WriteSingleOpFirmamentFixture($"air-v5-1-invalid-{op}-{Guid.NewGuid():N}", op, primary, secondary, heightOrDepth);
        var outputPath = Path.Combine(Path.GetTempPath(), $"air-v5-1-invalid-{Guid.NewGuid():N}.step");

        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exitCode = Aetheris.CLI.CliRunner.Run(["build", sourcePath, "--out", outputPath], stdout, stderr);
            Assert.Equal(1, exitCode);
            Assert.False(File.Exists(outputPath));
            Assert.Contains("build failed", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(sourcePath);
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void Analyze_Command_Provides_Numeric_Face_Detail_Anchors()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateCylinder(5d, 12d).Value, "cli-cylinder-face-truth");
        var face = AnalyzeFace(stepPath, 2);
        Assert.Equal(2, face.GetProperty("faceId").GetInt32());
        Assert.Equal("Plane", face.GetProperty("surfaceType").GetString());
        Assert.Equal("bound", face.GetProperty("surfaceStatus").GetString());
        AssertPoint(face.GetProperty("anchorPoint"), 0d, 0d, 6d);
        AssertVector(face.GetProperty("planarNormal"), 0d, 0d, 1d);
        Assert.Single(face.GetProperty("adjacentEdgeIds").EnumerateArray());
    }

    [Fact]
    public void Analyze_Command_Reports_Truthful_Edge_Length_Fields()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateCylinder(5d, 8d).Value, "cli-cylinder-edge-truth");

        var edges = Enumerable.Range(1, 3).Select(id => AnalyzeEdge(stepPath, id)).ToArray();
        var lineEdge = edges.Single(edge => edge.GetProperty("curveType").GetString() == "Line3");
        Assert.Equal(8d, lineEdge.GetProperty("arcLength").GetDouble(), 8);
        Assert.Equal(8d, lineEdge.GetProperty("parameterRange").GetDouble(), 8);
        Assert.Equal("computed", lineEdge.GetProperty("arcLengthStatus").GetString());

        var circleEdge = edges.First(edge => edge.GetProperty("curveType").GetString() == "Circle3");
        Assert.Equal(2d * double.Pi, circleEdge.GetProperty("parameterRange").GetDouble(), 8);
        Assert.Equal(10d * double.Pi, circleEdge.GetProperty("arcLength").GetDouble(), 8);
        Assert.Equal("computed", circleEdge.GetProperty("arcLengthStatus").GetString());
    }

    [Fact]
    public void Analyze_Command_Explains_Null_ArcLength_For_Unsupported_Curve_Kinds()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/nist/STC/nist_stc_06_asme1_ap242-e3.stp");
        var summary = AnalyzeSummary(stepPath);
        var maxEdgeId = summary.GetProperty("edgeIds").GetProperty("max").GetInt32();

        for (var edgeId = 1; edgeId <= maxEdgeId; edgeId++)
        {
            var edge = AnalyzeEdge(stepPath, edgeId);
            var curveType = edge.GetProperty("curveType").GetString();
            if (curveType is "Line3" or "Circle3")
            {
                continue;
            }

            Assert.True(edge.GetProperty("arcLength").ValueKind == JsonValueKind.Null);
            Assert.Equal("unsupported-for-curve-kind", edge.GetProperty("arcLengthStatus").GetString());
            return;
        }

        throw new Xunit.Sdk.XunitException("Expected at least one non-line/non-circle edge in NIST fixture.");
    }

    [Fact]
    public void Analyze_Command_Supports_Vertex_Detail()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/box_basic.step"), "--vertex", "1", "--json"],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var vertex = doc.RootElement.GetProperty("vertex");
        Assert.Equal(1, vertex.GetProperty("vertexId").GetInt32());
        Assert.True(vertex.TryGetProperty("position", out _));
        Assert.True(vertex.GetProperty("incidentEdgeIds").GetArrayLength() >= 1);
    }

    [Fact]
    public void Analyze_Map_BoxTop_Respects_Grid_And_Reports_DepthThickness()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateBox(10d, 6d, 4d).Value, "cli-map-box-top");
        using var doc = RunAnalyzeMap(stepPath, "--top", 6, 8);
        var root = doc.RootElement;

        var metadata = root.GetProperty("metadata");
        Assert.Equal("Top", metadata.GetProperty("view").GetString());
        Assert.Equal(6, metadata.GetProperty("rows").GetInt32());
        Assert.Equal(8, metadata.GetProperty("cols").GetInt32());
        Assert.Equal("-Z", metadata.GetProperty("rayDirectionAxis").GetString());

        var summary = root.GetProperty("summary");
        Assert.Equal(48, summary.GetProperty("totalSamples").GetInt32());
        Assert.Equal(48, summary.GetProperty("hitSamples").GetInt32());
        Assert.Equal(0d, summary.GetProperty("entryDepthMin").GetDouble(), 8);
        Assert.Equal(0d, summary.GetProperty("entryDepthMax").GetDouble(), 8);
        Assert.Equal(4d, summary.GetProperty("thicknessMin").GetDouble(), 8);
        Assert.Equal(4d, summary.GetProperty("thicknessMax").GetDouble(), 8);

        var grid = root.GetProperty("grid");
        Assert.Equal(6, grid.GetArrayLength());
        Assert.Equal(8, grid[0].GetArrayLength());
        var sample = grid[0][0];
        Assert.True(sample.GetProperty("hit").GetBoolean());
        Assert.Equal(4d, sample.GetProperty("thickness").GetDouble(), 8);
    }

    [Fact]
    public void Analyze_Map_SphereTop_Reveals_Thickness_Variation_And_Empty_Samples()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateSphere(3d).Value, "cli-map-sphere-top");
        using var doc = RunAnalyzeMap(stepPath, "--top", 11, 11);
        var summary = doc.RootElement.GetProperty("summary");

        Assert.True(summary.GetProperty("hitSamples").GetInt32() > 0);
        Assert.True(summary.GetProperty("emptySamples").GetInt32() > 0);
        Assert.True(summary.GetProperty("thicknessMax").GetDouble() > summary.GetProperty("thicknessMin").GetDouble());
    }

    [Fact]
    public void Analyze_Map_BoxRight_Reports_Coherent_Face_Ids_And_Surface_Types()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateBox(10d, 6d, 4d).Value, "cli-map-box-right");
        using var doc = RunAnalyzeMap(stepPath, "--right", 5, 7);
        var summary = doc.RootElement.GetProperty("summary");
        Assert.Equal(1, summary.GetProperty("visibleFaceIds").GetArrayLength());
        Assert.Equal(1, summary.GetProperty("visibleSurfaceTypes").GetArrayLength());
        Assert.Equal("Plane", summary.GetProperty("visibleSurfaceTypes")[0].GetString());
        foreach (var row in doc.RootElement.GetProperty("grid").EnumerateArray())
        {
            foreach (var sample in row.EnumerateArray())
            {
                Assert.True(sample.GetProperty("hit").GetBoolean());
                Assert.True(sample.GetProperty("entryFaceId").GetInt32() > 0);
                Assert.Equal("Plane", sample.GetProperty("entrySurfaceType").GetString());
            }
        }
    }

    [Fact]
    public void Analyze_Map_Cli_Contract_Returns_Expected_Json_Shape()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateBox(8d, 8d, 2d).Value, "cli-map-contract");
        using var doc = RunAnalyzeMap(stepPath, "--front", 4, 5);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("metadata", out _));
        Assert.True(root.TryGetProperty("summary", out _));
        Assert.True(root.TryGetProperty("grid", out var grid));
        Assert.True(root.TryGetProperty("notes", out _));
        Assert.Equal(4, grid.GetArrayLength());
        Assert.Equal(5, grid[0].GetArrayLength());
        Assert.True(grid[0][0].TryGetProperty("entryDepth", out _));
        Assert.True(grid[0][0].TryGetProperty("entryFaceId", out _));
        Assert.True(grid[0][0].TryGetProperty("entrySurfaceType", out _));
    }

    [Fact]
    public void Analyze_Map_RankedProbes_Emits_EvidenceBundle_And_SectionCommands()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateCylinder(4d, 10d).Value, "cli-map-a6-cylinder");
        using var doc = RunAnalyzeSixViewMap(stepPath, "8x8", "--rank-probes", "--evidence-bundle");
        var root = doc.RootElement;

        var ranked = root.GetProperty("rankedProbes");
        Assert.True(ranked.GetArrayLength() > 0);
        var first = ranked[0];
        Assert.True(first.GetProperty("score").GetDouble() > 0d);
        Assert.Contains("analytic provenance", first.GetProperty("reasons").EnumerateArray().Select(r => r.GetString()));
        Assert.Contains(first.GetProperty("recommendedActions").EnumerateArray(), a => a.GetProperty("kind").GetString() == "pointProbe");
        Assert.Contains(first.GetProperty("recommendedActions").EnumerateArray(), a => a.GetProperty("kind").GetString() == "sectionProbe" && a.GetProperty("command").GetString()!.Contains("analyze section", StringComparison.Ordinal));

        var bundle = root.GetProperty("evidenceBundle");
        Assert.Equal(6, bundle.GetProperty("coarseMap").GetProperty("views").GetInt32());
        Assert.True(bundle.GetProperty("rankedQuestions").GetArrayLength() > 0);
        Assert.True(bundle.GetProperty("suggestedActions").GetArrayLength() > 0);
    }

    [Fact]
    public void Analyze_Map_PointProbe_Emits_CompactSummary_And_Retains_Hits()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateBox(10d, 6d, 4d).Value, "cli-map-a6-point-summary");
        using var doc = RunAnalyzeRayMap(stepPath, "--plane", "xy", "--direction", "-z", "--point", "0,0");
        var root = doc.RootElement;
        var summary = root.GetProperty("pointSummary");
        Assert.True(summary.GetProperty("hitCount").GetInt32() >= 2);
        Assert.Equal("plane", summary.GetProperty("firstHit").GetProperty("family").GetString());
        Assert.Equal("plane", summary.GetProperty("lastHit").GetProperty("family").GetString());
        Assert.True(summary.GetProperty("familySequence").GetArrayLength() >= 1);
        Assert.True(root.GetProperty("hits").GetArrayLength() >= 2);
    }

    [Fact]
    public void Analyze_Map_RayProbe_BoxTop_Returns_Llm_Grid_Samples()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateBox(10d, 6d, 4d).Value, "cli-ray-map-box-top");
        using var doc = RunAnalyzeRayMap(stepPath, "--plane", "xy", "--direction", "-z", "--resolution", "5x5");
        var root = doc.RootElement;

        Assert.Equal("grid", root.GetProperty("mode").GetString());
        Assert.Equal("xy", root.GetProperty("plane").GetString());
        Assert.Equal("-z", root.GetProperty("direction").GetString());
        Assert.Equal(25, root.GetProperty("samples").GetArrayLength());
        Assert.Equal(1d, root.GetProperty("summary").GetProperty("hitCoverage").GetDouble(), 8);
        Assert.Equal("plane", root.GetProperty("summary").GetProperty("surfaceFamiliesHit").EnumerateObject().Single().Name);
        Assert.Equal("analytic-cir-tessellated-fallback", root.GetProperty("backendPolicy").GetString());
        Assert.True(root.GetProperty("summary").GetProperty("analyticHitCount").GetInt32() > 0);
        Assert.Equal(0, root.GetProperty("summary").GetProperty("tessellatedFallbackHitCount").GetInt32());
        var centerHit = root.GetProperty("samples")[12].GetProperty("firstHit");
        Assert.True(centerHit.GetProperty("position").GetProperty("z").GetDouble() > 0d);
        Assert.Equal("plane", centerHit.GetProperty("surfaceFamily").GetString());
        Assert.Equal("analytic", centerHit.GetProperty("intersectionMode").GetString());
        Assert.Equal("exact", centerHit.GetProperty("confidence").GetString());
        Assert.Equal(2, root.GetProperty("samples")[12].GetProperty("intersectionModes").GetProperty("analytic").GetInt32());
    }

    [Fact]
    public void Analyze_Map_SixView_Llm_Box_Returns_Compact_Summaries()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateBox(10d, 6d, 4d).Value, "cli-six-view-box");
        using var doc = RunAnalyzeSixViewMap(stepPath, "8x8");
        var root = doc.RootElement;

        Assert.Equal("six-view-summary", root.GetProperty("mode").GetString());
        Assert.Equal("analyze-map-v1", root.GetProperty("mapVersion").GetString());
        Assert.Equal(6, root.GetProperty("views").GetArrayLength());
        foreach (var view in root.GetProperty("views").EnumerateArray())
        {
            var summary = view.GetProperty("summary");
            Assert.Equal(64, summary.GetProperty("sampleCount").GetInt32());
            Assert.Equal(64, summary.GetProperty("hitCount").GetInt32());
            Assert.Equal(1d, summary.GetProperty("hitCoverage").GetDouble(), 8);
            Assert.True(summary.GetProperty("surfaceFamiliesHit").TryGetProperty("plane", out _));
            Assert.True(summary.GetProperty("backendCounts").GetProperty("analytic").GetInt32() > 0);
            Assert.Equal(0d, summary.GetProperty("fallbackRatio").GetDouble(), 8);
            Assert.True(view.TryGetProperty("compactGrid", out var compactGrid));
            Assert.Equal(8, compactGrid.GetProperty("width").GetInt32());
            Assert.Equal(8, compactGrid.GetProperty("height").GetInt32());
            Assert.Equal(8, compactGrid.GetProperty("rows").GetArrayLength());
            Assert.True(view.TryGetProperty("components", out var components));
            Assert.DoesNotContain(components.GetProperty("noHit").EnumerateArray(), c => c.GetProperty("classificationHint").GetString() == "interior-opening-candidate");
            Assert.NotEmpty(components.GetProperty("heightBands").EnumerateArray());
            Assert.NotEmpty(components.GetProperty("surfaceFamilies").EnumerateArray());
            Assert.NotEmpty(view.GetProperty("suggestedProbes").EnumerateArray());
        }

        Assert.NotEmpty(root.GetProperty("suggestedProbes").EnumerateArray());
    }

    [Fact]
    public void Analyze_Map_SixView_Llm_Cylinder_Reports_Surface_Component_Probe()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateCylinder(3d, 8d).Value, "cli-six-view-cylinder-component");
        using var doc = RunAnalyzeSixViewMap(stepPath, "17x17");
        var views = doc.RootElement.GetProperty("views").EnumerateArray().ToArray();
        var hasCylinder = views.Any(v => v.GetProperty("components").GetProperty("surfaceFamilies").EnumerateArray().Any(c => c.GetProperty("surfaceFamily").GetString() == "cylinder"));

        Assert.True(hasCylinder, doc.RootElement.GetRawText());
        Assert.Contains(doc.RootElement.GetProperty("suggestedProbes").EnumerateArray(), p => p.GetProperty("command").GetString()?.Contains("--point", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Analyze_Map_SixView_Llm_Torus_Discloses_Analytic_Ring_And_No_Fallback()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateTorus(3d, 1d).Value, "cli-six-view-torus");
        using var doc = RunAnalyzeSixViewMap(stepPath, "9x9");
        var root = doc.RootElement;
        var top = root.GetProperty("views").EnumerateArray().Single(v => v.GetProperty("name").GetString() == "top");

        Assert.True(top.GetProperty("summary").GetProperty("surfaceFamiliesHit").TryGetProperty("torus", out _));
        Assert.True(top.GetProperty("summary").GetProperty("backendCounts").GetProperty("analytic").GetInt32() > 0);
        Assert.Equal(0, top.GetProperty("summary").GetProperty("backendCounts").GetProperty("tessellated-fallback").GetInt32());
        Assert.DoesNotContain("~", string.Concat(top.GetProperty("compactGrid").GetProperty("rows").EnumerateArray().Select(r => r.GetString())));
        Assert.Contains(top.GetProperty("components").GetProperty("surfaceFamilies").EnumerateArray(), c => c.GetProperty("surfaceFamily").GetString() == "torus");
        Assert.Contains(top.GetProperty("suggestedProbes").EnumerateArray(), p => p.GetProperty("reason").GetString()?.Contains("torus", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Analyze_Map_SixView_Llm_LinearExtrusion_Discloses_Fallback()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata", "step242", "generated", "ruled-a2", "ellipse-linear-extrusion-production.step");
        using var doc = RunAnalyzeSixViewMap(stepPath, "4x4");
        var root = doc.RootElement;

        Assert.Contains("linear-extrusion", root.GetRawText(), StringComparison.Ordinal);
        Assert.Contains("tessellated-fallback", root.GetRawText(), StringComparison.Ordinal);
        Assert.Contains(root.GetProperty("diagnostics").EnumerateArray(), d => d.GetString()?.Contains("used tessellated fallback", StringComparison.Ordinal) == true);
        Assert.Contains(root.GetProperty("views").EnumerateArray(), v => v.GetProperty("components").GetProperty("noHit").GetArrayLength() > 0 || v.GetProperty("components").GetProperty("fallback").GetArrayLength() > 0);
    }


    [Fact]
    public void Analyze_Map_RayProbe_CylinderSide_Uses_Analytic_Exact_Hits()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateCylinder(3d, 8d).Value, "cli-ray-map-cylinder-side");
        using var doc = RunAnalyzeRayMap(stepPath, "--plane", "xz", "--direction", "+y", "--resolution", "5x5", "--point", "0,0");
        var root = doc.RootElement;

        Assert.True(root.GetProperty("summary").GetProperty("analyticHitCount").GetInt32() > 0);
        Assert.Equal(0, root.GetProperty("summary").GetProperty("tessellatedFallbackHitCount").GetInt32());
        Assert.Equal("analytic", root.GetProperty("intersectionMode").GetString());
        var hit = root.GetProperty("hits").EnumerateArray().First(h => h.GetProperty("surfaceFamily").GetString() == "cylinder");
        Assert.Equal("analytic", hit.GetProperty("intersectionMode").GetString());
        Assert.Equal("exact", hit.GetProperty("confidence").GetString());
        Assert.Empty(hit.GetProperty("diagnostics").EnumerateArray());
    }

    [Fact]
    public void Analyze_Map_RayProbe_Sphere_Uses_Analytic_Exact_Hits()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateSphere(3d).Value, "cli-ray-map-sphere");
        using var doc = RunAnalyzeRayMap(stepPath, "--plane", "xy", "--direction", "-z", "--resolution", "5x5", "--point", "0,0");
        var root = doc.RootElement;

        Assert.True(root.GetProperty("summary").GetProperty("analyticHitCount").GetInt32() > 0);
        Assert.Equal(0, root.GetProperty("summary").GetProperty("tessellatedFallbackHitCount").GetInt32());
        Assert.Equal("analytic", root.GetProperty("intersectionMode").GetString());
        var hit = root.GetProperty("hits").EnumerateArray().First(h => h.GetProperty("surfaceFamily").GetString() == "sphere");
        Assert.Equal("analytic", hit.GetProperty("intersectionMode").GetString());
        Assert.Equal("exact", hit.GetProperty("confidence").GetString());
        Assert.Empty(hit.GetProperty("diagnostics").EnumerateArray());
    }

    [Fact]
    public void Analyze_Map_RayProbe_Cone_Uses_Analytic_Exact_Hits()
    {
        var stepPath = ExportPrimitiveToTempStep(CreateCone(4d, 2d, 8d), "cli-ray-map-cone");
        using var doc = RunAnalyzeRayMap(stepPath, "--plane", "xz", "--direction", "+y", "--resolution", "5x5", "--point", "3,0");
        var root = doc.RootElement;

        Assert.True(root.GetProperty("summary").GetProperty("analyticHitCount").GetInt32() > 0);
        Assert.Equal(0, root.GetProperty("summary").GetProperty("tessellatedFallbackHitCount").GetInt32());
        var hit = root.GetProperty("hits").EnumerateArray().First(h => h.GetProperty("surfaceFamily").GetString() == "cone");
        Assert.Equal("analytic", hit.GetProperty("intersectionMode").GetString());
        Assert.Equal("exact", hit.GetProperty("confidence").GetString());
        Assert.InRange(hit.GetProperty("position").GetProperty("z").GetDouble(), 0d, 8d);
        Assert.True(hit.TryGetProperty("normal", out _));
    }

    [Fact]
    public void Analyze_Map_RayProbe_Torus_CenterHole_Does_Not_Lie()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateTorus(3d, 1d).Value, "cli-ray-map-torus-hole");
        using var doc = RunAnalyzeRayMap(stepPath, "--plane", "xz", "--direction", "+y", "--resolution", "5x5", "--point", "0,0");
        var root = doc.RootElement;

        Assert.Equal(0, root.GetProperty("summary").GetProperty("tessellatedFallbackHitCount").GetInt32());
        Assert.Equal(0, root.GetProperty("hitCount").GetInt32());
        Assert.DoesNotContain("torus", root.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_Map_RayProbe_Torus_RingHit_Uses_Analytic_Exact_Hits()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateTorus(3d, 1d).Value, "cli-ray-map-torus-ring");
        using var doc = RunAnalyzeRayMap(stepPath, "--plane", "xz", "--direction", "+y", "--resolution", "5x5", "--point", "3,0");
        var root = doc.RootElement;

        Assert.True(root.GetProperty("summary").GetProperty("analyticHitCount").GetInt32() > 0);
        Assert.Equal(0, root.GetProperty("summary").GetProperty("tessellatedFallbackHitCount").GetInt32());
        var hits = root.GetProperty("hits").EnumerateArray().Where(h => h.GetProperty("surfaceFamily").GetString() == "torus").ToArray();
        // The exact full-torus bounds now start the probe outside the tube, so the
        // ray correctly observes both the entering and exiting intersections.
        Assert.Equal(2, hits.Length);
        Assert.All(hits, hit =>
        {
            Assert.Equal("analytic", hit.GetProperty("intersectionMode").GetString());
            Assert.Equal("exact", hit.GetProperty("confidence").GetString());
            Assert.InRange(Math.Abs(hit.GetProperty("position").GetProperty("y").GetDouble()), 0.999d, 1.001d);
        });
    }

    [Fact]
    public void Analyze_Map_RayProbe_Torus_Outside_Misses()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateTorus(3d, 1d).Value, "cli-ray-map-torus-outside");
        using var doc = RunAnalyzeRayMap(stepPath, "--plane", "xz", "--direction", "+y", "--resolution", "5x5", "--point", "5,0");

        Assert.Equal(0, doc.RootElement.GetProperty("hitCount").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("summary").GetProperty("tessellatedFallbackHitCount").GetInt32());
    }

    [Fact]
    public void Analyze_Map_RayProbe_LinearExtrusion_Discloses_Tessellated_Fallback()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata", "step242", "generated", "ruled-a2", "ellipse-linear-extrusion-production.step");
        using var doc = RunAnalyzeRayMap(stepPath, "--plane", "xy", "--direction", "-z", "--resolution", "4x4");
        var root = doc.RootElement;

        Assert.Equal("analytic-cir-tessellated-fallback", root.GetProperty("backendPolicy").GetString());
        Assert.True(root.GetProperty("summary").TryGetProperty("tessellatedFallbackHitCount", out _));
        Assert.Contains("linear-extrusion", root.GetRawText(), StringComparison.Ordinal);
        Assert.Contains("Exact ray intersection unavailable for linear-extrusion; used tessellated fallback.", root.GetProperty("diagnostics").EnumerateArray().Select(d => d.GetString()));
        Assert.Contains("tessellated-fallback", root.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_Section_Box_XY_At_Midplane_Produces_One_Closed_Line_Loop()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateBox(10d, 6d, 4d).Value, "cli-section-box");
        using var doc = RunAnalyzeSection(stepPath, "--xy", 0d);
        var root = doc.RootElement;

        var metadata = root.GetProperty("metadata");
        Assert.Equal("XY", metadata.GetProperty("planeFamily").GetString());
        Assert.Equal("z = offset", metadata.GetProperty("offsetEquation").GetString());
        Assert.Equal("X", metadata.GetProperty("sectionAxisU").GetString());
        Assert.Equal("Y", metadata.GetProperty("sectionAxisV").GetString());

        var summary = root.GetProperty("summary");
        Assert.Equal(1, summary.GetProperty("loopCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("closedLoopCount").GetInt32());
        Assert.Equal(4, summary.GetProperty("lineSegmentCount").GetInt32());
        Assert.Equal(0, summary.GetProperty("arcSegmentCount").GetInt32());

        var loop = root.GetProperty("loops")[0];
        Assert.True(loop.GetProperty("isClosed").GetBoolean());
        Assert.Equal(4, loop.GetProperty("segments").GetArrayLength());
    }

    [Fact]
    public void Analyze_Section_Cylinder_XY_At_Midplane_Produces_Arc_Loop()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateCylinder(5d, 12d).Value, "cli-section-cylinder");
        using var doc = RunAnalyzeSection(stepPath, "--xy", 0d);
        var summary = doc.RootElement.GetProperty("summary");
        Assert.Equal(1, summary.GetProperty("loopCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("arcSegmentCount").GetInt32());
        Assert.Equal(0, summary.GetProperty("lineSegmentCount").GetInt32());

        var segment = doc.RootElement.GetProperty("loops")[0].GetProperty("segments")[0];
        Assert.Equal("arc", segment.GetProperty("kind").GetString());
        Assert.Equal(5d, segment.GetProperty("radius").GetDouble(), 8);
        Assert.Equal(2d * double.Pi, segment.GetProperty("sweepRadians").GetDouble(), 8);
    }

    [Fact]
    public void Analyze_Section_BoxWithHole_XY_Produces_Multiple_Loops()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/boolean_box_cylinder_hole.step");
        using var doc = RunAnalyzeSection(stepPath, "--xy", 0d);
        var summary = doc.RootElement.GetProperty("summary");
        Assert.True(summary.GetProperty("loopCount").GetInt32() >= 2);
        Assert.True(summary.GetProperty("closedLoopCount").GetInt32() >= 2);
    }

    [Fact]
    public void Analyze_Section_Cli_Contract_Returns_Expected_Json_Shape()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateBox(8d, 6d, 4d).Value, "cli-section-contract");
        using var doc = RunAnalyzeSection(stepPath, "--yz", 0d);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("metadata", out _));
        Assert.True(root.TryGetProperty("summary", out _));
        Assert.True(root.TryGetProperty("loops", out var loops));
        Assert.True(root.TryGetProperty("notes", out _));
        Assert.True(loops.GetArrayLength() >= 1);
    }

    [Fact]
    public void Analyze_Command_Provides_Cylinder_Sphere_And_Torus_Anchors_With_Sphere_Axis_Omitted()
    {
        var cylinderStepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateCylinder(4d, 12d).Value, "cli-cylinder-face-anchor");
        var sphereStepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateSphere(3d).Value, "cli-sphere-face-anchor");
        var torusStepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateTorus(7d, 2d).Value, "cli-torus-face-anchor");

        var cylinderFace = Enumerable.Range(1, 3)
            .Select(id => AnalyzeFace(cylinderStepPath, id))
            .Single(face => face.GetProperty("surfaceType").GetString() == "Cylinder");
        AssertPoint(cylinderFace.GetProperty("anchorPoint"), 0d, 0d, -6d);
        AssertVector(cylinderFace.GetProperty("axis"), 0d, 0d, 1d);
        Assert.Equal(4d, cylinderFace.GetProperty("radius").GetDouble(), 8);

        var sphereFace = AnalyzeFace(sphereStepPath, 1);
        Assert.Equal("Sphere", sphereFace.GetProperty("surfaceType").GetString());
        AssertPoint(sphereFace.GetProperty("anchorPoint"), 0d, 0d, 0d);
        Assert.False(sphereFace.TryGetProperty("axis", out _));
        Assert.Equal(3d, sphereFace.GetProperty("radius").GetDouble(), 8);

        var torusFace = AnalyzeFace(torusStepPath, 1);
        Assert.Equal("Torus", torusFace.GetProperty("surfaceType").GetString());
        AssertPoint(torusFace.GetProperty("anchorPoint"), 0d, 0d, 0d);
        AssertVector(torusFace.GetProperty("axis"), 0d, 1d, 0d);
        Assert.Equal(7d, torusFace.GetProperty("majorRadius").GetDouble(), 8);
        Assert.Equal(2d, torusFace.GetProperty("minorRadius").GetDouble(), 8);
    }


    [Fact]
    public void Build_And_Analyze_BoundedSingleEdgeFillet_Reports_CylindricalSurface_And_Manifoldness()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"cli-m5b-bounded-single-edge-fillet-{Guid.NewGuid():N}.step");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var buildStdout = new StringWriter();
        var buildStderr = new StringWriter();
        var buildExitCode = Aetheris.CLI.CliRunner.Run(
            ["build", Path.Combine(RepoRoot, "fixtures/LegacyV1/Corpus/valid/m5b-valid-fillet-concave-overlap-lroot.firmament"), "--out", outputPath],
            buildStdout,
            buildStderr);
        Assert.Equal(0, buildExitCode);
        Assert.True(File.Exists(outputPath), buildStderr.ToString());

        var summary = AnalyzeSummary(outputPath);
        Assert.Equal("enclosed-manifold", summary.GetProperty("structuralAssessment").GetString());
        Assert.True(summary.GetProperty("surfaceFamilies").GetProperty("cylinder").GetInt32() >= 1);

        var foundCylinder = false;
        foreach (var face in AnalyzeExistingFaces(outputPath, summary))
        {
            if (face.GetProperty("surfaceType").GetString() is not "Cylinder")
            {
                continue;
            }

            foundCylinder = true;
            Assert.Equal(1d, face.GetProperty("radius").GetDouble(), 8);
            break;
        }

        Assert.True(foundCylinder, "Expected at least one cylindrical face in bounded fillet export.");
        File.Delete(outputPath);
    }

    [Fact]
    public void Build_And_Analyze_BoundedChainedSameRadiusFillet_Reports_MultipleCylindricalSurfaces_And_Manifoldness()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"cli-m5b-bounded-chained-fillet-{Guid.NewGuid():N}.step");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var buildStdout = new StringWriter();
        var buildStderr = new StringWriter();
        var buildExitCode = Aetheris.CLI.CliRunner.Run(
            ["build", Path.Combine(RepoRoot, "fixtures/LegacyV1/Corpus/valid/m5b-valid-fillet-concave-chained-adjacent-pair.firmament"), "--out", outputPath],
            buildStdout,
            buildStderr);
        Assert.Equal(0, buildExitCode);
        Assert.True(File.Exists(outputPath), buildStderr.ToString());

        var summary = AnalyzeSummary(outputPath);
        Assert.Equal("enclosed-manifold", summary.GetProperty("structuralAssessment").GetString());
        Assert.True(summary.GetProperty("surfaceFamilies").GetProperty("cylinder").GetInt32() >= 2);

        var stepText = File.ReadAllText(outputPath);
        Assert.Contains("MANIFOLD_SOLID_BREP", stepText, StringComparison.Ordinal);
        Assert.Contains("ADVANCED_FACE", stepText, StringComparison.Ordinal);
        Assert.Contains("CYLINDRICAL_SURFACE", stepText, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", stepText, StringComparison.Ordinal);

        var cylinderCountAtRadius = AnalyzeExistingFaces(outputPath, summary)
            .Count(face => face.GetProperty("surfaceType").GetString() is "Cylinder"
                && Math.Abs(face.GetProperty("radius").GetDouble() - 0.4d) <= 1e-8);

        Assert.True(cylinderCountAtRadius >= 2, "Expected at least two radius-0.4 cylindrical faces in bounded chained fillet export.");
        File.Delete(outputPath);
    }

    [Fact]
    public void Build_And_Analyze_TriangularPrism_Example_Reports_IsoscelesContractBounds()
    {
        var outputPath = Path.Combine(RepoRoot, "testdata", "step242", "golden", "firmament-v1", "cli-triangular-prism-contract.step");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var buildStdout = new StringWriter();
        var buildStderr = new StringWriter();
        var buildExitCode = Aetheris.CLI.CliRunner.Run(
            ["build", Path.Combine(RepoRoot, "fixtures/LegacyV1/Examples/triangular_prism_basic.firmament"), "--out", outputPath],
            buildStdout,
            buildStderr);
        Assert.Equal(0, buildExitCode);
        Assert.True(File.Exists(outputPath), buildStderr.ToString());

        var analyzeStdout = new StringWriter();
        var analyzeStderr = new StringWriter();
        var analyzeExitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", outputPath, "--json"],
            analyzeStdout,
            analyzeStderr);
        Assert.Equal(0, analyzeExitCode);

        using var doc = JsonDocument.Parse(analyzeStdout.ToString());
        var summary = doc.RootElement.GetProperty("summary");
        Assert.Equal(5, summary.GetProperty("faceCount").GetInt32());
        Assert.Equal(9, summary.GetProperty("edgeCount").GetInt32());
        Assert.Equal(6, summary.GetProperty("vertexCount").GetInt32());
        var bbox = summary.GetProperty("boundingBox");
        AssertPoint(bbox.GetProperty("min"), -10d, -6d, 0d);
        AssertPoint(bbox.GetProperty("max"), 10d, 6d, 10d);

        File.Delete(outputPath);
    }

    [Fact]
    public void Build_And_Analyze_NonOrthogonalTriangularPrismCornerChamfer_Reports_Enclosed_And_Planar_Cut_Face()
    {
        var outputPath = Path.Combine(RepoRoot, "testdata", "step242", "golden", "firmament-v1", "cli-triangular-prism-corner-chamfer-e4.step");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var buildStdout = new StringWriter();
        var buildStderr = new StringWriter();
        var buildExitCode = Aetheris.CLI.CliRunner.Run(
            ["build", Path.Combine(RepoRoot, "fixtures/LegacyV1/Examples/m5a_chamfer_triangular_prism_corner_e4_basic.firmament"), "--out", outputPath],
            buildStdout,
            buildStderr);
        Assert.Equal(0, buildExitCode);
        Assert.True(File.Exists(outputPath), buildStderr.ToString());

        var analyzeStdout = new StringWriter();
        var analyzeStderr = new StringWriter();
        var analyzeExitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", outputPath, "--json"],
            analyzeStdout,
            analyzeStderr);
        Assert.Equal(0, analyzeExitCode);

        using var doc = JsonDocument.Parse(analyzeStdout.ToString());
        var summary = doc.RootElement.GetProperty("summary");
        Assert.Equal("enclosed-manifold", summary.GetProperty("structuralAssessment").GetString());
        Assert.Equal(6, summary.GetProperty("faceCount").GetInt32());
        Assert.Equal(12, summary.GetProperty("edgeCount").GetInt32());
        Assert.Equal(8, summary.GetProperty("vertexCount").GetInt32());
        var bbox = summary.GetProperty("boundingBox");
        AssertPoint(bbox.GetProperty("min"), -5d, -3d, -4d);
        AssertPoint(bbox.GetProperty("max"), 5d, 3d, 4d);

        var cutFace = AnalyzeFace(outputPath, 6);
        Assert.Equal("Plane", cutFace.GetProperty("surfaceType").GetString());
        Assert.Equal("bound", cutFace.GetProperty("surfaceStatus").GetString());
        Assert.Equal(3, cutFace.GetProperty("adjacentEdgeIds").GetArrayLength());

        File.Delete(outputPath);
    }

    [Fact]
    public void Build_And_Analyze_BoundedConcaveStraightEdgeChamfer_Reports_Enclosed_And_ChangedTopology()
    {
        var outputPath = Path.Combine(RepoRoot, "testdata", "step242", "golden", "firmament-v1", "cli-concave-edge-chamfer-e7b.step");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var buildStdout = new StringWriter();
        var buildStderr = new StringWriter();
        var buildExitCode = Aetheris.CLI.CliRunner.Run(
            ["build", Path.Combine(RepoRoot, "fixtures/LegacyV1/Corpus/valid/e7-valid-chamfer-concave-overlap-lroot.firmament"), "--out", outputPath],
            buildStdout,
            buildStderr);
        Assert.Equal(0, buildExitCode);
        Assert.True(File.Exists(outputPath), buildStderr.ToString());

        var analyzeStdout = new StringWriter();
        var analyzeStderr = new StringWriter();
        var analyzeExitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", outputPath, "--json"],
            analyzeStdout,
            analyzeStderr);
        Assert.Equal(0, analyzeExitCode);

        using var doc = JsonDocument.Parse(analyzeStdout.ToString());
        var summary = doc.RootElement.GetProperty("summary");
        Assert.Equal("enclosed-manifold", summary.GetProperty("structuralAssessment").GetString());
        Assert.Equal(11, summary.GetProperty("faceCount").GetInt32());
        Assert.Equal(27, summary.GetProperty("edgeCount").GetInt32());
        Assert.Equal(18, summary.GetProperty("vertexCount").GetInt32());
        var bbox = summary.GetProperty("boundingBox");
        AssertPoint(bbox.GetProperty("min"), -15d, -10d, 0d);
        AssertPoint(bbox.GetProperty("max"), 30d, 15d, 10d);

        File.Delete(outputPath);
    }

    [Fact]
    public void Analyze_Command_Uses_Binding_Missing_Surface_Status_Instead_Of_Unknown_Surface_Type()
    {
        var boxBody = BrepPrimitives.CreateBox(10d, 6d, 4d).Value;
        var brokenBody = RemoveFaceBinding(boxBody, new[] { 1 });

        var result = StepAnalyzer.AnalyzeImportedBody(brokenBody, "in-memory", faceId: 1);
        Assert.NotNull(result.Face);
        Assert.Null(result.Face!.SurfaceType);
        Assert.Equal("binding-missing", result.Face.SurfaceStatus);
    }


    [Fact]
    public void AnalyzeVolume_Box_ReturnsExpectedVolume()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/box_basic.step");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "volume", stepPath, "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("planar-closed-shell", doc.RootElement.GetProperty("method").GetString());
        Assert.Equal(14400d, doc.RootElement.GetProperty("volume").GetDouble(), 8);
    }

    [Fact]
    public void AnalyzeVolume_BoxMinusBox_PlanarBoolean_ReturnsExpectedVolume()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/boolean_subtract_basic.step");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "volume", stepPath, "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("planar-closed-shell", doc.RootElement.GetProperty("method").GetString());
        Assert.Equal(11.75d, doc.RootElement.GetProperty("volume").GetDouble(), 8);
    }

    [Fact]
    public void AnalyzeVolume_Cylinder_ReturnsExpectedVolume()
    {
        var radius=5d; var height=12d;
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateCylinder(radius, height).Value, "cli-volume-cylinder");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "volume", stepPath, "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var v = doc.RootElement.GetProperty("volume").GetDouble();
        Assert.InRange(v, Math.PI*radius*radius*height*0.999999, Math.PI*radius*radius*height*1.000001);
    }

    [Fact]
    public void AnalyzeVolume_AssemblyLikeStep_FailsClearly()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/OCCT/as1.step");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "volume", stepPath, "--json"], stdout, stderr);
        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("assembly-like", doc.RootElement.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeVolume_MixedCurvedTrimmedBody_FailsClearly()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/boolean_box_sphere_cavity_basic.step");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "volume", stepPath, "--json"], stdout, stderr);
        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("unsupported non-planar face", doc.RootElement.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeVolume_ApproximateModeRequiresResolution()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/box_basic.step");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "volume", stepPath, "--approximate"], stdout, stderr);
        Assert.Equal(1, exitCode);
        Assert.Contains("--resolution", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("700")]
    [InlineData("-2")]
    public void AnalyzeVolume_Approximate_ResolutionValidation(string resolution)
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/box_basic.step");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "volume", stepPath, "--approximate", "--resolution", resolution, "--json"], stdout, stderr);
        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Contains("between 8 and 512", doc.RootElement.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeVolume_ApproximateBoxVolume_IsClose()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateBox(10d, 6d, 4d).Value, "cli-volume-approx-box");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "volume", stepPath, "--approximate", "--resolution", "32", "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("approximate").GetBoolean());
        Assert.False(root.GetProperty("exact").GetBoolean());
        Assert.Equal("voxel-approximation", root.GetProperty("method").GetString());
        Assert.InRange(root.GetProperty("volume").GetDouble(), 239d, 241d);
        Assert.Equal("conservative-outside", root.GetProperty("unknownPolicy").GetString());
    }

    [Fact]
    public void AnalyzeVolume_ApproximateCurvedMixedVolume_ReturnsNumericEstimate()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/boolean_box_cylinder_hole.step");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "volume", stepPath, "--approximate", "--resolution", "48", "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("volume").GetDouble() > 0d);
        Assert.True(doc.RootElement.TryGetProperty("unknownCount", out _));
        Assert.True(doc.RootElement.TryGetProperty("unknownRatio", out _));
        Assert.Equal("conservative-outside", doc.RootElement.GetProperty("unknownPolicy").GetString());
    }

    [Fact]
    public void AnalyzeVolume_JsonContractIncludesApproximationMetadata()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/boolean_box_cylinder_hole.step");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "volume", stepPath, "--approximate", "--resolution", "64", "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(root.GetProperty("approximate").GetBoolean());
        Assert.False(root.GetProperty("exact").GetBoolean());
        Assert.Equal(64, root.GetProperty("resolution").GetInt32());
        Assert.True(root.TryGetProperty("voxelSize", out _));
        Assert.True(root.TryGetProperty("occupiedCount", out _));
        Assert.True(root.TryGetProperty("totalCount", out _));
        Assert.True(root.TryGetProperty("unknownCount", out _));
        Assert.True(root.TryGetProperty("unknownRatio", out _));
        Assert.Equal("conservative-outside", root.GetProperty("unknownPolicy").GetString());
    }

    [Fact]
    public void AnalyzeCompare_IdenticalFile_HasZeroDeltas()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/box_basic.step");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "compare", stepPath, stepPath, "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(0d, root.GetProperty("bboxComparison").GetProperty("minDelta").GetProperty("x").GetDouble(), 8);
        Assert.Equal(0, root.GetProperty("topologyComparison").GetProperty("faceCount").GetProperty("delta").GetInt32());
    }

    [Fact]
    public void AnalyzeCompare_DifferentFiles_ShowsNonZeroDifferences()
    {
        var refPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/box_basic.step");
        var candPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/boolean_subtract_basic.step");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "compare", refPath, candPath, "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        Assert.NotEqual(0d, root.GetProperty("bboxComparison").GetProperty("maxDelta").GetProperty("x").GetDouble(), 8);
        Assert.True(root.GetProperty("surfaceFamilyComparison").TryGetProperty("plane", out _));
    }

    [Fact]
    public void AnalyzeCompare_FailureSide_IsStructured()
    {
        var refPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/does-not-exist.step");
        var candPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/box_basic.step");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "compare", refPath, candPath, "--json"], stdout, stderr);
        Assert.Equal(1, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.False(root.GetProperty("reference").GetProperty("success").GetBoolean());
        Assert.True(root.GetProperty("candidate").GetProperty("success").GetBoolean());
    }

    [Fact]
    public void Analyze_Command_Returns_Structured_Json_Failure()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var missingPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/does-not-exist.step");
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", missingPath, "--json"], stdout, stderr);
        Assert.Equal(1, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));

        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(Path.GetFullPath(missingPath), root.GetProperty("stepPath").GetString());
        Assert.True(root.GetProperty("error").GetString()?.Length > 0);
    }

    [Fact]
    public void Analyze_Command_SingleRootStep_StillReturnsSuccess()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/box_basic.step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", stepPath, "--json"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        Assert.Equal(Path.GetFullPath(stepPath), root.GetProperty("stepPath").GetString());
        Assert.True(root.TryGetProperty("summary", out _));
    }

    [Fact]
    public void Analyze_Command_Default_Output_Is_Human_Readable_Summary()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/box_basic.step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", stepPath], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        var text = stdout.ToString();
        Assert.Contains("Structural assessment:", text, StringComparison.Ordinal);
        Assert.Contains("Faces:", text, StringComparison.Ordinal);
        Assert.Contains("Edges:", text, StringComparison.Ordinal);
        Assert.Contains("Vertices:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{\"stepPath\":", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_Command_MultiRootStep_JsonFailure_IsAssemblyLikeAndMachineFriendly()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/OCCT/as1.step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", stepPath, "--json"], stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));

        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("assembly-like-step", root.GetProperty("errorKind").GetString());
        Assert.Equal("assembly-like", root.GetProperty("classification").GetString());
        Assert.True(root.GetProperty("rigidRootCount").GetInt32() > 1);
        Assert.Contains("assembly extraction/import", root.GetProperty("routeHint").GetString(), StringComparison.OrdinalIgnoreCase);

        var diagnostics = root.GetProperty("diagnostics");
        Assert.True(diagnostics.GetArrayLength() >= 1);
        var first = diagnostics[0];
        Assert.Equal("Importer.AssemblyLike.StepMultiRoot", first.GetProperty("source").GetString());
    }

    [Fact]
    public void Analyze_Command_MultiRootStep_Default_Output_Has_AssemblyLike_Guidance()
    {
        var stepPath = Path.Combine(RepoRoot, "testdata/step242/OCCT/as1.step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", stepPath], stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stdout.ToString()));
        var text = stderr.ToString();
        Assert.Contains("assembly-like STEP", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("assembly extraction/import workflow", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Canon_Command_RoundTrips_Supported_Step_Through_Importer_Exporter_Path()
    {
        var inputPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/box_basic.step");
        var outputPath = Path.Combine(Path.GetTempPath(), $"cli-canon-roundtrip-{Guid.NewGuid():N}.step");

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["canon", inputPath, "--out", outputPath], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());
        Assert.Contains("Canonical STEP written", stdout.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(outputPath));

        var imported = Step242Importer.ImportBody(File.ReadAllText(outputPath));
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(d => d.Message)));

        File.Delete(outputPath);
    }

    [Fact]
    public void Canon_Command_Json_Success_Contract_Is_Machine_Friendly()
    {
        var inputPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/box_basic.step");
        var outputPath = Path.Combine(Path.GetTempPath(), $"cli-canon-json-{Guid.NewGuid():N}.step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["canon", inputPath, "--out", outputPath, "--json"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(Path.GetFullPath(inputPath), root.GetProperty("inputPath").GetString());
        Assert.Equal(Path.GetFullPath(outputPath), root.GetProperty("outputPath").GetString());
        Assert.True(root.GetProperty("bodyCount").GetInt32() >= 1);
        Assert.True(root.GetProperty("shellCount").GetInt32() >= 1);

        File.Delete(outputPath);
    }


    [Fact]
    public void Canon_Command_Production_Mode_Preserves_Supported_Header_And_Product_Metadata()
    {
        var sourceText = File.ReadAllText(Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/box_basic.step"))
            .Replace("FILE_DESCRIPTION(('Aetheris AP242 subset export'),'2;1');", "FILE_DESCRIPTION(('Vendor production description'),'2;1');", StringComparison.Ordinal)
            .Replace("FILE_NAME('aetheris_export.step','1970-01-01T00:00:00',('Aetheris'),('Aetheris'),'Aetheris.Kernel','Aetheris.Kernel','');", "FILE_NAME('vendor-widget.stp','2024-05-06T07:08:09',('Vendor Author'),('Vendor Org'),'Vendor Preprocessor','Vendor CAD','Vendor Approval');", StringComparison.Ordinal)
            .Replace("PRODUCT('AETHERIS','AetherisBody','',", "PRODUCT('VENDOR-ID','Vendor Widget','Vendor Product Description',", StringComparison.Ordinal);
        var inputPath = Path.Combine(Path.GetTempPath(), $"cli-canon-production-input-{Guid.NewGuid():N}.step");
        var outputPath = Path.Combine(Path.GetTempPath(), $"cli-canon-production-output-{Guid.NewGuid():N}.step");
        File.WriteAllText(inputPath, sourceText);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["canon", inputPath, "--out", outputPath, "--mode", "production", "--json"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());
        var outputText = File.ReadAllText(outputPath);
        Assert.Contains("FILE_DESCRIPTION(('Vendor production description'),'2;1');", outputText, StringComparison.Ordinal);
        Assert.Contains("FILE_NAME('vendor-widget.stp','2024-05-06T07:08:09',('Vendor Author'),('Vendor Org'),'Vendor CAD','Vendor CAD','Vendor Approval');", outputText, StringComparison.Ordinal);
        Assert.Contains("PRODUCT('AETHERIS','Vendor Widget','Vendor Product Description',", outputText, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP('Vendor Widget'", outputText, StringComparison.Ordinal);
        Assert.True(Step242Importer.ImportBody(outputText).IsSuccess);
        Assert.NotEqual(sourceText, outputText);

        File.Delete(inputPath);
        File.Delete(outputPath);
    }

    [Fact]
    public void Canon_Command_Json_Failure_Contract_Reports_Missing_Input()
    {
        var missingPath = Path.Combine(RepoRoot, "testdata/step242/golden/firmament-v1/not-real.step");
        var outputPath = Path.Combine(Path.GetTempPath(), $"cli-canon-missing-{Guid.NewGuid():N}.step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["canon", missingPath, "--out", outputPath, "--json"], stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("missing-input", root.GetProperty("errorKind").GetString());
        Assert.Equal(Path.GetFullPath(missingPath), root.GetProperty("inputPath").GetString());
        Assert.Equal(Path.GetFullPath(outputPath), root.GetProperty("outputPath").GetString());
        Assert.Contains("not found", root.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_Command_Treats_Periodic_Seam_Coedge_Incidence_As_Enclosed()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateTorus(6d, 1d).Value, "cli-torus-structure");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", stepPath, "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var summary = doc.RootElement.GetProperty("summary");
        Assert.Equal("enclosed-manifold", summary.GetProperty("structuralAssessment").GetString());
        Assert.Contains("coedge incidence", summary.GetProperty("structuralAssessmentBasis").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    private static BrepBody RemoveFaceBinding(BrepBody source, IReadOnlyCollection<int> faceIdsToSkip)
    {
        var bindings = new BrepBindingModel();
        foreach (var edgeBinding in source.Bindings.EdgeBindings)
        {
            bindings.AddEdgeBinding(edgeBinding);
        }

        foreach (var faceBinding in source.Bindings.FaceBindings)
        {
            if (!faceIdsToSkip.Contains(faceBinding.FaceId.Value))
            {
                bindings.AddFaceBinding(faceBinding);
            }
        }

        var vertexPoints = source.Topology.Vertices
            .Where(vertex => source.TryGetVertexPoint(vertex.Id, out _))
            .ToDictionary(
                vertex => vertex.Id,
                vertex =>
                {
                    source.TryGetVertexPoint(vertex.Id, out var point);
                    return point;
                });

        return new BrepBody(source.Topology, source.Geometry, bindings, vertexPoints);
    }

    private static JsonElement AnalyzeSummary(string stepPath)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", stepPath, "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        return doc.RootElement.GetProperty("summary").Clone();
    }

    private sealed record AnalyzeFaceAttempt(int FaceId, int ExitCode, string Stdout, string Stderr, JsonElement? Face)
    {
        public bool Succeeded => ExitCode == 0 && Face.HasValue;
    }

    private static IReadOnlyList<JsonElement> AnalyzeExistingFaces(string stepPath, JsonElement summary)
    {
        var faceIds = summary.GetProperty("faceIds");
        var minFaceId = faceIds.GetProperty("min").GetInt32();
        var maxFaceId = faceIds.GetProperty("max").GetInt32();
        var expectedFaceCount = faceIds.GetProperty("count").GetInt32();
        var isContiguous = faceIds.GetProperty("contiguous").GetBoolean();

        var faces = new List<JsonElement>(expectedFaceCount);
        var failures = new List<AnalyzeFaceAttempt>();

        for (var faceId = minFaceId; faceId <= maxFaceId; faceId++)
        {
            var attempt = TryAnalyzeFace(stepPath, faceId);
            if (attempt.Succeeded)
            {
                faces.Add(attempt.Face!.Value);
            }
            else
            {
                failures.Add(attempt);
            }
        }

        if (faces.Count != expectedFaceCount || (isContiguous && failures.Count > 0))
        {
            Assert.Fail($"Expected to analyze {expectedFaceCount} existing face(s) from range {minFaceId}..{maxFaceId} " +
                $"(contiguous={isContiguous}) but analyzed {faces.Count}. Failures: {FormatAnalyzeFaceFailures(failures)}");
        }

        return faces;
    }

    private static JsonElement AnalyzeFace(string stepPath, int faceId)
    {
        var attempt = TryAnalyzeFace(stepPath, faceId);
        Assert.True(attempt.Succeeded,
            $"AnalyzeFace failed for face {faceId} in '{stepPath}' with exit code {attempt.ExitCode}. " +
            $"stdout: {attempt.Stdout} stderr: {attempt.Stderr}");
        return attempt.Face!.Value;
    }

    private static AnalyzeFaceAttempt TryAnalyzeFace(string stepPath, int faceId)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", stepPath, "--face", faceId.ToString(), "--json"], stdout, stderr);
        if (exitCode != 0)
        {
            return new AnalyzeFaceAttempt(faceId, exitCode, stdout.ToString(), stderr.ToString(), null);
        }

        using var doc = JsonDocument.Parse(stdout.ToString());
        return new AnalyzeFaceAttempt(faceId, exitCode, stdout.ToString(), stderr.ToString(), doc.RootElement.GetProperty("face").Clone());
    }

    private static string FormatAnalyzeFaceFailures(IEnumerable<AnalyzeFaceAttempt> failures)
        => string.Join("; ", failures.Select(failure =>
            $"face {failure.FaceId} exit {failure.ExitCode}, stdout: {failure.Stdout}, stderr: {failure.Stderr}"));

    private static JsonElement AnalyzeEdge(string stepPath, int edgeId)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", stepPath, "--edge", edgeId.ToString(), "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        return doc.RootElement.GetProperty("edge").Clone();
    }

    private static JsonDocument RunAnalyzeMap(string stepPath, string viewFlag, int rows, int cols)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", "map", stepPath, viewFlag, "--rows", rows.ToString(), "--cols", cols.ToString(), "--json"],
            stdout,
            stderr);
        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());
        return JsonDocument.Parse(stdout.ToString());
    }

    private static JsonDocument RunAnalyzeRayMap(string stepPath, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "map", stepPath, .. args, "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());
        return JsonDocument.Parse(stdout.ToString());
    }

    private static JsonDocument RunAnalyzeSixViewMap(string stepPath, string resolution, params string[] extraArgs)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "map", stepPath, "--views", "six", "--resolution", resolution, "--llm", .. extraArgs, "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());
        return JsonDocument.Parse(stdout.ToString());
    }

    private static JsonDocument RunAnalyzeSection(string stepPath, string planeFlag, double offset)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", "section", stepPath, planeFlag, "--offset", offset.ToString("G17"), "--json"],
            stdout,
            stderr);
        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());
        return JsonDocument.Parse(stdout.ToString());
    }


    private static BrepBody CreateCone(double bottomRadius, double topRadius, double height)
    {
        var result = BrepRevolve.Create(
            [new ProfilePoint2D(bottomRadius, 0d), new ProfilePoint2D(topRadius, height)],
            new ExtrudeFrame3D(Point3D.Origin, Direction3D.Create(new Vector3D(0d, 0d, 1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d))),
            new RevolveAxis3D(Point3D.Origin, new Vector3D(0d, 0d, 1d)));
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        return result.Value;
    }

    private static string ExportPrimitiveToTempStep(BrepBody body, string stem)
    {
        var export = Step242Exporter.ExportBody(body);
        Assert.True(export.IsSuccess, string.Join(Environment.NewLine, export.Diagnostics.Select(d => d.Message)));
        var outputPath = Path.Combine(Path.GetTempPath(), $"{stem}-{Guid.NewGuid():N}.step");
        File.WriteAllText(outputPath, export.Value);
        return outputPath;
    }

    private static string WriteSingleOpFirmamentFixture(string name, string op, double primary, double secondary, double heightOrDepth)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}.firmament");
        var source = op switch
        {            "box" => $"""
firmament:
  version: 1

model:
  name: {name}
  units: mm

ops[1]:
  -
    op: box
    id: body1
    size[3]:
      {primary}
      {secondary}
      {heightOrDepth}
""",
            "cylinder" => $"""
firmament:
  version: 1

model:
  name: {name}
  units: mm

ops[1]:
  -
    op: cylinder
    id: body1
    radius: {primary}
    height: {heightOrDepth}
""",
            "cone" => $"""
firmament:
  version: 1

model:
  name: {name}
  units: mm

ops[1]:
  -
    op: cone
    id: body1
    bottom_radius: {primary}
    top_radius: {secondary}
    height: {heightOrDepth}
""",
            _ => throw new InvalidOperationException($"Unsupported op '{op}'.")
        };

        File.WriteAllText(outputPath, source);
        return outputPath;
    }

    private static void AssertPoint(JsonElement point, double x, double y, double z)
    {
        Assert.Equal(x, point.GetProperty("x").GetDouble(), 8);
        Assert.Equal(y, point.GetProperty("y").GetDouble(), 8);
        Assert.Equal(z, point.GetProperty("z").GetDouble(), 8);
    }

    private static void AssertVector(JsonElement vector, double x, double y, double z)
    {
        Assert.Equal(x, vector.GetProperty("x").GetDouble(), 8);
        Assert.Equal(y, vector.GetProperty("y").GetDouble(), 8);
        Assert.Equal(z, vector.GetProperty("z").GetDouble(), 8);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aetheris-cli-asm-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root for CLI tests.");
    }
}
