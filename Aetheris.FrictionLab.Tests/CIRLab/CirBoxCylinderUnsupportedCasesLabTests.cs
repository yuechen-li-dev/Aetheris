using Aetheris.FrictionLab;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class CirBoxCylinderUnsupportedCasesLabTests
{
    [Theory]
    [MemberData(nameof(Cases))]
    public void Rejects_Unsupported(CirNode root, CirLabRecognitionReason expected)
    {
        var result = CirBoxCylinderRecognitionLab.Recognize(root);
        Assert.False(result.Success);
        Assert.Equal(expected, result.Reason);
    }

    public static IEnumerable<object[]> Cases()
    {
        yield return [new CirUnionNode(new CirBoxNode(10d, 8d, 6d), new CirCylinderNode(2d, 8d)), CirLabRecognitionReason.RootNotSubtract];
        yield return [new CirSubtractNode(new CirSphereNode(3d), new CirCylinderNode(2d, 8d)), CirLabRecognitionReason.BaseNotBox];
        yield return [new CirSubtractNode(new CirBoxNode(10d, 8d, 6d), new CirSphereNode(2d)), CirLabRecognitionReason.ToolNotCylinder];
        yield return [new CirSubtractNode(new CirBoxNode(10d, 8d, 6d), new CirCylinderNode(2d, 4d)), CirLabRecognitionReason.NotThrough];
        yield return [new CirSubtractNode(new CirBoxNode(10d, 8d, 6d), new CirTransformNode(new CirCylinderNode(2d, 8d), Transform3D.CreateTranslation(new Vector3D(3d, 0d, 0d)))), CirLabRecognitionReason.TangentOrOutside];
        yield return [new CirSubtractNode(new CirBoxNode(10d, 8d, 6d), new CirTransformNode(new CirCylinderNode(2d, 8d), Transform3D.CreateTranslation(new Vector3D(7d, 0d, 0d)))), CirLabRecognitionReason.TangentOrOutside];
    }
}
