using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfaceRestrictedFieldMarchingSquaresTests
{
    [Fact]
    public void MarchingSquares_BoxFaceVsCylinder_ProducesSegments()
    {
        var (field, grid) = CreateGrid(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 20)));
        var result = RestrictedFieldMarchingSquaresExtractor.Extract(grid, field.Parameterization);

        Assert.True(result.Success);
        Assert.True(result.SegmentCount > 0);
        Assert.All(result.Segments, seg =>
        {
            Assert.InRange(seg.A.U, 0d, 1d);
            Assert.InRange(seg.A.V, 0d, 1d);
            Assert.InRange(seg.B.U, 0d, 1d);
            Assert.InRange(seg.B.V, 0d, 1d);
        });
        Assert.False(result.ContourStitchingImplemented);
        Assert.False(result.AnalyticSnapImplemented);
        Assert.False(result.ExactExportAvailable);
    }

    [Fact]
    public void MarchingSquares_BoxFaceVsSphere_ProducesSegments()
    {
        var (field, grid) = CreateGrid(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(6)));
        var result = RestrictedFieldMarchingSquaresExtractor.Extract(grid, field.Parameterization);

        Assert.True(result.Success);
        Assert.True(result.SegmentCount > 0);
    }

    [Fact]
    public void MarchingSquares_BoxFaceVsTorus_ProducesSegmentsOrPreciseNoContour()
    {
        var (field, grid) = CreateGrid(new CirSubtractNode(new CirBoxNode(12, 12, 12), new CirTorusNode(4, 1)));
        var result = RestrictedFieldMarchingSquaresExtractor.Extract(grid, field.Parameterization);

        if (result.SegmentCount > 0)
        {
            Assert.True(result.Success);
        }
        else
        {
            Assert.Contains("no-contour-segments-detected", result.Diagnostics);
        }
    }

    [Fact]
    public void MarchingSquares_DeterministicOrdering()
    {
        var (field, grid) = CreateGrid(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(6)));
        var a = RestrictedFieldMarchingSquaresExtractor.Extract(grid, field.Parameterization);
        var b = RestrictedFieldMarchingSquaresExtractor.Extract(grid, field.Parameterization);

        Assert.Equal(a.SegmentCount, b.SegmentCount);
        Assert.Equal(
            a.Segments.Select(s => (s.CellI, s.CellJ, s.A.U, s.A.V, s.B.U, s.B.V)),
            b.Segments.Select(s => (s.CellI, s.CellJ, s.A.U, s.A.V, s.B.U, s.B.V)));
    }

    [Fact]
    public void MarchingSquares_SkipsInsideOutsideOnlyCells()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(1));
        var source = Assert.Single(SourceSurfaceExtractor.Extract(root.Left).Descriptors.Where(d => d.ParameterPayloadReference == "top"));
        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Left);
        var grid = RestrictedFieldGridSampler.Sample(field, new RestrictedFieldGridOptions(17, 17));

        var result = RestrictedFieldMarchingSquaresExtractor.Extract(grid, field.Parameterization);
        Assert.Equal(0, result.SegmentCount);
        Assert.Contains("no-contour-segments-detected", result.Diagnostics);
    }

    [Fact]
    public void MarchingSquares_AmbiguousCasesAreDiagnosed()
    {
        static RestrictedFieldGridSample Sample(int i, int j, double value)
            => new(i, j, i, j, new RestrictedFieldSample(i, j, default, value, value < 0 ? RestrictedFieldSignClassification.InsideOpposite : RestrictedFieldSignClassification.OutsideOpposite, ["evaluation-available"]));

        var samples = new[]
        {
            Sample(0,0,-1), Sample(1,0,1),
            Sample(0,1,1), Sample(1,1,-1),
        };

        var cell = new RestrictedFieldCell(0, 0, 0, 1, 0, 1, [0, 1, 2, 3], RestrictedFieldCellClassification.Mixed, []);
        var grid = new RestrictedFieldSampleGrid(
            new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "synthetic", null, null, Aetheris.Kernel.Core.Math.Transform3D.Identity, "test", "test", null, FacePatchOrientationRole.Forward),
            2,
            2,
            samples,
            [cell],
            new RestrictedFieldGridCounts(4, 1, 0, 0, 0, 1, 0),
            ["grid-sampler-started"]);

        var result = RestrictedFieldMarchingSquaresExtractor.Extract(grid);
        Assert.True(result.SegmentCount > 0);
        Assert.Contains(result.Diagnostics, d => d.StartsWith("contour-ambiguous-cell-count:", StringComparison.Ordinal));
        Assert.Contains(result.Segments.SelectMany(s => s.Diagnostics), d => d.StartsWith("ambiguous-cell", StringComparison.Ordinal));
    }

    private static (SurfaceRestrictedField field, RestrictedFieldSampleGrid grid) CreateGrid(CirSubtractNode root)
    {
        var source = Assert.Single(SourceSurfaceExtractor.Extract(root.Left).Descriptors.Where(d => d.ParameterPayloadReference == "top"));
        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Left);
        var grid = RestrictedFieldGridSampler.Sample(field, new RestrictedFieldGridOptions(33, 33));
        return (field, grid);
    }
}
