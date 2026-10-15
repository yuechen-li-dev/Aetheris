using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentTypedToleranceCliTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));

    [Theory]
    [InlineData("unit-mismatch", "firmament-v2-tolerance-unit-mismatch")]
    [InlineData("sweep-unconsumed", "firmament-v2-static-scalar-consumer-unsupported")]
    [InlineData("wireform-unconsumed", "firmament-v2-static-scalar-consumer-unsupported")]
    public void ValidateBuildAndParseOnlyRejectTheSameLocatedTypeError(string specimen, string code)
    {
        var source = Path.Combine(Root, "fixtures/Regression/TypedTolerances", specimen + ".firmament");
        var destination = Path.Combine(Root, "artifacts/local/firmament-typed-tolerances", Guid.NewGuid() + ".step");
        foreach (var command in new[] { "validate", "build", "parse-only" })
        {
            var output = new StringWriter(); var error = new StringWriter();
            string[] arguments = command == "build" ? ["build", source, "--output", destination, "--json"]
                : ["validate", source, "--json", .. (command == "parse-only" ? new[] { "--parse-only" } : [])];
            Assert.Equal(1, CliRunner.Run(arguments, output, error));
            using var json = JsonDocument.Parse(output.ToString());
            var root = command == "build" ? json.RootElement : json.RootElement.GetProperty("firmamentV2Validation");
            var diagnostic = Assert.Single(root.GetProperty("diagnostics").EnumerateArray(),
                d => d.GetProperty("code").GetString() == code);
            Assert.Equal(source, diagnostic.GetProperty("sourceFile").GetString());
            Assert.True(diagnostic.GetProperty("line").GetInt32() > 0);
            if (specimen == "unit-mismatch")
            {
                Assert.Equal(3, diagnostic.GetProperty("line").GetInt32());
                Assert.Contains("mm", diagnostic.GetProperty("message").GetString());
            }
            Assert.False(File.Exists(destination));
        }
    }

    [Fact]
    public void NormalValidateAdmitsTheRealTypedPmiWitness()
    {
        var source = Path.Combine(Root, "fixtures/Canonical/PMI/typed-tolerance.firmament");
        var output = new StringWriter(); var error = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["validate", source, "--json", "--explain"], output, error));
        using var json = JsonDocument.Parse(output.ToString());
        var report = json.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("Built", report.GetProperty("validationLevel").GetString());
        Assert.Equal("valid", report.GetProperty("status").GetString());
    }

    [Fact]
    public void SweepValidationIdentifiesItsActualBuildLevel()
    {
        var source = Path.Combine(Root, "fixtures/Canonical/Features/Sweep/circular-planar-path-sweep.firmament");
        foreach (var parseOnly in new[] { true, false })
        {
            var output = new StringWriter(); var error = new StringWriter();
            Assert.Equal(0, CliRunner.Run(["validate", source, "--json", .. (parseOnly ? new[] { "--parse-only" } : [])], output, error));
            using var json = JsonDocument.Parse(output.ToString());
            var report = json.RootElement.GetProperty("firmamentV2Validation");
            Assert.Equal(parseOnly ? "ParsedNotBuilt" : "Built", report.GetProperty("validationLevel").GetString());
            Assert.Equal("UnifiedSweep", report.GetProperty("parserRoute").GetString());
        }
    }

    [Fact]
    public void LegacyPmiCannotBypassTheSharedExportCapabilityGate()
    {
        var source = Path.Combine(Root, "fixtures/Regression/TypedTolerances/legacy-datum-reference.firmament");
        var destination = Path.Combine(Root, "artifacts/local/firmament-typed-tolerances", Guid.NewGuid() + ".step");
        foreach (var build in new[] { true, false })
        {
            var output = new StringWriter(); var error = new StringWriter();
            Assert.Equal(1, CliRunner.Run(build ? ["build", source, "--output", destination, "--json"] : ["validate", source, "--json"], output, error));
            using var json = JsonDocument.Parse(output.ToString());
            var root = build ? json.RootElement : json.RootElement.GetProperty("firmamentV2Validation");
            Assert.Contains(root.GetProperty("diagnostics").EnumerateArray(), d => d.GetProperty("code").GetString() == "firmament-v2-pmi-datum-reference-export-unsupported");
            Assert.False(File.Exists(destination));
        }
        var parseOutput = new StringWriter(); var parseError = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["validate", source, "--parse-only", "--json"], parseOutput, parseError));
        using var parsed = JsonDocument.Parse(parseOutput.ToString());
        var report = parsed.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("ParsedNotBuilt", report.GetProperty("validationLevel").GetString());
        Assert.Equal("parsed-not-built", report.GetProperty("status").GetString());
        Assert.Contains(report.GetProperty("pmi").EnumerateArray(), p => p.GetProperty("exportSupport").GetString() == "deferred"
            && p.GetProperty("reason").GetString()!.Contains("datum-reference-export-unsupported", StringComparison.Ordinal));
    }
}
