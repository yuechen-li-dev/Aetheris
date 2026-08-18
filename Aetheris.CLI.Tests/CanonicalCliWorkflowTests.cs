using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class CanonicalCliWorkflowTests
{
    [Fact]
    public void Build_Defaults_To_Adjacent_Step_And_Reports_It()
    {
        var directory = Path.Combine(Path.GetTempPath(), "aetheris cli space " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "plate.firmament");
        var output = Path.ChangeExtension(source, ".step");
        File.WriteAllText(source, """
            Model Plate {
                Units: mm
                Box Body { Size: [10mm, 20mm, 3mm] }
            }
            """);

        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exitCode = Aetheris.CLI.CliRunner.Run(["build", source], stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(output), stderr.ToString());
            Assert.Contains("STEP: " + output, stdout.ToString(), StringComparison.Ordinal);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Inspect_Firmament_Reports_Semantic_Model_As_Json()
    {
        var source = Path.Combine(Path.GetTempPath(), $"inspect-{Guid.NewGuid():N}.firmament");
        File.WriteAllText(source, """
            Model Plate {
                Units: mm
                Box Body { Size: [10mm, 20mm, 3mm] }
            }
            """);
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exitCode = Aetheris.CLI.CliRunner.Run(["inspect", source, "--json"], stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
            using var document = JsonDocument.Parse(stdout.ToString());
            Assert.Equal("inspect", document.RootElement.GetProperty("command").GetString());
            Assert.True(document.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("Plate", document.RootElement.GetProperty("model").GetString());
        }
        finally { File.Delete(source); }
    }

    [Fact]
    public void Inspect_ConceptPath_ReportsTypedCapabilitiesMembersAndConsumers()
    {
        var root = FindRepositoryRoot();
        var source = Path.Combine(root, "fixtures", "Canonical", "valid", "concept-path-compose-profile.firmament");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["inspect", source, "--json"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        using var document = JsonDocument.Parse(stdout.ToString());
        var path = Assert.Single(document.RootElement.GetProperty("conceptPaths").EnumerateArray());
        Assert.Contains(path.GetProperty("capabilities").EnumerateArray(), value => value.GetString() == "ComposeProfileOperand");
        Assert.Contains(path.GetProperty("exposedMembers").EnumerateArray(), member => member.GetProperty("stableId").GetString() == "PlateOutline.South");
        Assert.Contains(path.GetProperty("consumers").EnumerateArray(), consumer => consumer.GetProperty("kind").GetString() == "ComposeOperation");
    }

    [Fact]
    public void Analyze_Rejects_Firmament_With_A_Clear_Route_Hint()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "part.firmament"], stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("STEP", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find the Aetheris repository root.");
    }
}
