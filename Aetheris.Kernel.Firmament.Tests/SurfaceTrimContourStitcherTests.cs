using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfaceTrimContourStitcherTests
{
    [Fact]
    public void ContourStitch_BoxFaceVsCylinder_ProducesClosedLoop()
    {
        var (field, grid) = CreateGrid(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 20)), 65);
        var extraction = RestrictedFieldMarchingSquaresExtractor.Extract(grid, field.Parameterization);
        var stitched = SurfaceTrimContourStitcher.Stitch(extraction);
        Assert.True(stitched.Chains.Any(c => c.Status == SurfaceTrimContourChainStatus.ClosedLoop || c.Status == SurfaceTrimContourChainStatus.BoundaryTouching));
        Assert.False(stitched.AnalyticSnapImplemented);
        Assert.False(stitched.BRepTopologyImplemented);
        Assert.False(stitched.ExactExportAvailable);
    }

    [Fact]
    public void ContourStitch_BoxFaceVsSphere_ProducesClosedLoop()
    {
        var (field, grid) = CreateGrid(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(6)), 65);
        var extraction = RestrictedFieldMarchingSquaresExtractor.Extract(grid, field.Parameterization);
        var stitched = SurfaceTrimContourStitcher.Stitch(extraction);
        Assert.Contains(stitched.Chains, c => c.Status == SurfaceTrimContourChainStatus.ClosedLoop || c.Status == SurfaceTrimContourChainStatus.BoundaryTouching);
    }

    [Fact]
    public void ContourStitch_BoxFaceVsTorus_StitchesOrReportsPrecisely()
    {
        var (field, grid) = CreateGrid(new CirSubtractNode(new CirBoxNode(12, 12, 12), new CirTorusNode(4, 1)), 65);
        var extraction = RestrictedFieldMarchingSquaresExtractor.Extract(grid, field.Parameterization);
        var stitched = SurfaceTrimContourStitcher.Stitch(extraction);
        if (extraction.SegmentCount == 0) Assert.Contains("contour-stitching-empty-input", stitched.Diagnostics);
        else Assert.True(stitched.Chains.Count > 0);
    }

    [Fact]
    public void ContourStitch_OpenBoundaryChain_ClassifiedBoundaryTouching()
    {
        var seg = new SurfaceTrimContourSegment2D(0, 0, new(0d, 0.25d), new(0.4d, 0.4d), RestrictedFieldCellClassification.Mixed, []);
        var extraction = new SurfaceTrimContourExtractionResult(true, RestrictedFieldContourExtractionMethod.MarchingSquares, 1, [seg], []);
        var stitched = SurfaceTrimContourStitcher.Stitch(extraction);
        Assert.Equal(SurfaceTrimContourChainStatus.BoundaryTouching, stitched.Chains.Single().Status);
    }

    [Fact]
    public void ContourStitch_AmbiguousBranch_Diagnosed()
    {
        var c = new SurfaceTrimContourPoint2D(0.5, 0.5);
        var segs = new[]
        {
            new SurfaceTrimContourSegment2D(0,0,c,new(0.6,0.5),RestrictedFieldCellClassification.Mixed,[]),
            new SurfaceTrimContourSegment2D(0,1,c,new(0.5,0.6),RestrictedFieldCellClassification.Mixed,[]),
            new SurfaceTrimContourSegment2D(1,0,c,new(0.4,0.5),RestrictedFieldCellClassification.Mixed,[]),
        };
        var extraction = new SurfaceTrimContourExtractionResult(true, RestrictedFieldContourExtractionMethod.MarchingSquares, segs.Length, segs, []);
        var stitched = SurfaceTrimContourStitcher.Stitch(extraction);
        Assert.Equal(SurfaceTrimContourChainStatus.Ambiguous, stitched.Chains.Single().Status);
        Assert.Contains(stitched.Diagnostics, d => d.StartsWith("contour-stitching-ambiguous-cluster-count:", StringComparison.Ordinal));
    }

    [Fact]
    public void ContourStitch_DeterministicOrdering()
    {
        var (field, grid) = CreateGrid(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(6)), 33);
        var extraction = RestrictedFieldMarchingSquaresExtractor.Extract(grid, field.Parameterization);
        var a = SurfaceTrimContourStitcher.Stitch(extraction);
        var b = SurfaceTrimContourStitcher.Stitch(extraction);
        Assert.Equal(a.Chains.Count, b.Chains.Count);
        Assert.Equal(a.Chains.Select(c => (c.Status, c.OrderingKey, c.Points.Count)), b.Chains.Select(c => (c.Status, c.OrderingKey, c.Points.Count)));
    }

    private static (SurfaceRestrictedField field, RestrictedFieldSampleGrid grid) CreateGrid(CirSubtractNode root, int resolution)
    {
        var source = Assert.Single(SourceSurfaceExtractor.Extract(root.Left).Descriptors.Where(d => d.ParameterPayloadReference == "top"));
        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Left);
        var grid = RestrictedFieldGridSampler.Sample(field, new RestrictedFieldGridOptions(resolution, resolution));
        return (field, grid);
    }
}
