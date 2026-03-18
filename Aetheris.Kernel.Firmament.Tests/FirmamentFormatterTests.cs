using Aetheris.Kernel.Firmament.Parsing;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentFormatterTests
{
    [Theory]
    [InlineData("testdata/firmament/fixtures/m3a-valid-primitive-only-lower.firmament")]
    [InlineData("testdata/firmament/fixtures/m3b-mixed-primitive-boolean-validation.firmament")]
    [InlineData("testdata/firmament/fixtures/m7a-valid-box-origin-placement.firmament")]
    [InlineData("testdata/firmament/fixtures/m8a-valid-schema-cnc.firmament")]
    [InlineData("testdata/firmament/fixtures/m8a-valid-schema-additive.firmament")]
    [InlineData("testdata/firmament/fixtures/m8a-valid-schema-injection-molded.firmament")]
    public void Format_CanonicalFixtures_RoundTrip_As_ByteForByte_NoOp(string fixturePath)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText(fixturePath);

        var result = Format(source);

        Assert.True(result.Formatting.IsSuccess);
        Assert.Equal(source, result.Formatting.Value.Text);
    }

    [Theory]
    [InlineData("testdata/firmament/fixtures/m3a-valid-primitive-only-lower.firmament")]
    [InlineData("testdata/firmament/fixtures/m3b-mixed-primitive-boolean-validation.firmament")]
    [InlineData("testdata/firmament/fixtures/m7d-valid-boolean-selector-placement.firmament")]
    [InlineData("testdata/firmament/fixtures/m8c-valid-schema-cnc-minimum-tool-radius.firmament")]
    public void Format_Is_Deterministic_For_Supported_Canonical_Fixtures(string fixturePath)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText(fixturePath);

        var first = Format(source);
        var second = Format(source);
        var third = Format(first.Formatting.Value.Text);

        Assert.True(first.Formatting.IsSuccess);
        Assert.True(second.Formatting.IsSuccess);
        Assert.True(third.Formatting.IsSuccess);
        Assert.Equal(first.Formatting.Value.Text, second.Formatting.Value.Text);
        Assert.Equal(first.Formatting.Value.Text, third.Formatting.Value.Text);
    }

    [Theory]
    [InlineData("testdata/firmament/fixtures/m3b-mixed-primitive-boolean-validation.firmament")]
    [InlineData("testdata/firmament/fixtures/m7d-valid-boolean-selector-placement.firmament")]
    [InlineData("testdata/firmament/fixtures/m8a-valid-schema-injection-molded.firmament")]
    public void Format_Does_Not_Change_Parsed_Meaning_For_Supported_Fixtures(string fixturePath)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText(fixturePath);
        var formatted = Format(source);

        Assert.True(formatted.Formatting.IsSuccess);

        var originalParse = FirmamentTopLevelParser.Parse(source);
        var reparsed = FirmamentTopLevelParser.Parse(formatted.Formatting.Value.Text);

        Assert.True(originalParse.IsSuccess);
        Assert.True(reparsed.IsSuccess);
        Assert.Equivalent(originalParse.Value, reparsed.Value);
    }

    [Fact]
    public void Format_CanonicalPmiSection_Preserves_EmptyExplicitArrayShape()
    {
        const string source = """
        firmament:
          version: 1
        
        model:
          name: demo
          units: mm
        
        ops[0]:
        
        pmi[0]:
        """;

        var result = Format(source);

        Assert.True(result.Formatting.IsSuccess);
        Assert.Equal(FirmamentCorpusHarness.NormalizeLf(source) + "\n", result.Formatting.Value.Text);
    }

    [Fact]
    public void Format_InvalidInput_Reuses_ParseFailureDiagnostics()
    {
        const string source = """
        firmament
          version: 1
        """;

        var result = Format(source);

        Assert.False(result.Formatting.IsSuccess);
        Assert.NotEmpty(result.Formatting.Diagnostics);
    }

    private static FirmamentFormatResult Format(string source)
    {
        var formatter = new FirmamentFormatter();
        return formatter.Format(new FirmamentFormatRequest(new FirmamentSourceDocument(FirmamentCorpusHarness.NormalizeLf(source))));
    }
}
