using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentFrontendX0CliTests
{
    [Theory]
    [InlineData("malformed-v2")]
    [InlineData("unknown-model-statement")]
    [InlineData("unknown-modify-statement")]
    [InlineData("unknown-box-field")]
    [InlineData("unknown-compose-statement")]
    [InlineData("unknown-boundary-field")]
    [InlineData("unsupported-xz-layout")]
    [InlineData("combined-fillet-hole-intersection")]
    [InlineData("unresolved-semantic-source")]
    [InlineData("edgefinish-generic-variant")]
    public void ValidateAndBuild_RejectSameSource_WithStructuredDiagnostics(string fixture)
    {
        var path = Fixture(fixture);
        var validation = new StringWriter(); var build = new StringWriter();
        Assert.Equal(1, CliRunner.Run(["validate", path, "--json"], validation, new StringWriter()));
        Assert.Equal(1, CliRunner.Run(["build", path, "--json"], build, new StringWriter()));
        using var v = JsonDocument.Parse(validation.ToString()); using var b = JsonDocument.Parse(build.ToString());
        Assert.Equal("invalid", v.RootElement.GetProperty("firmamentV2Validation").GetProperty("status").GetString());
        Assert.False(b.RootElement.GetProperty("success").GetBoolean());
        foreach (var diagnostic in v.RootElement.GetProperty("firmamentV2Validation").GetProperty("diagnostics").EnumerateArray()
            .Concat(b.RootElement.GetProperty("diagnostics").EnumerateArray()))
        {
            Assert.Equal(JsonValueKind.Object, diagnostic.ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(diagnostic.GetProperty("code").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(diagnostic.GetProperty("message").GetString()));
            Assert.Equal("fatal", diagnostic.GetProperty("severity").GetString());
            Assert.Equal(path, diagnostic.GetProperty("sourceFile").GetString());
            Assert.True(diagnostic.GetProperty("line").GetInt32() > 0);
        }
    }

    [Fact]
    public void ParseOnly_IsExplicitlyQualified_AndExplainIdentifiesTheRoute()
    {
        var stdout = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["validate", Fixture("combined-fillet-hole"), "--parse-only", "--explain"], stdout, new StringWriter()));
        Assert.Contains("parsed-not-built", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("UnifiedMechanical", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("ParsedNotBuilt", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void DisjointFilletAndHole_ValidateAndBuildBothAdmitTheOriginalSource()
    {
        var path = Fixture("combined-fillet-hole");
        var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../artifacts/local/fillet-hole-cli-tests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        var output = Path.Combine(directory, "combined.step");
        var validation = new StringWriter();
        var build = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["validate", path, "--json"], validation, new StringWriter()));
        Assert.Equal(0, CliRunner.Run(["build", path, "--output", output, "--json"], build, new StringWriter()));
        using var v = JsonDocument.Parse(validation.ToString());
        using var b = JsonDocument.Parse(build.ToString());
        Assert.Equal("valid", v.RootElement.GetProperty("firmamentV2Validation").GetProperty("status").GetString());
        Assert.Equal("Built", v.RootElement.GetProperty("firmamentV2Validation").GetProperty("validationLevel").GetString());
        Assert.True(b.RootElement.GetProperty("success").GetBoolean());
        Assert.True(File.Exists(output));
    }

    [Fact]
    public void TextBuildSummary_IdentifiesDocumentOwner()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Canonical/Features/EdgeFinish/semantic-selection-chamfer.firmament"));
        var output = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".step");
        try
        {
            var stdout = new StringWriter();
            Assert.Equal(0, CliRunner.Run(["build", path, "--output", output], stdout, new StringWriter()));
            Assert.Contains("Model: SelectionChamferPart", stdout.ToString(), StringComparison.Ordinal);
        }
        finally { File.Delete(output); }
    }

    private static string Fixture(string name) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "../../../../fixtures/Regression/FrontendX0", name + ".firmament"));
}
