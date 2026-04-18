using System.Text.Json;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class FirmasmAssemblyRoundtripExporterTests
{
    [Fact]
    public void ExportFromFile_NutBolt_WritesStepInteropPackageAndImportableParts()
    {
        var exporter = new FirmasmAssemblyRoundtripExporter();
        var sourcePath = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/firmasm/examples/occt-nut-bolt/nut-bolt-assembly.firmasm");
        var outputDirectory = CreateTempDirectory();

        try
        {
            var exportResult = exporter.ExportFromFile(sourcePath, outputDirectory);

            Assert.True(exportResult.IsSuccess, string.Join(Environment.NewLine, exportResult.Diagnostics.Select(d => d.Message)));
            Assert.Equal(2, exportResult.Value.ExportedInstances.Count);
            Assert.True(File.Exists(exportResult.Value.PackageManifestPath));

            using var document = JsonDocument.Parse(File.ReadAllText(exportResult.Value.PackageManifestPath));
            var root = document.RootElement;
            Assert.Equal("asm-a4-step-instance-package/v1", root.GetProperty("schema").GetString());
            Assert.Equal(".firmasm", root.GetProperty("nativeAuthority").GetString());
            Assert.Equal("outbound-step-interop", root.GetProperty("exportIntent").GetString());
            Assert.Equal(2, root.GetProperty("summary").GetProperty("instanceCount").GetInt32());

            foreach (var instance in exportResult.Value.ExportedInstances)
            {
                Assert.True(File.Exists(instance.StepPath), $"Expected exported STEP file '{instance.StepPath}'.");
                var import = Step242Importer.ImportBody(File.ReadAllText(instance.StepPath));
                Assert.True(import.IsSuccess, $"Failed to import exported STEP '{instance.StepPath}'.");
                Assert.Single(import.Value.Topology.Bodies);
            }
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ExportFromFile_As1_PreservesFlatInstanceCountInPackage()
    {
        var exporter = new FirmasmAssemblyRoundtripExporter();
        var sourcePath = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/firmasm/examples/occt-as1/as1-assembly.firmasm");
        var outputDirectory = CreateTempDirectory();

        try
        {
            var exportResult = exporter.ExportFromFile(sourcePath, outputDirectory);

            Assert.True(exportResult.IsSuccess, string.Join(Environment.NewLine, exportResult.Diagnostics.Select(d => d.Message)));
            Assert.Equal(18, exportResult.Value.ExportedInstances.Count);
            Assert.Equal(18, exportResult.Value.ComposedBodyCount);

            using var document = JsonDocument.Parse(File.ReadAllText(exportResult.Value.PackageManifestPath));
            var root = document.RootElement;
            Assert.Equal(18, root.GetProperty("instances").GetArrayLength());
            Assert.Equal(18, root.GetProperty("summary").GetProperty("composedBodyCount").GetInt32());
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aetheris-asm-a4-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
