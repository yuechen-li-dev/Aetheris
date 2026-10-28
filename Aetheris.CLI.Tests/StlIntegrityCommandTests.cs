using System.Text.Json;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.CLI.Tests;

public sealed class StlIntegrityCommandTests
{
    [Fact]
    public void Ctc01SerializedStlHasOppositeEdgeUsesAndOriginIndependentVolume()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        var folder = Path.Combine(root!.FullName, "artifacts/local/stl-integrity/tests"); Directory.CreateDirectory(folder);
        var output = Path.Combine(folder, Guid.NewGuid() + ".stl");
        try
        {
            var stdout = new StringWriter(); var stderr = new StringWriter();
            // A real CLI import/serialization is required here: checking the
            // memoized BRep or the production validator cannot qualify STL bytes.
            var exit = Aetheris.CLI.CliRunner.Run(["mesh", Path.Combine(root.FullName,
                "testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp"), "--format", "stl", "--output", output, "--json"], stdout, stderr);
            Assert.True(exit == 0, stdout + stderr.ToString());
            using var json = JsonDocument.Parse(stdout.ToString()); var report = json.RootElement;
            Assert.True(report.GetProperty("watertight").GetBoolean());
            Assert.True(report.GetProperty("consistentlyOriented").GetBoolean());
            Assert.Equal(0, report.GetProperty("orientationConflictEdgeCount").GetInt32());

            using var reader = new BinaryReader(File.OpenRead(output));
            reader.ReadBytes(80); var count = reader.ReadUInt32();
            Assert.Equal(84L + 50L * count, reader.BaseStream.Length);
            var triangles = new List<Point3D[]>();
            var edges = new Dictionary<(Point3D, Point3D), int>();
            for (var i = 0; i < count; i++)
            {
                reader.ReadBytes(12); var triangle = new[] { ReadPoint(), ReadPoint(), ReadPoint() }; reader.ReadUInt16();
                triangles.Add(triangle);
                for (var j = 0; j < 3; j++)
                {
                    var edge = (triangle[j], triangle[(j + 1) % 3]); edges[edge] = edges.GetValueOrDefault(edge) + 1;
                }
            }
            Assert.All(edges, pair => Assert.Equal(pair.Value, edges.GetValueOrDefault((pair.Key.Item2, pair.Key.Item1))));
            var volumes = new[] { new Point3D(0, 0, 0), new Point3D(1000, 2000, 3000), new Point3D(-1000, -2000, -3000) }
                .Select(origin => triangles.Sum(t => (t[0] - origin).Dot((t[1] - origin).Cross(t[2] - origin)) / 6)).ToArray();
            Assert.All(volumes, volume => Assert.InRange(double.Abs(volume - report.GetProperty("meshSignedVolumeMm3").GetDouble()), 0, .01));
            Assert.InRange(volumes[0], 14_640_000, 14_655_000);
            Point3D ReadPoint() => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
        finally { if (File.Exists(output)) File.Delete(output); }
    }
}
