using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentFrontendX1CliTests
{
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
