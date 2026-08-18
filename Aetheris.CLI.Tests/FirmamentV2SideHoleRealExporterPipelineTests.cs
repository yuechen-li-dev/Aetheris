using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentV2SideHoleRealExporterPipelineTests
{
    [Fact]
    public void SIDEHOLE_REAL_X1_locked_side_hole_builds_through_real_exporter_reimports_and_matches_volume()
    {
        const string fixtureId = "feature-v2-side-hole-step-verified";
        var fixturePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, $"../../../../fixtures/Region/valid/{fixtureId}.valid.firmfixture"));
        var outDir = Path.Combine(Path.GetTempPath(), "aetheris-sidehole-real-x1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        var stepPath = Path.Combine(outDir, fixtureId + ".step");

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["build", fixturePath, "--out", stepPath, "--json"], stdout, stderr);

        Assert.True(exit == 0, stderr.ToString());
        Assert.True(File.Exists(stepPath), "locked side-hole fixture reaches the real CLI build command");

        var fixtureText = File.ReadAllText(fixturePath);
        Assert.Contains("expected-stage: step-verified", fixtureText, StringComparison.Ordinal);
        Assert.Contains("current-stage: step-verified", fixtureText, StringComparison.Ordinal);
        Assert.Contains("roundtrip-required: true", fixtureText, StringComparison.Ordinal);
        Assert.Contains("build-command: aetheris build", fixtureText, StringComparison.Ordinal);

        var stepText = File.ReadAllText(stepPath);
        Assert.Contains("ISO-10303-21", stepText, StringComparison.Ordinal);
        Assert.True(CountStepEntities(stepText, "ADVANCED_FACE") > 0, "real Step242Exporter output contains advanced faces");
        Assert.True(CountStepEntities(stepText, "VERTEX_POINT") > 0, "real Step242Exporter output contains vertices");
        Assert.Contains("CYLINDRICAL_SURFACE", stepText, StringComparison.Ordinal);
        Assert.DoesNotContain("controlled fixture only", stepText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("golden path artifact", stepText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trace", stepText, StringComparison.OrdinalIgnoreCase);

        var import = Step242Importer.ImportBody(stepText);
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));
        var body = import.Value;
        Assert.True(body.Topology.Faces.Count() > 0);
        Assert.True(body.Topology.Vertices.Count() > 0);

        var cylinderFaces = body.Topology.Faces
            .Where(f => body.TryGetFaceSurface(f.Id, out var surface) && surface?.Kind == SurfaceGeometryKind.Cylinder)
            .ToArray();
        Assert.Single(cylinderFaces);
        body.TryGetFaceSurface(cylinderFaces[0].Id, out var cylinderSurface);
        Assert.NotNull(cylinderSurface?.Cylinder);
        Assert.InRange(Math.Abs(Math.Abs(cylinderSurface!.Cylinder!.Value.Axis.ToVector().X) - 1d), 0d, 1e-9);

        var expectedVolume = (10d * 8d * 6d) - (Math.PI * 1d * 1d * 10d);
        var volume = StepAnalyzer.AnalyzeVolume(stepPath);
        Assert.True(volume.Success, "side-hole volume analysis succeeds after STEP reimport");
        Assert.True(volume.Exact, "side-hole volume uses exact independent box-minus-X-cylinder formula");
        Assert.Equal("analytic-box-minus-x-hole", volume.Method);
        Assert.InRange(Math.Abs(volume.Volume - expectedVolume), 0d, 1e-8);
    }

    [Fact]
    public void ConstructionPlaneHole_AnalyticVolumeCountsOnePartitionedPhysicalCylinder()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var fixture = Path.Combine(root, "fixtures", "Hole", "valid", "construction-plane-through-hole.firmament");
        var stepPath = Path.Combine(Path.GetTempPath(), "aetheris-construction-plane-hole-" + Guid.NewGuid().ToString("N") + ".step");
        var stdout = new StringWriter(); var stderr = new StringWriter();

        Assert.Equal(0, Aetheris.CLI.CliRunner.Run(["build", fixture, "--out", stepPath, "--json"], stdout, stderr));
        var volume = StepAnalyzer.AnalyzeVolume(stepPath);

        Assert.True(volume.Success, "construction-plane source reaches independent STEP volume analysis");
        Assert.True(volume.Exact);
        Assert.Equal("analytic-box-minus-x-hole", volume.Method);
        Assert.Equal(72000d - Math.PI * 4d * 4d * 100d, volume.Volume, 8);
    }

    private static int CountStepEntities(string stepText, string entityName) =>
        Regex.Matches(stepText, "=\\s*" + Regex.Escape(entityName) + "\\s*\\(", RegexOptions.CultureInvariant).Count;
}
