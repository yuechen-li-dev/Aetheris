using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class Sol1CliTests
{
    [Fact]
    public void SculptureBuild_WritesStepEvidenceAndPreviewThroughDedicatedLane()
    {
        var root = RepositoryRoot();
        var source = Path.Combine(root, "fixtures", "Canonical", "VirtualSculpture", "sol-1.sculpture.json");
        var outputDirectory = Path.Combine(Path.GetTempPath(), "aetheris-sol-1-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        try
        {
            var step = Path.Combine(outputDirectory, "sol-1.step");
            var evidence = Path.Combine(outputDirectory, "sol-1.evidence.json");
            var preview = Path.Combine(outputDirectory, "sol-1.preview.svg");
            var stdout = new StringWriter(); var stderr = new StringWriter();
            var exit = CliRunner.Run(["sculpture", "build", source, "--out", step, "--evidence", evidence, "--preview", preview, "--json"], stdout, stderr);
            Assert.Equal(0, exit);
            Assert.Equal(string.Empty, stderr.ToString());
            Assert.True(File.Exists(step)); Assert.True(File.Exists(evidence)); Assert.True(File.Exists(preview));
            Assert.StartsWith("ISO-10303-21;", File.ReadAllText(step), StringComparison.Ordinal);
            Assert.Contains("<svg", File.ReadAllText(preview), StringComparison.Ordinal);
            using var command = JsonDocument.Parse(stdout.ToString());
            Assert.Equal("VirtualSculpture", command.RootElement.GetProperty("domain").GetString());
            Assert.Equal("Virtual", command.RootElement.GetProperty("mode").GetString());
            using var report = JsonDocument.Parse(File.ReadAllText(evidence));
            Assert.False(report.RootElement.GetProperty("isManufacturingGeometry").GetBoolean());
            Assert.Equal(0, report.RootElement.GetProperty("surfaceInventory").GetProperty("rationalProductSurfaces").GetInt32());
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Aetheris.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
