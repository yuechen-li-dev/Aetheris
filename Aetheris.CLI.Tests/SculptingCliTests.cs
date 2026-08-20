using System.Text.Json;
using Aetheris.CLI;

namespace Aetheris.CLI.Tests;

public sealed class SculptingCliTests
{
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

    private static string Root() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "Aetheris.slnx"))) d = d.Parent; return d!.FullName; }
}
