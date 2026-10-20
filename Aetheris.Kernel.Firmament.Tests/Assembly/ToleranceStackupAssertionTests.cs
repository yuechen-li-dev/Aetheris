using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class ToleranceStackupAssertionTests
{
    private static string Source() => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Assembly/bearing-module.firmament"));

    [Fact]
    public void ModernFieldsPreserveTheWorstCaseChainAndAuthoredLocation()
    {
        var source = Source().Replace("Between:", "between:", StringComparison.Ordinal)
            .Replace("Require: Clearance", "require: clearance", StringComparison.Ordinal);
        var parsed = new AssemblyM0Parser().Parse(source, "stackup.firmament");
        Assert.True(parsed.IsSuccess, string.Join("; ", parsed.Diagnostics.Select(d => d.Message)));
        var assertion = Assert.Single(parsed.Source!.StackupAsserts);
        Assert.StartsWith("Assert ToleranceStackup", source.Substring(assertion.SourceSpan!.Start, assertion.SourceSpan.Length));
        var compiled = new AssemblyM0Compiler().Compile(parsed.Source);
        Assert.True(compiled.IsSuccess, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var stackup = Assert.Single(compiled.Ir!.ToleranceStackups);
        Assert.Equal(45, stackup.Nominal);
        Assert.Equal(44.92, stackup.WorstCaseMinimum, 8);
        Assert.Equal(45.1, stackup.WorstCaseMaximum, 8);
        Assert.Equal(6, stackup.Contributions.Count);
    }

    [Fact]
    public void FailedStackupKeepsItsMessageAndOriginalSpan()
    {
        var source = Source().Replace(">= 44.90mm", ">= 45mm", StringComparison.Ordinal);
        var parsed = new AssemblyM0Parser().Parse(source, "stackup.firmament");
        Assert.True(parsed.IsSuccess);
        var result = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics.Where(d => d.Code == AssemblyM0Compiler.ToleranceAssertionFailure));
        Assert.Contains("44.92", diagnostic.Message);
        Assert.Equal("stackup.firmament", diagnostic.SourceSpan!.Source);
        Assert.StartsWith("Assert ToleranceStackup", source.Substring(diagnostic.SourceSpan.Start, diagnostic.SourceSpan.Length));
    }

    [Theory]
    [InlineData("Require: Clearance", "Requre: Clearance")]
    [InlineData("Require: Clearance >= 44.90mm;", "Require: Clearance >= 44.90mm; unexpected: 1mm;")]
    [InlineData("Between:", "between: [A, B]; Between:")]
    public void ExtraMisspelledAndDuplicateFieldsCannotBypassAdmission(string before, string after)
    {
        var parsed = new AssemblyM0Parser().Parse(Source().Replace(before, after, StringComparison.Ordinal));
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code.Contains("field-unknown", StringComparison.Ordinal) || d.Code.Contains("field-duplicate", StringComparison.Ordinal));
        Assert.All(parsed.Diagnostics, d => Assert.NotNull(d.SourceSpan));
    }

    [Fact]
    public void ReversedPathReversesAsymmetricToleranceContributions()
    {
        var source = Source().Replace("[BearingModule.FixedSupport.Housing.Datum, BearingModule.Rotor.Shaft.Shoulder]",
            "[BearingModule.Rotor.Shaft.Shoulder, BearingModule.FixedSupport.Housing.Datum]", StringComparison.Ordinal)
            .Replace(">= 44.90mm", ">= -45.11mm", StringComparison.Ordinal);
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.True(parsed.IsSuccess);
        var compiled = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.True(compiled.IsSuccess, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var stackup = Assert.Single(compiled.Ir!.ToleranceStackups);
        Assert.Equal(-45.1, stackup.WorstCaseMinimum, 8);
        Assert.Equal(-44.92, stackup.WorstCaseMaximum, 8);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingAndAmbiguousDimensionalPathsRejectAtTheAssertion(bool ambiguous)
    {
        var source = Source();
        var start = source.IndexOf("    Relation HousingSeatDepth:", StringComparison.Ordinal);
        var end = source.IndexOf(';', start) + 1;
        var relation = source[start..end];
        source = ambiguous ? source.Insert(end, "\n" + relation.Replace("HousingSeatDepth:", "AlternateDepth:", StringComparison.Ordinal))
            : source.Remove(start, end - start);
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.True(parsed.IsSuccess);
        var result = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == (ambiguous ? AssemblyM0Compiler.TolerancePathAmbiguous : AssemblyM0Compiler.TolerancePathMissing));
        Assert.StartsWith("Assert ToleranceStackup", source.Substring(diagnostic.SourceSpan!.Start, diagnostic.SourceSpan.Length));
    }
}
