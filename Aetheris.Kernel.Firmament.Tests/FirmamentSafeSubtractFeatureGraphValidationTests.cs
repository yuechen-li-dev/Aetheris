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
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-add-ordering.firmament", KernelDiagnosticCode.ValidationFailed, "Boolean feature 'joined' (add) cannot continue the safe subtract chain rooted at 'hole_a'", "firmament.feature-graph.invalid-composition-order")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-overlapping-composed-holes.firmament", KernelDiagnosticCode.NotImplemented, "Boolean feature 'hole_b' (subtract) overlaps previously accepted hole <unknown>", "BrepBoolean.AnalyticHole.HoleInterference")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-tangent-composed-holes.firmament", KernelDiagnosticCode.NotImplemented, "Boolean feature 'hole_b' (subtract) would be tangent to previously accepted hole <unknown>", "BrepBoolean.AnalyticHole.TangentContact")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-subtract-sphere.firmament", KernelDiagnosticCode.ValidationFailed, "Boolean feature 'cavity' (subtract) uses unsupported follow-on tool kind 'sphere'", "firmament.feature-graph.unsupported-follow-on-kind")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-subtract-box.firmament", KernelDiagnosticCode.ValidationFailed, "Boolean feature 'notch' (subtract) uses unsupported follow-on tool kind 'box'", "firmament.feature-graph.unsupported-follow-on-kind")]
    [InlineData("testdata/firmament/fixtures/m13b-invalid-composed-reenter-safe-family.firmament", KernelDiagnosticCode.ValidationFailed, "Boolean feature 'hole' (subtract) cannot re-enter the safe subtract family from 'joined'", "firmament.feature-graph.invalid-composition-order")]
    public void FeatureGraphValidator_FailsDeterministically_ForUnsupportedSequentialComposition(
        string fixturePath,
        KernelDiagnosticCode expectedCode,
        string expectedMessage,
        string expectedSource)
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));

        Assert.False(result.Compilation.IsSuccess);
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == expectedCode
            && diagnostic.Message.Contains(expectedMessage, StringComparison.Ordinal)
            && diagnostic.Source == expectedSource);
        Assert.DoesNotContain(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("requires at least one executed primitive or boolean body", StringComparison.Ordinal));
    }
}
