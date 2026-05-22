using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Analysis;

public enum CirNativeAnalysisInputKind { Firmament, CirTape, CirNode }
public enum CirNativeAnalysisResultKind { Approximate, ClassifierDerived }
public enum CirNativeEstimatorKind { Dense, Adaptive }
public sealed record CirNativeLoweringDiagnostic(int? OpIndex, string? FeatureId, string Message);
public sealed record CirNativeLoweringSummary(bool Supported, int SupportedOpCount, int UnsupportedOpCount, IReadOnlyList<CirNativeLoweringDiagnostic> Diagnostics);
public sealed record CirNativeBounds(Point3D Min, Point3D Max);
public sealed record CirNativePointClassification(Point3D Point, CirPointClassification Classification, double SignedDistance);
public sealed record CirNativeVolumeResult(CirNativeEstimatorKind Estimator, double EstimatedVolume, int? Resolution, CirAdaptiveVolumeOptions? AdaptiveOptions, int? SampledPointCount, int? TotalRegionsVisited, int? RegionsClassifiedInside, int? RegionsClassifiedOutside, int? RegionsSubdivided, int? RegionsSampledDirectly, int? UnknownOrRejectedRegions, int? MaxDepthReached, int TraceEventCount, IReadOnlyList<CirAdaptiveTraceEvent> TraceHead, bool Approximate);
public sealed record CirNativeAnalysisResult(bool Success, string Backend, CirNativeAnalysisInputKind InputKind, CirNativeAnalysisResultKind ResultKind, IReadOnlyList<string> Notes, IReadOnlyList<CirNativeLoweringDiagnostic> Diagnostics, CirNativeLoweringSummary? Lowering, CirNativeBounds? Bounds, CirNativeVolumeResult? Volume, IReadOnlyList<CirNativePointClassification> PointClassifications);

public static class CirNativeAnalysisService
{
    public static CirNativeAnalysisResult AnalyzeTape(CirTape tape, CirBounds bounds, IEnumerable<Point3D>? points = null, int? denseResolution = null, CirAdaptiveVolumeOptions? adaptiveOptions = null)
        => AnalyzeCore(tape, bounds, CirNativeAnalysisInputKind.CirTape, points, denseResolution, adaptiveOptions, null);

    public static CirNativeAnalysisResult AnalyzeNode(CirNode node, IEnumerable<Point3D>? points = null, int? denseResolution = null, CirAdaptiveVolumeOptions? adaptiveOptions = null)
        => AnalyzeCore(CirTapeLowerer.Lower(node), node.Bounds, CirNativeAnalysisInputKind.CirNode, points, denseResolution, adaptiveOptions, null);

    public static CirNativeAnalysisResult AnalyzeFirmamentPlan(FirmamentPrimitiveLoweringPlan plan, IEnumerable<Point3D>? points = null, int? denseResolution = null, CirAdaptiveVolumeOptions? adaptiveOptions = null)
    {
        var lowering = FirmamentCirLowerer.Lower(plan);
        var diagnostics = lowering.Diagnostics.Select(d => new CirNativeLoweringDiagnostic(null, d.Source, d.Message)).ToArray();
        var loweringSummary = new CirNativeLoweringSummary(lowering.IsSuccess, plan.Primitives.Count + plan.Booleans.Count, diagnostics.Length, diagnostics);

        if (!lowering.IsSuccess)
        {
            return new CirNativeAnalysisResult(false, "cir", CirNativeAnalysisInputKind.Firmament, CirNativeAnalysisResultKind.ClassifierDerived,
                ["BRep backend may still support materialized analysis; CIR lowering is unsupported for this model."], diagnostics, loweringSummary, null, null, []);
        }

        return AnalyzeCore(CirTapeLowerer.Lower(lowering.Value.Root), lowering.Value.Root.Bounds, CirNativeAnalysisInputKind.Firmament, points, denseResolution, adaptiveOptions, loweringSummary);
    }

    private static CirNativeAnalysisResult AnalyzeCore(CirTape tape, CirBounds bounds, CirNativeAnalysisInputKind inputKind, IEnumerable<Point3D>? points, int? denseResolution, CirAdaptiveVolumeOptions? adaptiveOptions, CirNativeLoweringSummary? lowering)
    {
        var classifications = (points ?? []).Select(point =>
        {
            var value = tape.Evaluate(point);
            var kind = double.Abs(value) <= 1e-6d ? CirPointClassification.Boundary : (value < 0d ? CirPointClassification.Inside : CirPointClassification.Outside);
            return new CirNativePointClassification(point, kind, value);
        }).ToArray();

        var notes = new List<string>();
        CirNativeAnalysisResultKind resultKind = CirNativeAnalysisResultKind.ClassifierDerived;
        CirNativeVolumeResult? volume = null;

        if (adaptiveOptions is not null)
        {
            var adaptive = CirAdaptiveVolumeEstimator.EstimateVolume(tape, bounds, adaptiveOptions);
            volume = new CirNativeVolumeResult(CirNativeEstimatorKind.Adaptive, adaptive.EstimatedVolume, null, adaptive.Options, null, adaptive.TotalRegionsVisited, adaptive.RegionsClassifiedInside, adaptive.RegionsClassifiedOutside, adaptive.RegionsSubdivided, adaptive.RegionsSampledDirectly, adaptive.UnknownOrRejectedRegions, adaptive.MaxDepthReached, adaptive.TraceEvents.Count, adaptive.TraceEvents, true);
            notes.AddRange(adaptive.Notes);
            resultKind = CirNativeAnalysisResultKind.Approximate;
        }
        else if (denseResolution.HasValue)
        {
            var resolution = int.Max(1, denseResolution.Value);
            var volumeNode = new CirTapeVolumeNode(tape, bounds);
            volume = new CirNativeVolumeResult(CirNativeEstimatorKind.Dense, CirVolumeEstimator.EstimateVolume(volumeNode, resolution), resolution, null, resolution * resolution * resolution, null, null, null, null, null, 0, null, 0, [], true);
            notes.Add("Dense CIR volume estimation uses regular grid center-point sampling and is approximate.");
            resultKind = CirNativeAnalysisResultKind.Approximate;
        }

        return new CirNativeAnalysisResult(true, "cir", inputKind, resultKind, notes, [], lowering, new CirNativeBounds(bounds.Min, bounds.Max), volume, classifications);
    }

    private sealed record CirTapeVolumeNode(CirTape Tape, CirBounds TapeBounds) : CirNode(CirNodeKind.Transform)
    {
        public override CirBounds Bounds => TapeBounds;
        public override double Evaluate(Point3D point) => Tape.Evaluate(point);
    }
}
