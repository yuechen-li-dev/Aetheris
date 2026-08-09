using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class HoleRecoveryPolicyTests
{
    private readonly HoleRecoveryPolicy _policy = new();

    [Fact]
    public void HolePolicy_DirectBoxCylinder_SelectsThroughHoleVariant()
    {
        var eval = _policy.Evaluate(new FrepMaterializerContext(new SdfSubtractNode(new SdfBoxNode(20, 10, 8), new SdfCylinderNode(2, 20))));
        Assert.True(eval.Admissible);
        Assert.Contains("selected-variant:ThroughHoleVariant", eval.Evidence);
        var plan = Assert.IsType<HoleRecoveryPlan>(eval.Plan);
        Assert.Equal(HoleKind.Through, plan.HoleKind);
        Assert.Single(plan.ProfileStack);
        Assert.Equal(HoleProfileSegmentKind.Cylindrical, plan.ProfileStack[0].SegmentKind);
    }

    [Fact]
    public void HolePolicy_TranslatedBoxCylinder_SelectsThroughHoleVariant()
    {
        var root = new SdfSubtractNode(new SdfTransformNode(new SdfBoxNode(20, 20, 10), Transform3D.CreateTranslation(new Vector3D(5, 2, 4))), new SdfTransformNode(new SdfCylinderNode(3, 16), Transform3D.CreateTranslation(new Vector3D(4, 1, 4))));
        var eval = _policy.Evaluate(new FrepMaterializerContext(root));
        Assert.True(eval.Admissible);
        Assert.Contains("translation-wrapper-supported", eval.Evidence);
    }

    [Fact]
    public void HolePolicy_BlindCylinder_RejectedByVariant()
    {
        var eval = _policy.Evaluate(new FrepMaterializerContext(new SdfSubtractNode(new SdfBoxNode(10, 10, 10), new SdfCylinderNode(2, 8))));
        Assert.False(eval.Admissible);
        Assert.Contains(eval.RejectionReasons, r => r.Contains("UnsupportedNotThroughHole", StringComparison.Ordinal));
    }
}
