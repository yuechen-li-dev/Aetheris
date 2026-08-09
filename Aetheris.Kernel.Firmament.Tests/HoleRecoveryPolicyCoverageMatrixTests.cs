using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class HoleRecoveryPolicyCoverageMatrixTests
{
    private readonly HoleRecoveryPolicy _policy = new();

    public static TheoryData<string, SdfNode, string, HoleKind, HoleDepthKind, FrepMaterializerCapability> SupportedRows =>
        new()
        {
            { "ThroughHole", BuildThroughHole(), nameof(ThroughHoleVariant), HoleKind.Through, HoleDepthKind.Through, FrepMaterializerCapability.ExactBRep },
            { "Counterbore", BuildCounterbore(), nameof(CounterboreVariant), HoleKind.Counterbore, HoleDepthKind.ThroughWithEntryRelief, FrepMaterializerCapability.ExactBRep },
            { "BlindHoleTop", BuildBlindHoleTop(), nameof(BlindHoleVariant), HoleKind.Blind, HoleDepthKind.Blind, FrepMaterializerCapability.ExactBRep },
            { "BlindHoleBottom", BuildBlindHoleBottom(), nameof(BlindHoleVariant), HoleKind.Blind, HoleDepthKind.Blind, FrepMaterializerCapability.ExactBRep },
            { "Countersink", BuildCountersink(), nameof(CountersinkVariant), HoleKind.Countersink, HoleDepthKind.ThroughWithEntryRelief, FrepMaterializerCapability.ExactBRep },
            { "ChamferedEntry", BuildChamferedEntry(), nameof(ChamferedEntryHoleVariant), HoleKind.ChamferedEntry, HoleDepthKind.ThroughWithEntryRelief, FrepMaterializerCapability.ExactBRep },
            { "ChamferedEntryBottom", BuildChamferedEntryBottom(), nameof(ChamferedEntryHoleVariant), HoleKind.ChamferedEntry, HoleDepthKind.ThroughWithEntryRelief, FrepMaterializerCapability.ExactBRep },
            { "SteppedHole", BuildSteppedHole(), nameof(SteppedHoleVariant), HoleKind.Stepped, HoleDepthKind.ThroughWithEntryRelief, FrepMaterializerCapability.ExactBRep }
        };

    public static TheoryData<string, SdfNode> UnsupportedRows =>
        new()
        {
            { "BoxSphere", new SdfSubtractNode(new SdfBoxNode(20, 20, 10), new SdfSphereNode(3)) },
            { "TangentCylinder", new SdfSubtractNode(new SdfBoxNode(20, 20, 10), new SdfTransformNode(new SdfCylinderNode(10, 20), Transform3D.CreateTranslation(new Vector3D(0,0,0)))) },
            { "UnsupportedTransform", new SdfSubtractNode(new SdfTransformNode(new SdfBoxNode(20, 20, 10), Transform3D.CreateRotationX(Math.PI / 6d)), new SdfCylinderNode(2, 20)) },
            { "NonCoaxialCounterboreOrCountersink", new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(20,20,10), new SdfCylinderNode(2,20)), new SdfTransformNode(new SdfCylinderNode(4,4), Transform3D.CreateTranslation(new Vector3D(2,0,-3)))) }
        };

    [Theory]
    [MemberData(nameof(SupportedRows))]
    public void HoleCoverageMatrix_SelectsExpectedVariantForSupportedCases(string _, SdfNode root, string expectedVariant, HoleKind kind, HoleDepthKind depth, FrepMaterializerCapability capability)
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
    public void HoleCoverageMatrix_UnsupportedCasesFallBackOrReject(string _, SdfNode root)
    {
        var eval = _policy.Evaluate(new FrepMaterializerContext(root));
        Assert.False(eval.Admissible);
        Assert.Null(eval.Plan);
        Assert.NotEmpty(eval.RejectionReasons);
        Assert.Contains(eval.Diagnostics, d => d.Contains("Fallback selected", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(SupportedRows))]
    public void HoleCoverageMatrix_WrongVariantsRejectSupportedCases(string rowName, SdfNode root, string expectedVariant, HoleKind expectedHoleKind, HoleDepthKind expectedDepthKind, FrepMaterializerCapability expectedCapability)
    {
        var eval = _policy.Evaluate(new FrepMaterializerContext(root));
        Assert.True(eval.Admissible);
        Assert.False(string.IsNullOrWhiteSpace(rowName));
        Assert.Equal(expectedCapability, eval.Capability);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(expectedHoleKind, plan.HoleKind);
        Assert.Equal(expectedDepthKind, plan.DepthKind);
        var all = new[] { nameof(ThroughHoleVariant), nameof(CounterboreVariant), nameof(BlindHoleVariant), nameof(ChamferedEntryHoleVariant), nameof(CountersinkVariant), nameof(SteppedHoleVariant) };
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
    public void HoleCoverageMatrix_ExecutableVariantsProduceBRep(string rowName, SdfNode root, string expectedVariant, HoleKind expectedHoleKind, HoleDepthKind expectedDepthKind, FrepMaterializerCapability expectedCapability)
    {
        Assert.False(string.IsNullOrWhiteSpace(rowName));
        var eval = _policy.Evaluate(new FrepMaterializerContext(root));
        Assert.Contains($"selected-variant:{expectedVariant}", eval.Evidence);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(expectedHoleKind, plan.HoleKind);
        Assert.Equal(expectedDepthKind, plan.DepthKind);
        Assert.Equal(expectedCapability, eval.Capability);
        var exec = HoleRecoveryExecutor.Execute(plan);
        if (plan.HoleKind == HoleKind.Stepped)
        {
            Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, exec.Status);
            Assert.NotNull(exec.Body);
            Assert.Contains(exec.Diagnostics, d => d.Contains("stepped executor route: profile-stack-extrude", StringComparison.Ordinal));
            var steppedStep = Step242Exporter.ExportBody(exec.Body!);
            Assert.True(steppedStep.IsSuccess);
            Assert.Contains("MANIFOLD_SOLID_BREP", steppedStep.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("BREP_WITH_VOIDS", steppedStep.Value, StringComparison.Ordinal);
            return;
        }

        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, exec.Status);
        Assert.NotNull(exec.Body);
    }

    [Theory]
    [MemberData(nameof(SupportedRows))]
    public void HoleCoverageMatrix_CurrentHoleVariantsAreManifoldNotVoids(string rowName, SdfNode root, string expectedVariant, HoleKind expectedHoleKind, HoleDepthKind expectedDepthKind, FrepMaterializerCapability expectedCapability)
    {
        Assert.False(string.IsNullOrWhiteSpace(rowName));
        var eval = _policy.Evaluate(new FrepMaterializerContext(root));
        Assert.Contains($"selected-variant:{expectedVariant}", eval.Evidence);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(expectedHoleKind, plan.HoleKind);
        Assert.Equal(expectedDepthKind, plan.DepthKind);
        Assert.Equal(expectedCapability, eval.Capability);
        var exec = HoleRecoveryExecutor.Execute(plan);
        if (plan.HoleKind == HoleKind.Stepped)
        {
            Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, exec.Status);
            Assert.NotNull(exec.Body);
            Assert.Contains(exec.Diagnostics, d => d.Contains("stepped executor route: profile-stack-extrude", StringComparison.Ordinal));
            var steppedStep = Step242Exporter.ExportBody(exec.Body!);
            Assert.True(steppedStep.IsSuccess);
            Assert.Contains("MANIFOLD_SOLID_BREP", steppedStep.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("BREP_WITH_VOIDS", steppedStep.Value, StringComparison.Ordinal);
            return;
        }

        Assert.Equal(HoleRecoveryExecutionStatus.Succeeded, exec.Status);
        Assert.NotNull(exec.Body);
        var step = Step242Exporter.ExportBody(exec.Body!);
        Assert.True(step.IsSuccess);
        Assert.Contains("MANIFOLD_SOLID_BREP", step.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", step.Value, StringComparison.Ordinal);
    }

    private static SdfNode BuildThroughHole() => new SdfSubtractNode(new SdfBoxNode(20, 20, 10), new SdfCylinderNode(2, 20));
    private static SdfNode BuildBlindHoleTop() => new SdfSubtractNode(new SdfBoxNode(20, 20, 10), new SdfTransformNode(new SdfCylinderNode(2, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, 3))));
    private static SdfNode BuildBlindHoleBottom() => new SdfSubtractNode(new SdfBoxNode(20, 20, 10), new SdfTransformNode(new SdfCylinderNode(2, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))));
    private static SdfNode BuildCounterbore() => new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(20, 20, 10), new SdfCylinderNode(2, 20)), new SdfTransformNode(new SdfCylinderNode(4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, -3))));
    private static SdfNode BuildCountersink() => new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(20, 20, 10), new SdfCylinderNode(2, 20)), new SdfTransformNode(new SdfConeNode(2, 4, 4), Transform3D.CreateTranslation(new Vector3D(0, 0, 3))));
    private static SdfNode BuildChamferedEntry() => new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(20, 20, 10), new SdfCylinderNode(2, 20)), new SdfTransformNode(new SdfConeNode(2, 2.8, 1), Transform3D.CreateTranslation(new Vector3D(0, 0, 4.5))));
    private static SdfNode BuildChamferedEntryBottom() => new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(20, 20, 10), new SdfCylinderNode(2, 20)), new SdfTransformNode(new SdfConeNode(2.8, 2, 1), Transform3D.CreateTranslation(new Vector3D(0, 0, -4.5))));
    private static SdfNode BuildSteppedHole() => new SdfSubtractNode(new SdfSubtractNode(new SdfSubtractNode(new SdfBoxNode(20,20,10), new SdfCylinderNode(2,20)), new SdfTransformNode(new SdfCylinderNode(3,6), Transform3D.CreateTranslation(new Vector3D(0,0,2)))), new SdfTransformNode(new SdfCylinderNode(4,4), Transform3D.CreateTranslation(new Vector3D(0,0,3))));
}
