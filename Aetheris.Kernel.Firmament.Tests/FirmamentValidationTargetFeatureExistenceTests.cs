using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentValidationTargetFeatureExistenceTests
{
    [Theory]
    [InlineData("testdata/firmament/fixtures/m2c-valid-expect-exists-bare-target-earlier.firmament")]
    [InlineData("testdata/firmament/fixtures/m2c-valid-expect-selectable-bare-target-earlier.firmament")]
    [InlineData("testdata/firmament/fixtures/m2c-valid-mixed-bare-and-selector-targets.firmament")]
    public void Compiler_Accepts_BareValidationTargets_WhenFeatureDefinedEarlier(string fixturePath)
    {
        var result = CompileFixture(fixturePath);

        Assert.True(result.Compilation.IsSuccess);
    }

    [Theory]
    [InlineData("testdata/firmament/fixtures/m2c-invalid-expect-exists-bare-target-nonexistent.firmament", "expect_exists", "missing")]
    [InlineData("testdata/firmament/fixtures/m2c-invalid-expect-selectable-bare-target-nonexistent.firmament", "expect_selectable", "missing")]
    [InlineData("testdata/firmament/fixtures/m2c-invalid-expect-exists-bare-target-forward.firmament", "expect_exists", "later")]
    [InlineData("testdata/firmament/fixtures/m2c-invalid-expect-selectable-bare-target-forward.firmament", "expect_selectable", "later")]
    public void Compiler_Rejects_BareValidationTargets_WhenFeatureMissingOrForward(string fixturePath, string opName, string targetId)
    {
        var first = CompileFixture(fixturePath);
        var second = CompileFixture(fixturePath);

        Assert.False(first.Compilation.IsSuccess);
        Assert.False(second.Compilation.IsSuccess);

        var firstDiagnostic = Assert.Single(first.Compilation.Diagnostics);
        var secondDiagnostic = Assert.Single(second.Compilation.Diagnostics);

        Assert.Equal(firstDiagnostic.Message, secondDiagnostic.Message);
        Assert.Equal(
            $"[{FirmamentDiagnosticCodes.ValidationTargetUnknownFeatureId.Value}] Validation op '{opName}' at index 0 references unknown feature id '{targetId}' via field 'target'.",
            firstDiagnostic.Message);
    }

    [Theory]
    [InlineData("testdata/firmament/fixtures/m2c-valid-expect-exists-selector-target-unresolved.firmament")]
    [InlineData("testdata/firmament/fixtures/m2c-valid-expect-selectable-selector-target-unresolved.firmament")]
    public void Compiler_Leaves_SelectorShapedValidationTargets_Unchecked(string fixturePath)
    {
        var result = CompileFixture(fixturePath);

        Assert.True(result.Compilation.IsSuccess);
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath)
    {
        var fixtureText = FirmamentCorpusHarness.ReadFixtureText(fixturePath);
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(fixtureText)));
    }
}
