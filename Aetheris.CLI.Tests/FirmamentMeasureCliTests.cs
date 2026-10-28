using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentMeasureCliTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
    private static string Fixture(string name) => Path.Combine(Root(), "fixtures/Regression/Assertions", name + ".firmament");

    [Fact]
    public void CompilerMeasurementsProvideCopyableAssertionsAndDiscloseTheirLevel()
    {
        var output = new StringWriter(); var error = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["measure", Fixture("box"), "--json"], output, error));
        Assert.Empty(error.ToString());
        using var json = JsonDocument.Parse(output.ToString()); var root = json.RootElement;
        Assert.Equal("MeasuredNotAsserted", root.GetProperty("status").GetString());
        Assert.False(root.GetProperty("assertionsEvaluated").GetBoolean());
        Assert.Equal(480, root.GetProperty("volume").GetProperty("valueMm3").GetDouble());
        Assert.Equal(3, root.GetProperty("assertionTemplates").GetArrayLength());
        Assert.Contains("expected: 480mm^3", root.GetProperty("assertionTemplates")[0].GetString());
        Assert.Equal(6, root.GetProperty("topology").GetProperty("faces").GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidateAndBuildRejectTheSameFailedAssertionOnItsAuthoredLine(bool withExpansion)
    {
        var scratch = Path.Combine(Root(), "artifacts/local/tests/assertions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            var path = Path.Combine(scratch, "bad.firmament"); var destination = Path.Combine(scratch, "bad.step");
            var source = (withExpansion ? "Record Unused { value: Length }\n" : "")
                + File.ReadAllText(Fixture("box")).Replace("expected: 480mm^3", "expected: 479mm^3", StringComparison.Ordinal);
            File.WriteAllText(path, source);
            var expectedLine = source[..source.IndexOf("Assert Volume", StringComparison.Ordinal)].Count(c => c == '\n') + 1;
            foreach (var command in new[] { "validate", "build" })
            {
                var output = new StringWriter(); var error = new StringWriter();
                Assert.Equal(1, CliRunner.Run(command == "build" ? [command, path, "--out", destination, "--json"] : [command, path, "--json"], output, error));
                using var json = JsonDocument.Parse(output.ToString()); var root = json.RootElement;
                if (root.TryGetProperty("firmamentV2Validation", out var validation)) root = validation;
                var failure = root.GetProperty("diagnostics").EnumerateArray().Single(d => d.GetProperty("code").GetString() == "firmament-v2-assert-volume-failed");
                Assert.Equal(expectedLine, failure.GetProperty("line").GetInt32());
            }
            Assert.False(File.Exists(destination));
            var measured = new StringWriter(); var measureError = new StringWriter();
            Assert.Equal(0, CliRunner.Run(["measure", path, "--json"], measured, measureError));
            using var report = JsonDocument.Parse(measured.ToString());
            Assert.False(report.RootElement.GetProperty("assertionsEvaluated").GetBoolean());
        }
        finally { Directory.Delete(scratch, true); }
    }

    [Fact]
    public void AssemblyValidationBuildAndInspectionEnforceTheSameStackupAndPreserveItsSpan()
    {
        var scratch = Path.Combine(Root(), "artifacts/local/tests/assertions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            var source = File.ReadAllText(Path.Combine(Root(), "fixtures/Canonical/Assembly/template-block-pair.firmament"))
                .Replace("Between:", "between:", StringComparison.Ordinal)
                .Replace("Require: Clearance >= 2mm", "require: clearance >= 3mm", StringComparison.Ordinal);
            var path = Path.Combine(scratch, "bad-stackup.firmament"); var destination = Path.Combine(scratch, "bad.step");
            File.WriteAllText(path, source);
            foreach (var command in new[] { "validate", "build", "inspect" })
            {
                var output = new StringWriter(); var error = new StringWriter();
                var args = command == "inspect" ? new[] { "asm", "inspect", path, "--json" }
                    : command == "build" ? [command, path, "--out", destination, "--json"] : [command, path, "--json"];
                Assert.Equal(1, CliRunner.Run(args, output, error));
                using var json = JsonDocument.Parse(output.ToString());
                var failure = json.RootElement.GetProperty("diagnostics").EnumerateArray()
                    .Single(d => d.GetProperty("code").GetString() == "assembly-tolerance-assertion-failure");
                Assert.Contains("worst-case minimum 2", failure.GetProperty("message").GetString());
                var span = failure.GetProperty("sourceSpan");
                Assert.StartsWith("Assert ToleranceStackup", source.Substring(span.GetProperty("start").GetInt32(), span.GetProperty("length").GetInt32()));
                if (command == "inspect")
                {
                    var stack = json.RootElement.GetProperty("assemblyIr").GetProperty("toleranceStackups")[0];
                    Assert.Equal(2, stack.GetProperty("worstCaseMinimum").GetDouble());
                    Assert.False(stack.GetProperty("passed").GetBoolean());
                }
            }
            Assert.False(File.Exists(destination));
        }
        finally { Directory.Delete(scratch, true); }
    }

    [Fact]
    public void SdfMeasurementCarriesUncertaintyAndKeepsMeshComparisonDiagnosticOnly()
    {
        var output = new StringWriter(); var error = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["measure", Fixture("sphere"), "--sdf", "--compare-mesh", "--error", "50", "--json"], output, error));
        using var json = JsonDocument.Parse(output.ToString()); var root = json.RootElement;
        var volume = root.GetProperty("volume");
        var expected = 4 * Math.PI * 1000 / 3;
        Assert.InRange(expected, volume.GetProperty("lowerBoundMm3").GetDouble(), volume.GetProperty("upperBoundMm3").GetDouble());
        Assert.True(volume.GetProperty("authoritative").GetBoolean());
        Assert.Equal("SdfAdaptiveOccupiedVolumeInterval", volume.GetProperty("method").GetString());
        Assert.False(volume.GetProperty("budgetExhausted").GetBoolean());
        Assert.InRange(volume.GetProperty("errorBoundMm3").GetDouble(), 0, 50);
        Assert.True(volume.GetProperty("regionsVisited").GetInt32() < 100_001);
        Assert.True(volume.GetProperty("affineCutRegions").GetInt32() > 0);
        Assert.True(root.TryGetProperty("meshComparison", out _));
    }
}
