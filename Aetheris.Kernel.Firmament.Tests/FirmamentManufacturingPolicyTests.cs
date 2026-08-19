using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentManufacturingPolicyTests
{
    [Theory]
    [InlineData("cnc-dfm-policy.firmament", "CNC", "CncManufacturingPolicy")]
    [InlineData("fdm-dfm-policy.firmament", "FDM", "FdmManufacturingPolicy")]
    [InlineData("sheet-metal-dfm-policy.firmament", "SheetMetal", "SheetMetalManufacturingPolicy")]
    public void CanonicalPolicyFamilies_AreTypedMaterializedAndDiscoverable(string fixture, string process, string contract)
    {
        var parse = FirmamentV2Parser.Parse(Source(fixture));

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var policy = Assert.Single(FirmamentManufacturingPolicyResolver.Resolve(parse.Document!, process));
        Assert.Contains(contract, policy.Source, StringComparison.Ordinal);
        Assert.NotEmpty(policy.Members);
    }

    [Fact]
    public void CanonicalCncPolicy_EnforcesMinimumToolRadiusOnRealSemanticHole()
    {
        var source = Source("cnc-dfm-policy.firmament")
            .Replace("MinimumToolRadius: 0.75mm", "MinimumToolRadius: 3mm", StringComparison.Ordinal);

        var parse = FirmamentV2Parser.Parse(source);
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var dfm = FirmamentV2DfmEnforcement.Validate(parse.Document!);

        Assert.False(dfm.IsSuccess);
        Assert.Contains(dfm.Diagnostics, diagnostic => diagnostic.Message.StartsWith(FirmamentV2Parser.DfmMinimumToolRadiusViolation, StringComparison.Ordinal));
    }

    [Fact]
    public void HistoricalLowercaseCncTemplate_RemainsACompatibilityInput()
    {
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(Path.Combine(
            FirmamentCorpusHarness.RepoRoot(), "fixtures", "Compatibility", "LegacyAliases", "Invalid", "Templates", "template-v2-cnc-min-tool-radius-enforced.invalid.firmfixture")));
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var dfm = FirmamentV2DfmEnforcement.Validate(parse.Document!);

        Assert.False(dfm.IsSuccess);
        Assert.Contains(dfm.Diagnostics, diagnostic => diagnostic.Message.StartsWith(FirmamentV2Parser.DfmMinimumToolRadiusViolation, StringComparison.Ordinal));
    }

    private static string Source(string fixture) => File.ReadAllText(Path.Combine(
        FirmamentCorpusHarness.RepoRoot(), "fixtures", "Canonical", "Templates", fixture));
}
