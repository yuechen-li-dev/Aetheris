using System.Text.Json;
using Aetheris.CLI;

namespace Aetheris.CLI.Tests;

public sealed class FeatureFunctionCliTests
{
    [Fact]
    public void Inspect_ReportsFeatureDefinitionsInvocationsExpansionAndSemanticKind()
    {
        var output = new StringWriter(); var error = new StringWriter();
        var exit = CliRunner.Run(["inspect", Fixture("Canonical/Feature/feature-with-derived-values.firmament"), "--json"], output, error);

        Assert.Equal(0, exit);
        Assert.Equal(string.Empty, error.ToString());
        using var json = JsonDocument.Parse(output.ToString());
        var root = json.RootElement;
        Assert.Equal("M8Counterbore", root.GetProperty("featureDefinitions")[0].GetProperty("name").GetString());
        Assert.Equal("M8Counterbore", root.GetProperty("featureInvocations")[0].GetProperty("generatedByFeature").GetString());
        Assert.Equal("Hole<Counterbore>", root.GetProperty("featureInvocations")[0].GetProperty("expandedSemanticKind").GetString());
        Assert.Equal(1, root.GetProperty("featureExpansion").GetProperty("definitionCount").GetInt32());
        Assert.Equal(1, root.GetProperty("featureExpansion").GetProperty("invocationCount").GetInt32());
        Assert.Equal("Hole<Counterbore> M8Counterbore__0", root.GetProperty("features")[0].GetString());
    }

    [Fact]
    public void Validate_ReturnsFailureAndFatalTypedFeatureDiagnostic()
    {
        var output = new StringWriter(); var error = new StringWriter();
        var exit = CliRunner.Run(["validate", Fixture("Invalid/Feature/wrong-argument-type.firmament"), "--json"], output, error);

        Assert.Equal(1, exit);
        using var json = JsonDocument.Parse(output.ToString());
        var report = json.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("invalid", report.GetProperty("status").GetString());
        Assert.Contains(report.GetProperty("diagnostics").EnumerateArray(), diagnostic =>
            diagnostic.GetProperty("severity").GetString() == "fatal"
            && diagnostic.GetProperty("code").GetString()!.StartsWith("firmament-feature-argument-type:MountHole:Center", StringComparison.Ordinal));
    }

    private static string Fixture(string relative) => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "../../../../fixtures", relative.Replace('/', Path.DirectorySeparatorChar)));
}
