using System.Text.Json;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.CLI.Tests;

public sealed class GenericSculptingCliTests
{
    [Theory]
    [InlineData("box-cage", "stl")]
    [InlineData("sphere-cage", "stl")]
    [InlineData("box-cage", "glb")]
    [InlineData("sphere-cage", "glb")]
    public void RealMeshExportsCarryActualGeometryAndReplayableProvenance(string fixture, string format)
    {
        WithSource(fixture, s => s, (source, directory) =>
        {
            var output = Path.Combine(directory, "sculpt." + format); var stdout = new StringWriter(); var stderr = new StringWriter();
            Assert.True(CliRunner.Run(["validate", source, "--json"], stdout, stderr) == 0, stdout + stderr.ToString());
            using (var validation = JsonDocument.Parse(stdout.ToString()))
            {
                var v = validation.RootElement.GetProperty("firmamentV2Validation"); Assert.Equal("MeshBuilt", v.GetProperty("validationLevel").GetString()); Assert.False(v.GetProperty("stepExportAvailable").GetBoolean());
            }
            stdout.GetStringBuilder().Clear();
            Assert.True(CliRunner.Run(["mesh", source, "--format", format, "--output", output, "--json"], stdout, stderr) == 0, stdout + stderr.ToString());
            using var report = JsonDocument.Parse(stdout.ToString()); var root = report.RootElement; Assert.True(root.GetProperty("success").GetBoolean());
            var triangleCount = root.GetProperty("triangles").GetInt32(); Assert.True(triangleCount > 12); Assert.False(root.GetProperty("stepExportAvailable").GetBoolean());
            using var provenance = JsonDocument.Parse(File.ReadAllText(output + ".sculpt.json"));
            var replay = Aetheris.Surfacing.MeshConstructionReplayer.Replay(provenance.RootElement.GetProperty("construction").GetRawText());
            Assert.Equal(root.GetProperty("stateId").GetString(), replay.StateId);
            if (format == "stl")
            {
                var bytes = File.ReadAllBytes(output); Assert.Equal(triangleCount, BitConverter.ToInt32(bytes, 80)); Assert.Equal(84 + 50 * triangleCount, bytes.Length);
                var indices = new List<int>(); var positions = new List<Point3D>(); var ids = new Dictionary<Point3D, int>();
                for (var i = 0; i < triangleCount; i++) for (var j = 0; j < 3; j++)
                {
                    var at = 84 + 50 * i + 12 + 12 * j; var p = new Point3D(BitConverter.ToSingle(bytes, at), BitConverter.ToSingle(bytes, at + 4), BitConverter.ToSingle(bytes, at + 8));
                    if (!ids.TryGetValue(p, out var id)) { id = positions.Count; ids.Add(p, id); positions.Add(p); }
                    indices.Add(id);
                }
                var mesh = new TriangleMesh(positions, [], indices, new HashSet<(int, int)>(), "");
                Assert.True(TriangleMeshValidator.TryValidateClosed(mesh, out var topology, out var failure), failure); Assert.True(topology.SignedVolume > 0);
            }
            else
            {
                var bytes = File.ReadAllBytes(output); Assert.Equal(0x46546C67u, BitConverter.ToUInt32(bytes, 0)); Assert.Equal(bytes.Length, BitConverter.ToInt32(bytes, 8));
                using var gltf = JsonDocument.Parse(bytes.AsMemory(20, BitConverter.ToInt32(bytes, 12)));
                Assert.Equal("2.0", gltf.RootElement.GetProperty("asset").GetProperty("version").GetString());
                Assert.Contains(gltf.RootElement.GetProperty("accessors").EnumerateArray(), a => a.GetProperty("count").GetInt32() == triangleCount * 3);
                var positionIndex = gltf.RootElement.GetProperty("meshes")[0].GetProperty("primitives")[0].GetProperty("attributes").GetProperty("POSITION").GetInt32();
                var accessor = gltf.RootElement.GetProperty("accessors")[positionIndex];
                var points = replay.Cage.Vertices.Select(v => v.Position).ToArray();
                Assert.Equal(points.Min(p => p.X), accessor.GetProperty("min")[0].GetDouble(), 4);
                Assert.Equal(points.Max(p => p.Z), accessor.GetProperty("max")[2].GetDouble(), 4);
            }
        });
    }

    [Theory]
    [InlineData("preserve: [v1]", "preserve: [v0]", "mesh-preservation-failed")]
    [InlineData("vertices: [v0]", "vertices: [typo]", "mesh-vertex-unresolved")]
    [InlineData("levels: 2", "levels: 99", "mesh-subdivision-budget-invalid")]
    [InlineData("maximumDeviation: 0.01mm", "maximumDeviation: 0.01mm; heigth: 3mm", "field-unknown")]
    [InlineData("mode: Smooth", "mode: Jelly", "mesh-crease-mode-invalid")]
    [InlineData("output: edited", "MeshState wide { input: edited; Scale { vertices: [All]; factor: [1e40, 1e40, 1e40]; origin: [0mm, 0mm, 0mm] } } output: wide", "mesh-export-precision-exhausted")]
    [InlineData("output: edited", "MeshState wide { input: edited; Scale { vertices: [All]; factor: [1e8, 1e8, 1e8]; origin: [0mm, 0mm, 0mm] } } output: wide", "mesh-export-precision-budget-exceeded")]
    [InlineData("Move {", "Relax {", "mesh-operation-cardinality")]
    [InlineData("offset: [0mm, 0mm, 1mm]", "offset: [0mm, 0mm, 100mm]", "mesh-local-foldover")]
    [InlineData("input: refined; preserve:", "input: exact; preserve:", "mesh-predecessor-unresolved")]
    public void InvalidMeshIntentRejectsInValidateAndBuildWithoutArtifacts(string old, string replacement, string code)
    {
        WithSource("box-cage", s => s.Replace(old, replacement), (source, directory) =>
        {
            var output = Path.Combine(directory, "rejected.glb");
            foreach (var args in new[] { new[] { "validate", source, "--json" }, new[] { "build", source, "--out", output, "--json" } })
            {
                var stdout = new StringWriter(); var stderr = new StringWriter(); Assert.True(CliRunner.Run(args, stdout, stderr) == 1, stdout + stderr.ToString());
                using var json = JsonDocument.Parse(stdout.ToString()); var root = json.RootElement; if (args[0] == "validate") root = root.GetProperty("firmamentV2Validation");
                Assert.Contains(root.GetProperty("diagnostics").EnumerateArray(), d => d.GetProperty("code").GetString()!.Contains(code, StringComparison.Ordinal) && d.GetProperty("line").GetInt32() > 1);
            }
            Assert.False(File.Exists(output)); Assert.False(File.Exists(output + ".sculpt.json"));
        });
    }

    [Fact]
    public void AValidMeshCannotBeExportedAsStepOrOverwriteItsSource()
    {
        WithSource("box-cage", s => s, (source, directory) =>
        {
            var original = File.ReadAllText(source);
            foreach (var output in new[] { Path.Combine(directory, "invalid.step"), source })
            {
                var stdout = new StringWriter(); Assert.Equal(1, CliRunner.Run(["build", source, "--out", output, "--json"], stdout, new StringWriter()));
                using var json = JsonDocument.Parse(stdout.ToString()); Assert.False(json.RootElement.GetProperty("success").GetBoolean());
            }
            Assert.Equal(original, File.ReadAllText(source)); Assert.False(File.Exists(Path.Combine(directory, "invalid.step")));
        });
    }

    private static void WithSource(string fixture, Func<string, string> transform, Action<string, string> assertion)
    {
        var p = new DirectoryInfo(AppContext.BaseDirectory); while (p is not null && !File.Exists(Path.Combine(p.FullName, "Aetheris.slnx"))) p = p.Parent;
        var directory = Path.Combine(p!.FullName, "artifacts/local/tests/generic-sculpting", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try { var source = Path.Combine(directory, "source.firmament"); File.WriteAllText(source, transform(File.ReadAllText(Path.Combine(p.FullName, "fixtures/Regression/GenericSculpting/" + fixture + ".firmament")))); assertion(source, directory); }
        finally { Directory.Delete(directory, true); }
    }
}
