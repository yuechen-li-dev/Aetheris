using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class PipingAdmissionCliTests
{
    [Fact]
    public void LowercaseConnectionStyleIsBoundAndGeometryMatchesCanonical()
    {
        var path = Path.Combine(Root(), "fixtures/Regression/FrontendX2/piping-field-casing.firmament");
        var stdout = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["inspect", path, "--json"], stdout, new StringWriter()));
        using var json = JsonDocument.Parse(stdout.ToString());
        var piping = json.RootElement.GetProperty("piping");
        Assert.All(piping.GetProperty("ports").EnumerateArray(), port => Assert.Equal("Weld", port.GetProperty("connectionType").GetString()));
        stdout = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["validate", path, "--json"], stdout, new StringWriter()));
        using var validation = JsonDocument.Parse(stdout.ToString());
        var report = validation.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("Built", report.GetProperty("validationLevel").GetString());
        Assert.Equal("UnifiedPiping", report.GetProperty("parserRoute").GetString());
        var source = File.ReadAllText(path);
        var nominal = File.ReadAllText(Path.Combine(Root(), "fixtures/Canonical/Piping/pipe-segment.firmament"));
        Assert.Equal(Kernel.Firmament.FirmamentBuildAndExport.CompileSource(nominal).Value.StepText,
            Kernel.Firmament.FirmamentBuildAndExport.CompileSource(source).Value.StepText);
    }

    [Theory]
    [InlineData("", "firmament-v2-canonical-field-unknown", 6)]
    [InlineData("schema WireForm\n", "firmament-schema-mismatch", 1)]
    public void ValidateInspectAndBuildAgreeAndPreserveStructuredLocations(string prefix, string code, int line)
    {
        var directory = Path.Combine(Root(), "artifacts/local/tests/piping-admission", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "invalid.firmament"); var output = Path.Combine(directory, "invalid.step");
        File.WriteAllText(source, prefix + File.ReadAllText(Path.Combine(Root(), "fixtures/Invalid/Piping/unknown-port-field.firmament")));
        try
        {
            foreach (var operation in new[] { "validate", "inspect", "build" })
            {
                var stdout = new StringWriter(); var stderr = new StringWriter();
                var args = operation == "build" ? new[] { operation, source, "--out", output, "--json" } : [operation, source, "--json"];
                Assert.NotEqual(0, CliRunner.Run(args, stdout, stderr));
                using var json = JsonDocument.Parse(stdout.ToString());
                var result = operation == "validate" ? json.RootElement.GetProperty("firmamentV2Validation") : json.RootElement;
                var diagnostic = Assert.Single(result.GetProperty("diagnostics").EnumerateArray(), d => d.GetProperty("code").GetString()!.Split(':')[0] == code);
                Assert.Equal(line, diagnostic.GetProperty("line").GetInt32());
                Assert.Equal(Path.GetFullPath(source), Path.GetFullPath(diagnostic.GetProperty("sourceFile").GetString()!));
                Assert.NotEmpty(diagnostic.GetProperty("message").GetString()!);
                if (prefix.Length == 0)
                {
                    var authored = File.ReadAllText(source);
                    var start = authored.IndexOf("unexpectedIntent:", StringComparison.Ordinal);
                    Assert.Equal(start, diagnostic.GetProperty("sourceSpan").GetProperty("start").GetInt32());
                    Assert.Equal(start - authored.LastIndexOf('\n', start), diagnostic.GetProperty("column").GetInt32());
                }
                Assert.False(File.Exists(output));
            }
            var textError = new StringWriter();
            Assert.NotEqual(0, CliRunner.Run(["validate", source], new StringWriter(), textError));
            Assert.Contains(code, textError.ToString());
            Assert.Contains(prefix.Length == 0 ? $":{line}:" : $"({line},", textError.ToString());
        }
        finally { File.Delete(source); Directory.Delete(directory); }
    }

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Aetheris.slnx"))) d = d.Parent;
        return d?.FullName ?? throw new DirectoryNotFoundException();
    }
}
