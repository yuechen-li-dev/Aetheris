using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentV2SemanticPmiStepPipelineTests
{
    public static TheoryData<string, string, string, double> Cases => new()
    {
        { "pmi-v2-hole-diameter-callout-emits-in-step", "SHAPE_DIMENSION_REPRESENTATION('diameter:base.mount'", "PROPERTY_DEFINITION('diameter:base.mount'", 480d - Math.PI * 1d * 1d * 6d },
        { "pmi-v2-datum-plane-emits-in-step", "SHAPE_ASPECT('firmament-datum:A'", "PROPERTY_DEFINITION('datum:A:base'", 480d }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void STEP_V2_X7_semantic_pmi_step_verified_builds_emits_reimports_and_matches_volume(string fixtureId, string primaryEvidence, string secondaryEvidence, double expectedVolume)
    {
        var fixturePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, $"../../../../fixtures/FirmamentV2/PMI/valid/{fixtureId}.valid.firmfixture"));
        var outDir = Path.Combine(Path.GetTempPath(), "aetheris-step-v2-x7", Guid.NewGuid().ToString("N"));
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
        Assert.Contains("semantic-pmi-required: true", fixtureText, StringComparison.Ordinal);
        Assert.Contains("graphical-pmi-required: false", fixtureText, StringComparison.Ordinal);
        Assert.Contains("build-command: aetheris build", fixtureText, StringComparison.Ordinal);

        var stepText = File.ReadAllText(stepPath);
        Assert.True(CountStepEntities(stepText, "ADVANCED_FACE") > 0, fixtureId + " emits real STEP advanced faces");
        Assert.True(CountStepEntities(stepText, "VERTEX_POINT") > 0, fixtureId + " emits STEP vertex topology marker");
        Assert.Contains(primaryEvidence, stepText, StringComparison.Ordinal);
        Assert.Contains(secondaryEvidence, stepText, StringComparison.Ordinal);
        Assert.DoesNotContain("DRAUGHTING_CALLOUT", stepText, StringComparison.Ordinal);
        Assert.DoesNotContain("ANNOTATION_PLANE", stepText, StringComparison.Ordinal);

        var import = Step242Importer.ImportBody(stepText);
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));
        Assert.True(import.Value.Topology.Faces.Count() > 0);
        Assert.True(import.Value.Topology.Vertices.Count() > 0);

        var volume = StepAnalyzer.AnalyzeVolume(stepPath);
        Assert.True(volume.Success, fixtureId + " volume analysis succeeds");
        Assert.True(volume.Exact, fixtureId + " volume should use exact analysis.");
        Assert.InRange(Math.Abs(volume.Volume - expectedVolume), 0d, 1e-8);
    }

    private static int CountStepEntities(string stepText, string entityName) =>
        Regex.Matches(stepText, "=\\s*" + Regex.Escape(entityName) + "\\s*\\(", RegexOptions.CultureInvariant).Count;
}
