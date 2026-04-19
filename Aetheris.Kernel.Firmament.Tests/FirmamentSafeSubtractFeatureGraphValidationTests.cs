using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Firmament.Validation;

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
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-subtract-sphere.firmament", KernelDiagnosticCode.NotImplemented, "Boolean feature 'cavity' (subtract): Boolean Subtract does not support analytic tool surface kind 'Sphere' in the safe boolean family.", "BrepBoolean.UnsupportedAnalyticSurfaceKind")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-subtract-box.firmament", KernelDiagnosticCode.NotImplemented, "Boolean feature 'notch' (subtract): Boolean Subtract: bounded mixed through-void builder rejects tangent/edge-grazing analytic-prismatic interactions", "BrepBooleanBoxMixedThroughVoidBuilder.Build")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-subtract-straight-slot.firmament", KernelDiagnosticCode.NotImplemented, "Boolean feature 'slot_b' (subtract): Boolean Subtract: bounded mixed through-void builder rejects tangent/edge-grazing analytic-prismatic interactions", "BrepBooleanBoxMixedThroughVoidBuilder.Build")]
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

    [Fact]
    public void FeatureGraphValidator_Rejects_AdditiveRootOutsideRecognizedSafeFamily_WithSpecificDiagnostic()
    {
        var boolean = new FirmamentLoweredBoolean(
            OpIndex: 2,
            FeatureId: "hole",
            Kind: FirmamentLoweredBooleanKind.Subtract,
            PrimaryReferenceField: "from",
            PrimaryReferenceFeatureId: "joined",
            Tool: new FirmamentLoweredToolOp(
                "cylinder",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["radius"] = "2",
                    ["height"] = "20"
                },
                RawValue: string.Empty),
            Placement: null);

        var result = FirmamentSafeSubtractFeatureGraphValidator.ValidateNextBoolean(
            boolean,
            new Dictionary<string, FirmamentSafeSubtractFeatureGraphState>(StringComparer.Ordinal)
            {
                ["joined"] = FirmamentSafeSubtractFeatureGraphState.BoundedOrthogonalAdditiveOutsideSafeRoot
            },
            new Dictionary<string, Aetheris.Kernel.Core.Brep.BrepBody>());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.ValidationFailed
            && diagnostic.Source == "firmament.feature-graph.unrecognized-additive-root"
            && diagnostic.Message.Contains("prior add result is not a recognized bounded orthogonal safe subtract root", StringComparison.Ordinal));
    }

    [Fact]
    public void FeatureGraphValidator_PreservesRecognizedState_AfterPrismaticSubtract_WhenSafeCompositionMetadataExists()
    {
        const string fixture = """
firmament:
  version: 1

model:
  name: state_continuity_after_prismatic_subtract
  units: mm

ops[4]:
  -
    op: box
    id: root
    size[3]:
      40
      30
      12

  -
    op: subtract
    id: seed_hole
    from: root
    with:
      op: cylinder
      radius: 3
      height: 20

  -
    op: subtract
    id: through_slot
    from: seed_hole
    with:
      op: slot_cut
      length: 20
      width: 8
      height: 20
      corner_radius: 4

  -
    op: subtract
    id: continuation_hole
    from: through_slot
    with:
      op: cylinder
      radius: 2
      height: 20
""";

        var result = FirmamentCorpusHarness.Compile(fixture);

        Assert.True(result.Compilation.IsSuccess, string.Join(Environment.NewLine, result.Compilation.Diagnostics.Select(d => d.Message)));
        Assert.Equal(3, result.Compilation.Value.PrimitiveExecutionResult!.ExecutedBooleans.Count);
    }
}
