using System.Text.Json;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class RuledStepProbeHarnessTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Harness_Workflow_Generates_Deterministic_Wrapper_And_Reimports_Ruled_Probe()
    {
        var sourceProbe = Path.Combine(RepoRoot, "testdata", "step242", "generated", "ruled-a2", "ellipse-linear-extrusion-production.step");
        Assert.True(File.Exists(sourceProbe), $"Expected ruled production probe at '{sourceProbe}'.");

        var root = Path.Combine(Path.GetTempPath(), "aetheris-ruled-step-probe", Guid.NewGuid().ToString("N"));
        var inputDir = Path.Combine(root, "input");
        var wrapperDir = Path.Combine(root, "wrapper");
        var outputDir = Path.Combine(root, "output");
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(wrapperDir);
        Directory.CreateDirectory(outputDir);

        const string probeName = "ellipseLinearExtrusionProduction";
        var originalProbeName = Path.GetFileName(sourceProbe);
        var rawProbeCopy = Path.Combine(inputDir, originalProbeName);
        var canonicalInput = Path.Combine(inputDir, probeName + ".canonical-input.step");
        var wrapperPath = Path.Combine(wrapperDir, probeName + ".firm");
        var outputStepPath = Path.Combine(outputDir, probeName + ".canonical.step");

        File.Copy(sourceProbe, rawProbeCopy, overwrite: true);

        try
        {
            var canon = RunCli("canon", rawProbeCopy, "--out", canonicalInput, "--mode", "production", "--json");
            Assert.Equal(0, canon.ExitCode);
            Assert.True(File.Exists(canonicalInput));

            var wrapper = BuildWrapperSource(probeName, Path.GetFileName(canonicalInput));
            File.WriteAllText(wrapperPath, wrapper);

            Assert.Equal(Path.Combine(wrapperDir, "ellipseLinearExtrusionProduction.firm"), wrapperPath);
            Assert.Equal(Path.Combine(outputDir, "ellipseLinearExtrusionProduction.canonical.step"), outputStepPath);
            Assert.Contains("InlineStep", wrapper, StringComparison.Ordinal);
            Assert.Contains("../input/ellipseLinearExtrusionProduction.canonical-input.step", wrapper, StringComparison.Ordinal);

            var build = RunCli("build", wrapperPath, "--out", outputStepPath, "--json");
            Assert.Equal(0, build.ExitCode);
            Assert.True(File.Exists(outputStepPath));

            var stepText = File.ReadAllText(outputStepPath);
            Assert.Contains("SURFACE_OF_LINEAR_EXTRUSION", stepText, StringComparison.Ordinal);
            Assert.Contains("ELLIPSE", stepText, StringComparison.Ordinal);
            Assert.DoesNotContain("B_SPLINE_SURFACE_WITH_KNOTS", stepText, StringComparison.Ordinal);

            var import = Step242Importer.ImportBody(stepText);
            Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
            Assert.Contains(import.Value.Geometry.Surfaces, entry => entry.Value.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.LinearExtrusion);

            var reimportSmokePath = Path.Combine(outputDir, probeName + ".reimport-smoke.step");
            var reimport = RunCli("canon", outputStepPath, "--out", reimportSmokePath, "--json");
            Assert.Equal(0, reimport.ExitCode);
            Assert.True(File.Exists(reimportSmokePath));

            var analyze = RunCli("analyze", outputStepPath, "--json");
            using var doc = JsonDocument.Parse(analyze.StdOut);
            if (analyze.ExitCode == 0)
            {
                var summary = doc.RootElement.GetProperty("summary");
                Assert.Equal(1, summary.GetProperty("bodyCount").GetInt32());
                Assert.True(summary.GetProperty("faceCount").GetInt32() >= 1);
            }
            else
            {
                Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
                Assert.True(doc.RootElement.TryGetProperty("errorKind", out _));
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Harness_Documentation_And_Script_Are_Present_And_Reference_InlineStep()
    {
        var scriptPath = Path.Combine(RepoRoot, "tools", "Run-RuledStepProbe.ps1");
        var docPath = Path.Combine(RepoRoot, "docs", "development", "implementation", "ruled-tooling-a0-inline-step-probe-harness.md");
        var ruledA2Path = Path.Combine(RepoRoot, "docs", "development", "implementation", "ruled-a2-linear-extrusion-production-and-snapshot-refresh.md");

        Assert.True(File.Exists(scriptPath), $"Expected ruled probe script at '{scriptPath}'.");
        Assert.True(File.Exists(docPath), $"Expected ruled probe doc at '{docPath}'.");

        var script = File.ReadAllText(scriptPath);
        Assert.Contains("param(", script, StringComparison.Ordinal);
        Assert.Contains("InlineStep", script, StringComparison.Ordinal);
        Assert.Contains("probe-report.json", script, StringComparison.Ordinal);
        Assert.Contains("Validate-Step-FreeCAD.ps1", script, StringComparison.Ordinal);

        var doc = File.ReadAllText(docPath);
        Assert.Contains("Run-RuledStepProbe.ps1", doc, StringComparison.Ordinal);
        Assert.Contains("InlineStep", doc, StringComparison.Ordinal);
        Assert.Contains("tooling only", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("artifacts/local/demos/ruled-probes/<probe-name>/", doc, StringComparison.Ordinal);

        var ruledA2 = File.ReadAllText(ruledA2Path);
        Assert.Contains("ruled-tooling-a0-inline-step-probe-harness.md", ruledA2, StringComparison.Ordinal);
    }

    private static string BuildWrapperSource(string identifier, string stagedFileName) =>
        $@"model {identifier}ProbeHarness {{
    units mm

    solid {identifier}: InlineStep {{
        path: ""../input/{stagedFileName}""
    }}
}}";

    private static (int ExitCode, string StdOut, string StdErr) RunCli(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = CliRunner.Run(args, stdout, stderr);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root for ruled probe tests.");
    }
}
