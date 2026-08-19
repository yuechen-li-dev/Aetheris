using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentV2DiagnosticRoutingCliTests
{
    [Fact]
    public void BuildAndValidate_RecognizedMalformedConceptStruct_ExposeTheSameFatalV2DiagnosticsAsJson()
    {
        var fixture = FixturePath();
        var buildOut = new StringWriter();
        var validateOut = new StringWriter();

        var buildExit = CliRunner.Run(["build", fixture, "--json"], buildOut, new StringWriter());
        var validateExit = CliRunner.Run(["validate", fixture, "--json"], validateOut, new StringWriter());

        Assert.Equal(1, buildExit);
        Assert.Equal(1, validateExit);
        using var build = JsonDocument.Parse(buildOut.ToString());
        using var validate = JsonDocument.Parse(validateOut.ToString());
        var buildMessages = build.RootElement.GetProperty("diagnostics").EnumerateArray().Select(diagnostic => diagnostic.GetProperty("message").GetString()).ToArray();
        var validationDiagnostics = validate.RootElement.GetProperty("firmamentV2Validation").GetProperty("diagnostics").EnumerateArray().ToArray();

        Assert.Contains("HoleLocalCenterInvalid", buildMessages);
        Assert.Contains("firmament-concept-missing-member:BracketConcept.RequiredExpose", buildMessages);
        Assert.Contains(validationDiagnostics, diagnostic => diagnostic.GetProperty("code").GetString() == "HoleLocalCenterInvalid" && diagnostic.GetProperty("severity").GetString() == "fatal");
        Assert.Contains(validationDiagnostics, diagnostic => diagnostic.GetProperty("code").GetString() == "firmament-concept-missing-member:BracketConcept.RequiredExpose" && diagnostic.GetProperty("severity").GetString() == "fatal");
        Assert.DoesNotContain(buildMessages, message => message!.Contains("FIRM-PARSE-0001", StringComparison.Ordinal) || message.Contains("canonical TOON-style text", StringComparison.Ordinal));
    }

    private static string FixturePath() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Invalid/Language/concept-struct-diagnostic-routing-x1.invalid.firmfixture"));
}
