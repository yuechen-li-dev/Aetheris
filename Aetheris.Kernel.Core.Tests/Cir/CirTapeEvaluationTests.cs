using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Tests.Cir;

public sealed class CirTapeEvaluationTests
{
    [Fact]
    public void Tape_Box_MatchesRecursiveEvaluate()
    {
        var node = new CirBoxNode(10d, 8d, 6d);
        AssertTapeMatches(node, new Point3D(0d, 0d, 0d), new Point3D(6d, 0d, 0d), new Point3D(5d, 0d, 0d));
    }

    [Fact]
    public void Tape_Cylinder_MatchesRecursiveEvaluate()
    {
        var node = new CirCylinderNode(3d, 10d);
        AssertTapeMatches(node, new Point3D(1d, 1d, 0d), new Point3D(4d, 0d, 0d), new Point3D(3d, 0d, 0d));
    }

    [Fact]
    public void Tape_Sphere_MatchesRecursiveEvaluate()
    {
        var node = new CirSphereNode(5d);
        AssertTapeMatches(node, new Point3D(0d, 0d, 0d), new Point3D(6d, 0d, 0d), new Point3D(5d, 0d, 0d));
    }

    [Fact]
    public void Tape_BoxMinusCylinder_MatchesRecursiveEvaluate()
    {
        var node = new CirSubtractNode(new CirBoxNode(20d, 20d, 20d), new CirCylinderNode(3d, 24d));
        AssertTapeMatches(node, new Point3D(8d, 0d, 0d), new Point3D(0d, 0d, 0d), new Point3D(3d, 0d, 0d));
    }

    [Fact]
    public void Tape_UnionIntersect_MatchesRecursiveEvaluate()
    {
        var union = new CirUnionNode(new CirSphereNode(3d), new CirBoxNode(4d, 4d, 4d));
        var node = new CirIntersectNode(union, new CirCylinderNode(4d, 6d));
        AssertTapeMatches(node, new Point3D(0d, 0d, 0d), new Point3D(4d, 4d, 0d), new Point3D(0d, 0d, 3d));
    }

    [Fact]
    public void Tape_Transform_MatchesRecursiveEvaluate()
    {
        var transform = Transform3D.CreateTranslation(new Vector3D(2d, -1d, 3d)) * Transform3D.CreateRotationZ(double.Pi / 6d);
        var node = new CirTransformNode(new CirBoxNode(4d, 2d, 6d), transform);
        AssertTapeMatches(node, new Point3D(2d, -1d, 3d), new Point3D(7d, -1d, 3d), new Point3D(4d, 0d, 3d));
    }

    [Fact]
    public void Tape_DeterministicLowering()
    {
        var node = new CirSubtractNode(
            new CirUnionNode(new CirBoxNode(8d, 8d, 8d), new CirSphereNode(6d)),
            new CirTransformNode(new CirCylinderNode(2d, 12d), Transform3D.CreateTranslation(new Vector3D(1d, 2d, 0d))));

        var first = CirTapeLowerer.Lower(node);
        var second = CirTapeLowerer.Lower(node);

        Assert.Equal(first.OutputSlot, second.OutputSlot);
        Assert.Equal(first.SlotCount, second.SlotCount);
        Assert.Equal(first.Instructions, second.Instructions);
        Assert.Equal(first.BoxPayloads, second.BoxPayloads);
        Assert.Equal(first.CylinderPayloads, second.CylinderPayloads);
        Assert.Equal(first.SpherePayloads, second.SpherePayloads);

        var probe = new Point3D(1.25d, -0.5d, 2d);
        Assert.Equal(first.Evaluate(probe), second.Evaluate(probe), 12);
    }

    private static void AssertTapeMatches(CirNode node, params Point3D[] points)
    {
        var tape = CirTapeLowerer.Lower(node);
        foreach (var point in points)
        {
            Assert.Equal(node.Evaluate(point), tape.Evaluate(point), 12);
        }
    }
}
