using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class AssemblyM0CliTests
{
    [Fact]
    public void AsmInspect_EmitsMachineReadableAssemblyIr()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "fixtures", "AssemblyM0", "bearing-module.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["asm", "inspect", path, "--json"], stdout, stderr);
        Assert.Equal(0, exit);
        using var json = JsonDocument.Parse(stdout.ToString());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        var ir = json.RootElement.GetProperty("assemblyIr");
        Assert.Equal("aetheris/assembly-ir/m0", ir.GetProperty("schema").GetString());
        Assert.Equal(2, ir.GetProperty("mates").GetArrayLength());
        Assert.Equal("passed", ir.GetProperty("toleranceStackups")[0].GetProperty("status").GetString());
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void AsmInspect_FailingAssertReturnsFailureJsonWithChain()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "fixtures", "AssemblyM0", "bearing-module-failing.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["asm", "inspect", path, "--json"], stdout, stderr);
        Assert.Equal(1, exit);
        using var json = JsonDocument.Parse(stdout.ToString());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(5, json.RootElement.GetProperty("assemblyIr").GetProperty("toleranceStackups")[0].GetProperty("contributions").GetArrayLength());
        Assert.Contains(json.RootElement.GetProperty("diagnostics").EnumerateArray(), x => x.GetProperty("code").GetString() == "assembly-tolerance-assertion-failure");
    }

    [Fact]
    public void AsmInspect_TemplateAssembly_EmitsExecutableGeometryResidualsAndDefinitionReuseSeam()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "fixtures", "AssemblyM1", "template-block-pair.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["asm", "inspect", path, "--json"], stdout, stderr);
        Assert.Equal(0, exit);
        using var json = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("aetheris/assembly-ir/m1", json.RootElement.GetProperty("assemblyIr").GetProperty("schema").GetString());
        var geometry = json.RootElement.GetProperty("geometryArtifact");
        Assert.Equal("aetheris/assembly-geometry/m1", geometry.GetProperty("schema").GetString());
        Assert.Equal(2, geometry.GetProperty("definitions").GetArrayLength());
        Assert.Equal(2, geometry.GetProperty("instances").GetArrayLength());
        Assert.All(geometry.GetProperty("mateResiduals").EnumerateArray(), residual => Assert.True(residual.GetProperty("passed").GetBoolean()));
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void AsmInspect_LegacyFirmasmMigratesAndReportsPlacementAuthority()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "fixtures", "Assembly", "LegacyImports", "examples", "occt-as1", "as1-assembly.firmasm");
        var stdout = new StringWriter(); var stderr = new StringWriter();

        var exit = Aetheris.CLI.CliRunner.Run(["asm", "inspect", path, "--json"], stdout, stderr);

        Assert.Equal(0, exit);
        using var json = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(18, json.RootElement.GetProperty("geometryArtifact").GetProperty("instances").GetArrayLength());
        Assert.All(
            json.RootElement.GetProperty("assemblyIr").GetProperty("instances").EnumerateArray().Where(instance => instance.GetProperty("kind").GetString() == "Part"),
            instance => Assert.Equal("LegacyExplicit", instance.GetProperty("placementAuthority").GetString()));
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void AsmImportStepAndExportAp242_UseRealCurrentProfilePath()
    {
        var root = FindRepoRoot();
        var source = Path.Combine(root, "testdata", "step242", "OCCT", "as1.step");
        var temporary = Path.Combine(Path.GetTempPath(), "aetheris-cli-m2-" + Guid.NewGuid().ToString("N"));
        try
        {
            var importOut = new StringWriter(); var importError = new StringWriter();
            var importExit = Aetheris.CLI.CliRunner.Run(["asm", "import-step", source, "--out", temporary, "--json"], importOut, importError);
            Assert.Equal(0, importExit);
            using var imported = JsonDocument.Parse(importOut.ToString());
            Assert.Equal(5, imported.RootElement.GetProperty("definitionCount").GetInt32());
            Assert.Equal(27, imported.RootElement.GetProperty("occurrenceCount").GetInt32());

            var firmasm = imported.RootElement.GetProperty("firmasmPath").GetString()!;
            var exportedPath = Path.Combine(temporary, "roundtrip.step");
            var exportOut = new StringWriter(); var exportError = new StringWriter();
            var exportExit = Aetheris.CLI.CliRunner.Run(["asm", "export-ap242", firmasm, "--out", exportedPath, "--json"], exportOut, exportError);
            Assert.Equal(0, exportExit);
            Assert.True(File.Exists(exportedPath));
            using var exported = JsonDocument.Parse(exportOut.ToString());
            Assert.Equal(27, exported.RootElement.GetProperty("occurrenceCount").GetInt32());
            Assert.Equal(5, exported.RootElement.GetProperty("definitionCount").GetInt32());
            Assert.Empty(importError.ToString());
            Assert.Empty(exportError.ToString());
        }
        finally
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }

    [Fact]
    public void AsmInspect_PositiveVolumeInterferenceReturnsFatalStructuredDiagnostic()
    {
        var root = FindRepoRoot();
        var canonical = Path.Combine(root, "fixtures", "AssemblyM1", "template-block-pair.firmament");
        var source = File.ReadAllText(canonical).Replace(
            "Lower PlaneCoincident Moving.Base Fixed.Seat;",
            "Lower PlaneCoincident Moving.Seat Fixed.Seat;",
            StringComparison.Ordinal);
        var temporary = Path.Combine(Path.GetTempPath(), "aetheris-cli-overlap-" + Guid.NewGuid().ToString("N") + ".firmament");
        try
        {
            File.WriteAllText(temporary, source);
            var stdout = new StringWriter(); var stderr = new StringWriter();

            var exit = Aetheris.CLI.CliRunner.Run(["asm", "inspect", temporary, "--json"], stdout, stderr);

            Assert.Equal(1, exit);
            using var json = JsonDocument.Parse(stdout.ToString());
            Assert.False(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Contains(json.RootElement.GetProperty("diagnostics").EnumerateArray(), diagnostic =>
                diagnostic.GetProperty("code").GetString() == "assembly-solid-volume-interference"
                && diagnostic.GetProperty("severity").GetString() == "Error");
            Assert.Empty(stderr.ToString());
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        { if (File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) return directory.FullName; directory = directory.Parent; }
        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
