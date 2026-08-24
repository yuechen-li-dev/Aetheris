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

    [Fact]
    public void WireframeCommandRejectsOutOfRangeDensity()
    {
        var stderr = new StringWriter();
        var exit = CliRunner.Run(["wireframe", "missing.step", "--density", "99"], new StringWriter(), stderr);
        Assert.Equal(1, exit);
        Assert.Contains("Usage: aetheris wireframe", stderr.ToString());
    }
}
