using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class RestrictedContourSnapAnalyzerTests
{
    [Fact]
    public void Snap_BoxFaceVsCylinder_ProducesCircleCandidate()
    {
        var stitched = BuildStitched(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 20)), 65);
        var result = RestrictedContourSnapAnalyzer.Analyze(stitched, new RestrictedContourSnapOptions(0.06d, 0.02d, 8));
        var candidate = Assert.Single(result.Candidates.Where(c => c.Kind == RestrictedContourSnapKind.Circle && c.Status == RestrictedContourSnapStatus.Candidate));
        var p = Assert.IsType<CircleSnapParameters2D>(candidate.Parameters);
        Assert.True(double.IsFinite(p.CenterU) && double.IsFinite(p.CenterV) && double.IsFinite(p.Radius));
        Assert.True(candidate.MaxError <= 0.06d);
        Assert.False(result.ExactTrimAccepted);
    }

    [Fact]
    public void Snap_BoxFaceVsSphere_ProducesCircleCandidate()
    {
        var stitched = BuildStitched(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(6)), 65);
        var result = RestrictedContourSnapAnalyzer.Analyze(stitched, new RestrictedContourSnapOptions(0.06d, 0.02d, 8));
        Assert.Contains(result.Candidates, c => c.Kind == RestrictedContourSnapKind.Circle && c.Status == RestrictedContourSnapStatus.Candidate);
    }

    [Fact]
    public void Snap_BoxFaceVsTorus_RejectsOrDefersNonCircularCandidate()
    {
        var stitched = BuildStitched(new CirSubtractNode(new CirBoxNode(12, 12, 12), new CirTorusNode(4, 1)), 65);
        var result = RestrictedContourSnapAnalyzer.Analyze(stitched, new RestrictedContourSnapOptions(0.01d, 0.01d, 8));
        var circles = result.Candidates.Where(c => c.Kind == RestrictedContourSnapKind.Circle).ToArray();
        Assert.True(circles.Length == 0 || circles.All(c => c.Status != RestrictedContourSnapStatus.Candidate || c.Diagnostics.Contains("snap-candidate-only:not-exact-trim")));
        Assert.False(result.ExactTrimAccepted);
        Assert.False(result.BRepTopologyImplemented);
        Assert.False(result.StepExportImplemented);
    }

    [Fact]
    public void Snap_OpenBoundaryChain_LineCandidateOrDeferred()
    {
        var segA = new SurfaceTrimContourSegment2D(0, 0, new(0d, 0.25d), new(0.2d, 0.3d), RestrictedFieldCellClassification.Mixed, []);
        var segB = new SurfaceTrimContourSegment2D(1, 0, new(0.2d, 0.3d), new(0.4d, 0.35d), RestrictedFieldCellClassification.Mixed, []);
        var extraction = new SurfaceTrimContourExtractionResult(true, RestrictedFieldContourExtractionMethod.MarchingSquares, 2, [segA, segB], []);
        var stitched = SurfaceTrimContourStitcher.Stitch(extraction);
        var result = RestrictedContourSnapAnalyzer.Analyze(stitched, new RestrictedContourSnapOptions(0.02d, 0.05d, 3));
        Assert.Contains(result.Candidates, c => c.Kind == RestrictedContourSnapKind.Line && (c.Status == RestrictedContourSnapStatus.Candidate || c.Status == RestrictedContourSnapStatus.Deferred));
    }

    [Fact]
    public void Snap_DeterministicResults()
    {
        var stitched = BuildStitched(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(6)), 65);
        var options = new RestrictedContourSnapOptions(0.06d, 0.02d, 8);
        var a = RestrictedContourSnapAnalyzer.Analyze(stitched, options);
        var b = RestrictedContourSnapAnalyzer.Analyze(stitched, options);
        Assert.Equal(a.Candidates.Select(c => (c.ChainId, c.Kind, c.Status, c.MaxError, c.MeanError, c.SampleCount)), b.Candidates.Select(c => (c.ChainId, c.Kind, c.Status, c.MaxError, c.MeanError, c.SampleCount)));
    }

    [Fact]
    public void Snap_TooFewPoints_Rejected()
    {
        var seg = new SurfaceTrimContourSegment2D(0, 0, new(0.1d, 0.1d), new(0.2d, 0.2d), RestrictedFieldCellClassification.Mixed, []);
        var extraction = new SurfaceTrimContourExtractionResult(true, RestrictedFieldContourExtractionMethod.MarchingSquares, 1, [seg], []);
        var stitched = SurfaceTrimContourStitcher.Stitch(extraction);
        var result = RestrictedContourSnapAnalyzer.Analyze(stitched, new RestrictedContourSnapOptions(0.01d, 0.01d, 5));
        Assert.Contains(result.Candidates, c => c.Status == RestrictedContourSnapStatus.Rejected && c.Diagnostics.Any(d => d.StartsWith("snap-rejected-too-few-points:")));
    }

    private static SurfaceTrimContourStitchResult BuildStitched(CirSubtractNode root, int resolution)
    {
        var source = Assert.Single(SourceSurfaceExtractor.Extract(root.Left).Descriptors.Where(d => d.ParameterPayloadReference == "top"));
        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Left);
        var grid = RestrictedFieldGridSampler.Sample(field, new RestrictedFieldGridOptions(resolution, resolution));
        var extraction = RestrictedFieldMarchingSquaresExtractor.Extract(grid, field.Parameterization);
        return SurfaceTrimContourStitcher.Stitch(extraction);
    }
}
