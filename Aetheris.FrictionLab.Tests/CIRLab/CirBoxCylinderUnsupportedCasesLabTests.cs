using Aetheris.FrictionLab;
using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class CirBoxCylinderUnsupportedCasesLabTests
{
    [Theory]
    [MemberData(nameof(Cases))]
    public void Rejects_Unsupported(SdfNode root, CirLabRecognitionReason expected)
    {
        var result = CirBoxCylinderRecognitionLab.Recognize(root);
        Assert.False(result.Success);
        Assert.Equal(expected, result.Reason);
    }

    public static IEnumerable<object[]> Cases()
    {
        yield return [new SdfUnionNode(new SdfBoxNode(10d, 8d, 6d), new SdfCylinderNode(2d, 8d)), CirLabRecognitionReason.RootNotSubtract];
        yield return [new SdfSubtractNode(new SdfSphereNode(3d), new SdfCylinderNode(2d, 8d)), CirLabRecognitionReason.BaseNotBox];
        yield return [new SdfSubtractNode(new SdfBoxNode(10d, 8d, 6d), new SdfSphereNode(2d)), CirLabRecognitionReason.ToolNotCylinder];
        yield return [new SdfSubtractNode(new SdfBoxNode(10d, 8d, 6d), new SdfCylinderNode(2d, 4d)), CirLabRecognitionReason.NotThrough];
        yield return [new SdfSubtractNode(new SdfBoxNode(10d, 8d, 6d), new SdfTransformNode(new SdfCylinderNode(2d, 8d), Transform3D.CreateTranslation(new Vector3D(3d, 0d, 0d)))), CirLabRecognitionReason.TangentOrOutside];
        yield return [new SdfSubtractNode(new SdfBoxNode(10d, 8d, 6d), new SdfTransformNode(new SdfCylinderNode(2d, 8d), Transform3D.CreateTranslation(new Vector3D(7d, 0d, 0d)))), CirLabRecognitionReason.TangentOrOutside];
    }
}
