using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Cir;

public sealed class CirEvaluationTests
{
    [Fact]
    public void CIR_Box_EvaluatePoints()
    {
        var box = new CirBoxNode(10d, 8d, 6d);

        Assert.True(box.Evaluate(new Point3D(0d, 0d, 0d)) < 0d);
        Assert.True(box.Evaluate(new Point3D(6d, 0d, 0d)) > 0d);
        Assert.Equal(0d, box.Evaluate(new Point3D(5d, 0d, 0d)), 6);
    }

    [Fact]
    public void CIR_Cylinder_EvaluatePoints()
    {
        var cylinder = new CirCylinderNode(3d, 10d);

        Assert.True(cylinder.Evaluate(new Point3D(1d, 1d, 0d)) < 0d);
        Assert.True(cylinder.Evaluate(new Point3D(4d, 0d, 0d)) > 0d);
        Assert.Equal(0d, cylinder.Evaluate(new Point3D(3d, 0d, 0d)), 6);
    }

    [Fact]
    public void CIR_BoxMinusCylinder_EvaluatePoints()
    {
        var box = new CirBoxNode(20d, 20d, 20d);
        var cylinder = new CirCylinderNode(3d, 24d);
        var cut = new CirSubtractNode(box, cylinder);

        Assert.True(cut.Evaluate(new Point3D(8d, 0d, 0d)) < 0d);
        Assert.True(cut.Evaluate(new Point3D(0d, 0d, 0d)) > 0d);
        Assert.True(cut.Evaluate(new Point3D(12d, 0d, 0d)) > 0d);
        Assert.True(double.Abs(cut.Evaluate(new Point3D(3d, 0d, 0d))) < 1e-6);
    }

    [Fact]
    public void CIR_BoxMinusCylinder_ApproximateVolume_IsReasonable()
    {
        var box = new CirBoxNode(20d, 20d, 20d);
        var cylinder = new CirCylinderNode(3d, 24d);
        var cut = new CirSubtractNode(box, cylinder);

        var boxEstimate = CirVolumeEstimator.EstimateVolume(box, resolution: 40);
        var cutEstimate = CirVolumeEstimator.EstimateVolume(cut, resolution: 40);

        var expectedBox = 20d * 20d * 20d;
        var expectedCut = expectedBox - (double.Pi * 3d * 3d * 20d);

        Assert.InRange(boxEstimate, expectedBox * 0.97d, expectedBox * 1.03d);
        Assert.InRange(cutEstimate, expectedCut * 0.94d, expectedCut * 1.06d);
    }

    [Fact]
    public void CIR_DoesNotAffectExistingBRepPath()
    {
        var box = Aetheris.Kernel.Core.Brep.BrepPrimitives.CreateBox(10d, 10d, 10d);
        Assert.True(box.IsSuccess);
    }
}
