using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2ParserTests
{
    [Fact]
    public void FirmamentV2Parser_Box_ParsesModelUnitsAndSolid()
    {
        var result = FirmamentV2Parser.Parse(Source("Primitive/valid/box-v2.valid.firmfixture"));
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        Assert.NotNull(result.Document);
        var document = result.Document!;
        Assert.Equal("BoxExample", document.ModelName);
        Assert.Equal("mm", document.Units);
        Assert.Equal("base", document.Solid.Name);
        Assert.Equal("Box", document.Solid.RecordType);
        Assert.Equal([10, 8, 6], document.Solid.Box.Size);
    }

    [Fact]
    public void FirmamentV2Parser_Box_LowersToFeatureAir()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("Primitive/valid/box-v2.valid.firmfixture"));
        Assert.True(result.ParseSucceeded, string.Join(", ", result.Diagnostics));
        Assert.Equal("FirmamentV2Parser", result.ParserName);
        Assert.Equal("feature-air", result.FrontendStageReached);
        Assert.NotNull(result.FeatureAir);
        Assert.Equal("CreateBox", result.FeatureAir!.FeatureAirNodeKind);
        Assert.Null(result.ConstructiveAir);
    }

    [Fact]
    public void FirmamentV2Parser_MissingUnits_IsDiagnostic()
    {
        var result = FirmamentV2Parser.Parse(Source("Primitive/invalid/box-v2-missing-units.invalid.firmfixture"));
        Assert.False(result.IsSuccess);
        Assert.Contains(FirmamentV2Parser.MissingUnits, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_NegativeSize_IsDegenerateDimension()
    {
        var result = FirmamentV2Parser.Parse(Source("Primitive/invalid/box-v2-negative-size.invalid.firmfixture"));
        Assert.False(result.IsSuccess);
        Assert.Contains(FirmamentV2Parser.DegenerateDimension, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_UnknownRecord_IsDiagnostic()
    {
        var result = FirmamentV2Parser.Parse(Source("Primitive/invalid/box-v2-unknown-record.invalid.firmfixture"));
        Assert.False(result.IsSuccess);
        Assert.Contains(FirmamentV2Parser.UnknownRecordType, result.Diagnostics);
    }

    [Fact]
    public void FirmamentV2Parser_DoesNotUseV1Parser()
    {
        var result = FirmamentFrontendTraceProbe.ParseV2Only(Source("Primitive/valid/box-v2.valid.firmfixture"));
        Assert.Equal("FirmamentV2Parser", result.ParserName);
        Assert.Contains("firmament-v2-no-v1-parser", result.Diagnostics);
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("FirmamentTopLevelParser", StringComparison.Ordinal));
    }

    [Fact]
    public void FirmamentV2Parser_UnsupportedConstruct_RemainsMetadataClassified()
    {
        var result = FirmamentV2Parser.Parse(Source("RecordDerivation/valid/box-with-size-variant-v2.valid.firmfixture"));
        Assert.False(result.IsSuccess);
        Assert.Contains(FirmamentV2Parser.UnsupportedConstruct, result.Diagnostics);
    }

    private static string Source(string relative)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2", relative));
        var lines = File.ReadAllLines(path);
        var bodyStart = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("//", StringComparison.Ordinal));
        return string.Join(Environment.NewLine, lines.Skip(Math.Max(0, bodyStart)));
    }
}
