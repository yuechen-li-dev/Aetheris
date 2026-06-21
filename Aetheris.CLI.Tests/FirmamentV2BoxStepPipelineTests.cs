using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentV2BoxStepPipelineTests
{
    private static readonly string FixturePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2/Primitive/valid/pipeline-v2-box-step-verified.valid.firmfixture"));

    [Fact]
    public void STEP_V2_A1_pipeline_v2_box_step_verified_builds_emits_reimports_and_matches_volume()
    {
        var outDir = Path.Combine(Path.GetTempPath(), "aetheris-step-v2-a1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        var stepPath = Path.Combine(outDir, "pipeline-v2-box-step-verified.step");

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["build", FixturePath, "--out", stepPath, "--json"], stdout, stderr);

        Assert.True(exit == 0, stderr.ToString());
        Assert.True(File.Exists(stepPath), "pipeline-v2-box-reaches-build-command");

        var stepText = File.ReadAllText(stepPath);
        Assert.Contains("ISO-10303-21", stepText, StringComparison.Ordinal);
        Assert.True(CountStepEntities(stepText, "ADVANCED_FACE") >= 6, "pipeline-v2-box-emits-real-step advanced faces");
        Assert.True(CountStepEntities(stepText, "VERTEX_POINT") > 0, "pipeline-v2-box-emits-real-step vertices");
        Assert.DoesNotContain("controlled fixture only", stepText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trace", stepText, StringComparison.OrdinalIgnoreCase);

        var import = Step242Importer.ImportBody(stepText);
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));
        Assert.Equal(6, import.Value.Topology.Faces.Count());
        Assert.Equal(8, import.Value.Topology.Vertices.Count());
        Assert.Equal(12, import.Value.Topology.Edges.Count());

        var volume = StepAnalyzer.AnalyzeVolume(stepPath);
        Assert.True(volume.Success, "pipeline-v2-box-volume-matches-expected");
        Assert.True(volume.Exact, "V2 Box volume should use exact closed-shell analysis.");
        Assert.InRange(Math.Abs(volume.Volume - 480d), 0d, 1e-9);
    }

    private static int CountStepEntities(string stepText, string entityName) =>
        Regex.Matches(stepText, "=\\s*" + Regex.Escape(entityName) + "\\s*\\(", RegexOptions.CultureInvariant).Count;
}
