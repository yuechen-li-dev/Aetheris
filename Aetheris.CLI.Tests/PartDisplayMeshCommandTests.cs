using System.Text.Json;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class PartDisplayMeshCommandTests
{
    [Fact]
    public void DisplayAndExactStepComeFromTheAdmittedPartAndRejectWithoutReplacingArtifacts()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var directory = Path.Combine(root!.FullName, "artifacts/local/tests/part-display", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var fixture = Path.Combine(root.FullName, "fixtures/Canonical/Learn/01-plate.firmament");
        var display = Path.Combine(directory, "part.json");
        var step = Path.Combine(directory, "part.step");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        int Run(string source) => CliRunner.Run(["mesh", source, "--format", "display-json", "--output", display, "--step-output", step, "--json"], stdout, stderr);
        Assert.True(Run(fixture) == 0, stdout + stderr.ToString());
        using var document = JsonDocument.Parse(File.ReadAllText(display));
        var definition = Assert.Single(document.RootElement.GetProperty("definitions").EnumerateArray());
        Assert.Equal("mm", document.RootElement.GetProperty("units").GetString());
        var positions = definition.GetProperty("positions").EnumerateArray().Select(v => v.GetDouble()).ToArray();
        Assert.Equal(positions.Length, definition.GetProperty("normals").GetArrayLength());
        Assert.True(definition.GetProperty("indices").GetArrayLength() > 0);
        Assert.Equal(-40, positions.Where((_, i) => i % 3 == 0).Min(), 8);
        Assert.Equal(40, positions.Where((_, i) => i % 3 == 0).Max(), 8);
        Assert.Equal(8, positions.Where((_, i) => i % 3 == 2).Max(), 8);
        var imported = Step242Importer.ImportBody(File.ReadAllText(step));
        Assert.True(imported.IsSuccess);
        Assert.Equal(7, imported.Value.Topology.Faces.Count());
        var acceptedDisplay = File.ReadAllBytes(display); var acceptedStep = File.ReadAllBytes(step);
        var invalid = Path.Combine(directory, "invalid.firmament");
        File.WriteAllText(invalid, File.ReadAllText(fixture).Replace("diameter: 8mm", "diameter: -8mm"));
        Assert.NotEqual(0, Run(invalid));
        Assert.Equal(acceptedDisplay, File.ReadAllBytes(display));
        Assert.Equal(acceptedStep, File.ReadAllBytes(step));
        Assert.NotEqual(0, CliRunner.Run(["mesh", fixture, "--format", "obj", "--step-output", step], stdout, stderr));
        Assert.Equal(acceptedStep, File.ReadAllBytes(step));
    }
}
