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
    public void Analyze_Command_Reports_Summary_Facts()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", Path.Combine(RepoRoot, "testdata/firmament/exports/box_basic.step"), "--json"],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        var summary = root.GetProperty("summary");
        Assert.Equal(1, summary.GetProperty("bodyCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("shellCount").GetInt32());
        Assert.Equal(6, summary.GetProperty("faceCount").GetInt32());
        Assert.Equal(12, summary.GetProperty("edgeCount").GetInt32());
        Assert.Equal(8, summary.GetProperty("vertexCount").GetInt32());
        Assert.Equal("enclosed-manifold", summary.GetProperty("structuralAssessment").GetString());
        Assert.Equal(0, summary.GetProperty("surfaceFamilies").GetProperty("bspline").GetInt32());
    }

    [Fact]
    public void Analyze_Command_Provides_Numeric_Face_Detail_Anchors()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateCylinder(5d, 12d).Value, "cli-cylinder-face-truth");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", stepPath, "--face", "2", "--json"],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var face = doc.RootElement.GetProperty("face");
        Assert.Equal(2, face.GetProperty("faceId").GetInt32());
        Assert.Equal("Plane", face.GetProperty("surfaceType").GetString());
        AssertPoint(face.GetProperty("anchorPoint"), 0d, 0d, 6d);
        AssertVector(face.GetProperty("planarNormal"), 0d, 0d, 1d);
        Assert.Single(face.GetProperty("adjacentEdgeIds").EnumerateArray());
    }

    [Fact]
    public void Analyze_Command_Reports_Truthful_Edge_Length_Fields()
    {
        var stepPath = ExportPrimitiveToTempStep(BrepPrimitives.CreateCylinder(5d, 8d).Value, "cli-cylinder-edge-truth");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", stepPath, "--edge", "1", "--json"],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var edge = doc.RootElement.GetProperty("edge");
        Assert.Equal(1, edge.GetProperty("edgeId").GetInt32());
        Assert.Equal("Line3", edge.GetProperty("curveType").GetString());
        Assert.Equal(8d, edge.GetProperty("arcLength").GetDouble(), 8);
        Assert.Equal(8d, edge.GetProperty("parameterRange").GetDouble(), 8);

        stdout.GetStringBuilder().Clear();
        stderr.GetStringBuilder().Clear();
        exitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", stepPath, "--edge", "2", "--json"],
            stdout,
            stderr);
        Assert.Equal(0, exitCode);
        using var circleDoc = JsonDocument.Parse(stdout.ToString());
        var circleEdge = circleDoc.RootElement.GetProperty("edge");
        Assert.Equal("Circle3", circleEdge.GetProperty("curveType").GetString());
        Assert.Equal(2d * double.Pi, circleEdge.GetProperty("parameterRange").GetDouble(), 8);
        Assert.Equal(10d * double.Pi, circleEdge.GetProperty("arcLength").GetDouble(), 8);
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
    public void Analyze_Command_Provides_Cylinder_Sphere_And_Torus_Anchors()
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
        AssertVector(sphereFace.GetProperty("axis"), 0d, 0d, 1d);
        Assert.Equal(3d, sphereFace.GetProperty("radius").GetDouble(), 8);

        var torusFace = AnalyzeFace(torusStepPath, 1);
        Assert.Equal("Torus", torusFace.GetProperty("surfaceType").GetString());
        AssertPoint(torusFace.GetProperty("anchorPoint"), 0d, 0d, 0d);
        AssertVector(torusFace.GetProperty("axis"), 0d, 1d, 0d);
        Assert.Equal(7d, torusFace.GetProperty("majorRadius").GetDouble(), 8);
        Assert.Equal(2d, torusFace.GetProperty("minorRadius").GetDouble(), 8);
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

    private static JsonElement AnalyzeFace(string stepPath, int faceId)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", stepPath, "--face", faceId.ToString(), "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        return doc.RootElement.GetProperty("face").Clone();
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
