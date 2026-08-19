using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentV2SourceCompositionStepPipelineTests
{
    public static TheoryData<string, string, double> DerivationCases => new()
    {
        { "Regression/RecordDerivation/valid/derivation-v2-with-size-override-step-verified.valid.firmfixture", "derivation-v2-with-size-override-step-verified", 12d * 8d * 6d },
        { "Regression/RecordDerivation/valid/derivation-v2-with-chained-twice-step-verified.valid.firmfixture", "derivation-v2-with-chained-twice-step-verified", 12d * 8d * 7d }
    };


    public static TheoryData<string, string, double> CompositeCases => new()
    {
        { "Regression/Composite/valid/composite-v2-two-independent-holes-step-verified.valid.firmfixture", "composite-v2-two-independent-holes-step-verified", 480d - 2d * Math.PI * 1d * 1d * 6d },
        { "Regression/Composite/valid/composite-v2-adjacent-non-overlapping-holes-step-verified.valid.firmfixture", "composite-v2-adjacent-non-overlapping-holes-step-verified", 480d - 2d * Math.PI * 1d * 1d * 6d },
        { "Regression/Composite/valid/composite-v2-hole-plus-derived-variant-step-verified.valid.firmfixture", "composite-v2-hole-plus-derived-variant-step-verified", 12d * 8d * 6d - Math.PI * 1d * 1d * 6d }
    };

    [Theory]
    [MemberData(nameof(CompositeCases))]
    public void STEP_V2_X4_composite_multi_hole_fixtures_build_emit_reimport_and_match_evidence(string fixtureRelativePath, string fixtureId, double expectedVolume)
    {
        var fixturePath = Fixture(fixtureRelativePath);
        var stepPath = TempStep(fixtureId);

        var (stepText, fixtureText) = BuildAndReadStep(fixturePath, stepPath, fixtureId);
        AssertStageHonesty(fixtureText, "multi-feature-composition", 4);
        if (fixtureId.Contains("derived-variant", StringComparison.Ordinal))
        {
            Assert.Contains("with derivation + AirHoleFeature(SimpleShaft)", fixtureText, StringComparison.Ordinal);
            Assert.Contains("solid base: Box { size: [10, 8, 6] }", fixtureText, StringComparison.Ordinal);
            Assert.Contains("solid wider: base with { size: [12, 8, 6] }", fixtureText, StringComparison.Ordinal);
            Assert.Contains("modify wider", fixtureText, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("AirHoleFeature(SimpleShaft) x2", fixtureText, StringComparison.Ordinal);
        }

        Assert.True(CountStepEntities(stepText, "ADVANCED_FACE") > 0, fixtureId + " emits real STEP advanced faces");
        Assert.True(CountStepEntities(stepText, "VERTEX_POINT") > 0, fixtureId + " emits STEP vertex topology marker");
        AssertNoTraceOnlyStep(stepText);

        var import = Step242Importer.ImportBody(stepText);
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));
        var body = import.Value;
        var expectedCylinderFaceCount = fixtureId.Contains("derived-variant", StringComparison.Ordinal) ? 1 : 2;
        Assert.Equal(expectedCylinderFaceCount, body.Topology.Faces.Count(f => body.TryGetFaceSurface(f.Id, out var surface) && surface?.Kind == SurfaceGeometryKind.Cylinder));

        var volume = StepAnalyzer.AnalyzeVolume(stepPath);
        Assert.True(volume.Success, fixtureId + " volume analysis succeeds");
        Assert.True(volume.Exact, fixtureId + " volume should use exact semantic multi-hole interval analysis");
        Assert.Equal("analytic-box-minus-z-hole", volume.Method);
        Assert.InRange(Math.Abs(volume.Volume - expectedVolume), 0d, 1e-8);
        if (fixtureId.Contains("derived-variant", StringComparison.Ordinal))
        {
            var staleBaseVolume = 10d * 8d * 6d - Math.PI * 1d * 1d * 6d;
            Assert.True(Math.Abs(volume.Volume - staleBaseVolume) > 1d, "derived-variant-plus-hole volume must not reuse stale base 10 x 8 x 6 dimensions");
        }
    }

    [Fact]
    public void STEP_V2_X4_overlapping_composite_holes_reject_with_clear_diagnostic()
    {
        const string fixtureId = "composite-v2-overlapping-holes-rejected-with-clear-diagnostic";
        var fixturePath = Fixture("Compatibility/LegacyAliases/Invalid/Composite/composite-v2-overlapping-holes-rejected-with-clear-diagnostic.invalid.firmfixture");
        var stepPath = TempStep(fixtureId);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = Aetheris.CLI.CliRunner.Run(["build", fixturePath, "--out", stepPath, "--json"], stdout, stderr);

        Assert.NotEqual(0, exit);
        var combined = stdout.ToString() + stderr.ToString();
        Assert.Contains("firmament-v2-semantic-hole-overlap", combined, StringComparison.Ordinal);
        Assert.False(File.Exists(stepPath), "invalid overlapping fixture must not emit success AP242");
        var fixtureText = File.ReadAllText(fixturePath);
        Assert.Contains("current-stage: deterministic rejection", fixtureText, StringComparison.Ordinal);
        Assert.Contains("expected-diagnostic: firmament-v2-semantic-hole-overlap", fixtureText, StringComparison.Ordinal);
    }


    [Theory]
    [InlineData("Compatibility/LegacyAliases/Invalid/Templates/template-v2-cnc-min-tool-radius-enforced.invalid.firmfixture", "template-v2-cnc-min-tool-radius-enforced", "firmament-v2-dfm-minimum-tool-radius-violation")]
    [InlineData("Compatibility/LegacyAliases/Invalid/Templates/template-v2-concept-unit-mismatch-rejected-at-build.invalid.firmfixture", "template-v2-concept-unit-mismatch-rejected-at-build", "firmament-v2-dfm-concept-unit-mismatch")]
    public void STEP_V2_X6_template_dfm_concept_invalid_fixtures_reject_during_real_build(string fixtureRelativePath, string fixtureId, string expectedDiagnostic)
    {
        var fixturePath = Fixture(fixtureRelativePath);
        var stepPath = TempStep(fixtureId);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = Aetheris.CLI.CliRunner.Run(["build", fixturePath, "--out", stepPath, "--json"], stdout, stderr);

        Assert.NotEqual(0, exit);
        var combined = stdout.ToString() + stderr.ToString();
        Assert.Contains(expectedDiagnostic, combined, StringComparison.Ordinal);
        Assert.False(File.Exists(stepPath), fixtureId + " must not emit successful AP242");
        var fixtureText = File.ReadAllText(fixturePath);
        Assert.Contains("tier: 5", fixtureText, StringComparison.Ordinal);
        Assert.Contains("current-stage: deterministic rejection", fixtureText, StringComparison.Ordinal);
        Assert.Contains("feature-area: dfm/concept-enforcement", fixtureText, StringComparison.Ordinal);
        Assert.Contains("build-command: aetheris build", fixtureText, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(DerivationCases))]
    public void STEP_V2_X3_derivation_fixtures_build_emit_reimport_and_match_box_evidence(string fixtureRelativePath, string fixtureId, double expectedVolume)
    {
        var fixturePath = Fixture(fixtureRelativePath);
        var stepPath = TempStep(fixtureId);

        var (stepText, fixtureText) = BuildAndReadStep(fixturePath, stepPath, fixtureId);
        AssertStageHonesty(fixtureText, "derivation", 3);

        Assert.True(CountStepEntities(stepText, "ADVANCED_FACE") > 0, fixtureId + " emits real STEP advanced faces");
        Assert.True(CountStepEntities(stepText, "VERTEX_POINT") > 0, fixtureId + " emits STEP vertex topology marker");
        AssertNoTraceOnlyStep(stepText);

        var import = Step242Importer.ImportBody(stepText);
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));
        Assert.Equal(6, import.Value.Topology.Faces.Count());
        Assert.Equal(8, import.Value.Topology.Vertices.Count());
        Assert.Equal(12, import.Value.Topology.Edges.Count());

        var volume = StepAnalyzer.AnalyzeVolume(stepPath);
        Assert.True(volume.Success, fixtureId + " volume analysis succeeds");
        Assert.True(volume.Exact, fixtureId + " volume should use exact closed-shell analysis");
        Assert.InRange(Math.Abs(volume.Volume - expectedVolume), 0d, 1e-9);
    }

    [Fact]
    public void STEP_V2_X3_semantic_face_alias_fixture_builds_emits_reimports_and_matches_hole_evidence()
    {
        const string fixtureId = "semanticref-v2-expose-face-alias-resolves-in-step";
        var fixturePath = Fixture("Regression/SemanticRefs/valid/semanticref-v2-expose-face-alias-resolves-in-step.valid.firmfixture");
        var stepPath = TempStep(fixtureId);

        var (stepText, fixtureText) = BuildAndReadStep(fixturePath, stepPath, fixtureId);
        AssertStageHonesty(fixtureText, "semantic-reference", 3);
        Assert.Contains("on: top", fixtureText, StringComparison.Ordinal);
        Assert.Contains("face(+Z) => top", fixtureText, StringComparison.Ordinal);

        Assert.True(CountStepEntities(stepText, "ADVANCED_FACE") > 0, fixtureId + " emits real STEP advanced faces");
        Assert.True(CountStepEntities(stepText, "VERTEX_POINT") > 0, fixtureId + " emits STEP vertex topology marker");
        AssertNoTraceOnlyStep(stepText);

        var import = Step242Importer.ImportBody(stepText);
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));
        var body = import.Value;
        Assert.True(body.Topology.Faces.Count() > 0);
        Assert.True(body.Topology.Vertices.Count() > 0);
        Assert.Equal(1, body.Topology.Faces.Count(f => body.TryGetFaceSurface(f.Id, out var surface) && surface?.Kind == SurfaceGeometryKind.Cylinder));

        var volume = StepAnalyzer.AnalyzeVolume(stepPath);
        Assert.True(volume.Success, fixtureId + " volume analysis succeeds");
        Assert.True(volume.Exact, fixtureId + " volume should use exact semantic hole interval analysis");
        Assert.Equal("analytic-box-minus-z-hole", volume.Method);
        Assert.InRange(Math.Abs(volume.Volume - (480d - Math.PI * 1d * 1d * 6d)), 0d, 1e-8);
    }

    private static (string StepText, string FixtureText) BuildAndReadStep(string fixturePath, string stepPath, string fixtureId)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["build", fixturePath, "--out", stepPath, "--json"], stdout, stderr);

        Assert.True(exit == 0, stderr.ToString());
        Assert.True(File.Exists(stepPath), fixtureId + " reaches real build command");
        var stepText = File.ReadAllText(stepPath);
        Assert.Contains("ISO-10303-21", stepText, StringComparison.Ordinal);
        return (stepText, File.ReadAllText(fixturePath));
    }

    private static void AssertStageHonesty(string fixtureText, string featureArea, int tier)
    {
        Assert.Contains($"tier: {tier}", fixtureText, StringComparison.Ordinal);
        Assert.Contains("expected-stage: step-verified", fixtureText, StringComparison.Ordinal);
        Assert.Contains("current-stage: step-verified", fixtureText, StringComparison.Ordinal);
        Assert.Contains("roundtrip-required: true", fixtureText, StringComparison.Ordinal);
        Assert.Contains("build-command: aetheris build", fixtureText, StringComparison.Ordinal);
        Assert.Contains("feature-area: " + featureArea, fixtureText, StringComparison.Ordinal);
    }

    private static void AssertNoTraceOnlyStep(string stepText)
    {
        Assert.DoesNotContain("controlled fixture only", stepText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trace", stepText, StringComparison.OrdinalIgnoreCase);
    }

    private static string Fixture(string relativePath) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures", relativePath));

    private static string TempStep(string fixtureId)
    {
        var outDir = Path.Combine(Path.GetTempPath(), "aetheris-step-v2-x3", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        return Path.Combine(outDir, fixtureId + ".step");
    }

    private static int CountStepEntities(string stepText, string entityName) =>
        Regex.Matches(stepText, "=\\s*" + Regex.Escape(entityName) + "\\s*\\(", RegexOptions.CultureInvariant).Count;
}
