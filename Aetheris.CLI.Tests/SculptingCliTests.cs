using System.Text.Json;
using Aetheris.CLI;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Surfacing;

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

    [Fact]
    public void SectionChainCageFitsTheExactBodyAndReplaysTwoEditsWithTheSameRim()
    {
        var source = Path.Combine(Root(), "fixtures/Canonical/Sculpting/compact-mouse.firmament");
        var folder = Path.Combine(Root(), "artifacts/local/tests/section-chain-cage", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var inspection = Run("inspect", source);
            Assert.Equal("Sculpting", inspection.RootElement.GetProperty("domain").GetString());
            var states = inspection.RootElement.GetProperty("meshStates");
            var seed = states.GetProperty("Seed");
            var fit = seed.GetProperty("sectionFit");
            Assert.True(fit.GetProperty("maximumSampledTransitionDeviationMm").GetDouble() <
                fit.GetProperty("initialMaximumSampledTransitionDeviationMm").GetDouble());

            var stepPath = Path.Combine(folder, "blockout.step");
            using var blockout = Run("build", source, "--state", "Exact", "--out", stepPath);
            var imported = Step242Importer.ImportBody(File.ReadAllText(stepPath));
            Assert.True(imported.IsSuccess);
            Assert.Equal(22, imported.Value.Topology.Faces.Count());
            var sourceStateId = blockout.RootElement.GetProperty("state").GetProperty("stateId").GetProperty("value").GetString();

            var raised = ExportAndReplay("PalmComfort");
            var lowered = ExportAndReplay("LowFront");
            Assert.NotEqual(raised.StateId, lowered.StateId);
            Assert.Equal(raised.SectionFit!.Rim, lowered.SectionFit!.Rim);
            Assert.InRange(raised.SectionFit.Rim.MaximumSampledDeviationMm, 0, 0.15);
            Assert.Equal(raised.Cage.Faces.Select(f => f.Id), lowered.Cage.Faces.Select(f => f.Id));

            var raisedMesh = SubdivisionSculptor.PrepareExport(raised).NativeMesh;
            var loweredMesh = SubdivisionSculptor.PrepareExport(lowered).NativeMesh;
            Assert.True(raisedMesh.Positions.Count > raised.Cage.Vertices.Count);
            Assert.Equal(raisedMesh.Positions.Where(p => p.Z <= 12.000001),
                loweredMesh.Positions.Where(p => p.Z <= 12.000001));
            Assert.True(raisedMesh.Positions.Max(p => p.Z) - loweredMesh.Positions.Max(p => p.Z) > 0.5);

            MeshSculptState ExportAndReplay(string name)
            {
                var path = Path.Combine(folder, name + ".glb");
                using var report = Run("build", source, "--state", name, "--out", path);
                Assert.Equal(sourceStateId, report.RootElement.GetProperty("sourceBodyStateId").GetString());
                Assert.True(report.RootElement.GetProperty("topology").GetProperty("isWatertight").GetBoolean());
                Assert.Equal(0x46546C67u, BitConverter.ToUInt32(File.ReadAllBytes(path), 0));
                using var sidecar = JsonDocument.Parse(File.ReadAllText(path + ".sculpt.json"));
                var replay = MeshConstructionReplayer.Replay(sidecar.RootElement.GetProperty("construction").GetRawText());
                Assert.Equal(report.RootElement.GetProperty("stateId").GetString(), replay.StateId);
                Assert.Equal("Rim", replay.Construction.RimSection);
                return replay;
            }
        }
        finally
        {
            foreach (var path in Directory.GetFiles(folder)) File.Delete(path);
            Directory.Delete(folder);
        }
    }

    [Theory]
    [InlineData("maximumRimDeviation: 0.15mm", "maximumRimDeviation: 0.08mm", "mesh-section-rim-deviation-exceeded")]
    [InlineData("output: PalmComfort", "MeshState Uncrease { input: Seed; Crease { edges: [All]; mode: Smooth } }\n    output: Uncrease", "mesh-section-rim-crease-changed")]
    public void SectionChainCageRejectsAnUnsatisfiedRimContract(string before, string after, string diagnostic)
    {
        var folder = Path.Combine(Root(), "artifacts/local/tests/section-chain-cage", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var source = Path.Combine(folder, "invalid.firmament");
        var output = Path.Combine(folder, "invalid.glb");
        try
        {
            var mouse = File.ReadAllText(Path.Combine(Root(), "fixtures/Canonical/Sculpting/compact-mouse.firmament"));
            File.WriteAllText(source, mouse.Replace(before, after));
            var stdout = new StringWriter();
            Assert.Equal(1, CliRunner.Run(["build", source, "--out", output, "--json"], stdout, new StringWriter()));
            Assert.Contains(diagnostic, stdout.ToString(), StringComparison.Ordinal);
            Assert.False(File.Exists(output));
        }
        finally
        {
            File.Delete(source);
            Directory.Delete(folder);
        }
    }

    private static JsonDocument Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Assert.True(CliRunner.Run([.. args, "--json"], stdout, stderr) == 0, stdout + stderr.ToString());
        return JsonDocument.Parse(stdout.ToString());
    }

    private static string Root() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "Aetheris.slnx"))) d = d.Parent; return d!.FullName; }
}
