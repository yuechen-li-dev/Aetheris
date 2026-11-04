using System.Text.Json;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Surfacing;

namespace Aetheris.CLI.Tests;

public sealed class WireframeCliTests
{
    [Fact]
    public void WireframeCommandRendersImportedTrimmedBrepDeterministically()
    {
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-wireframe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var body = SectionChainMaterializer.Materialize(SectionChainTemplates.ErgonomicFairingG1()).Body!;
            var step = Step242Exporter.ExportBody(body);
            Assert.True(step.IsSuccess);
            var input = Path.Combine(directory, "model.step"); var first = Path.Combine(directory, "first.svg"); var second = Path.Combine(directory, "second.svg");
            File.WriteAllText(input, step.Value);

            var stdout = new StringWriter(); var stderr = new StringWriter();
            var exit = CliRunner.Run(["wireframe", input, "--out", first, "--view", "iso", "--density", "9", "--json"], stdout, stderr);
            Assert.Equal(0, exit); Assert.Empty(stderr.ToString()); Assert.True(File.Exists(first));
            using var json = JsonDocument.Parse(stdout.ToString());
            var evidence = json.RootElement.GetProperty("evidence");
            Assert.Equal(30, evidence.GetProperty("faceCount").GetInt32());
            Assert.Equal(30, evidence.GetProperty("facesWithTrimmedIsolines").GetInt32());
            Assert.True(evidence.GetProperty("isoPolylineCount").GetInt32() > 0);
            Assert.Equal(120, json.RootElement.GetProperty("pcurveRecovery").GetProperty("count").GetInt32());

            Assert.Equal(0, CliRunner.Run(["wireframe", input, "--out", second, "--view", "iso", "--density", "9"], new StringWriter(), new StringWriter()));
            Assert.Equal(File.ReadAllText(first), File.ReadAllText(second));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Periodic faces (a cylinder's side, a closed rim's torus, a bore) are bands in their periodic
    /// parameter: their trims do not close as polygons in the plane. Every face still gets isolines, and every
    /// isoline stays on the part.</summary>
    [Theory]
    [InlineData("cylinder-rounded", 5)]
    [InlineData("frustum-rounded", 5)]
    [InlineData("bushing-rounded", 12)]
    [InlineData("counterbore-rounded", 33)]
    public void PeriodicFacesGetIsolinesInsideTheirTrims(string fixture, int faces)
    {
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-wireframe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var step = Path.Combine(directory, "part.step");
            var source = Path.Combine(Root(), "fixtures/Regression/UniformRounding", fixture + ".firmament");
            Assert.Equal(0, CliRunner.Run(["build", source, "--out", step], new StringWriter(), new StringWriter()));
            var stdout = new StringWriter();
            Assert.Equal(0, CliRunner.Run(["wireframe", step, "--out", Path.Combine(directory, "part.svg"), "--view", "iso", "--density", "8", "--json"], stdout, new StringWriter()));
            using var json = JsonDocument.Parse(stdout.ToString());
            var evidence = json.RootElement.GetProperty("evidence");
            Assert.Equal(faces, evidence.GetProperty("faceCount").GetInt32());
            Assert.Equal(faces, evidence.GetProperty("facesWithTrimmedIsolines").GetInt32());

            var body = Step242Importer.ImportBody(File.ReadAllText(step)).Value;
            var geometry = Aetheris.Kernel.Core.Visualization.BrepWireframeSvgRenderer.SampleGeometry(body, new(Aetheris.Kernel.Core.Visualization.WireframeView.Isometric, 8, 32));
            var edges = geometry.Boundaries.SelectMany(b => b.Points).ToArray();
            var (min, max) = (new[] { edges.Min(p => p.X), edges.Min(p => p.Y), edges.Min(p => p.Z) }, new[] { edges.Max(p => p.X), edges.Max(p => p.Y), edges.Max(p => p.Z) });
            // A rim torus can bulge past its contact circles (on a frustum's leaning wall): allow the fillet radius.
            var slack = double.Parse(System.Text.RegularExpressions.Regex.Match(File.ReadAllText(source), @"target: AllEdges; radius: ([0-9.]+)mm").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.All(geometry.Isolines.SelectMany(l => l.Points), p =>
                Assert.True(p.X >= min[0] - slack && p.X <= max[0] + slack && p.Y >= min[1] - slack && p.Y <= max[1] + slack && p.Z >= min[2] - slack && p.Z <= max[2] + slack, $"isoline point {p} is off the part"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aetheris.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root.");
    }

    [Fact]
    public void WireframeCommandRejectsOutOfRangeDensity()
    {
        var stderr = new StringWriter();
        var exit = CliRunner.Run(["wireframe", "missing.step", "--density", "99"], new StringWriter(), stderr);
        Assert.Equal(1, exit);
        Assert.Contains("Usage: aetheris wireframe", stderr.ToString());
    }
}
