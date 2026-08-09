using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class MeshObjCommandTests
{
    [Fact]
    public void MeshObj_HexBolt_ExportsStructuredPolygonsDirectlyFromSurfaceMeshIr()
    {
        var root = FindRepoRoot();
        var output = Path.Combine(Path.GetTempPath(), $"aetheris-hexbolt-{Guid.NewGuid():N}.obj");
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exit = Aetheris.CLI.CliRunner.Run(
                ["mesh", Path.Combine(root, "testdata/firmament/examples/mcmaster_91180a151_threadless_hex_bolt.step"), "--format", "obj", "--output", output, "--json"], stdout, stderr);

            Assert.Equal(0, exit);
            Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
            using var result = JsonDocument.Parse(stdout.ToString());
            Assert.Equal("obj", result.RootElement.GetProperty("format").GetString());
            Assert.Equal(896, result.RootElement.GetProperty("quadCount").GetInt32());
            Assert.Equal(77, result.RootElement.GetProperty("triangleCount").GetInt32());
            Assert.True(result.RootElement.GetProperty("quadPercentage").GetDouble() > 90d);
            var obj = File.ReadAllText(output);
            Assert.Contains("vt ", obj);
            Assert.Contains("vn ", obj);
            Assert.Contains("f ", obj);
            Assert.Contains("/", obj); // OBJ corners carry attribute identity separately from v identity.
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void MeshObj_Ctc01_SamplesAllForeignSplineTrimsAndExportsWatertightStructuredMesh()
    {
        var root = FindRepoRoot();
        var output = Path.Combine(Path.GetTempPath(), $"aetheris-ctc01-{Guid.NewGuid():N}.obj");
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exit = Aetheris.CLI.CliRunner.Run(
                ["mesh", Path.Combine(root, "testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp"), "--format", "obj", "--output", output, "--json"], stdout, stderr);

            Assert.Equal(0, exit);
            Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
            using var result = JsonDocument.Parse(stdout.ToString());
            Assert.Equal(117, result.RootElement.GetProperty("patchCount").GetInt32());
            Assert.Equal(14, result.RootElement.GetProperty("sampledBSplineTrimCount").GetInt32());
            Assert.Equal(14, result.RootElement.GetProperty("trimResolutions").GetArrayLength());
            Assert.True(result.RootElement.GetProperty("watertight").GetBoolean());
            Assert.Equal(0, result.RootElement.GetProperty("crackCount").GetInt32());
            Assert.Equal(0, result.RootElement.GetProperty("nonManifoldEdgeCount").GetInt32());
            // Concave spline-trim fallbacks are deliberately constrained
            // triangles; structured four-sided regions still retain quads.
            Assert.True(result.RootElement.GetProperty("quadCount").GetInt32() > 8300);
            Assert.Equal(2766, result.RootElement.GetProperty("triangleCount").GetInt32());
            Assert.True(File.Exists(output));
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
