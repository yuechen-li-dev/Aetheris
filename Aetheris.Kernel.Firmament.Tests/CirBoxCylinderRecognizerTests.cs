using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CirBoxCylinderRecognizerTests
{
    [Fact]
    public void Recognizes_DirectSubtract_BoxCylinder()
    {
        var input = new CirBoxCylinderRecognizerInput(new CirSubtractNode(new CirBoxNode(20, 10, 8), new CirCylinderNode(2, 20)));
        var result = CirBoxCylinderRecognizer.Recognize(input);
        Assert.True(result.Success);
        Assert.Equal(CirBoxCylinderRecognitionReason.Recognized, result.Reason);
        Assert.NotNull(result.Value);
        Assert.Equal(CirBoxCylinderAxisKind.Z, result.Value!.Axis);
    }

    [Fact]
    public void Recognizes_TranslationWrapped_Subtract_BoxCylinder()
    {
        var root = new CirSubtractNode(
            new CirTransformNode(new CirBoxNode(20, 20, 10), Transform3D.CreateTranslation(new Vector3D(5, 2, 4))),
            new CirTransformNode(new CirCylinderNode(3, 16), Transform3D.CreateTranslation(new Vector3D(4, 1, 4))));
        var result = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(root));
        Assert.True(result.Success);
        Assert.Equal(8d, result.Value!.ClearanceXPlus, 6);
    }

    [Fact]
    public void Rejects_NonSubtractRoot()
    {
        var result = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(new CirBoxNode(2,2,2)));
        Assert.False(result.Success);
        Assert.Equal(CirBoxCylinderRecognitionReason.UnsupportedRootNotSubtract, result.Reason);
    }

    [Fact]
    public void Rejects_LeftNotBox()
    {
        var result = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(new CirSubtractNode(new CirSphereNode(3), new CirCylinderNode(1, 10))));
        Assert.False(result.Success);
        Assert.Equal(CirBoxCylinderRecognitionReason.UnsupportedLeftNotBox, result.Reason);
    }

    [Fact]
    public void Rejects_RightNotCylinder()
    {
        var result = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(new CirSubtractNode(new CirBoxNode(10,10,10), new CirSphereNode(2))));
        Assert.False(result.Success);
        Assert.Equal(CirBoxCylinderRecognitionReason.UnsupportedRightNotCylinder, result.Reason);
    }

    [Fact]
    public void Rejects_UnsupportedTransform()
    {
        var rotated = new CirSubtractNode(new CirBoxNode(10,10,10), new CirTransformNode(new CirCylinderNode(2, 20), Transform3D.CreateRotationX(0.2)));
        var result = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(rotated));
        Assert.False(result.Success);
        Assert.Equal(CirBoxCylinderRecognitionReason.UnsupportedTransform, result.Reason);
    }

    [Fact]
    public void Rejects_BlindCylinder()
    {
        var root = new CirSubtractNode(new CirBoxNode(10,10,10), new CirCylinderNode(2, 8));
        var result = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(root));
        Assert.False(result.Success);
        Assert.Equal(CirBoxCylinderRecognitionReason.UnsupportedNotThroughHole, result.Reason);
    }

    [Fact]
    public void Rejects_Tangent_And_Outside()
    {
        var tangent = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(new CirSubtractNode(new CirBoxNode(10,10,10), new CirTransformNode(new CirCylinderNode(2, 20), Transform3D.CreateTranslation(new Vector3D(3, 0, 0))))));
        Assert.False(tangent.Success);
        Assert.Equal(CirBoxCylinderRecognitionReason.UnsupportedTangentOrGrazing, tangent.Reason);

        var outside = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(new CirSubtractNode(new CirBoxNode(10,10,10), new CirTransformNode(new CirCylinderNode(2, 20), Transform3D.CreateTranslation(new Vector3D(3.001 + ToleranceContext.Default.Linear, 0, 0))))));
        Assert.False(outside.Success);
        Assert.Equal(CirBoxCylinderRecognitionReason.UnsupportedCylinderOutsideBox, outside.Reason);
    }

    [Fact]
    public void Allows_Offset_ThroughHole_WithStrictClearance()
    {
        var root = new CirSubtractNode(new CirBoxNode(12, 12, 10), new CirTransformNode(new CirCylinderNode(2, 20), Transform3D.CreateTranslation(new Vector3D(1, -1, 0))));
        var result = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(root));
        Assert.True(result.Success);
    }

    [Fact]
    public void Replay_IsOptional_AndMismatchIsDiagnosticOnly()
    {
        var root = new CirSubtractNode(new CirBoxNode(12, 12, 10), new CirCylinderNode(2, 20));
        var noReplay = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(root));
        Assert.True(noReplay.Success);

        var mismatchReplay = new NativeGeometryReplayLog([
            new NativeGeometryReplayOperation(0, "base", "primitive:box", null, null, null, null, new NativeGeometryResolvedPlacement(NativeGeometryPlacementKind.None, null, null, Vector3D.Zero, Vector3D.Zero, true, null), null),
            new NativeGeometryReplayOperation(1, "cut", "boolean:subtract", "base", "box", "tool", null, new NativeGeometryResolvedPlacement(NativeGeometryPlacementKind.None, null, null, Vector3D.Zero, Vector3D.Zero, true, null), null)
        ]);
        var withReplay = CirBoxCylinderRecognizer.Recognize(new CirBoxCylinderRecognizerInput(root, mismatchReplay));
        Assert.True(withReplay.Success);
        Assert.Equal(CirBoxCylinderRecognitionReason.ReplayMismatch, withReplay.Reason);
    }
}
