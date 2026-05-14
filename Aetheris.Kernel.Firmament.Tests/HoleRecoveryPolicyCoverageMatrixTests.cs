using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class HoleRecoveryPolicyCoverageMatrixTests
{
    private readonly HoleRecoveryPolicy _policy = new();

    public static TheoryData<string, CirNode, string, HoleKind, HoleDepthKind, FrepMaterializerCapability> SupportedRows =>
        new()
        {
            { "ThroughHole", BuildThroughHole(), nameof(ThroughHoleVariant), HoleKind.Through, HoleDepthKind.Through, FrepMaterializerCapability.ExactBRep },
            { "Counterbore", BuildCounterbore(), nameof(CounterboreVariant), HoleKind.Counterbore, HoleDepthKind.ThroughWithEntryRelief, FrepMaterializerCapability.ExactBRep },
            { "BlindHoleTop", BuildBlindHoleTop(), nameof(BlindHoleVariant), HoleKind.Blind, HoleDepthKind.Blind, FrepMaterializerCapability.ExactBRep },
            { "BlindHoleBottom", BuildBlindHoleBottom(), nameof(BlindHoleVariant), HoleKind.Blind, HoleDepthKind.Blind, FrepMaterializerCapability.ExactBRep },
            { "Countersink", BuildCountersink(), nameof(CountersinkVariant), HoleKind.Countersink, HoleDepthKind.ThroughWithEntryRelief, FrepMaterializerCapability.ExactBRep }
        };

    public static TheoryData<string, CirNode> UnsupportedRows =>
        new()
        {
            { "BoxSphere", new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirSphereNode(3)) },
            { "TangentCylinder", new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirTransformNode(new CirCylinderNode(10, 20), Transform3D.CreateTranslation(new Vector3D(0,0,0)))) },
            { "UnsupportedTransform", new CirSubtractNode(new CirTransformNode(new CirBoxNode(20, 20, 10), Transform3D.CreateRotationX(Math.PI / 6d)), new CirCylinderNode(2, 20)) },
            { "NonCoaxialCounterboreOrCountersink", new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20,20,10), new CirCylinderNode(2,20)), new CirTransformNode(new CirCylinderNode(4,4), Transform3D.CreateTranslation(new Vector3D(2,0,-3)))) }
        };

    [Theory]
    [MemberData(nameof(SupportedRows))]
    public void HoleCoverageMatrix_SelectsExpectedVariantForSupportedCases(string _, CirNode root, string expectedVariant, HoleKind kind, HoleDepthKind depth, FrepMaterializerCapability capability)
    {
        var eval = _policy.Evaluate(new FrepMaterializerContext(root));
        Assert.True(eval.Admissible);
        Assert.Equal(capability, eval.Capability);
        Assert.Contains($"selected-variant:{expectedVariant}", eval.Evidence);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(kind, plan.HoleKind);
        Assert.Equal(depth, plan.DepthKind);
        Assert.NotEmpty(eval.Diagnostics);
    }

    [Theory]
    [MemberData(nameof(UnsupportedRows))]
    public void HoleCoverageMatrix_UnsupportedCasesFallBackOrReject(string _, CirNode root)
    {
        var eval = _policy.Evaluate(new FrepMaterializerContext(root));
        Assert.False(eval.Admissible);
        Assert.Null(eval.Plan);
        Assert.NotEmpty(eval.RejectionReasons);
        Assert.Contains(eval.Diagnostics, d => d.Contains("Fallback selected", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(SupportedRows))]
    public void HoleCoverageMatrix_WrongVariantsRejectSupportedCases(string rowName, CirNode root, string expectedVariant, HoleKind expectedHoleKind, HoleDepthKind expectedDepthKind, FrepMaterializerCapability expectedCapability)
    {
        var eval = _policy.Evaluate(new FrepMaterializerContext(root));
        Assert.True(eval.Admissible);
        Assert.False(string.IsNullOrWhiteSpace(rowName));
        Assert.Equal(expectedCapability, eval.Capability);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(expectedHoleKind, plan.HoleKind);
        Assert.Equal(expectedDepthKind, plan.DepthKind);
        var all = new[] { nameof(ThroughHoleVariant), nameof(CounterboreVariant), nameof(BlindHoleVariant), nameof(CountersinkVariant) };
        foreach (var variant in all.Where(v => !string.Equals(v, expectedVariant, StringComparison.Ordinal)))
        {
            Assert.Contains(eval.Diagnostics, d => d.Contains($"Variant considered: {variant}; admissible=False.", StringComparison.Ordinal));
            Assert.Contains(eval.Diagnostics, d => d.Contains($"Variant rejected: {variant};", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void HoleCoverageMatrix_DiagnosticsIncludeVariantTrace()
    {
        var eval = _policy.Evaluate(new FrepMaterializerContext(BuildCounterbore()));
        Assert.Contains(eval.Diagnostics, d => d.Contains("Variants evaluated", StringComparison.Ordinal));
        Assert.Contains(eval.Diagnostics, d => d.Contains("Variant considered: ThroughHoleVariant", StringComparison.Ordinal));
        Assert.Contains(eval.Diagnostics, d => d.Contains("Variant considered: CounterboreVariant", StringComparison.Ordinal));
        Assert.Contains(eval.Diagnostics, d => d.Contains("Selected hole variant: CounterboreVariant", StringComparison.Ordinal));
        Assert.Contains(eval.Diagnostics, d => d.Contains("Produced plan kind", StringComparison.Ordinal));
    }

    [Fact]
    public void HoleCoverageMatrix_ProfileStackSummariesArePresent()
    {
        var eval = _policy.Evaluate(new FrepMaterializerContext(BuildCountersink()));
        Assert.Contains(eval.Diagnostics, d => d.Contains("Profile stack summary", StringComparison.Ordinal));
        Assert.Contains(eval.Diagnostics, d => d.Contains("Conical", StringComparison.Ordinal));
        Assert.Contains(eval.Diagnostics, d => d.Contains("Cylindrical", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(SupportedRows))]
    public void HoleCoverageMatrix_ExecutableVariantsProduceBRep(string rowName, CirNode root, string expectedVariant, HoleKind expectedHoleKind, HoleDepthKind expectedDepthKind, FrepMaterializerCapability expectedCapability)
    {
        Assert.False(string.IsNullOrWhiteSpace(rowName));
        var eval = _policy.Evaluate(new FrepMaterializerContext(root));
        Assert.Contains($"selected-variant:{expectedVariant}", eval.Evidence);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(expectedHoleKind, plan.HoleKind);
        Assert.Equal(expectedDepthKind, plan.DepthKind);
        Assert.Equal(expectedCapability, eval.Capability);
        var exec = HoleRecoveryExecutor.Execute(plan);
        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, exec.Status);
        Assert.NotNull(exec.Body);
    }

    [Theory]
    [MemberData(nameof(SupportedRows))]
    public void HoleCoverageMatrix_CurrentHoleVariantsAreManifoldNotVoids(string rowName, CirNode root, string expectedVariant, HoleKind expectedHoleKind, HoleDepthKind expectedDepthKind, FrepMaterializerCapability expectedCapability)
    {
        Assert.False(string.IsNullOrWhiteSpace(rowName));
        var eval = _policy.Evaluate(new FrepMaterializerContext(root));
        Assert.Contains($"selected-variant:{expectedVariant}", eval.Evidence);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(expectedHoleKind, plan.HoleKind);
        Assert.Equal(expectedDepthKind, plan.DepthKind);
        Assert.Equal(expectedCapability, eval.Capability);
        var exec = HoleRecoveryExecutor.Execute(plan);
        var step = Step242Exporter.ExportBody(exec.Body!);
        Assert.True(step.IsSuccess);
        Assert.Contains("MANIFOLD_SOLID_BREP", step.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", step.Value, StringComparison.Ordinal);
    }

    private static CirNode BuildThroughHole() => new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20));
    private static CirNode BuildBlindHoleTop() => new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirTransformNode(new CirCylinderNode(2, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, 3))));
    private static CirNode BuildBlindHoleBottom() => new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirTransformNode(new CirCylinderNode(2, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))));
    private static CirNode BuildCounterbore() => new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)), new CirTransformNode(new CirCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))));
    private static CirNode BuildCountersink() => new CirSubtractNode(new CirSubtractNode(new CirBoxNode(20, 20, 10), new CirCylinderNode(2, 20)), new CirTransformNode(new CirConeNode(2, 4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, 3))));
}
