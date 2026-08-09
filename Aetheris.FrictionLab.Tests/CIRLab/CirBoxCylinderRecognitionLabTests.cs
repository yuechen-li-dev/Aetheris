using Aetheris.FrictionLab;
using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class CirBoxCylinderRecognitionLabTests
{
    [Fact]
    public void Recognizes_DirectCanonicalSubtract()
    {
        var root = new SdfSubtractNode(new SdfBoxNode(10d, 8d, 6d), new SdfCylinderNode(2d, 8d));
        var result = CirBoxCylinderRecognitionLab.Recognize(root);
        Assert.True(result.Success);
        Assert.Equal("Z", result.Axis);
    }

    [Fact]
    public void Recognizes_TranslatedOperands()
    {
        var box = new SdfTransformNode(new SdfBoxNode(10d, 8d, 6d), Transform3D.CreateTranslation(new Vector3D(2d, 1d, 5d)));
        var cyl = new SdfTransformNode(new SdfCylinderNode(2d, 8d), Transform3D.CreateTranslation(new Vector3D(2.5d, 1.5d, 5d)));
        var root = new SdfSubtractNode(box, cyl);
        var result = CirBoxCylinderRecognitionLab.Recognize(root);
        Assert.True(result.Success);
    }

    [Fact]
    public void Rejects_NonTranslationTransform()
    {
        var box = new SdfTransformNode(new SdfBoxNode(10d, 8d, 6d), Transform3D.CreateRotationX(Math.PI / 4d));
        var root = new SdfSubtractNode(box, new SdfCylinderNode(2d, 8d));
        var result = CirBoxCylinderRecognitionLab.Recognize(root);
        Assert.False(result.Success);
        Assert.Equal(CirLabRecognitionReason.UnsupportedTransform, result.Reason);
    }
}
