using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class RestrictedContourSnapSelectionTests
{
    [Fact]
    public void SnapSelection_BoxFaceVsCylinder_SelectsCircleCandidate()
    {
        var stitched = BuildStitched(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 20)), 65);
        var options = new RestrictedContourSnapOptions(0.06d, 0.02d, 8);
        var analysis = RestrictedContourSnapAnalyzer.Analyze(stitched, options);
        var selected = RestrictedContourSnapSelector.Select(stitched, analysis, options);

        Assert.Equal(RestrictedContourSnapRouteKind.AnalyticCircle, selected.SelectedRoute);
        Assert.True(selected.AcceptedAnalyticCandidate);
        Assert.Equal(RestrictedContourExportCapability.ElementaryCurveCandidate, selected.ExportCapability);
        Assert.False(selected.BRepTopologyImplemented);
        Assert.False(selected.StepExportImplemented);
    }

    [Fact]
    public void SnapSelection_BoxFaceVsSphere_SelectsCircleCandidate()
    {
        var stitched = BuildStitched(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(6)), 65);
        var options = new RestrictedContourSnapOptions(0.06d, 0.02d, 8);
        var selected = RestrictedContourSnapSelector.Select(stitched, RestrictedContourSnapAnalyzer.Analyze(stitched, options), options);
        Assert.Equal(RestrictedContourSnapRouteKind.AnalyticCircle, selected.SelectedRoute);
        Assert.True(selected.AcceptedAnalyticCandidate);
    }

    [Fact]
    public void SnapSelection_OpenBoundaryLine_SelectsLineOrDefersPrecisely()
    {
        var segA = new SurfaceTrimContourSegment2D(0, 0, new(0d, 0.25d), new(0.2d, 0.3d), RestrictedFieldCellClassification.Mixed, []);
        var segB = new SurfaceTrimContourSegment2D(1, 0, new(0.2d, 0.3d), new(0.4d, 0.35d), RestrictedFieldCellClassification.Mixed, []);
        var stitched = SurfaceTrimContourStitcher.Stitch(new SurfaceTrimContourExtractionResult(true, RestrictedFieldContourExtractionMethod.MarchingSquares, 2, [segA, segB], []));
        var options = new RestrictedContourSnapOptions(0.02d, 0.05d, 3);
        var selected = RestrictedContourSnapSelector.Select(stitched, RestrictedContourSnapAnalyzer.Analyze(stitched, options), options);

        Assert.True(selected.SelectedRoute is RestrictedContourSnapRouteKind.AnalyticLine or RestrictedContourSnapRouteKind.NumericalOnly);
        if (selected.SelectedRoute != RestrictedContourSnapRouteKind.AnalyticLine)
        {
            Assert.Contains(selected.Diagnostics, d => d.Contains("numerical-only", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void SnapSelection_Torus_DoesNotClaimGenericExactness()
    {
        var stitched = BuildStitched(new CirSubtractNode(new CirBoxNode(12, 12, 12), new CirTorusNode(4, 1)), 65);
        var options = new RestrictedContourSnapOptions(0.01d, 0.01d, 8);
        var selected = RestrictedContourSnapSelector.Select(stitched, RestrictedContourSnapAnalyzer.Analyze(stitched, options), options);
        Assert.Contains(selected.Diagnostics, d => d == "snap-selection-torus-generic-exactness-not-implied");
        Assert.False(selected.BRepTopologyImplemented);
        Assert.False(selected.StepExportImplemented);
    }

    [Fact]
    public void SnapSelection_RejectedHighError_FallsBackNumericalOnlyOrDeferred()
    {
        var candidate = new RestrictedContourSnapCandidate(0, RestrictedContourSnapKind.Circle, RestrictedContourSnapStatus.Rejected, null, 0.2d, 0.1d, 10, ["high-error"]);
        var chain = new SurfaceTrimContourChain2D(0, SurfaceTrimContourChainStatus.ClosedLoop, [new SurfaceTrimContourChainPoint2D(0,0,0,"A"), new SurfaceTrimContourChainPoint2D(1,1,0,"B")], [0], true, false, [], "k");
        var stitched = new SurfaceTrimContourStitchResult(true, [chain], 1, 0, 0, 0, 0, []);
        var analysis = new RestrictedContourSnapAnalysisResult(true, [candidate], []);
        var selected = RestrictedContourSnapSelector.Select(stitched, analysis, new RestrictedContourSnapOptions(0.01d, 0.01d, 2));
        Assert.Equal(RestrictedContourSnapRouteKind.NumericalOnly, selected.SelectedRoute);
        Assert.False(selected.AcceptedAnalyticCandidate);
    }

    [Fact]
    public void SnapSelection_DeterministicTieBreak()
    {
        var chain = new SurfaceTrimContourChain2D(0, SurfaceTrimContourChainStatus.ClosedLoop, [new SurfaceTrimContourChainPoint2D(0,0,0,"A"), new SurfaceTrimContourChainPoint2D(1,0,0,"B"), new SurfaceTrimContourChainPoint2D(0,0,0,"A")], [0], true, false, [], "k");
        var stitched = new SurfaceTrimContourStitchResult(true, [chain], 1, 0, 0, 0, 0, []);
        var circle = new RestrictedContourSnapCandidate(0, RestrictedContourSnapKind.Circle, RestrictedContourSnapStatus.Candidate, new CircleSnapParameters2D(0,0,1), 0.001d, 0.001d, 9, []);
        var line = new RestrictedContourSnapCandidate(0, RestrictedContourSnapKind.Line, RestrictedContourSnapStatus.Candidate, new LineSnapParameters2D(0,0,1,0), 0.001d, 0.001d, 9, []);
        var analysis = new RestrictedContourSnapAnalysisResult(true, [circle, line], []);
        var selected = RestrictedContourSnapSelector.Select(stitched, analysis, new RestrictedContourSnapOptions(0.01d, 0.01d, 8));
        Assert.Equal(RestrictedContourSnapRouteKind.AnalyticCircle, selected.SelectedRoute);
    }

    [Fact]
    public void SnapSelection_CandidateTracesIncludeRejectedReasons()
    {
        var chain = new SurfaceTrimContourChain2D(0, SurfaceTrimContourChainStatus.OpenChain, [new SurfaceTrimContourChainPoint2D(0,0,0,"A"), new SurfaceTrimContourChainPoint2D(0.1,0.3,0,"B")], [0], false, false, [], "k");
        var stitched = new SurfaceTrimContourStitchResult(true, [chain], 0, 1, 0, 0, 0, []);
        var circle = new RestrictedContourSnapCandidate(0, RestrictedContourSnapKind.Circle, RestrictedContourSnapStatus.Rejected, null, 0.2d, 0.1d, 2, ["bad-circle"]);
        var analysis = new RestrictedContourSnapAnalysisResult(true, [circle], []);
        var selected = RestrictedContourSnapSelector.Select(stitched, analysis, new RestrictedContourSnapOptions(0.01d, 0.01d, 2));
        Assert.Contains(selected.CandidateTraces, t => !t.Admissible && !string.IsNullOrWhiteSpace(t.RejectionReason));
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
