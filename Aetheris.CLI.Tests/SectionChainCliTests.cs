using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class SectionChainCliTests
{
    [Fact]
    public void FirmamentFileBuildInspectAndValidateUseTheSameSectionChainPipeline()
    {
        var input = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Canonical/SectionChain/two-section-ruled.firmament"));
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-section-chain-authored-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var output = Path.Combine(directory, "authored.step");
            var buildOut = new StringWriter(); var buildErr = new StringWriter();
            Assert.Equal(0, CliRunner.Run(["section-chain", "build", input, "--out", output, "--json"], buildOut, buildErr));
            Assert.Equal(string.Empty, buildErr.ToString());
            using var built = JsonDocument.Parse(buildOut.ToString());
            Assert.Equal("TwoSectionRuled", built.RootElement.GetProperty("sectionChain").GetProperty("stableId").GetString());
            Assert.True(built.RootElement.GetProperty("pcurves").GetProperty("loopClosureValid").GetBoolean());

            foreach (var operation in new[] { "inspect", "validate" })
            {
                var stdout = new StringWriter(); var stderr = new StringWriter();
                Assert.Equal(0, CliRunner.Run(["section-chain", operation, input, "--json"], stdout, stderr));
                using var report = JsonDocument.Parse(stdout.ToString());
                Assert.True(report.RootElement.GetProperty("success").GetBoolean());
                Assert.Equal(2, report.RootElement.GetProperty("sections").GetArrayLength());
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void FlagshipBuildWritesStepAndStructuredChainEvidence()
    {
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-section-chain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var output = Path.Combine(directory, "flagship.step");
            var stdout = new StringWriter(); var stderr = new StringWriter();
            var exit = CliRunner.Run(["section-chain", "build", "flagship", "--out", output, "--json"], stdout, stderr);
            Assert.Equal(0, exit);
            Assert.Equal(string.Empty, stderr.ToString());
            Assert.True(File.Exists(output));
            Assert.True(File.Exists(Path.ChangeExtension(output, ".evidence.json")));
            using var report = JsonDocument.Parse(stdout.ToString());
            Assert.Equal(8, report.RootElement.GetProperty("sectionChain").GetProperty("sectionCount").GetInt32());
            Assert.Equal(7, report.RootElement.GetProperty("sectionChain").GetProperty("transitionCount").GetInt32());
            Assert.Equal(0, report.RootElement.GetProperty("rationalProductSurfaces").GetInt32());
            Assert.True(report.RootElement.GetProperty("stepReimport").GetProperty("success").GetBoolean());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
