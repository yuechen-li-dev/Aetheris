using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242ImportSharedUtilitiesTests
{
    [Fact]
    public void RequireSingleEntityByName_FindsSingleRoot()
    {
        var parse = Step242SubsetParser.Parse(Step242FixtureCorpus.CanonicalBoxGolden);
        Assert.True(parse.IsSuccess);

        var result = Step242ImportSharedUtilities.RequireSingleEntityByName(
            parse.Value,
            "MANIFOLD_SOLID_BREP",
            "missing",
            "Importer.Missing",
            "multiple",
            "Importer.Multiple");

        Assert.True(result.IsSuccess);
        Assert.Equal("MANIFOLD_SOLID_BREP", result.Value.Name);
    }

    [Fact]
    public void RequireSingleEntityByName_ReturnsMissingDiagnosticWhenAbsent()
    {
        var parse = Step242SubsetParser.Parse("ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\nENDSEC;\nEND-ISO-10303-21;");
        Assert.True(parse.IsSuccess);

        var result = Step242ImportSharedUtilities.RequireSingleEntityByName(
            parse.Value,
            "MANIFOLD_SOLID_BREP",
            "missing",
            "Importer.Missing",
            "multiple",
            "Importer.Multiple");

        Assert.False(result.IsSuccess);
        Assert.Equal("Importer.Missing", Assert.Single(result.Diagnostics).Source);
    }

    [Fact]
    public void ExecuteWithGuardrail_ConvertsArgumentExceptionToDiagnosticFailure()
    {
        var result = Step242ImportSharedUtilities.ExecuteWithGuardrail(() => throw new ArgumentException("boom"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Importer.Guardrail", Assert.Single(result.Diagnostics).Source);
    }
}
