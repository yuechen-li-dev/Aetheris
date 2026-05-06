using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Analysis;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CirAnalysisServiceTests
{
    [Fact]
    public void CirAnalysisService_TapeInput_VolumeAndPoints()
    {
        var node = new CirBoxNode(4d, 4d, 4d);
        var tape = CirTapeLowerer.Lower(node);

        var result = CirNativeAnalysisService.AnalyzeTape(
            tape,
            node.Bounds,
            [new Point3D(0d, 0d, 0d), new Point3D(10d, 0d, 0d)],
            denseResolution: 20);

        Assert.True(result.Success);
        Assert.Equal("cir", result.Backend);
        Assert.Equal(CirNativeAnalysisInputKind.CirTape, result.InputKind);
        Assert.Equal(CirNativeAnalysisResultKind.Approximate, result.ResultKind);
        Assert.NotNull(result.Volume);
        Assert.Equal(CirNativeEstimatorKind.Dense, result.Volume!.Estimator);
        Assert.Equal(2, result.PointClassifications.Count);
        Assert.Contains(result.PointClassifications, p => p.Classification == CirPointClassification.Inside);
        Assert.Contains(result.PointClassifications, p => p.Classification == CirPointClassification.Outside);
    }

    [Fact]
    public void CirAnalysisService_FirmamentPlan_BoxBasic_Succeeds()
    {
        var plan = CompilePlan("testdata/firmament/examples/box_basic.firmament");
        var result = CirNativeAnalysisService.AnalyzeFirmamentPlan(plan, denseResolution: 30);

        Assert.True(result.Success);
        Assert.Equal(CirNativeAnalysisInputKind.Firmament, result.InputKind);
        Assert.NotNull(result.Bounds);
        Assert.NotNull(result.Volume);
        Assert.True(result.Volume!.EstimatedVolume > 0d);
        Assert.NotNull(result.Lowering);
        Assert.True(result.Lowering!.Supported);
    }

    [Fact]
    public void CirAnalysisService_FirmamentPlan_BoxMinusCylinder_Succeeds()
    {
        var plan = CompilePlan("testdata/firmament/examples/boolean_box_cylinder_hole.firmament");
        var result = CirNativeAnalysisService.AnalyzeFirmamentPlan(plan, denseResolution: 32);

        Assert.True(result.Success);
        Assert.NotNull(result.Volume);
        Assert.Equal(CirNativeEstimatorKind.Dense, result.Volume!.Estimator);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(result.Notes, n => n.Contains("fallback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CirAnalysisService_UnsupportedFirmamentPlan_FailsClearly()
    {
        var plan = CompilePlan("testdata/firmament/examples/rounded_corner_box_basic.firmament");
        var result = CirNativeAnalysisService.AnalyzeFirmamentPlan(plan, denseResolution: 24);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Notes, n => n.Contains("BRep backend may still support materialized analysis", StringComparison.Ordinal));
        Assert.Null(result.Volume);
    }

    [Fact]
    public void AdaptiveVolume_MetadataPresent()
    {
        var plan = CompilePlan("testdata/firmament/examples/boolean_box_cylinder_hole.firmament");
        var options = new CirAdaptiveVolumeOptions(MaxDepth: 6, DirectSampleGrid: 2, MaxTraceEvents: 20);

        var result = CirNativeAnalysisService.AnalyzeFirmamentPlan(plan, adaptiveOptions: options);

        Assert.True(result.Success);
        Assert.NotNull(result.Volume);
        Assert.Equal(CirNativeEstimatorKind.Adaptive, result.Volume!.Estimator);
        Assert.NotNull(result.Volume.AdaptiveOptions);
        Assert.Equal(options.MaxDepth, result.Volume.AdaptiveOptions!.MaxDepth);
        Assert.True(result.Volume.TotalRegionsVisited > 0);
        Assert.True(result.Volume.TraceEventCount > 0);
        Assert.True(result.Volume.RegionsSubdivided >= 0);
    }

    private static Lowering.FirmamentPrimitiveLoweringPlan CompilePlan(string fixture)
    {
        var compile = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(fixture));
        Assert.True(compile.Compilation.IsSuccess);
        return compile.Compilation.Value.PrimitiveLoweringPlan!;
    }
}
