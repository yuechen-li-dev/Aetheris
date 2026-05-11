using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class TieredTrimRepresentationBuilderTests
{
    [Fact]
    public void TieredTrim_CylinderCircleSelection_BuildsAnalyticCircleRepresentation()
    {
        var (field, stitched, selection) = BuildSelection(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 20)), 65, new RestrictedContourSnapOptions(0.06d, 0.02d, 8));
        var result = TieredTrimRepresentationBuilder.Build(selection, stitched, field);

        Assert.Equal(TieredTrimRepresentationKind.AnalyticCircle, result.Representation.Kind);
        Assert.NotNull(result.Representation.Circle);
        Assert.NotNull(result.Representation.NumericalContour);
        Assert.Equal(TieredTrimExportCapability.ElementaryCurveCandidate, result.Representation.ExportCapability);
        Assert.False(result.Representation.ExactStepExported);
        Assert.False(result.Representation.BRepTopologyEmitted);
    }

    [Fact]
    public void TieredTrim_SphereCircleSelection_BuildsAnalyticCircleRepresentation()
    {
        var (field, stitched, selection) = BuildSelection(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(6)), 65, new RestrictedContourSnapOptions(0.06d, 0.02d, 8));
        var result = TieredTrimRepresentationBuilder.Build(selection, stitched, field);
        Assert.Equal(TieredTrimRepresentationKind.AnalyticCircle, result.Representation.Kind);
        Assert.NotNull(result.Representation.Circle);
    }

    [Fact]
    public void TieredTrim_OpenLineSelection_BuildsAnalyticLineOrDeferredRepresentation()
    {
        var segA = new SurfaceTrimContourSegment2D(0, 0, new(0d, 0.25d), new(0.2d, 0.3d), RestrictedFieldCellClassification.Mixed, []);
        var segB = new SurfaceTrimContourSegment2D(1, 0, new(0.2d, 0.3d), new(0.4d, 0.35d), RestrictedFieldCellClassification.Mixed, []);
        var stitched = SurfaceTrimContourStitcher.Stitch(new SurfaceTrimContourExtractionResult(true, RestrictedFieldContourExtractionMethod.MarchingSquares, 2, [segA, segB], []));
        var options = new RestrictedContourSnapOptions(0.02d, 0.05d, 3);
        var selection = RestrictedContourSnapSelector.Select(stitched, RestrictedContourSnapAnalyzer.Analyze(stitched, options), options);
        var result = TieredTrimRepresentationBuilder.Build(selection, stitched, null);

        Assert.True(result.Representation.Kind is TieredTrimRepresentationKind.AnalyticLine or TieredTrimRepresentationKind.NumericalOnly or TieredTrimRepresentationKind.Deferred);
        if (result.Representation.Kind != TieredTrimRepresentationKind.AnalyticLine)
        {
            Assert.Contains(result.Representation.Diagnostics, d => d.Contains("deferred", StringComparison.Ordinal) || d.Contains("numerical-only", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void TieredTrim_Torus_DoesNotClaimGenericExactness()
    {
        var (field, stitched, selection) = BuildSelection(new CirSubtractNode(new CirBoxNode(12, 12, 12), new CirTorusNode(4, 1)), 65, new RestrictedContourSnapOptions(0.01d, 0.01d, 8));
        var result = TieredTrimRepresentationBuilder.Build(selection, stitched, field);
        Assert.Contains(result.Diagnostics, d => d == "tiered-trim-torus-generic-exactness-not-claimed");
        Assert.False(result.Representation.ExactStepExported);
    }

    [Fact]
    public void TieredTrim_NumericalOnly_IsNotExportable()
    {
        var chain = new SurfaceTrimContourChain2D(0, SurfaceTrimContourChainStatus.ClosedLoop, [new SurfaceTrimContourChainPoint2D(0,0,0,"A"), new SurfaceTrimContourChainPoint2D(1,1,0,"B")], [0], true, false, [], "k");
        var stitched = new SurfaceTrimContourStitchResult(true, [chain], 1, 0, 0, 0, 0, []);
        var candidate = new RestrictedContourSnapCandidate(0, RestrictedContourSnapKind.Circle, RestrictedContourSnapStatus.Rejected, null, 0.2d, 0.1d, 10, ["high-error"]);
        var selection = RestrictedContourSnapSelector.Select(stitched, new RestrictedContourSnapAnalysisResult(true, [candidate], []), new RestrictedContourSnapOptions(0.01d, 0.01d, 2));

        var result = TieredTrimRepresentationBuilder.Build(selection, stitched, null);
        Assert.Equal(TieredTrimRepresentationKind.NumericalOnly, result.Representation.Kind);
        Assert.Equal(TieredTrimExportCapability.NumericalOnlyNotExportable, result.Representation.ExportCapability);
    }

    [Fact]
    public void TieredTrim_MissingProvenance_Diagnosed()
    {
        var chain = new SurfaceTrimContourChain2D(0, SurfaceTrimContourChainStatus.OpenChain, [new SurfaceTrimContourChainPoint2D(0,0,0,"A"), new SurfaceTrimContourChainPoint2D(1,0,0,"B")], [0], false, false, [], "k");
        var stitched = new SurfaceTrimContourStitchResult(true, [chain], 0, 1, 0, 0, 0, []);
        var candidate = new RestrictedContourSnapCandidate(0, RestrictedContourSnapKind.Line, RestrictedContourSnapStatus.Candidate, new LineSnapParameters2D(0,0,1,0), 0.001d, 0.001d, 10, []);
        var selection = new RestrictedContourSnapSelectionResult(true, RestrictedContourSnapRouteKind.AnalyticLine, candidate, true, RestrictedContourExportCapability.ElementaryCurveCandidate, [], []);

        var result = TieredTrimRepresentationBuilder.Build(selection, stitched, null);
        Assert.Contains(result.Representation.SurfaceIntersectionProvenance.Diagnostics, d => d.StartsWith("tiered-trim-provenance-missing", StringComparison.Ordinal));
    }

    private static (SurfaceRestrictedField field, SurfaceTrimContourStitchResult stitched, RestrictedContourSnapSelectionResult selection) BuildSelection(CirSubtractNode root, int resolution, RestrictedContourSnapOptions options)
    {
        var source = Assert.Single(SourceSurfaceExtractor.Extract(root.Left).Descriptors.Where(d => d.ParameterPayloadReference == "top"));
        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, SubtractOperandSide.Left);
        var grid = RestrictedFieldGridSampler.Sample(field, new RestrictedFieldGridOptions(resolution, resolution));
        var extraction = RestrictedFieldMarchingSquaresExtractor.Extract(grid, field.Parameterization);
        var stitched = SurfaceTrimContourStitcher.Stitch(extraction);
        var selection = RestrictedContourSnapSelector.Select(stitched, RestrictedContourSnapAnalyzer.Analyze(stitched, options), options);
        return (field, stitched, selection);
    }
}
