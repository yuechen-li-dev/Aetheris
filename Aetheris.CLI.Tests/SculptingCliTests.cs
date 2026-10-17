using System.Text.Json;
using Aetheris.CLI;

namespace Aetheris.CLI.Tests;

public sealed class SculptingCliTests
{
    [Theory]
    [InlineData("surf-x3b-add-section-chain-grip")]
    [InlineData("surf-x4-remove-section-chain-g1-duct")]
    public void EmbeddedStationChainsStayWithSculptingOwner(string name)
    {
        var fixture = Path.Combine(Root(), "fixtures/Canonical/BodyState", name + ".firmament");
        var directory = Path.Combine(Root(), "artifacts/local/tests/sculpting-owner", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var output = Path.Combine(directory, "part.step");
        var mismatched = Path.Combine(directory, "mismatched.firmament");
        try
        {
            foreach (var operation in new[] { "validate", "inspect", "build" })
            {
                var stdout = new StringWriter(); var stderr = new StringWriter();
                string[] args = operation == "build" ? [operation, fixture, "--output", output, "--json"] : [operation, fixture, "--json"];
                Assert.Equal(0, CliRunner.Run(args, stdout, stderr));
                using var json = JsonDocument.Parse(stdout.ToString());
                var result = operation == "validate" ? json.RootElement.GetProperty("firmamentV2Validation") : json.RootElement;
                Assert.Equal("Sculpting", result.GetProperty("domain").GetString());
            }
            Assert.True(File.Exists(output));
            File.WriteAllText(mismatched, "schema SectionChain\n" + File.ReadAllText(fixture));
            var rejected = Path.Combine(directory, "rejected.step");
            foreach (var operation in new[] { "validate", "build" })
            {
                var stdout = new StringWriter();
                string[] args = operation == "build" ? [operation, mismatched, "--output", rejected, "--json"] : [operation, mismatched, "--json"];
                Assert.NotEqual(0, CliRunner.Run(args, stdout, new StringWriter()));
                Assert.Contains("firmament-schema-mismatch", stdout.ToString());
                Assert.False(File.Exists(rejected));
            }
        }
        finally
        {
            foreach (var path in Directory.GetFiles(directory)) File.Delete(path);
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void BuildInspectAndValidateExposeStructuredSculptStateEvidence()
    {
        var fixture = Path.Combine(Root(), "fixtures", "Canonical", "Sculpting", "sculpted-housing.firmament");
        var output = Path.Combine(Path.GetTempPath(), "aetheris-surf-x0-" + Guid.NewGuid().ToString("N") + ".step");
        try
        {
            var buildOut = new StringWriter(); var buildErr = new StringWriter();
            Assert.Equal(0, CliRunner.Run(["build", fixture, "--output", output, "--json"], buildOut, buildErr));
            using var build = JsonDocument.Parse(buildOut.ToString()); Assert.True(build.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("Sculpting", build.RootElement.GetProperty("domain").GetString()); Assert.Equal(0, build.RootElement.GetProperty("rationalNurbs").GetInt32());
            Assert.Equal("state-8960030e57e7b7d897d9", build.RootElement.GetProperty("geometricDelta").GetProperty("inputState").GetProperty("value").GetString());
            Assert.True(File.Exists(output)); Assert.True(File.Exists(Path.ChangeExtension(output, ".delta.json")));
            var inspectOut = new StringWriter(); Assert.Equal(0, CliRunner.Run(["inspect", fixture, "--json"], inspectOut, new StringWriter()));
            using var inspect = JsonDocument.Parse(inspectOut.ToString()); Assert.Equal(2, inspect.RootElement.GetProperty("states").GetArrayLength());
            var validateOut = new StringWriter(); Assert.Equal(0, CliRunner.Run(["validate", fixture, "--json"], validateOut, new StringWriter()));
            Assert.Contains("\"domain\": \"Sculpting\"", validateOut.ToString(), StringComparison.Ordinal);
        }
        finally { if (File.Exists(output)) File.Delete(output); var delta = Path.ChangeExtension(output, ".delta.json"); if (File.Exists(delta)) File.Delete(delta); }
    }

    [Fact]
    public void PatchReplacementBuildAndReinspectionExposeNonRationalSurfaceAndInteriorBounds()
    {
        var fixture = Path.Combine(Root(), "fixtures", "Canonical", "Sculpting", "surf-x1-freeform-housing.firmament");
        var output = Path.Combine(Path.GetTempPath(), "aetheris-surf-x1-" + Guid.NewGuid().ToString("N") + ".step");
        try
        {
            var buildOut = new StringWriter(); Assert.Equal(0, CliRunner.Run(["build", fixture, "--output", output, "--json"], buildOut, new StringWriter()));
            using var build = JsonDocument.Parse(buildOut.ToString()); var root = build.RootElement;
            Assert.Equal(1, root.GetProperty("surfaceInventory").GetProperty("nonRationalBSpline").GetInt32()); Assert.Equal(0, root.GetProperty("rationalNurbs").GetInt32());
            var patch = root.GetProperty("surfacePatches")[0]; Assert.Equal("CrownPatch", patch.GetProperty("patchId").GetString()); Assert.Equal("NonRationalBSpline", patch.GetProperty("exportClass").GetString());
            Assert.Equal(6, patch.GetProperty("controlCountU").GetInt32()); Assert.Equal(4, patch.GetProperty("continuityContracts").GetArrayLength());
            var analyzeOut = new StringWriter(); Assert.Equal(0, CliRunner.Run(["analyze", output, "--json"], analyzeOut, new StringWriter()));
            using var analyze = JsonDocument.Parse(analyzeOut.ToString()); var summary = analyze.RootElement.GetProperty("summary");
            Assert.Equal("enclosed-manifold", summary.GetProperty("structuralAssessment").GetString()); Assert.Equal(1, summary.GetProperty("surfaceFamilies").GetProperty("bspline").GetInt32());
            Assert.InRange(summary.GetProperty("boundingBox").GetProperty("max").GetProperty("z").GetDouble(), 26.269, 26.270);
        }
        finally { if (File.Exists(output)) File.Delete(output); var delta = Path.ChangeExtension(output, ".delta.json"); if (File.Exists(delta)) File.Delete(delta); }
    }

    [Fact]
    public void JudgedBlendBuildExposesCompactCandidateTraceAndProvenance()
    {
        var fixture = Path.Combine(Root(), "fixtures", "Canonical", "Sculpting", "surf-x2-judged-housing.firmament");
        var output = Path.Combine(Path.GetTempPath(), "aetheris-surf-x2-" + Guid.NewGuid().ToString("N") + ".step");
        try
        {
            var buildOut = new StringWriter(); var buildErr = new StringWriter();
            Assert.Equal(0, CliRunner.Run(["build", fixture, "--output", output, "--json"], buildOut, buildErr));
            using var build = JsonDocument.Parse(buildOut.ToString()); var root = build.RootElement;
            var judgment = root.GetProperty("blendJudgment");
            Assert.Equal("PowerM3Degree6", judgment.GetProperty("selectedCandidateId").GetString());
            Assert.Equal(4, judgment.GetProperty("candidates").GetArrayLength());
            Assert.Contains(judgment.GetProperty("candidates").EnumerateArray(), item => item.GetProperty("disposition").GetString() == "Rejected");
            Assert.Equal(0, root.GetProperty("rationalNurbs").GetInt32());
            using var delta = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(output, ".delta.json")));
            Assert.Equal("PowerM3Degree6", delta.RootElement.GetProperty("delta").GetProperty("blendJudgment").GetProperty("selectedCandidateId").GetString());
        }
        finally { if (File.Exists(output)) File.Delete(output); var delta = Path.ChangeExtension(output, ".delta.json"); if (File.Exists(delta)) File.Delete(delta); }
    }

    private static string Root() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "Aetheris.slnx"))) d = d.Parent; return d!.FullName; }
}
