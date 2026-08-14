using System.Globalization;
using System.Text;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.SheetMetal;

public sealed record SheetNominalCandidate(
    string SubjectId, string Quantity, double Measured, double ProposedNominal,
    double Delta, double Tolerance, string Unit, SheetMetalIntentConfidence Confidence,
    string Evidence, bool Accepted = false);

public sealed record SheetGroupingCandidate(
    string StableId, string Kind, IReadOnlyList<string> Members,
    SheetMetalIntentConfidence Confidence, string Evidence);

public sealed record SheetCornerEvidence(
    string StableId, SheetCornerKind Kind, IReadOnlyList<string> AdjacentRegions,
    SheetMetalIntentConfidence Confidence, string Evidence);

public sealed record SheetReliefEvidence(
    string StableId, SheetReliefKind Kind, string OwningRegionId, double? Width,
    double? Depth, SheetMetalIntentConfidence Confidence, string Evidence);

public sealed record RecoveredSheetMetalEvidence(
    SheetMetalPartIr Part,
    SheetThicknessRecognition Thickness,
    IReadOnlyList<SheetNominalCandidate> NominalCandidates,
    IReadOnlyList<SheetGroupingCandidate> GroupingCandidates,
    IReadOnlyList<SheetCornerEvidence> Corners,
    IReadOnlyList<SheetReliefEvidence> Reliefs,
    IReadOnlyList<string> Ambiguities,
    string DeterministicId);

public sealed record RecoveredFirmamentDraft(
    string Source,
    SheetMetalProvenanceCategory Provenance,
    IReadOnlyDictionary<string, IReadOnlyList<int>> SourceFaceBindings);

public sealed record SheetMetalIntentRecoveryResult(
    RecoveredSheetMetalEvidence Evidence,
    RecoveredFirmamentDraft Draft,
    string ReconstructionBrief);

public sealed record SheetMetalNominalizationPolicy(
    double LinearTolerance = 0.01d,
    double AngularToleranceDegrees = 0.01d,
    IReadOnlyList<double>? MetricSeries = null,
    IReadOnlyList<double>? InchFractions = null)
{
    public IReadOnlyList<double> EffectiveMetricSeries => MetricSeries ??
        [0.5, 0.8, 1, 1.2, 1.5, 1.6, 1.9, 2, 2.5, 3, 4, 5, 6, 6.35, 8, 10, 12, 12.7, 15, 19.05, 25, 25.4, 31.75, 38.1, 44.45, 50.8, 57.15, 63.5, 76.2, 88.9, 101.6, 114.3, 127, 152.4, 177.8, 190.5, 203.2, 228.6, 241.3, 254, 304.8, 365.125, 381, 457.2, 482.6];
    public IReadOnlyList<double> EffectiveInchFractions => InchFractions ??
        Enumerable.Range(1, 64).Select(i => i * 25.4 / 64d)
            .Concat(Enumerable.Range(1, 100).Select(i => i * 0.005d * 25.4d)).Distinct().ToArray();
}

public static class SheetMetalIntentRecovery
{
    public static SheetMetalIntentRecoveryResult Recover(SheetMetalRecognitionResult recognition, SheetMetalNominalizationPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(recognition);
        if (recognition.Part is null) throw new ArgumentException("Recognition did not produce a Sheet Metal part.", nameof(recognition));
        policy ??= new(); var part = recognition.Part;
        var nominals = Nominals(part, policy);
        var groupings = Groupings(part, policy.LinearTolerance);
        var ambiguities = new List<string>
        {
            "Recovered region boundaries and bend adjacency are geometric facts; authored flange/feature history is not recoverable from STEP alone.",
            "K-factor 0.5 is a flattening policy assumption unless source manufacturing metadata says otherwise.",
            "CTC-03 cut loops may be one repeated operation, but STEP contains no authoritative feature-history grouping."
        };
        if (part.RecognitionStatus != SheetMetalRecognitionStatus.Complete)
            ambiguities.Add($"Machine recovery status is {part.RecognitionStatus}; unsupported boundary faces remain forensic evidence.");
        var corners = InferCorners(part);
        var reliefs = InferReliefs(part);
        var id = SheetMetalRecognizer.StableHash(string.Join('|', part.StableId, part.Thickness.ToString("R", CultureInfo.InvariantCulture), string.Join(',', nominals.Select(n => $"{n.SubjectId}:{n.ProposedNominal:R}"))));
        var evidence = new RecoveredSheetMetalEvidence(part, recognition.Thickness, nominals, groupings, corners, reliefs, ambiguities, id);
        var draftText = SheetMetalManufacturingArtifacts.WriteRecoveredFirmament(part, part.FormedBody is null ? "source.step" : part.Regions.First().Source.SourcePath ?? "source.step");
        var bindings = part.Regions.Cast<object>().Concat(part.Bends).Concat(part.Features).ToDictionary(
            x => x switch { SheetRegionIr r => r.StableId, SheetBendIr b => b.StableId, SheetFeatureIr f => f.StableId, _ => "unknown" },
            x => x switch { SheetRegionIr r => r.Source.FaceIds, SheetBendIr b => b.Source.FaceIds, SheetFeatureIr f => f.Source.FaceIds, _ => [] }, StringComparer.Ordinal);
        var draft = new RecoveredFirmamentDraft(draftText, SheetMetalProvenanceCategory.Recovered, bindings);
        return new(evidence, draft, WriteBrief(evidence));
    }

    public static IReadOnlyList<SheetNominalCandidate> Nominals(SheetMetalPartIr part, SheetMetalNominalizationPolicy policy)
    {
        var result = new List<SheetNominalCandidate>();
        Add("part", "Thickness", part.Thickness, policy.LinearTolerance, "mm", false);
        foreach (var bend in part.Bends)
        {
            Add(bend.StableId, "BendAngle", bend.BendAngleRadians * 180 / Math.PI, policy.AngularToleranceDegrees, "deg", true);
            Add(bend.StableId, "InsideRadius", bend.InsideRadius, policy.LinearTolerance, "mm", false);
        }
        foreach (var feature in part.Features.Where(f => f.Diameter.HasValue)) Add(feature.StableId, "Diameter", feature.Diameter!.Value, policy.LinearTolerance, "mm", false);
        return result.OrderBy(x => x.SubjectId, StringComparer.Ordinal).ThenBy(x => x.Quantity, StringComparer.Ordinal).ToArray();

        void Add(string id, string quantity, double measured, double tolerance, string unit, bool angular)
        {
            var candidates = angular ? new[] { 15d, 30d, 45d, 60d, 90d, 120d, 135d, 180d } : policy.EffectiveMetricSeries.Concat(policy.EffectiveInchFractions).Distinct();
            var nominal = candidates.OrderBy(v => Math.Abs(v - measured)).First(); var delta = nominal - measured;
            if (Math.Abs(delta) > tolerance) return;
            var inch = !angular && policy.EffectiveInchFractions.Any(v => Math.Abs(v - nominal) < 1e-10);
            result.Add(new(id, quantity, measured, nominal, delta, tolerance, unit, SheetMetalIntentConfidence.StrongCandidate,
                angular ? "Canonical bend angle within bounded angular tolerance." : inch ? "Common fractional/decimal-inch value converted to millimetres within tolerance." : "Common metric/manufacturing value within bounded linear tolerance."));
        }
    }

    private static IReadOnlyList<SheetGroupingCandidate> Groupings(SheetMetalPartIr part, double tolerance)
    {
        var result = new List<SheetGroupingCandidate>();
        foreach (var group in part.Bends.GroupBy(b => (A: Math.Round(b.BendAngleRadians * 180 / Math.PI, 3), R: Math.Round(b.InsideRadius, 3))).Where(g => g.Count() > 1))
            result.Add(new($"repeated-bends-{result.Count + 1}", "RepeatedBendPolicy", group.Select(x => x.StableId).Order(StringComparer.Ordinal).ToArray(), SheetMetalIntentConfidence.StrongCandidate, $"{group.Count()} bends repeat angle {group.Key.A:G6} deg and inside radius {group.Key.R:G6} mm."));
        foreach (var group in part.Features.GroupBy(f => (f.Kind, Size: Math.Round(f.Diameter ?? Extent(f.Boundary3D), 3))).Where(g => g.Count() > 1))
            result.Add(new($"repeated-cuts-{result.Count + 1}", "RepeatedCut", group.Select(x => x.StableId).Order(StringComparer.Ordinal).ToArray(), SheetMetalIntentConfidence.StrongCandidate, $"{group.Count()} {group.Key.Kind} cuts share size within {tolerance:G4} mm."));
        return result;
        static double Extent(IReadOnlyList<Point3D> p) => p.Count == 0 ? 0 : Math.Max(p.Max(x => x.X) - p.Min(x => x.X), Math.Max(p.Max(x => x.Y) - p.Min(x => x.Y), p.Max(x => x.Z) - p.Min(x => x.Z)));
    }

    private static IReadOnlyList<SheetCornerEvidence> InferCorners(SheetMetalPartIr part) => part.Bends
        .SelectMany(a => part.Bends.Where(b => string.CompareOrdinal(a.StableId, b.StableId) < 0 &&
            (a.AdjacentRegionA == b.AdjacentRegionA || a.AdjacentRegionA == b.AdjacentRegionB || a.AdjacentRegionB == b.AdjacentRegionA || a.AdjacentRegionB == b.AdjacentRegionB))
            .Select(b => new SheetCornerEvidence($"corner-{a.StableId}-{b.StableId}", SheetCornerKind.Unknown,
                new[] { a.AdjacentRegionA, a.AdjacentRegionB, b.AdjacentRegionA, b.AdjacentRegionB }.Distinct().Order(StringComparer.Ordinal).ToArray(),
                SheetMetalIntentConfidence.Ambiguous, "Two bends meet a recovered planar region; boundary trimming is insufficient to assert a corner family.")))
        .OrderBy(x => x.StableId, StringComparer.Ordinal).ToArray();

    private static IReadOnlyList<SheetReliefEvidence> InferReliefs(SheetMetalPartIr part) => part.Features
        .Where(f => f.Kind is SheetFeatureKind.Slot or SheetFeatureKind.ProfileHole)
        .Where(f => part.Bends.Any(b => (b.AdjacentRegionA == f.OwningRegionId || b.AdjacentRegionB == f.OwningRegionId) &&
            ((f.Center-b.AxisOrigin).Cross(b.AxisDirection).Length <= 4*part.Thickness)))
        .Select(f => new SheetReliefEvidence($"relief-candidate-{f.StableId}", SheetReliefKind.Rectangular, f.OwningRegionId, null, null,
            SheetMetalIntentConfidence.WeakCandidate, "Profile cut lies on a bend-adjacent region; classification remains a suggestion, not authored authority."))
        .ToArray();

    public static string WriteBrief(RecoveredSheetMetalEvidence evidence)
    {
        var p = evidence.Part; var b = new StringBuilder();
        b.AppendLine($"Part: {p.StableId}").AppendLine($"  Recovery status: {p.RecognitionStatus}")
            .AppendLine($"  Constant thickness: {p.Thickness:G12} mm ± {evidence.Thickness.Tolerance:G6}")
            .AppendLine($"  Regions: {p.Regions.Count} ({p.Regions.Count(r => r.Kind == SheetRegionKind.Planar)} planar, {p.Regions.Count(r => r.Kind == SheetRegionKind.CylindricalBend)} bends)")
            .AppendLine($"  Bends: {p.Bends.Count}").AppendLine($"  Cuts: {p.Features.Count}")
            .AppendLine("Strong nominal candidates:");
        foreach (var n in evidence.NominalCandidates.Where(n => n.Confidence == SheetMetalIntentConfidence.StrongCandidate))
            b.AppendLine($"  {n.SubjectId}.{n.Quantity}: {n.Measured:G12}{n.Unit} -> {n.ProposedNominal:G12}{n.Unit} (delta {n.Delta:G6}, {n.Evidence})");
        b.AppendLine("Likely structural groupings:"); foreach (var g in evidence.GroupingCandidates) b.AppendLine($"  {g.Kind}: [{string.Join(", ", g.Members)}] — {g.Evidence}");
        b.AppendLine("Ambiguities:"); foreach (var a in evidence.Ambiguities) b.AppendLine($"  - {a}");
        b.AppendLine("Verification targets:").AppendLine("  thickness; bidirectional formed boundaries; bend axes/angles/radii/adjacency; flat outline/cuts/bend lines; topology and DFM");
        return b.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}

public enum SheetMetalComparisonStatus { Pass, PassWithKnownDifferences, NeedsReview, Fail }

public sealed record SheetMetalReconstructionPolicy(
    double PositionTolerance = 0.05d,
    double BendAxisTolerance = 0.05d,
    double BendAngleToleranceDegrees = 0.05d,
    double BendRadiusTolerance = 0.05d,
    double FlatPatternTolerance = 0.1d,
    double FeatureTolerance = 0.1d);

public sealed record SheetResidualStatistics(double Rms, double P95, double Maximum, int SampleCount);
public sealed record SheetBendComparison(string SourceBendId, string IntentBendId, double AxisResidual, double AxisAngleResidualDegrees, double BendAngleResidualDegrees, double RadiusResidual, bool AdjacencyMatches, SheetMetalComparisonStatus Status);
public sealed record SheetFeatureComparison(string SourceFeatureId, string IntentFeatureId, double CenterResidual, double SizeResidual, SheetMetalComparisonStatus Status);
public sealed record SheetFlatComparison(double WidthResidual, double HeightResidual, SheetResidualStatistics Contour, IReadOnlyList<SheetFeatureComparison> Cuts, int BendLineCountDelta, bool HasOverlap, SheetMetalComparisonStatus Status);
public sealed record SheetMetalIntentComparisonReport(SheetMetalComparisonStatus Status, double ThicknessResidual, SheetResidualStatistics SourceToIntent, SheetResidualStatistics IntentToSource, IReadOnlyList<SheetBendComparison> Bends, IReadOnlyList<SheetFeatureComparison> Features, SheetFlatComparison FlatPattern, IReadOnlyList<string> KnownDifferences, IReadOnlyList<string> Diagnostics);

public static class SheetMetalIntentComparer
{
    public static SheetMetalIntentComparisonReport Compare(SheetMetalPartIr source, SheetMetalPartIr intent, SheetMetalReconstructionPolicy? policy = null, IReadOnlyList<string>? knownDifferences = null)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(intent); policy ??= new(); knownDifferences ??= [];
        var acceptedDifferences=knownDifferences.ToList();
        var sourcePoints = source.Regions.SelectMany(r => r.Boundary3D).ToArray(); var intentPoints = intent.Regions.SelectMany(r => r.Boundary3D).ToArray();
        var s2i = Stats(sourcePoints.Select(p => Nearest(p, intentPoints))); var i2s = Stats(intentPoints.Select(p => Nearest(p, sourcePoints)));
        var remaining = intent.Bends.ToList(); var bends = new List<SheetBendComparison>();
        foreach (var bend in source.Bends.OrderBy(b => b.StableId, StringComparer.Ordinal))
        {
            var match = remaining.OrderBy(x => AxisDistance(bend, x) + Math.Abs(bend.BendAngleRadians - x.BendAngleRadians) * 10 + Math.Abs(bend.InsideRadius - x.InsideRadius)).FirstOrDefault();
            if (match is null) continue; remaining.Remove(match);
            var axis = AxisDistance(bend, match); var axisAngle = AxisAngle(bend.AxisDirection, match.AxisDirection); var angle = Math.Abs(bend.BendAngleRadians - match.BendAngleRadians) * 180 / Math.PI; var radius = Math.Abs(bend.InsideRadius - match.InsideRadius);
            var adjacency = SameAdjacency(bend, match); var bendStatus = axis <= policy.BendAxisTolerance && axisAngle <= policy.BendAngleToleranceDegrees && angle <= policy.BendAngleToleranceDegrees && radius <= policy.BendRadiusTolerance && adjacency ? SheetMetalComparisonStatus.Pass : SheetMetalComparisonStatus.Fail;
            bends.Add(new(bend.StableId, match.StableId, axis, axisAngle, angle, radius, adjacency, bendStatus));
        }
        var features = CompareFeatures(source.Features, intent.Features, policy.FeatureTolerance);
        var sf = SheetMetalFlattener.Flatten(source); var inf = SheetMetalFlattener.Flatten(intent); var flat = CompareFlat(sf, inf, policy.FlatPatternTolerance, features);
        var thickness = Math.Abs(source.Thickness - intent.Thickness);
        if(thickness>1e-9&&intent.Evidence.Any(e=>e.Predicate=="accepted-thickness-nominal"))acceptedDifferences.Add($"Accepted thickness nominalization: source {source.Thickness:G12} mm -> intent {intent.Thickness:G12} mm (delta {thickness:G6} mm).");
        var unexplained = thickness > policy.PositionTolerance || s2i.P95 > policy.PositionTolerance || i2s.P95 > policy.PositionTolerance || bends.Any(b => b.Status == SheetMetalComparisonStatus.Fail) || features.Any(f => f.Status == SheetMetalComparisonStatus.Fail) || flat.Status == SheetMetalComparisonStatus.Fail;
        var status = unexplained ? SheetMetalComparisonStatus.Fail : acceptedDifferences.Count > 0 ? SheetMetalComparisonStatus.PassWithKnownDifferences : SheetMetalComparisonStatus.Pass;
        var diagnostics = new List<string>(); if (remaining.Count > 0 || bends.Count != source.Bends.Count) diagnostics.Add($"Bend count mismatch: source {source.Bends.Count}, intent {intent.Bends.Count}."); if (source.Features.Count != intent.Features.Count) diagnostics.Add($"Feature count mismatch: source {source.Features.Count}, intent {intent.Features.Count}.");
        return new(status, thickness, s2i, i2s, bends, features, flat, acceptedDifferences, diagnostics);
    }

    private static IReadOnlyList<SheetFeatureComparison> CompareFeatures(IReadOnlyList<SheetFeatureIr> source, IReadOnlyList<SheetFeatureIr> intent, double tolerance)
    {
        var remaining = intent.ToList(); var result = new List<SheetFeatureComparison>();
        foreach (var feature in source.OrderBy(f => f.StableId, StringComparer.Ordinal))
        {
            var match = remaining.Where(f => f.Kind == feature.Kind).OrderBy(f => (f.Center - feature.Center).Length).FirstOrDefault() ?? remaining.OrderBy(f => (f.Center - feature.Center).Length).FirstOrDefault();
            if (match is null) continue; remaining.Remove(match); var center = (feature.Center - match.Center).Length; var size = Math.Abs((feature.Diameter ?? Extent(feature.Boundary3D)) - (match.Diameter ?? Extent(match.Boundary3D)));
            result.Add(new(feature.StableId, match.StableId, center, size, center <= tolerance && size <= tolerance ? SheetMetalComparisonStatus.Pass : SheetMetalComparisonStatus.Fail));
        }
        return result;
        static double Extent(IReadOnlyList<Point3D> p) => p.Count == 0 ? 0 : new[] { p.Max(x => x.X)-p.Min(x => x.X), p.Max(x => x.Y)-p.Min(x => x.Y), p.Max(x => x.Z)-p.Min(x => x.Z) }.Max();
    }

    private static SheetFlatComparison CompareFlat(SheetMetalFlatPatternIr a, SheetMetalFlatPatternIr b, double tolerance, IReadOnlyList<SheetFeatureComparison> cuts)
    {
        var width = Math.Abs((a.Bounds?.Width ?? 0) - (b.Bounds?.Width ?? 0)); var height = Math.Abs((a.Bounds?.Height ?? 0) - (b.Bounds?.Height ?? 0));
        var contour = Stats(a.Boundary.Select(p => Nearest(p, b.Boundary))); var overlaps = b.Status == FlatPatternStatus.Overlapping;
        var status = width <= tolerance && height <= tolerance && contour.P95 <= tolerance && cuts.All(c => c.Status == SheetMetalComparisonStatus.Pass) && a.BendLines.Count == b.BendLines.Count && !overlaps ? SheetMetalComparisonStatus.Pass : SheetMetalComparisonStatus.Fail;
        return new(width, height, contour, cuts, b.BendLines.Count - a.BendLines.Count, overlaps, status);
    }

    private static SheetResidualStatistics Stats(IEnumerable<double> values) { var v = values.Where(double.IsFinite).Order().ToArray(); if (v.Length == 0) return new(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, 0); return new(Math.Sqrt(v.Average(x => x*x)), v[(int)Math.Ceiling(.95 * v.Length) - 1], v[^1], v.Length); }
    private static double Nearest(Point3D p, IReadOnlyList<Point3D> q) => q.Count == 0 ? double.PositiveInfinity : q.Min(x => (p-x).Length);
    private static double Nearest(SheetPoint2 p, IReadOnlyList<SheetPoint2> q) => q.Count == 0 ? double.PositiveInfinity : q.Min(x => Math.Sqrt((p.X-x.X)*(p.X-x.X)+(p.Y-x.Y)*(p.Y-x.Y)));
    private static double AxisDistance(SheetBendIr a, SheetBendIr b) { var u = Normalize(a.AxisDirection); var v = Normalize(b.AxisDirection); var cross = u.Cross(v); if (cross.TryNormalize(out var n)) return Math.Abs((b.AxisOrigin-a.AxisOrigin).Dot(n)); return ((b.AxisOrigin-a.AxisOrigin).Cross(u)).Length; }
    private static double AxisAngle(Vector3D a, Vector3D b) => Math.Acos(Math.Clamp(Math.Abs(Normalize(a).Dot(Normalize(b))), -1, 1)) * 180 / Math.PI;
    private static Vector3D Normalize(Vector3D v) => v.TryNormalize(out var n) ? n : v;
    private static bool SameAdjacency(SheetBendIr a, SheetBendIr b) => (a.AdjacentRegionA == b.AdjacentRegionA && a.AdjacentRegionB == b.AdjacentRegionB) || (a.AdjacentRegionA == b.AdjacentRegionB && a.AdjacentRegionB == b.AdjacentRegionA) || (a.Source.FaceIds.Count > 0 && a.Source.FaceIds.SequenceEqual(b.Source.FaceIds));
}
