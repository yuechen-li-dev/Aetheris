using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class GarmentCommandTests
{
    [Fact]
    public void FittingBuildExportsCompiledCommandsWithoutMaterializingConceptGeometry()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx")))
        {
            root = root.Parent;
        }
        string input = Path.Combine(root!.FullName, "fixtures/Canonical/Garment/temporary-fitting.firmament");
        string output = Path.Combine(root.FullName, "artifacts/local/tests/garment", Guid.NewGuid().ToString("N"));
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        int code = CliRunner.Run(["garment", "build", input, "--out-dir", output, "--json"], stdout, stderr);
        Assert.Equal(0, code);
        using var evidence = JsonDocument.Parse(stdout.ToString());
        var document = evidence.RootElement;
        Assert.Equal(0, document.GetProperty("pins").GetInt32());
        Assert.Equal(1, document.GetProperty("panels").GetArrayLength());
        var guide = Assert.Single(document.GetProperty("fittingGuides").EnumerateArray());
        Assert.Equal("guides.collarFloor", guide.GetProperty("conceptPlane").GetString());
        Assert.Equal(1, guide.GetProperty("offset").GetSingle());
        Assert.Equal(5, guide.GetProperty("releaseAfter").GetInt32());
        Assert.Equal(JsonValueKind.Null, document.GetProperty("drape").ValueKind);
        Assert.DoesNotContain("collarFloor", File.ReadAllText(Path.Combine(output, "garment.obj")));
        Assert.DoesNotContain("collarFloor", File.ReadAllText(Path.Combine(output, "garment.usda")));
    }

    [Theory]
    [InlineData("--substeps", "0")]
    [InlineData("--substeps", "65")]
    [InlineData("--iterations", "0")]
    [InlineData("--iterations", "65")]
    public void InvalidSolverBudgetsStopBeforeReadingInput(string option, string value)
    {
        var stderr = new StringWriter();
        Assert.Equal(1, CliRunner.Run(["garment", "drape", "missing.firmament", option, value], new StringWriter(), stderr));
        Assert.Contains("Unknown or incomplete garment option", stderr.ToString());
    }

    [Theory]
    [InlineData("--substeps", "8")]
    [InlineData("--iterations", "24")]
    [InlineData("--until-settled", null)]
    public void SolverOptionsCannotSilentlyAffectBuildOrPreview(string option, string? value)
    {
        foreach (string operation in new[] { "build", "preview", "inspect", "pose" })
        {
            var args = new List<string> { "garment", operation, "missing.firmament", option };
            if (value is not null)
            {
                args.Add(value);
            }
            var stderr = new StringWriter();
            Assert.Equal(1, CliRunner.Run(args.ToArray(), new StringWriter(), stderr));
            Assert.Contains("Solver and settling options belong to garment drape", stderr.ToString());
        }
    }

    [Fact]
    public void GarmentCodeImportWritesEditablePatternsAndInvalidInputWritesNothing()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        string input = Path.Combine(root!.FullName, "fixtures/Canonical/Garment/GarmentCode/dress.json");
        string output = Path.Combine(root.FullName, "artifacts/local/tests/garment", Guid.NewGuid().ToString("N"));
        string original = File.ReadAllText(input);
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["garment", "import-garmentcode", input, "--out-dir", output, "--json"], stdout, stderr));
        using var evidence = JsonDocument.Parse(stdout.ToString());
        Assert.False(evidence.RootElement.GetProperty("simulated").GetBoolean());
        Assert.False(evidence.RootElement.GetProperty("meshed").GetBoolean());
        Assert.Equal(4, evidence.RootElement.GetProperty("panels").GetArrayLength());
        Assert.Equal(original, File.ReadAllText(Path.Combine(output, "specification.json")));
        string source = Path.Combine(output, "garment.firmament");
        Assert.Equal(0, CliRunner.Run(["garment", "build", source, "--out-dir", Path.Combine(output, "build"), "--json"],
            new StringWriter(), stderr));
        string invalidInput = Path.Combine(output, "invalid.json");
        File.WriteAllText(invalidInput, "{}");
        string rejected = Path.Combine(output, "rejected");
        Assert.Equal(1, CliRunner.Run(["garment", "import-garmentcode", invalidInput, "--out-dir", rejected, "--json"],
            new StringWriter(), stderr));
        Assert.False(Directory.Exists(rejected));
        Assert.Equal(original, File.ReadAllText(input));
    }

    [Fact]
    public void PoseCommandExportsWithoutMeshingAndPreservesBodyIdentity()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        string directory = Path.Combine(root!.FullName, "artifacts/local/tests/garment", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string sourcePath = Path.Combine(directory, "pose.firmament");
        string bodyPath = Path.Combine(directory, "body.json");
        string output = Path.Combine(directory, "posed");
        File.WriteAllText(sourcePath, "Pose fitting { figure: Anatolia; from: TPose; }");
        var canonical = Aetheris.Humanoid.HumanoidRestPoseNormalizer.Normalize(Aetheris.Humanoid.CanonicalAdultTemplate.Create());
        Aetheris.Humanoid.HumanoidGameplayBody.Save(new(Aetheris.Humanoid.HumanoidGameplayBody.Schema,
            "cli-test-body", canonical.Surface, canonical.Skeleton, [], canonical.Provenance), bodyPath);
        byte[] original = File.ReadAllBytes(bodyPath);
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        int code = CliRunner.Run(["garment", "pose", sourcePath, "--body", bodyPath, "--pose", "fitting",
            "--out-dir", output, "--json"], stdout, stderr);
        Assert.True(code is 0 or 2, stderr.ToString());
        using var report = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(report.RootElement.GetProperty("success").GetBoolean() ? 0 : 2, code);
        Assert.False(report.RootElement.GetProperty("simulated").GetBoolean());
        Assert.Equal(canonical.Skeleton.RestPoseId, report.RootElement.GetProperty("restPoseId").GetString());
        Assert.Equal("cli-test-body", report.RootElement.GetProperty("id").GetString());
        Assert.Equal(["body.obj", "evidence.json"], Directory.GetFiles(output).Select(Path.GetFileName).Order());
        Assert.Equal(original, File.ReadAllBytes(bodyPath));
        var body = Aetheris.Humanoid.HumanoidGameplayBody.Load(bodyPath);
        var recipe = Aetheris.Kernel.Firmament.Humanoid.HumanoidPoseAuthoring.Parse(File.ReadAllText(sourcePath)).Poses["fitting"];
        var solved = Aetheris.Kernel.Firmament.Humanoid.HumanoidPoseAuthoring.Solve(recipe, body);
        Assert.Equal(Aetheris.Kernel.Firmament.Garment.GarmentExport.BodyObj(body, body.EvaluatePositions(solved)),
            File.ReadAllText(Path.Combine(output, "body.obj")));
    }

    [Fact]
    public void RealSchemaRoutesThroughValidateInspectBuildAndLanguageFormatting()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        string input = Path.Combine(root!.FullName, "fixtures/Canonical/Garment/skirt.firmament");
        string output = Path.Combine(root.FullName, "artifacts/local/tests/garment", Guid.NewGuid().ToString("N"));
        foreach (string command in new[] { "validate", "inspect" })
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            Assert.True(CliRunner.Run([command, input, "--json"], stdout, stderr) == 0, stderr.ToString());
            using var report = JsonDocument.Parse(stdout.ToString());
            Assert.True(report.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("Garment", report.RootElement.GetProperty("domain").GetString());
        }
        var buildOutput = new StringWriter();
        var errors = new StringWriter();
        string previewDirectory = Path.Combine(output, "preview");
        var previewOutput = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["garment", "preview", input, "--out-dir", previewDirectory, "--json"], previewOutput, errors));
        Assert.Equal(["evidence.json", "patterns.svg"], Directory.GetFiles(previewDirectory).Select(Path.GetFileName).Order());
        using var preview = JsonDocument.Parse(previewOutput.ToString());
        Assert.False(preview.RootElement.GetProperty("meshed").GetBoolean());
        Assert.False(preview.RootElement.GetProperty("simulated").GetBoolean());
        Assert.True(CliRunner.Run(["garment", "build", input, "--out-dir", output, "--json"], buildOutput, errors) == 0, errors.ToString());
        foreach (string file in new[] { "garment.json", "garment.obj", "garment.usda", "patterns.svg", "evidence.json" })
            Assert.True(new FileInfo(Path.Combine(output, file)).Length > 0, file);
        using var artifact = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "garment.json")));
        using var buildEvidence = JsonDocument.Parse(buildOutput.ToString());
        Assert.Equal(0, buildEvidence.RootElement.GetProperty("meshQuality").GetProperty("unreferencedVertices").GetInt32());
        Assert.True(buildEvidence.RootElement.GetProperty("meshQuality").GetProperty("minimumEdgeLength").GetDouble() > 0);
        Assert.Equal("aetheris.garment.v1", artifact.RootElement.GetProperty("schema").GetString());
        Assert.NotEqual(0, CliRunner.Run(["build", input], new StringWriter(), errors));
        Assert.Contains("garment-step-export-unsupported", errors.ToString());
        Assert.NotEqual(0, CliRunner.Run(["garment", "drape", input], new StringWriter(), errors));
        Assert.Contains("garment-body-required", errors.ToString());
    }
}
