using System.Globalization;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.SheetMetal;

/// <summary>
/// One machine-detected bend plus the explicit authority that decides whether source
/// unfolding may consume it. The recovered geometric ID remains immutable underneath
/// an optional engineer/LLM name.
/// </summary>
public sealed record RecognizedBend(
    string SourceBendId,
    string Name,
    RecognizedBendStatus Status,
    SheetBendIr Geometry,
    double Confidence,
    IReadOnlyList<SheetEvidence> Evidence,
    string Authority);

public sealed record RecognizedRegionAdjacency(
    string BendId,
    string RegionA,
    string RegionB);

public sealed record RecognizedSheetMetalModel(
    string StableId,
    BrepBody SourceBody,
    string SourcePath,
    double Thickness,
    IReadOnlyList<SheetRegionIr> Regions,
    IReadOnlyList<RecognizedBend> Bends,
    IReadOnlyList<SheetFeatureIr> Cuts,
    IReadOnlyList<RecognizedRegionAdjacency> RegionAdjacency,
    string RootRegionId,
    SheetMetalRecognitionStatus RecognitionStatus,
    IReadOnlyList<SheetEvidence> RecognitionEvidence,
    IReadOnlyList<SheetMetalDiagnostic> Diagnostics,
    SheetMetalPartIr DetectedPart);

public sealed record SheetMetalBendRecognitionDecision(
    string BendId,
    string? Name,
    RecognizedBendStatus Status);

public sealed record SheetMetalRecognitionPlan(
    string StableId,
    string SourcePath,
    double RecognizedThickness,
    string RootRegionId,
    RecoveredFlatReferenceKind ReferenceKind,
    IReadOnlyList<SheetMetalBendRecognitionDecision> Bends,
    string Authority,
    string DeterministicHash);

public sealed record RecognitionPlanValidation(
    bool IsValid,
    RecognizedSheetMetalModel? Model,
    IReadOnlyList<SheetMetalDiagnostic> Diagnostics);

public sealed record RecoveredFlatSegmentProvenance(
    string FlatSegmentId,
    string SourceRegionId,
    IReadOnlyList<int> SourceFaceIds,
    IReadOnlyList<int> SourceEdgeIds,
    string Derivation);

public sealed record RecoveredFlatReference(
    string StableId,
    RecoveredFlatReferenceKind ReferenceKind,
    string RootRegionId,
    string Frame,
    PlanarContour2? OuterAndInnerContours,
    IReadOnlyList<FlatRegion2D> Regions,
    IReadOnlyList<FlatCutLoop> InnerContours,
    IReadOnlyList<FlatBendLine> BendLines,
    IReadOnlyList<SourceToFlatMapping> RegionMap,
    IReadOnlyList<RecoveredFlatSegmentProvenance> SourceProvenance,
    SheetMetalRecognitionPlan RecognitionPlan,
    FlatPatternBounds? Bounds,
    FlatPatternStatus Status,
    IReadOnlyList<SheetMetalDiagnostic> Diagnostics,
    RecoveredContourAcceptance ContourAcceptance,
    IReadOnlyList<RecoveryJunctionRepair> JunctionRepairs,
    RecoveryStitchSummary? StitchSummary,
    string DeterministicHash,
    TimeSpan GraphTime,
    TimeSpan UnfoldTime,
    TimeSpan StitchTime);

public static class RecognizedSheetMetalRecovery
{
    public static RecognizedSheetMetalModel FromDetection(SheetMetalRecognitionResult detection)
    {
        ArgumentNullException.ThrowIfNull(detection);
        var part = detection.Part ?? throw new ArgumentException("Detection did not produce a Sheet Metal interpretation.", nameof(detection));
        var body = part.FormedBody ?? throw new ArgumentException("Imported recognition must retain its source body.", nameof(detection));
        var bends = part.Bends.OrderBy(x => x.StableId, StringComparer.Ordinal).Select((bend, index) => new RecognizedBend(
            bend.StableId,
            DefaultName(part, bend, index),
            IsGeometricallyStrong(bend, part) ? RecognizedBendStatus.Candidate : RecognizedBendStatus.Ambiguous,
            bend,
            IsGeometricallyStrong(bend, part) ? 1d : .5d,
            bend.Evidence,
            "machine-detected candidate; not yet unfolding authority")).ToArray();
        var sourcePath = part.Regions.Select(x => x.Source.SourcePath).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "BRep";
        var adjacency = bends.Select(x => new RecognizedRegionAdjacency(x.SourceBendId, x.Geometry.AdjacentRegionA, x.Geometry.AdjacentRegionB)).ToArray();
        var id = $"recognized-{SheetMetalRecognizer.StableHash(part.StableId + "|" + string.Join('|', bends.Select(x => x.SourceBendId)))[..16]}";
        return new(id, body, sourcePath, part.Thickness, part.Regions, bends, part.Features, adjacency,
            part.BaseRegionId, part.RecognitionStatus, part.Evidence, detection.Diagnostics, part);
    }

    /// <summary>Deterministically accepts only geometry-supported, unambiguous candidates.</summary>
    public static SheetMetalRecognitionPlan CreateAutomaticPlan(RecognizedSheetMetalModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var decisions = model.Bends.OrderBy(x => x.SourceBendId, StringComparer.Ordinal).Select(x => new SheetMetalBendRecognitionDecision(
            x.SourceBendId, x.Name,
            x.Status == RecognizedBendStatus.Candidate && x.Confidence >= .99d ? RecognizedBendStatus.Recognized : x.Status)).ToArray();
        return CreatePlan(model, decisions, model.RootRegionId, RecoveredFlatReferenceKind.GeometricMidSurface,
            "deterministic validated automatic recognition");
    }

    public static SheetMetalRecognitionPlan CreatePlan(
        RecognizedSheetMetalModel model,
        IReadOnlyList<SheetMetalBendRecognitionDecision> decisions,
        string rootRegionId,
        RecoveredFlatReferenceKind referenceKind,
        string authority)
    {
        var canonical = string.Join('|', model.SourcePath, model.Thickness.ToString("R", CultureInfo.InvariantCulture), rootRegionId,
            referenceKind, authority, string.Join(';', decisions.OrderBy(x => x.BendId, StringComparer.Ordinal).Select(x => $"{x.BendId}:{x.Name}:{x.Status}")));
        var hash = SheetMetalRecognizer.StableHash(canonical);
        return new($"recognition-plan-{hash[..16]}", model.SourcePath, model.Thickness, rootRegionId, referenceKind,
            decisions.OrderBy(x => x.BendId, StringComparer.Ordinal).ToArray(), authority, hash);
    }

    public static RecognitionPlanValidation ValidatePlan(RecognizedSheetMetalModel detected, SheetMetalRecognitionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(detected); ArgumentNullException.ThrowIfNull(plan);
        var diagnostics = new List<SheetMetalDiagnostic>();
        if (Math.Abs(plan.RecognizedThickness - detected.Thickness) > .01d)
            diagnostics.Add(new(SheetMetalDiagnosticCodes.RecognitionAssertionInvalid, SheetMetalDiagnosticSeverity.Error,
                $"Asserted thickness {plan.RecognizedThickness:G12} mm contradicts measured thickness {detected.Thickness:G12} mm."));
        if (!detected.Regions.Any(x => x.StableId == plan.RootRegionId && x.Kind == SheetRegionKind.Planar))
            diagnostics.Add(new(SheetMetalDiagnosticCodes.RecognitionAssertionInvalid, SheetMetalDiagnosticSeverity.Error,
                $"Asserted root '{plan.RootRegionId}' is not a recovered planar region."));
        var decisionIds = new HashSet<string>(StringComparer.Ordinal);
        var accepted = new List<RecognizedBend>();
        foreach (var decision in plan.Bends.OrderBy(x => x.BendId, StringComparer.Ordinal))
        {
            if (!decisionIds.Add(decision.BendId))
            {
                diagnostics.Add(new(SheetMetalDiagnosticCodes.RecognitionAssertionInvalid, SheetMetalDiagnosticSeverity.Error,
                    $"Recognition plan contains duplicate bend decision '{decision.BendId}'."));
                continue;
            }
            var candidate = detected.Bends.FirstOrDefault(x => x.SourceBendId == decision.BendId);
            if (candidate is null)
            {
                diagnostics.Add(new(SheetMetalDiagnosticCodes.RecognitionAssertionInvalid, SheetMetalDiagnosticSeverity.Error,
                    $"Asserted bend '{decision.BendId}' has no machine-detected cylindrical support."));
                continue;
            }
            if (decision.Status == RecognizedBendStatus.Recognized && !IsGeometricallyStrong(candidate.Geometry, detected.DetectedPart))
                diagnostics.Add(new(SheetMetalDiagnosticCodes.RecognitionAssertionInvalid, SheetMetalDiagnosticSeverity.Error,
                    $"Bend '{decision.BendId}' cannot be accepted: cylindrical support, bounded angle, radius, or two-region adjacency is inconsistent.", candidate.Geometry.Source.FaceIds));
            accepted.Add(candidate with
            {
                Name = string.IsNullOrWhiteSpace(decision.Name) ? candidate.Name : decision.Name.Trim(),
                Status = decision.Status,
                Authority = plan.Authority
            });
        }
        foreach (var candidate in detected.Bends.Where(x => !decisionIds.Contains(x.SourceBendId)))
            accepted.Add(candidate);
        var recognized = accepted.Where(x => x.Status == RecognizedBendStatus.Recognized).ToArray();
        if (recognized.Length == 0)
            diagnostics.Add(new(SheetMetalDiagnosticCodes.RecognitionPlanIncomplete, SheetMetalDiagnosticSeverity.Error,
                "Source unfolding requires at least one explicitly recognized bend."));
        ValidateGraph(plan.RootRegionId, recognized, detected.Regions, diagnostics);
        var valid = diagnostics.All(x => x.Severity != SheetMetalDiagnosticSeverity.Error);
        return new(valid, valid ? detected with { RootRegionId = plan.RootRegionId, Bends = accepted.OrderBy(x => x.SourceBendId, StringComparer.Ordinal).ToArray(), Diagnostics = detected.Diagnostics.Concat(diagnostics).ToArray() } : null, diagnostics);
    }

    private static void ValidateGraph(string root, IReadOnlyList<RecognizedBend> bends, IReadOnlyList<SheetRegionIr> regions, ICollection<SheetMetalDiagnostic> diagnostics)
    {
        var planar = regions.Where(x => x.Kind == SheetRegionKind.Planar).Select(x => x.StableId).ToHashSet(StringComparer.Ordinal);
        var reached = new HashSet<string>(StringComparer.Ordinal) { root }; var edges = 0; var queue = new Queue<string>(); queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var bend in bends.Where(x => x.Geometry.AdjacentRegionA == current || x.Geometry.AdjacentRegionB == current))
            {
                var next = bend.Geometry.AdjacentRegionA == current ? bend.Geometry.AdjacentRegionB : bend.Geometry.AdjacentRegionA;
                edges++;
                if (reached.Add(next)) queue.Enqueue(next);
            }
        }
        if (edges / 2 >= reached.Count)
            diagnostics.Add(new(SheetMetalDiagnosticCodes.RegionGraphCycle, SheetMetalDiagnosticSeverity.Error,
                "Recognized bend graph contains a cycle; one unambiguous unfold transform per region cannot be established."));
        var admittedRegions = bends.SelectMany(x => new[] { x.Geometry.AdjacentRegionA, x.Geometry.AdjacentRegionB }).Append(root).ToHashSet(StringComparer.Ordinal);
        var missing = admittedRegions.Where(x => planar.Contains(x) && !reached.Contains(x)).Order(StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
            diagnostics.Add(new(SheetMetalDiagnosticCodes.DisconnectedGraph, SheetMetalDiagnosticSeverity.Error,
                $"Recognized bend graph is disconnected from root '{root}': {string.Join(", ", missing)}."));
    }

    private static bool IsGeometricallyStrong(SheetBendIr bend, SheetMetalPartIr part) =>
        bend.Source.FaceIds.Count >= 2 && bend.InsideRadius >= 0d && bend.Thickness > 0d &&
        double.IsFinite(bend.BendAngleRadians) && bend.BendAngleRadians > 1e-6 && bend.BendAngleRadians < Math.PI * 2d - 1e-6 &&
        bend.AxisDirection.TryNormalize(out _) && bend.AdjacentRegionA != bend.AdjacentRegionB &&
        part.Regions.Any(x => x.StableId == bend.AdjacentRegionA && x.Kind == SheetRegionKind.Planar) &&
        part.Regions.Any(x => x.StableId == bend.AdjacentRegionB && x.Kind == SheetRegionKind.Planar) &&
        part.Regions.Any(x => x.Kind == SheetRegionKind.CylindricalBend && x.Source.FaceIds.SequenceEqual(bend.Source.FaceIds));

    private static string DefaultName(SheetMetalPartIr part, SheetBendIr bend, int index)
    {
        if (bend.AdjacentRegionA == part.BaseRegionId || bend.AdjacentRegionB == part.BaseRegionId)
        {
            var other = bend.AdjacentRegionA == part.BaseRegionId ? bend.AdjacentRegionB : bend.AdjacentRegionA;
            var suffix = other.Split('-').LastOrDefault() ?? (index + 1).ToString("D2", CultureInfo.InvariantCulture);
            return $"BaseFlangeBend{suffix}";
        }
        return $"RecoveredBend{index + 1:D2}";
    }
}
