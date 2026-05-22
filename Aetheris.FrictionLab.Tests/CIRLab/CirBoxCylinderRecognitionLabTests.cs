using Aetheris.FrictionLab;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class CirBoxCylinderRecognitionLabTests
{
    [Fact]
    public void Recognizes_DirectCanonicalSubtract()
    {
        var root = new CirSubtractNode(new CirBoxNode(10d, 8d, 6d), new CirCylinderNode(2d, 8d));
        var result = CirBoxCylinderRecognitionLab.Recognize(root);
        Assert.True(result.Success);
        Assert.Equal("Z", result.Axis);
    }

    [Fact]
    public void Recognizes_TranslatedOperands()
    {
        var box = new CirTransformNode(new CirBoxNode(10d, 8d, 6d), Transform3D.CreateTranslation(new Vector3D(2d, 1d, 5d)));
        var cyl = new CirTransformNode(new CirCylinderNode(2d, 8d), Transform3D.CreateTranslation(new Vector3D(2.5d, 1.5d, 5d)));
        var root = new CirSubtractNode(box, cyl);
        var result = CirBoxCylinderRecognitionLab.Recognize(root);
        Assert.True(result.Success);
    }

    [Fact]
    public void Rejects_NonTranslationTransform()
    {
        var box = new CirTransformNode(new CirBoxNode(10d, 8d, 6d), Transform3D.CreateRotationX(Math.PI / 4d));
        var root = new CirSubtractNode(box, new CirCylinderNode(2d, 8d));
        var result = CirBoxCylinderRecognitionLab.Recognize(root);
        Assert.False(result.Success);
        Assert.Equal(CirLabRecognitionReason.UnsupportedTransform, result.Reason);
    }
}
