using System.Text.Json;

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
    }

    [Fact]
    public void Analyze_Command_Supports_Face_Detail()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", Path.Combine(RepoRoot, "testdata/firmament/exports/box_basic.step"), "--face", "1", "--json"],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var face = doc.RootElement.GetProperty("face");
        Assert.Equal(1, face.GetProperty("faceId").GetInt32());
        Assert.Equal("Plane", face.GetProperty("surfaceType").GetString());
        Assert.True(face.GetProperty("adjacentEdgeIds").GetArrayLength() >= 4);
    }

    [Fact]
    public void Analyze_Command_Supports_Edge_Detail()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(
            ["analyze", Path.Combine(RepoRoot, "testdata/firmament/exports/box_basic.step"), "--edge", "1", "--json"],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var edge = doc.RootElement.GetProperty("edge");
        Assert.Equal(1, edge.GetProperty("edgeId").GetInt32());
        Assert.Equal("Line3", edge.GetProperty("curveType").GetString());
        Assert.True(edge.GetProperty("adjacentFaceIds").GetArrayLength() >= 1);
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
