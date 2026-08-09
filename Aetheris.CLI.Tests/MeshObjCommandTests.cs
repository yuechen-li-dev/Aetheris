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
    public void MeshObj_Ctc01_ReportsItsFirstUnsupportedGenericCurveInsteadOfFallingBack()
    {
        var root = FindRepoRoot();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = Aetheris.CLI.CliRunner.Run(
            ["mesh", Path.Combine(root, "testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp"), "--format", "obj", "--json"], stdout, stderr);

        Assert.Equal(1, exit);
        using var result = JsonDocument.Parse(stdout.ToString());
        var diagnostic = result.RootElement.GetProperty("diagnostics")[0].GetString();
        Assert.Contains("edge 87", diagnostic, StringComparison.Ordinal);
        Assert.Contains("BSpline3", diagnostic, StringComparison.Ordinal);
        var coverage = result.RootElement.GetProperty("coverage");
        Assert.Equal(117, coverage.GetProperty("analyticSupportFaceCount").GetInt32());
        Assert.Equal(14, coverage.GetProperty("boundaryBlockers").GetArrayLength());
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
