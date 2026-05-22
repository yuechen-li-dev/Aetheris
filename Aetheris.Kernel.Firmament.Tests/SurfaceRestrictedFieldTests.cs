using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfaceRestrictedFieldTests
{
    [Fact]
    public void RestrictedFieldGrid_BoxFaceVsCylinder_HasMixedCells()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 20));
        var source = Assert.Single(SourceSurfaceExtractor.Extract(root.Left).Descriptors.Where(d => d.ParameterPayloadReference == "top"));
        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Left);

        var grid = RestrictedFieldGridSampler.Sample(field, new RestrictedFieldGridOptions(17, 17));
        Assert.Equal(17 * 17, grid.Counts.SampleCount);
        Assert.Equal(16 * 16, grid.Counts.CellCount);
        Assert.True(grid.Counts.MixedCellCount > 0 || grid.Counts.BoundaryCellCount > 0);
        Assert.True(grid.Counts.InsideCellCount > 0);
        Assert.True(grid.Counts.OutsideCellCount > 0);
        Assert.Contains("contour-extraction-not-implemented", grid.Diagnostics);
    }

    [Fact]
    public void RestrictedFieldGrid_BoxFaceVsSphere_HasMixedCells()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(6));
        var source = Assert.Single(SourceSurfaceExtractor.Extract(root.Left).Descriptors.Where(d => d.ParameterPayloadReference == "top"));
        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Left);

        var grid = RestrictedFieldGridSampler.Sample(field, new RestrictedFieldGridOptions(17, 17));
        Assert.True(grid.Counts.MixedCellCount > 0 || grid.Counts.BoundaryCellCount > 0);
    }

    [Fact]
    public void RestrictedFieldGrid_BoxFaceVsTorus_SamplesAndReportsMixedOrDeferred()
    {
        var root = new CirSubtractNode(new CirBoxNode(12, 12, 12), new CirTorusNode(4, 1));
        var source = Assert.Single(SourceSurfaceExtractor.Extract(root.Left).Descriptors.Where(d => d.ParameterPayloadReference == "top"));
        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Left);

        var grid = RestrictedFieldGridSampler.Sample(field, new RestrictedFieldGridOptions(17, 17));
        Assert.Equal(17 * 17, grid.Counts.SampleCount);
        Assert.True(grid.Counts.MixedCellCount >= 0);
        Assert.Contains("export-materialization-unchanged", grid.Diagnostics);
    }

    [Fact]
    public void RestrictedFieldGrid_DeterministicOrdering()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(6));
        var source = Assert.Single(SourceSurfaceExtractor.Extract(root.Left).Descriptors.Where(d => d.ParameterPayloadReference == "top"));
        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Left);

        var a = RestrictedFieldGridSampler.Sample(field, new RestrictedFieldGridOptions(17, 17));
        var b = RestrictedFieldGridSampler.Sample(field, new RestrictedFieldGridOptions(17, 17));
        Assert.Equal(a.Counts, b.Counts);
        Assert.Equal(a.Samples.Select(s => (s.I, s.J, s.Sample.Value, s.Sample.SignClassification)), b.Samples.Select(s => (s.I, s.J, s.Sample.Value, s.Sample.SignClassification)));
        Assert.Equal(a.Cells.Select(c => (c.CellI, c.CellJ, c.Classification)), b.Cells.Select(c => (c.CellI, c.CellJ, c.Classification)));
    }

    [Theory]
    [InlineData(1, 17)]
    [InlineData(17, 1)]
    public void RestrictedFieldGrid_InvalidResolution_Rejected(int u, int v)
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(6));
        var source = Assert.Single(SourceSurfaceExtractor.Extract(root.Left).Descriptors.Where(d => d.ParameterPayloadReference == "top"));
        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Left);

        Assert.Throws<ArgumentOutOfRangeException>(() => RestrictedFieldGridSampler.Sample(field, new RestrictedFieldGridOptions(u, v)));
    }

    [Fact]
    public void RestrictedFieldGrid_CellClassificationPolicy()
    {
        static RestrictedFieldGridSample Sample(int i, int j, RestrictedFieldSignClassification sign, double value = 1d)
            => new(i, j, i, j, new RestrictedFieldSample(i, j, Point3D.Origin, value, sign, ["evaluation-available"]));

        var inside = RestrictedFieldGridSampler.ClassifyCell([Sample(0,0,RestrictedFieldSignClassification.InsideOpposite,-1), Sample(1,0,RestrictedFieldSignClassification.InsideOpposite,-1), Sample(0,1,RestrictedFieldSignClassification.InsideOpposite,-1), Sample(1,1,RestrictedFieldSignClassification.InsideOpposite,-1)]);
        var outside = RestrictedFieldGridSampler.ClassifyCell([Sample(0,0,RestrictedFieldSignClassification.OutsideOpposite,1), Sample(1,0,RestrictedFieldSignClassification.OutsideOpposite,1), Sample(0,1,RestrictedFieldSignClassification.OutsideOpposite,1), Sample(1,1,RestrictedFieldSignClassification.OutsideOpposite,1)]);
        var mixed = RestrictedFieldGridSampler.ClassifyCell([Sample(0,0,RestrictedFieldSignClassification.InsideOpposite,-1), Sample(1,0,RestrictedFieldSignClassification.OutsideOpposite,1), Sample(0,1,RestrictedFieldSignClassification.InsideOpposite,-1), Sample(1,1,RestrictedFieldSignClassification.OutsideOpposite,1)]);
        var boundary = RestrictedFieldGridSampler.ClassifyCell([Sample(0,0,RestrictedFieldSignClassification.Boundary,0), Sample(1,0,RestrictedFieldSignClassification.Boundary,0), Sample(0,1,RestrictedFieldSignClassification.Boundary,0), Sample(1,1,RestrictedFieldSignClassification.Boundary,0)]);

        Assert.Equal(RestrictedFieldCellClassification.Inside, inside);
        Assert.Equal(RestrictedFieldCellClassification.Outside, outside);
        Assert.Equal(RestrictedFieldCellClassification.Mixed, mixed);
        Assert.Equal(RestrictedFieldCellClassification.Boundary, boundary);
    }

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
