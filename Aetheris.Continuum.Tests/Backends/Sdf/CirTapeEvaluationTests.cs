using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Continuum.Tests.Backends.Sdf;

public sealed class SdfTapeEvaluationTests
{
    [Fact]
    public void Tape_Transform_NoRecursiveChildEvaluation()
    {
        var node = new SdfTransformNode(new SdfBoxNode(4d, 2d, 6d), Transform3D.CreateTranslation(new Vector3D(2d, -1d, 3d)));
        var tape = SdfTapeLowerer.Lower(node);

        Assert.DoesNotContain(tape.Instructions, i => i.SourceKind == SdfNodeKind.Transform);
        Assert.All(tape.BoxPayloads, payload => Assert.NotEqual(Transform3D.Identity, payload.InverseTransform));
    }

    [Fact]
    public void Tape_Transform_MatchesRecursiveEvaluate()
    {
        var transform = Transform3D.CreateTranslation(new Vector3D(2d, -1d, 3d)) * Transform3D.CreateRotationZ(double.Pi / 6d);
        var node = new SdfTransformNode(new SdfBoxNode(4d, 2d, 6d), transform);
        AssertTapeMatches(node, new Point3D(2d, -1d, 3d), new Point3D(7d, -1d, 3d), new Point3D(4d, 0d, 3d));
    }

    [Fact]
    public void Tape_NestedTransform_MatchesRecursiveEvaluate()
    {
        var node = new SdfTransformNode(
            new SdfTransformNode(
                new SdfSphereNode(2.5d),
                Transform3D.CreateRotationY(double.Pi / 4d) * Transform3D.CreateTranslation(new Vector3D(0.5d, 0d, 1d))),
            Transform3D.CreateTranslation(new Vector3D(3d, -1d, 2d)) * Transform3D.CreateRotationX(double.Pi / 3d));

        AssertTapeMatches(node, new Point3D(3d, -1d, 2d), new Point3D(4.5d, -0.5d, 2.25d), new Point3D(0d, 0d, 0d));
    }

    [Fact]
    public void Tape_BooleanUnderTransform_MatchesRecursiveEvaluate()
    {
        var booleanNode = new SdfSubtractNode(new SdfBoxNode(10d, 8d, 6d), new SdfCylinderNode(2d, 10d));
        var node = new SdfTransformNode(booleanNode, Transform3D.CreateTranslation(new Vector3D(1d, 2d, -1d)) * Transform3D.CreateRotationZ(double.Pi / 8d));

        AssertTapeMatches(node, new Point3D(1d, 2d, -1d), new Point3D(5d, 2d, -1d), new Point3D(1d, 5d, -1d));
    }

    [Fact]
    public void Tape_AllCurrentNodes_MatchRecursiveEvaluate()
    {
        var transformedCylinder = new SdfTransformNode(
            new SdfCylinderNode(1.5d, 9d),
            Transform3D.CreateTranslation(new Vector3D(-2d, 0.5d, 1d)) * Transform3D.CreateRotationY(double.Pi / 5d));
        var union = new SdfUnionNode(new SdfSphereNode(3d), transformedCylinder);
        var subtract = new SdfSubtractNode(union, new SdfBoxNode(2.5d, 7d, 2d));
        var node = new SdfIntersectNode(subtract, new SdfTransformNode(new SdfBoxNode(10d, 10d, 10d), Transform3D.CreateTranslation(new Vector3D(0.5d, -0.5d, 0d))));

        AssertTapeMatches(node,
            new Point3D(0d, 0d, 0d),
            new Point3D(1d, 2d, -1d),
            new Point3D(-3d, 0.5d, 1d),
            new Point3D(4d, 4d, 4d),
            new Point3D(-5d, -2d, 2d));
    }

    [Fact]
    public void Tape_DeterministicLowering()
    {
        var node = new SdfSubtractNode(
            new SdfUnionNode(new SdfBoxNode(8d, 8d, 8d), new SdfSphereNode(6d)),
            new SdfTransformNode(new SdfCylinderNode(2d, 12d), Transform3D.CreateTranslation(new Vector3D(1d, 2d, 0d))));

        var first = SdfTapeLowerer.Lower(node);
        var second = SdfTapeLowerer.Lower(node);

        Assert.Equal(first.OutputSlot, second.OutputSlot);
        Assert.Equal(first.SlotCount, second.SlotCount);
        Assert.Equal(first.Instructions, second.Instructions);
        Assert.Equal(first.BoxPayloads, second.BoxPayloads);
        Assert.Equal(first.CylinderPayloads, second.CylinderPayloads);
        Assert.Equal(first.SpherePayloads, second.SpherePayloads);
        Assert.Equal(first.TorusPayloads, second.TorusPayloads);

        var probe = new Point3D(1.25d, -0.5d, 2d);
        Assert.Equal(first.Evaluate(probe), second.Evaluate(probe), 12);
    }

    [Fact]
    public void Tape_Torus_MatchesRecursiveEvaluate()
    {
        var node = new SdfTransformNode(new SdfTorusNode(6d, 2d), Transform3D.CreateTranslation(new Vector3D(1d, 0d, 0.5d)));
        AssertTapeMatches(node, new Point3D(7d, 0d, 0.5d), new Point3D(1d, 0d, 0.5d), new Point3D(1d, 0d, 2.5d));
    }

    private static void AssertTapeMatches(SdfNode node, params Point3D[] points)
    {
        var tape = SdfTapeLowerer.Lower(node);
        foreach (var point in points)
        {
            var expected = node.Evaluate(point);
            var actual = tape.Evaluate(point);
            Assert.InRange(double.Abs(expected - actual), 0d, 1e-6d);
        }
    }
}
