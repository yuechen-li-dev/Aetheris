using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentV2SemanticHoleStepPipelineTests
{
    public static TheoryData<string, double, int, int> Cases => new()
    {
        { "feature-v2-shaft-hole-through-step-verified", 480d - Math.PI * 1d * 1d * 6d, 1, 0 },
        { "feature-v2-shaft-hole-blind-step-verified", 480d - Math.PI * 1d * 1d * 3d, 1, 0 },
        { "feature-v2-counterbore-step-verified", 480d - ((Math.PI * 1d * 1d * 6d) + (Math.PI * ((2d * 2d) - (1d * 1d)) * 1d)), 2, 0 },
        { "feature-v2-countersink-step-verified", 480d - ((Math.PI * 1d * 1d * 6d) + ((Math.PI * 1d / 3d) * ((2d * 2d) + (2d * 1d) + (1d * 1d))) - (Math.PI * 1d * 1d * 1d)), 1, 1 }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void STEP_V2_X2_semantic_hole_step_verified_builds_emits_reimports_and_matches_volume(string fixtureId, double expectedVolume, int expectedCylinders, int expectedCones)
    {
        var fixturePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, $"../../../../fixtures/Hole/valid/{fixtureId}.valid.firmfixture"));
        var outDir = Path.Combine(Path.GetTempPath(), "aetheris-step-v2-x2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        var stepPath = Path.Combine(outDir, fixtureId + ".step");

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["build", fixturePath, "--out", stepPath, "--json"], stdout, stderr);

        Assert.True(exit == 0, stderr.ToString());
        Assert.True(File.Exists(stepPath), fixtureId + " reaches real build command");

        var fixtureText = File.ReadAllText(fixturePath);
        Assert.Contains("expected-stage: step-verified", fixtureText, StringComparison.Ordinal);
        Assert.Contains("current-stage: step-verified", fixtureText, StringComparison.Ordinal);
        Assert.Contains("roundtrip-required: true", fixtureText, StringComparison.Ordinal);
        Assert.Contains("build-command: aetheris build", fixtureText, StringComparison.Ordinal);

        var stepText = File.ReadAllText(stepPath);
        Assert.Contains("ISO-10303-21", stepText, StringComparison.Ordinal);
        Assert.True(CountStepEntities(stepText, "ADVANCED_FACE") > 0, fixtureId + " emits real STEP advanced faces");
        Assert.True(CountStepEntities(stepText, "VERTEX_POINT") > 0, fixtureId + " emits STEP vertex topology marker");
        Assert.DoesNotContain("controlled fixture only", stepText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trace", stepText, StringComparison.OrdinalIgnoreCase);

        var import = Step242Importer.ImportBody(stepText);
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));
        var body = import.Value;
        Assert.True(body.Topology.Faces.Count() > 0);
        Assert.True(body.Topology.Vertices.Count() > 0);

        var cylinderCount = body.Topology.Faces.Count(f => body.TryGetFaceSurface(f.Id, out var surface) && surface?.Kind == SurfaceGeometryKind.Cylinder);
        var coneCount = body.Topology.Faces.Count(f => body.TryGetFaceSurface(f.Id, out var surface) && surface?.Kind == SurfaceGeometryKind.Cone);
        Assert.Equal(expectedCylinders, cylinderCount);
        Assert.Equal(expectedCones, coneCount);

        var volume = StepAnalyzer.AnalyzeVolume(stepPath);
        Assert.True(volume.Success, fixtureId + " volume analysis succeeds");
        Assert.True(volume.Exact, fixtureId + " volume should use exact semantic hole interval analysis.");
        Assert.Equal("analytic-box-minus-z-hole", volume.Method);
        Assert.InRange(Math.Abs(volume.Volume - expectedVolume), 0d, 1e-8);
    }

    private static int CountStepEntities(string stepText, string entityName) =>
        Regex.Matches(stepText, "=\\s*" + Regex.Escape(entityName) + "\\s*\\(", RegexOptions.CultureInvariant).Count;
}
