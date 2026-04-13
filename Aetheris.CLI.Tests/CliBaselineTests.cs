using System.Text.Json;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class CliBaselineTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Build_Command_Builds_Firmament_To_Step()
    {
        var outputPath = Path.Combine(RepoRoot, "testdata", "firmament", "exports", "cli-build-probe.step");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["build", Path.Combine(RepoRoot, "testdata/firmament/examples/box_basic.firmament"), "--out", outputPath],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath), stderr.ToString());
        Assert.Contains("Build succeeded", stdout.ToString());

        File.Delete(outputPath);
    }

    [Fact]
    public void Analyze_Command_Reports_Summary_Facts_And_Discoverability()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", Path.Combine(RepoRoot, "testdata/firmament/exports/box_basic.step"), "--json"],
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

        var lineEdge = AnalyzeEdge(stepPath, 1);
        Assert.Equal(1, lineEdge.GetProperty("edgeId").GetInt32());
        Assert.Equal("Line3", lineEdge.GetProperty("curveType").GetString());
        Assert.Equal(8d, lineEdge.GetProperty("arcLength").GetDouble(), 8);
        Assert.Equal(8d, lineEdge.GetProperty("parameterRange").GetDouble(), 8);
        Assert.Equal("computed", lineEdge.GetProperty("arcLengthStatus").GetString());

        var circleEdge = AnalyzeEdge(stepPath, 2);
        Assert.Equal("Circle3", circleEdge.GetProperty("curveType").GetString());
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
            ["analyze", Path.Combine(RepoRoot, "testdata/firmament/exports/box_basic.step"), "--vertex", "1", "--json"],
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
    public void Analyze_Command_Provides_Cylinder_Sphere_And_Torus_Anchors_With_Sphere_Axis_Omitted()
    {
        var cylinderStepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateCylinder(4d, 12d).Value, "cli-cylinder-face-anchor");
        var sphereStepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateSphere(3d).Value, "cli-sphere-face-anchor");
        var torusStepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateTorus(7d, 2d).Value, "cli-torus-face-anchor");

        var cylinderFace = AnalyzeFace(cylinderStepPath, 1);
        Assert.Equal("Cylinder", cylinderFace.GetProperty("surfaceType").GetString());
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
    public void Build_And_Analyze_TriangularPrism_Example_Reports_IsoscelesContractBounds()
    {
        var outputPath = Path.Combine(RepoRoot, "testdata", "firmament", "exports", "cli-triangular-prism-contract.step");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var buildStdout = new StringWriter();
        var buildStderr = new StringWriter();
        var buildExitCode = Aetheris.CLI.CliRunner.Run(
            ["build", Path.Combine(RepoRoot, "testdata/firmament/examples/triangular_prism_basic.firmament"), "--out", outputPath],
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
        var outputPath = Path.Combine(RepoRoot, "testdata", "firmament", "exports", "cli-triangular-prism-corner-chamfer-e4.step");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var buildStdout = new StringWriter();
        var buildStderr = new StringWriter();
        var buildExitCode = Aetheris.CLI.CliRunner.Run(
            ["build", Path.Combine(RepoRoot, "testdata/firmament/examples/m5a_chamfer_triangular_prism_corner_e4_basic.firmament"), "--out", outputPath],
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
        var outputPath = Path.Combine(RepoRoot, "testdata", "firmament", "exports", "cli-concave-edge-chamfer-e7b.step");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var buildStdout = new StringWriter();
        var buildStderr = new StringWriter();
        var buildExitCode = Aetheris.CLI.CliRunner.Run(
            ["build", Path.Combine(RepoRoot, "testdata/firmament/fixtures/e7-valid-chamfer-concave-overlap-lroot.firmament"), "--out", outputPath],
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
    public void Analyze_Command_Returns_Structured_Json_Failure()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var missingPath = Path.Combine(RepoRoot, "testdata/firmament/exports/does-not-exist.step");
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

    private static JsonElement AnalyzeFace(string stepPath, int faceId)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", stepPath, "--face", faceId.ToString(), "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        return doc.RootElement.GetProperty("face").Clone();
    }

    private static JsonElement AnalyzeEdge(string stepPath, int edgeId)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", stepPath, "--edge", edgeId.ToString(), "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        return doc.RootElement.GetProperty("edge").Clone();
    }

    private static string ExportPrimitiveToTempStep(BrepBody body, string stem)
    {
        var export = Step242Exporter.ExportBody(body);
        Assert.True(export.IsSuccess, string.Join(Environment.NewLine, export.Diagnostics.Select(d => d.Message)));
        var outputPath = Path.Combine(Path.GetTempPath(), $"{stem}-{Guid.NewGuid():N}.step");
        File.WriteAllText(outputPath, export.Value);
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
