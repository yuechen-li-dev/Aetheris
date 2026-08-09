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
            Assert.Equal(905, result.RootElement.GetProperty("quadCount").GetInt32());
            Assert.Equal(7, result.RootElement.GetProperty("triangleCount").GetInt32());
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
            // M7 confines dense authoritative trims to local bands and bridges
            // while retaining the structured curved-support quads.
            Assert.Equal(8416, result.RootElement.GetProperty("quadCount").GetInt32());
            Assert.Equal(505, result.RootElement.GetProperty("triangleCount").GetInt32());
            var planar = result.RootElement.GetProperty("planarAudit");
            Assert.Equal(56, planar.GetProperty("faceCount").GetInt32());
            Assert.Equal(181, planar.GetProperty("triangleCount").GetInt32());
            Assert.Equal(702, planar.GetProperty("ngonCount").GetInt32());
            Assert.Equal(552, planar.GetProperty("featureBandCellCount").GetInt32());
            Assert.Equal(52, planar.GetProperty("bridgeCellCount").GetInt32());
            Assert.Equal(0, planar.GetProperty("m6FallbackFaceCount").GetInt32());
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
