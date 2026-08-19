using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentV2AnalyticPrimitiveStepPipelineTests
{
    public static TheoryData<string, double, int, int?> Cases => new()
    {
        { "primitive-v2-cylinder-step-verified", Math.PI * 2d * 2d * 10d, 3, 4 },
        { "primitive-v2-cone-step-verified", Math.PI * 10d / 3d * (3d * 3d + 3d * 1d + 1d * 1d), 3, 4 },
        { "primitive-v2-sphere-step-verified", 4d / 3d * Math.PI * 5d * 5d * 5d, 1, null },
        { "primitive-v2-torus-step-verified", 2d * Math.PI * Math.PI * 8d * 2d * 2d, 1, 1 }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void STEP_V2_X1_analytic_primitive_step_verified_builds_emits_reimports_and_matches_volume(string fixtureId, double expectedVolume, int expectedFaces, int? expectedVertices)
    {
        var fixturePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, $"../../../../fixtures/Regression/Primitive/valid/{fixtureId}.valid.firmfixture"));
        var outDir = Path.Combine(Path.GetTempPath(), "aetheris-step-v2-x1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        var stepPath = Path.Combine(outDir, fixtureId + ".step");

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["build", fixturePath, "--out", stepPath, "--json"], stdout, stderr);

        Assert.True(exit == 0, stderr.ToString());
        Assert.True(File.Exists(stepPath), fixtureId + " reaches real build command");

        var stepText = File.ReadAllText(stepPath);
        Assert.Contains("ISO-10303-21", stepText, StringComparison.Ordinal);
        Assert.True(CountStepEntities(stepText, "ADVANCED_FACE") > 0, fixtureId + " emits real STEP advanced faces");
        Assert.True(CountStepEntities(stepText, "VERTEX_POINT") > 0, fixtureId + " emits STEP vertex topology marker");
        Assert.DoesNotContain("controlled fixture only", stepText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trace", stepText, StringComparison.OrdinalIgnoreCase);

        var import = Step242Importer.ImportBody(stepText);
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));
        Assert.Equal(expectedFaces, import.Value.Topology.Faces.Count());
        if (expectedVertices is not null)
        {
            Assert.Equal(expectedVertices.Value, import.Value.Topology.Vertices.Count());
        }

        var volume = StepAnalyzer.AnalyzeVolume(stepPath);
        Assert.True(volume.Success, fixtureId + " volume analysis succeeds");
        Assert.True(volume.Exact, fixtureId + " volume should use exact analytic/closed-shell analysis.");
        Assert.InRange(Math.Abs(volume.Volume - expectedVolume), 0d, 1e-8);
    }

    private static int CountStepEntities(string stepText, string entityName) =>
        Regex.Matches(stepText, "=\\s*" + Regex.Escape(entityName) + "\\s*\\(", RegexOptions.CultureInvariant).Count;
}
