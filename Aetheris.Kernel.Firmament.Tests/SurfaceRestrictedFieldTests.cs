using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfaceRestrictedFieldTests
{
    [Fact]
    public void RestrictedField_BoxFaceVsCylinder_EvaluatesDeterministically()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 20));
        var source = Assert.Single(SourceSurfaceExtractor.Extract(root.Left).Descriptors.Where(d => d.ParameterPayloadReference == "top"));
        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Left);

        Assert.Equal(SurfaceRestrictedFieldStatus.Ready, field.Status);
        var s1 = field.Evaluate(0.5d, 0.5d);
        var s2 = field.Evaluate(0.5d, 0.5d);
        Assert.Equal(s1.Value, s2.Value, 12);
        Assert.Equal(RestrictedFieldSignClassification.InsideOpposite, s1.SignClassification);
        Assert.Contains("evaluation-available", field.Diagnostics);
        Assert.Contains("contour-extraction-not-implemented", field.Diagnostics);

        var far = field.Evaluate(0.95d, 0.95d);
        Assert.Equal(RestrictedFieldSignClassification.OutsideOpposite, far.SignClassification);
    }

    [Fact]
    public void RestrictedField_BoxFaceVsSphere_EvaluatesDeterministically()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(6));
        var source = Assert.Single(SourceSurfaceExtractor.Extract(root.Left).Descriptors.Where(d => d.ParameterPayloadReference == "top"));
        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Left);

        var a = field.Evaluate(0.5d, 0.5d);
        var b = field.Evaluate(0.5d, 0.5d);
        Assert.Equal(a.Value, b.Value, 12);
        Assert.Equal(RestrictedFieldSignClassification.InsideOpposite, a.SignClassification);
    }

    [Fact]
    public void RestrictedField_BoxFaceVsTorus_EvaluatesDespiteMaterializationDeferred()
    {
        var root = new CirSubtractNode(new CirBoxNode(12, 12, 12), new CirTorusNode(4, 1));
        var source = Assert.Single(SourceSurfaceExtractor.Extract(root.Left).Descriptors.Where(d => d.ParameterPayloadReference == "top"));
        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Left);

        Assert.Equal(SurfaceRestrictedFieldStatus.Ready, field.Status);
        var a = field.Evaluate(0.5d, 0.5d);
        var b = field.Evaluate(0.5d, 0.5d);
        Assert.Equal(a.Value, b.Value, 12);
        Assert.Contains("export-materialization-unchanged", field.Diagnostics);
    }

    [Fact]
    public void RestrictedField_RejectsNonRectanglePlanarSource()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var circlePlanar = Assert.Single(SourceSurfaceExtractor.Extract(root.Right).Descriptors.Where(d => d.ParameterPayloadReference == "cap-top"));

        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, circlePlanar, SubtractOperandSide.Right);
        Assert.Equal(SurfaceRestrictedFieldStatus.Unsupported, field.Status);
        Assert.Contains(field.Diagnostics, d => d.Contains("bounded-kind-Circle", StringComparison.Ordinal));
    }

    [Fact]
    public void RestrictedField_RejectsMissingBoundedGeometry()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "synthetic", null, null, Transform3D.Identity, "test", nameof(CirBoxNode), null, FacePatchOrientationRole.Forward);
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(3));

        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Left);
        Assert.Equal(SurfaceRestrictedFieldStatus.Unsupported, field.Status);
        Assert.Contains("bounded-rectangle-missing", field.Diagnostics);
    }

    [Fact]
    public void RestrictedField_SubtractSideSelection_SelectsOppositeOperand()
    {
        var left = new CirSphereNode(2);
        var right = new CirCylinderNode(1, 8);
        var root = new CirSubtractNode(left, right);
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "synthetic", BoundedPlanarPatchGeometry.CreateRectangle(new Point3D(-1, -1, 0), new Point3D(1, -1, 0), new Point3D(1, 1, 0), new Point3D(-1, 1, 0), new Vector3D(0, 0, 1)), null, Transform3D.Identity, "test", nameof(CirBoxNode), null, FacePatchOrientationRole.Forward);

        var leftSource = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Left);
        var rightSource = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Right);

        var pLeft = leftSource.Evaluate(1d, 1d).Value;
        var pRight = rightSource.Evaluate(0.5d, 0.5d).Value;
        Assert.NotEqual(pLeft, pRight);
        Assert.Contains("opposite-operand-selected:right", leftSource.Diagnostics);
        Assert.Contains("opposite-operand-selected:left", rightSource.Diagnostics);
    }
}
