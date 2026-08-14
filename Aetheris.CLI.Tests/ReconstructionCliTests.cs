using System.Text.Json;
using Xunit;

namespace Aetheris.CLI.Tests;

public sealed class ReconstructionCliTests
{
    [Fact]
    public void Reconstruction_help_is_explicitly_experimental_and_approximate()
    {
        var output = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["reconstruct", "--help"], output, new StringWriter()));
        Assert.Contains("Experimentally", output.ToString()); Assert.Contains("not CAD feature", output.ToString());
    }

    [Fact]
    public void Reconstruction_writes_obj_compact_report_and_error_samples()
    {
        var root = Path.Combine(Path.GetTempPath(), "aetheris-reconstruct-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "sheet.ply"); var output = Path.Combine(root, "sheet.obj");
            var report = Path.Combine(root, "report.json"); var error = Path.Combine(root, "error.ply");
            File.WriteAllText(input, """
                ply
                format ascii 1.0
                element vertex 4
                property float x
                property float y
                property float z
                element face 2
                property list uchar int vertex_indices
                end_header
                0 0 0
                1 0 0
                1 1 0.1
                0 1 0.1
                3 0 1 2
                3 0 2 3
                """);
            var stdout = new StringWriter(); var stderr = new StringWriter();
            var exit = CliRunner.Run(["reconstruct", "mesh", input, "--mode", "fast", "--out", output,
                "--report", report, "--error-ply", error, "--json"], stdout, stderr);
            Assert.Equal(0, exit); Assert.Empty(stderr.ToString()); Assert.True(File.Exists(output)); Assert.True(File.Exists(report)); Assert.True(File.Exists(error));
            Assert.Contains("f 1 2 3 4", File.ReadAllText(output));
            using var json = JsonDocument.Parse(stdout.ToString());
            Assert.True(json.RootElement.GetProperty("experimental").GetBoolean());
            Assert.Equal("Success", json.RootElement.GetProperty("status").GetString());
            Assert.Equal(1, json.RootElement.GetProperty("statistics").GetProperty("topology").GetProperty("boundaryLoops").GetInt32());
        }
        finally { Directory.Delete(root, true); }
    }
}
