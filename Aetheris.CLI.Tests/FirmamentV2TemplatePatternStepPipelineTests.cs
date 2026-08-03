using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentV2TemplatePatternStepPipelineTests
{
    [Theory]
    [InlineData("template-m4b-compact.firmament", 60d, 40d, 20d, 22d)]
    [InlineData("template-m4b-standard.firmament", 80d, 50d, 25d, 30d)]
    public void TemplatePattern_BuildsReimportsAndProducesExactTwoHoleGeometry(string fixtureName, double width, double depth, double height, double centerX)
    {
        var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../demos", fixtureName));
        var stepPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = CliRunner.Run(["build", fixture, "--out", stepPath, "--json"], stdout, stderr);

        Assert.True(exit == 0, stderr.ToString());
        var step = File.ReadAllText(stepPath);
        var imported = Step242Importer.ImportBody(step);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(d => d.Message)));
        var body = imported.Value;
        Assert.Equal(2, body.Topology.Faces.Count(face => body.TryGetFaceSurface(face.Id, out var surface) && surface?.Kind == SurfaceGeometryKind.Cylinder));

        var volume = StepAnalyzer.AnalyzeVolume(stepPath);
        Assert.True(volume.Success);
        Assert.True(volume.Exact);
        Assert.Equal("analytic-box-minus-z-hole", volume.Method);
        Assert.InRange(Math.Abs(volume.Volume - (width * depth * height - 2d * Math.PI * 4.25d * 4.25d * height)), 0d, 1e-8);
        Assert.Contains("\"patternExpansions\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"resolvedPoint3\": [", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains(centerX.ToString("0", System.Globalization.CultureInfo.InvariantCulture), stdout.ToString(), StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(step, "=\\s*CYLINDRICAL_SURFACE\\s*\\(", RegexOptions.CultureInvariant).Count);
    }
}
