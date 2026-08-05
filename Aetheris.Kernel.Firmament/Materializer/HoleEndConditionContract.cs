using Aetheris.Kernel.Core.Air;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Immutable, plan-owned observation of material along a construction-plane Hole
/// axis.  It deliberately says nothing about the feature the author requested:
/// traversal describes host reality; <see cref="HoleEndConditionContract"/>
/// compares that reality with the authored contract.
/// </summary>
public enum HoleHostTraversalClassification
{
    NoMaterial,
    OneContiguousInterval,
    MultipleContiguousPartitionsOfOneMaterialSpan,
    DisconnectedMaterialIntervals,
    TangentialContact,
    AmbiguousTransition
}

public sealed record HoleHostMaterialIntervalEvidence(
    double Start,
    double End,
    string RegionId,
    string SourceProvenance,
    bool FullCircularFootprintSupported,
    string EntryBoundary,
    string ExitBoundary)
{
    public double Length => End - Start;
}

public sealed record HoleHostTraversalEvidence(
    string HoleId,
    string HostId,
    string ConstructionPlaneId,
    double[] WorldMouthCenter,
    double[] Axis,
    double Radius,
    HoleHostTraversalClassification Classification,
    IReadOnlyList<HoleHostMaterialIntervalEvidence> OrderedIntervals,
    IReadOnlyList<string> Diagnostics)
{
    public (double Start, double End)? PhysicalMaterialSpan => Classification is HoleHostTraversalClassification.OneContiguousInterval or HoleHostTraversalClassification.MultipleContiguousPartitionsOfOneMaterialSpan
        && OrderedIntervals.Count > 0
        ? (OrderedIntervals.Min(x => x.Start), OrderedIntervals.Max(x => x.End))
        : null;
}

public sealed record HoleEndConditionContractEvidence(
    string DeclaredEndCondition,
    string DeclaredTermination,
    double? ShaftDepth,
    double? TipLength,
    double? TotalDepth,
    double? RemainingWall,
    bool IsThroughAll,
    bool IsBlind,
    bool MouthInsideMaterial,
    bool TipInsideMaterial,
    bool HasExit,
    bool HasDrillPoint,
    bool ContractSatisfied,
    IReadOnlyList<string> Diagnostics);

/// <summary>Strict authored Hole end-condition checks shared by plan creation and inspection.</summary>
internal static class HoleEndConditionContract
{
    private const double Tolerance = 1e-9;

    public static HoleEndConditionContractEvidence Evaluate(AirHoleFeature feature, HoleHostTraversalEvidence traversal)
    {
        var diagnostics = new List<string>();
        var span = traversal.PhysicalMaterialSpan;
        var mouthInside = span is { } physical && physical.Start <= Tolerance && physical.End > Tolerance && traversal.OrderedIntervals.First().FullCircularFootprintSupported;
        if (!mouthInside) diagnostics.Add($"HoleMouthDoesNotEnterMaterial: feature={feature.FeatureId}; traversal={traversal.Classification}; intervals={Format(traversal)}.");
        if (traversal.Diagnostics.Any(x => x.StartsWith("MouthInsideHostUnexpectedly", StringComparison.Ordinal)))
        {
            mouthInside = false;
            diagnostics.Add($"MouthInsideHostUnexpectedly: feature={feature.FeatureId}; declared construction-plane mouth is not an entering material boundary.");
        }
        if (traversal.Diagnostics.Any(x => x.StartsWith("MouthMissesHost", StringComparison.Ordinal)))
            diagnostics.Add($"MouthMissesHost: feature={feature.FeatureId}; declared construction-plane mouth does not meet host material.");
        if (traversal.Diagnostics.Any(x => x.StartsWith("DirectionDoesNotEnterMaterial", StringComparison.Ordinal)))
            diagnostics.Add($"DirectionDoesNotEnterMaterial: feature={feature.FeatureId}; local +Z does not enter host material at the declared mouth.");
        if (traversal.Classification == HoleHostTraversalClassification.DisconnectedMaterialIntervals)
            diagnostics.Add($"HoleHostTraversalDisconnected: feature={feature.FeatureId}; intervals={Format(traversal)}.");
        if (traversal.Classification is HoleHostTraversalClassification.AmbiguousTransition or HoleHostTraversalClassification.TangentialContact)
            diagnostics.Add($"HoleHostTraversalAmbiguous: feature={feature.FeatureId}; traversal={traversal.Classification}; intervals={Format(traversal)}.");
        if (traversal.OrderedIntervals.Any(x => !x.FullCircularFootprintSupported))
            diagnostics.Add($"HoleFootprintLeavesHost: feature={feature.FeatureId}; radius={feature.Shaft.Radius:R}; intervals={Format(traversal)}.");

        var through = feature.EndCondition is AirHoleEndCondition.ThroughAll;
        var drill = feature.Termination is AirHoleTermination.DrillPoint point ? point : null;
        double? shaft = null;
        double? tip = null;
        double? total = null;
        if (drill is not null)
        {
            tip = feature.Shaft.Radius / Math.Tan(drill.PointAngleDegrees * Math.PI / 360d);
            switch (feature.EndCondition)
            {
                case AirHoleEndCondition.ShaftDepth value: shaft = value.Value; total = shaft + tip; break;
                case AirHoleEndCondition.TotalDepth value: total = value.Value; shaft = total - tip; break;
                default: diagnostics.Add($"HoleBlindDepthMissing: feature={feature.FeatureId}; DrillPoint requires ShaftDepth or TotalDepth."); break;
            }
            if (shaft < -Tolerance || total <= Tolerance) diagnostics.Add($"HoleBlindDepthInvalid: feature={feature.FeatureId}; shaftDepth={shaft:R}; totalDepth={total:R}.");
        }

        double? wall = span is { } s && total is { } requested ? s.End - requested : null;
        var tipInside = total is null || span is { } materialSpan && total < materialSpan.End - Tolerance;
        if (drill is not null && !tipInside && span is { } breachSpan && total is { } requestedTotal)
            diagnostics.Add($"BlindHoleBreakthrough: feature={feature.FeatureId}; declaredEndCondition={feature.EndCondition.Kind}; totalDepth={requestedTotal:R}; hostInterval=[{breachSpan.Start:R},{breachSpan.End:R}]; violation={requestedTotal - breachSpan.End:R}.");

        // A plan has not yet been created, so HasExit is a demanded postcondition
        // for ThroughAll rather than an observation of materialized topology.
        var satisfied = diagnostics.Count == 0;
        return new(feature.EndCondition.Kind.ToString(), drill is null ? "FlatBottom" : "DrillPoint", shaft, tip, total, wall,
            through, !through, mouthInside, tipInside, through, drill is not null, satisfied, diagnostics);
    }

    private static string Format(HoleHostTraversalEvidence traversal) => string.Join(",", traversal.OrderedIntervals.Select(x => $"[{x.Start:R},{x.End:R}]/{x.RegionId}"));
}
