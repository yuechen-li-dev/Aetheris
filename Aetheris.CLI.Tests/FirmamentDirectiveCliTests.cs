using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentDirectiveCliTests
{
    [Theory]
    [InlineData("ignored-include", "assembly-include-file-not-found")]
    [InlineData("malformed-include", "firmament-v2-directive-malformed")]
    [InlineData("schema-inline-unknown", "firmament-schema-unknown")]
    [InlineData("schema-comment-unknown", "firmament-schema-unknown")]
    [InlineData("schema-inline-wire", "firmament-schema-mismatch")]
    [InlineData("schema-template-mismatch", "firmament-schema-mismatch")]
    public void ValidateBuildAndParseOnlyCannotBypassDirectives(string name, string code)
    {
        var outputPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../artifacts/local/firmament-directives-casing", Guid.NewGuid() + ".step"));
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Regression/FrontendDirectives", name + ".firmament"));
        foreach (var command in new[] { "validate", "build", "parse-only" })
        {
            var output = new StringWriter(); var error = new StringWriter();
            string[] arguments = command == "build" ? ["build", path, "--output", outputPath, "--json"]
                : ["validate", path, "--json", .. (command == "parse-only" ? new[] { "--parse-only" } : [])];
            Assert.Equal(1, CliRunner.Run(arguments, output, error));
            using var json = JsonDocument.Parse(output.ToString());
            var root = command == "build" ? json.RootElement : json.RootElement.GetProperty("firmamentV2Validation");
            var diagnostics = root.GetProperty("diagnostics").EnumerateArray().ToArray();
            Assert.Contains(diagnostics, d => d.GetProperty("code").GetString()!.StartsWith(code, StringComparison.Ordinal));
            Assert.All(diagnostics, d => Assert.Equal(JsonValueKind.Object, d.ValueKind));
            if (name != "schema-inline-wire") Assert.All(diagnostics, d =>
            {
                Assert.Equal(path, d.GetProperty("sourceFile").GetString());
                Assert.True(d.GetProperty("line").GetInt32() > 0);
                Assert.False(string.IsNullOrWhiteSpace(d.GetProperty("message").GetString()));
            });
            Assert.False(File.Exists(outputPath));
        }
    }
}
