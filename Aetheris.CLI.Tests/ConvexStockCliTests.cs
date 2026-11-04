using System.Text.Json;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class ConvexStockCliTests
{
    public static TheoryData<string, int> Accepted => new()
    {
        { "box-rounded", 26 }, { "ellipsoid", 8 },
        { "polyhedron-4", 4 }, { "polyhedron-6", 6 }, { "polyhedron-8", 8 }, { "polyhedron-12", 12 }, { "polyhedron-20", 20 },
        { "polyhedron-4-rounded", 14 }, { "polyhedron-6-rounded", 26 }, { "polyhedron-8-rounded", 26 },
        { "polyhedron-12-rounded", 62 }, { "polyhedron-20-rounded", 62 },
        // Every edge of a body built first: rim tori, line strips, sphere corners.
        { "../UniformRounding/cylinder-rounded", 5 }, { "../UniformRounding/frustum-rounded", 5 },
        { "../UniformRounding/box-hole-rounded", 29 }, { "../UniformRounding/d-profile-rounded", 14 },
        // Concave edges too.
        { "../UniformRounding/bushing-rounded", 12 }, { "../UniformRounding/counterbore-rounded", 33 },
        { "../UniformRounding/blind-hole-rounded", 30 },
        // Tangent junctions, and a corner where a concave edge meets convex ones.
        { "../UniformRounding/rounded-rect-rounded", 26 }, { "../UniformRounding/ell-rounded", 38 }
    };

    [Theory]
    [MemberData(nameof(Accepted))]
    public void ValidateBuildAndMeshUseTheActualStock(string fixture, int faces)
    {
        WithSource(Fixture(fixture), (path, output) =>
        {
            foreach (var command in new[] { "validate", "build" })
            {
                var stdout = new StringWriter(); var stderr = new StringWriter();
                var args = command == "validate" ? new[] { command, path, "--json" } : new[] { command, path, "--out", output, "--json" };
                Assert.True(CliRunner.Run(args, stdout, stderr) == 0, stdout + stderr.ToString());
            }
            var imported = Step242Importer.ImportBody(File.ReadAllText(output));
            Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics));
            Assert.Equal(faces, imported.Value.Topology.Faces.Count());
            var mesh = new StringWriter(); var error = new StringWriter();
            Assert.True(CliRunner.Run(["mesh", output, "--format", "obj", "--output", output + ".obj", "--json"], mesh, error) == 0, mesh + error.ToString());
            using var json = JsonDocument.Parse(mesh.ToString()); var report = json.RootElement;
            Assert.True(report.GetProperty("watertight").GetBoolean(), mesh.ToString());
            Assert.True(report.GetProperty("connected").GetBoolean(), mesh.ToString());
            Assert.True(report.GetProperty("outwardOriented").GetBoolean(), mesh.ToString());
        });
    }

    [Theory]
    [InlineData("Ellipsoid stock { radii: [18mm, 12mm, 25mm]; heigth: 4mm }", "", "field-unknown")]
    [InlineData("Polyhedron<10> stock { edgeLength: 20mm }", "", "polyhedron-face-count-unsupported")]
    [InlineData("Polyhedron<6> stock { edgeLength: 20 }", "", "field-value-invalid")]
    [InlineData("Ellipsoid stock { radii: [18mm, 0mm, 25mm] }", "", "ellipsoid-radii-invalid")]
    [InlineData("Cylinder stock { radius: 10mm; height: 20mm }", "EdgeFinish<Fillet> rounding { target: AllEdges; radius: 12mm }", "uniform-rounding-radius-too-large")]
    [InlineData("Ellipsoid stock { radii: [18mm, 12mm, 25mm] }", "EdgeFinish<Fillet> rounding { target: AllEdges; radius: 1mm }", "uniform-rounding-elliptical-edge-unsupported")]
    [InlineData("Box stock { size: [20mm, 20mm, 20mm] }", "EdgeFinish<Fillet> rounding { target: AllEdges; radius: 10mm }", "UniformFilletRadiusTooLarge")]
    [InlineData("Box stock { size: [20mm, 20mm, 20mm] }", "EdgeFinish<Fillet> rounding { target: AllEdges; face: [1, 2]; radius: 1mm }", "field-value-invalid")]
    [InlineData("Box stock { size: [20mm, 20mm, 20mm] }", "EdgeFinish<Fillet> rounding { target: AllEdges; kind: 9; radius: 1mm }", "field-value-invalid")]
    [InlineData("Box stock { size: [20mm, 20mm, 20mm] }", "EdgeFinish<Fillet> rounding { target: AllEdges; distance: 1mm }", "field-value-invalid")]
    [InlineData("Box stock { size: [20mm, 20mm, 20mm] }", "EdgeFinish<Chamfer> rounding { target: AllEdges; distance: 1mm }", "uniform-stock-fillet-unsupported")]
    [InlineData("Box stock { size: [20mm, 20mm, 20mm] }", "EdgeFinish<Fillet> rounding { target: AllEdges; radius: 1mm } Hole<Shaft> H { on: +Z; center: [5mm, 0mm]; diameter: 6mm; end: ThroughAll }", "uniform-rounding-radius-too-large")]
    public void UnsupportedAndInvalidIntentCannotProduceAnArtifact(string stock, string operation, string code)
    {
        var source = "Model Rejected {\n units: mm\n " + stock + "\n" + (operation.Length == 0 ? "" : " Modify stock {\n " + operation + "\n }\n") + "}";
        WithSource(source, (path, output) =>
        {
            foreach (var command in new[] { "validate", "build" })
            {
                var stdout = new StringWriter(); var stderr = new StringWriter();
                var args = command == "validate" ? new[] { command, path, "--json" } : new[] { command, path, "--out", output, "--json" };
                Assert.NotEqual(0, CliRunner.Run(args, stdout, stderr));
                using var json = JsonDocument.Parse(stdout.ToString());
                var report = command == "validate" ? json.RootElement.GetProperty("firmamentV2Validation") : json.RootElement;
                Assert.Contains(report.GetProperty("diagnostics").EnumerateArray(), d =>
                    (d.GetProperty("code").GetString()!.Contains(code, StringComparison.Ordinal) || d.GetProperty("message").GetString()!.Contains(code, StringComparison.Ordinal))
                    && d.GetProperty("line").GetInt32() > 1);
            }
            Assert.False(File.Exists(output));
        });
    }

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(Root(), "fixtures/Regression/ConvexStock", name + ".firmament"));
    private static void WithSource(string source, Action<string, string> assertion)
    {
        var directory = Path.Combine(Root(), "artifacts/local/tests/convex-stock", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "source.firmament"); File.WriteAllText(path, source);
            assertion(path, Path.Combine(directory, "body.step"));
        }
        finally { Directory.Delete(directory, true); }
    }
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
