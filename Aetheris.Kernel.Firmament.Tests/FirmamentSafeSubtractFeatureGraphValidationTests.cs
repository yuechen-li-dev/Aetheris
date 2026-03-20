using Aetheris.Kernel.Core.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentSafeSubtractFeatureGraphValidationTests
{
    [Fact]
    public void FeatureGraphValidator_Allows_CurrentCanonicalSafeChains()
    {
        var twoCylinder = FirmamentCorpusHarness.Compile(
            FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/boolean_two_cylinder_holes_basic.firmament"));
        var cylinderCone = FirmamentCorpusHarness.Compile(
            FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/boolean_cylinder_cone_holes_basic.firmament"));

        Assert.True(twoCylinder.Compilation.IsSuccess);
        Assert.True(cylinderCone.Compilation.IsSuccess);
    }

    [Theory]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-add-ordering.firmament", "violates safe subtract feature-graph ordering", "firmament.feature-graph")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-overlapping-composed-holes.firmament", "HoleInterference", "BrepBoolean.AnalyticHole.HoleInterference")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-tangent-composed-holes.firmament", "TangentContact", "BrepBoolean.AnalyticHole.TangentContact")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-subtract-sphere.firmament", "unsupported follow-on tool kind 'sphere'", "firmament.feature-graph")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-subtract-box.firmament", "unsupported follow-on tool kind 'box'", "firmament.feature-graph")]
    [InlineData("testdata/firmament/fixtures/m13b-invalid-composed-reenter-safe-family.firmament", "outside that supported family", "firmament.feature-graph")]
    public void FeatureGraphValidator_FailsDeterministically_ForUnsupportedSequentialComposition(
        string fixturePath,
        string expectedMessage,
        string expectedSource)
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));

        Assert.False(result.Compilation.IsSuccess);
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && diagnostic.Message.Contains(expectedMessage, StringComparison.Ordinal)
            && diagnostic.Source == expectedSource);
        Assert.DoesNotContain(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("requires at least one executed primitive or boolean body", StringComparison.Ordinal));
    }
}
