using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentFrontendX2CliTests
{
    [Fact]
    public void ExplainReportsTheActualFeatureScopeAndExpansionWithoutClaimingCompleteMaps()
    {
        var path = Fixture("scoped-drill.firmament"); var stdout = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["validate", path, "--json", "--explain"], stdout, new StringWriter()));
        using var json = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("valid", json.RootElement.GetProperty("firmamentV2Validation").GetProperty("status").GetString());
        var semantics = json.RootElement.GetProperty("frontendSemantics");
        Assert.Equal("UnifiedAstFeature", semantics.GetProperty("featureOwner").GetString());
        var invocation = Assert.Single(semantics.GetProperty("featureInvocations").EnumerateArray());
        Assert.Equal("document/Model ScopedPlate", invocation.GetProperty("bindingScope").GetString());
        Assert.Equal("Drill", invocation.GetProperty("definitionOrigin").GetProperty("binding").GetString());
        Assert.Equal("inline", invocation.GetProperty("invocationOrigin").GetProperty("sourceFile").GetString());
        Assert.Contains("pending", semantics.GetProperty("sourceProvenance").GetString());
    }

    [Theory]
    [InlineData("sibling-feature.invalid.firmament", "firmament-feature-unknown:Local", 8)]
    [InlineData("sibling-data.invalid.firmament", "firmament-binding-name-not-visible:secret", 6)]
    public void UnresolvedSiblingIsRejectedByValidateAndBuildWithTheAuthoredLocation(string fixture, string code, int line)
    {
        var path = Fixture(fixture); var validation = new StringWriter();
        Assert.Equal(1, CliRunner.Run(["validate", path, "--json"], validation, new StringWriter()));
        using var json = JsonDocument.Parse(validation.ToString());
        var diagnostic = Assert.Single(json.RootElement.GetProperty("firmamentV2Validation").GetProperty("diagnostics").EnumerateArray(),
            d => d.GetProperty("code").GetString()!.StartsWith(code, StringComparison.Ordinal));
        Assert.Equal(code, diagnostic.GetProperty("code").GetString());
        Assert.Equal(line, diagnostic.GetProperty("line").GetInt32());
        Assert.Contains("not visible", diagnostic.GetProperty("message").GetString());
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-x2-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var output = Path.Combine(directory, "part.step"); var stdout = new StringWriter();
            Assert.Equal(1, CliRunner.Run(["build", path, "--output", output, "--json"], stdout, new StringWriter()));
            Assert.False(File.Exists(output));
            Assert.Contains(code, stdout.ToString());
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void TemplateShadowingAndSiblingRejectionUseTheNormalAdmissionPath()
    {
        var stdout = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["validate", Fixture("template-shadowing.firmament"), "--json", "--explain"], stdout, new StringWriter()));
        using var positive = JsonDocument.Parse(stdout.ToString());
        var semantics = positive.RootElement.GetProperty("frontendSemantics");
        Assert.Contains("UnifiedAstTemplate", semantics.GetProperty("templateOwner").GetString());
        var receipt = Assert.Single(semantics.GetProperty("templateInstantiations").EnumerateArray());
        Assert.Equal("30mm", receipt.GetProperty("valueArguments").GetProperty("width").GetString());
        Assert.StartsWith("document/Model LocalFamily", receipt.GetProperty("definitionScope").GetString());
        stdout = new StringWriter();
        var path = Fixture("sibling-template.invalid.firmament");
        Assert.Equal(1, CliRunner.Run(["validate", path, "--json"], stdout, new StringWriter()));
        using var negative = JsonDocument.Parse(stdout.ToString());
        var diagnostic = Assert.Single(negative.RootElement.GetProperty("firmamentV2Validation").GetProperty("diagnostics").EnumerateArray(),
            d => d.GetProperty("code").GetString() == "firmament-template-not-found:Family");
        Assert.Equal(8, diagnostic.GetProperty("line").GetInt32());
        Assert.Contains("not visible", diagnostic.GetProperty("message").GetString());
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-x2-template-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var output = Path.Combine(directory, "part.step");
            Assert.Equal(1, CliRunner.Run(["build", path, "--output", output, "--json"], new StringWriter(), new StringWriter()));
            Assert.False(File.Exists(output));
        }
        finally { Directory.Delete(directory, true); }
    }
    private static string Fixture(string file) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "../../../../fixtures/Regression/FrontendX2", file));
}
