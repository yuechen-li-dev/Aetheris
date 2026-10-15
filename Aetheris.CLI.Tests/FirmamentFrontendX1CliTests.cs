using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentFrontendX1CliTests
{
    [Fact]
    public void CorpusUsesAssemblyAuthorityAndRetainsNegativeSyntaxEvidence()
    {
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-corpus-x1-" + Guid.NewGuid().ToString("N"));
        var input = Path.Combine(directory, "sources"); var output = Path.Combine(directory, "evidence");
        Directory.CreateDirectory(input);
        try
        {
            File.Copy(Fixture("Regression/FrontendX1/assembly-modern-contract.firmament"), Path.Combine(input, "pair.FIRMAMENT"));
            File.Copy(Fixture("Regression/FrontendX1/sculpt-leading-plus.invalid.firmament"), Path.Combine(input, "sculpt.invalid.firmament"));
            Assert.Equal(0, CliRunner.Run(["frontend-shadow", "--corpus", input, "--production", "--output-dir", output], new StringWriter(), new StringWriter()));
            using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "report.json")));
            Assert.Equal(2, report.RootElement.GetProperty("sourceCount").GetInt32());
            var rows = report.RootElement.GetProperty("entries").EnumerateArray().ToArray();
            var assembly = Assert.Single(rows, e => e.GetProperty("owner").GetString() == "Assembly");
            Assert.Equal(0, assembly.GetProperty("productionExit").GetInt32());
            Assert.True(assembly.GetProperty("structuralSuccess").GetBoolean());
            Assert.Equal("NegativeFixtureRejected", Assert.Single(rows, e => e.GetProperty("source").GetString()!.Contains("sculpt")).GetProperty("classification").GetString());
            Assert.All(File.ReadLines(Path.Combine(output, "entries.jsonl")), line =>
            {
                using var row = JsonDocument.Parse(line);
                Assert.False(string.IsNullOrWhiteSpace(row.RootElement.GetProperty("sha256").GetString()));
            });
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void SculptingCannotDiscardInvalidSourceBeforeItsModel()
    {
        var source = Fixture("Regression/FrontendX1/sculpt-leading-plus.invalid.firmament");
        var validation = new StringWriter();
        Assert.Equal(1, CliRunner.Run(["validate", source, "--json"], validation, new StringWriter()));
        using var json = JsonDocument.Parse(validation.ToString());
        var diagnostics = json.RootElement.GetProperty("firmamentV2Validation").GetProperty("diagnostics");
        Assert.Contains(diagnostics.EnumerateArray(), d => d.GetProperty("code").GetString() == "firmament-syntax-statement"
            && d.GetProperty("line").GetInt32() == 1 && d.GetProperty("column").GetInt32() == 1);
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-x1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var output = Path.Combine(directory, "rejected.step");
            Assert.Equal(1, CliRunner.Run(["build", source, "--output", output, "--json"], new StringWriter(), new StringWriter()));
            Assert.False(File.Exists(output));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Explain_ProductionRejectsStructurallyRecognizedUnsupportedIntent()
    {
        var stdout = new StringWriter();
        var source = Fixture("Regression/FrontendX0/unknown-compose-statement.firmament");
        Assert.Equal(1, CliRunner.Run(["validate", source, "--shadow", "--explain", "--json"], stdout, new StringWriter()));
        using var json = JsonDocument.Parse(stdout.ToString());
        var root = json.RootElement; var shadow = root.GetProperty("shadow");
        Assert.Equal("Parsed", shadow.GetProperty("shadowStatus").GetString());
        Assert.Equal("StructuralOnlyUnbound", shadow.GetProperty("validationLevel").GetString());
        Assert.Equal("UnifiedMechanical", shadow.GetProperty("productionProjection").GetProperty("parserRoute").GetString());
        Assert.False(shadow.GetProperty("comparison").GetProperty("semanticParityQualified").GetBoolean());

        Assert.Equal("invalid", root.GetProperty("productionValidation").GetProperty("firmamentV2Validation").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("ast").ValueKind);
    }

    [Fact]
    public void ExplicitAst_ContainsOriginalFieldsAndSpans()
    {
        var stdout = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["frontend-shadow", Fixture("Regression/FrontendX0/unknown-boundary-field.firmament"), "--ast"], stdout, new StringWriter()));
        using var json = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(JsonValueKind.Object, json.RootElement.GetProperty("ast").ValueKind);
        Assert.Contains("heigth", json.RootElement.GetProperty("ast").GetRawText());
        Assert.Contains("\"span\"", json.RootElement.GetProperty("ast").GetRawText());
    }

    [Fact]
    public void StructuralFailure_IsNotMaskedByProductionProjection()
    {
        var stdout = new StringWriter();
        Assert.Equal(1, CliRunner.Run(["frontend-shadow", Fixture("Regression/FrontendX1/malformed-field.firmament"), "--json"], stdout, new StringWriter()));
        using var json = JsonDocument.Parse(stdout.ToString());
        var shadow = json.RootElement.GetProperty("shadow");
        Assert.Equal("Rejected", shadow.GetProperty("shadowStatus").GetString());
        var diagnostic = Assert.Single(shadow.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("Syntax", diagnostic.GetProperty("stage").GetString());
        Assert.Equal("Box Body", diagnostic.GetProperty("target").GetString());
        Assert.Equal(2, diagnostic.GetProperty("line").GetInt32());
    }

    private static string Fixture(string relative) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures", relative));
}
