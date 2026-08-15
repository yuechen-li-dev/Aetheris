using System.Diagnostics;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.SheetMetal;

/// <summary>
/// Unfolds the imported body's geometric mid-surface from an explicitly validated bend
/// set. This is a forensic recovery reference, not an authored manufacturing flat.
/// </summary>
public static class RecoveredSourceFlattener
{
    private const double Tolerance = 1e-7;

    public static RecoveredFlatReference Flatten(RecognizedSheetMetalModel detected, SheetMetalRecognitionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(detected); ArgumentNullException.ThrowIfNull(plan);
        var graphClock = Stopwatch.StartNew();
        var validation = RecognizedSheetMetalRecovery.ValidatePlan(detected, plan);
        graphClock.Stop();
        if (!validation.IsValid || validation.Model is null)
            return Failed(detected, plan, validation.Diagnostics, graphClock.Elapsed);
        var model = validation.Model;
        var accepted = model.Bends.Where(x => x.Status == RecognizedBendStatus.Recognized).Select(x => x.Geometry).ToArray();
        var referencePolicy = plan.ReferenceKind switch
        {
            RecoveredFlatReferenceKind.GeometricMidSurface => new SheetMetalFlattenPolicy(.5d),
            _ => model.DetectedPart.FlatPatternPolicy
        };
        var part = model.DetectedPart with
        {
            BaseRegionId = plan.RootRegionId,
            Bends = accepted,
            FlatPatternPolicy = referencePolicy,
            RecognitionStatus = SheetMetalRecognitionStatus.Complete
        };

        var unfoldClock = Stopwatch.StartNew();
        var geometric = SheetMetalFlattener.Flatten(part, referencePolicy);
        unfoldClock.Stop();
        var diagnostics = validation.Diagnostics.Concat(geometric.Diagnostics).ToList();
        var mappings = geometric.SourceToFlatMappings.ToDictionary(x => x.SourceRegionId, StringComparer.Ordinal);
        var exactRegions = new List<FlatRegion2D>();
        var provenance = new List<RecoveredFlatSegmentProvenance>();
        foreach (var region in geometric.Regions2D.OrderBy(x => x.StableId, StringComparer.Ordinal))
        {
            if (region.Kind != SheetRegionKind.Planar || !mappings.TryGetValue(region.SourceRegionId, out var mapping))
            {
                exactRegions.Add(region);
                continue;
            }
            var source = model.Regions.Single(x => x.StableId == region.SourceRegionId);
            var exact = ExtractRegionOuter(model.SourceBody, source, mapping, diagnostics, provenance);
            exactRegions.Add(region with
            {
                ExactContour = exact ?? region.ExactContour,
                Boundary = exact is null ? region.Boundary : Vertices(exact),
                MappingKind = exact is null ? region.MappingKind : "exact source BRep line/arc loop through geometric mid-surface unfold transform"
            });
        }

        var exactCuts = geometric.CutLoops.Select(cut =>
        {
            if (!mappings.TryGetValue(cut.SourceRegionId, out var mapping)) return cut;
            var feature = model.Cuts.First(x => x.StableId == cut.FeatureId);
            var exact = ExtractFeature(model.SourceBody, feature, mapping, diagnostics, provenance);
            return exact is null ? cut : cut with { ExactContour = exact, Boundary = Vertices(exact) };
        }).ToArray();

        var stitchClock = Stopwatch.StartNew();
        var stitched = Stitch(model, exactRegions, exactCuts, diagnostics, out var recoveryStitch);
        stitchClock.Stop();
        var allPoints = exactRegions.SelectMany(x => x.Boundary).Concat(exactCuts.SelectMany(x => x.Boundary)).ToArray();
        var bounds = allPoints.Length == 0 ? null : new FlatPatternBounds(allPoints.Min(x => x.X), allPoints.Min(x => x.Y), allPoints.Max(x => x.X), allPoints.Max(x => x.Y));
        var status = stitched is null || geometric.Status is FlatPatternStatus.Unsupported or FlatPatternStatus.Overlapping
            ? geometric.Status == FlatPatternStatus.Overlapping ? FlatPatternStatus.Overlapping : FlatPatternStatus.Partial
            : FlatPatternStatus.Valid;
        var hashBasis = string.Join('|', plan.DeterministicHash, status, stitched?.Provenance ?? "no-stitched-contour",
            string.Join(';', exactRegions.Select(x => $"{x.StableId}:{ContourSignature(x.ExactContour)}")),
            string.Join(';', exactCuts.Select(x => $"{x.FeatureId}:{ContourSignature(x.ExactContour)}")),
            geometric.DeterministicHash);
        var hash = SheetMetalRecognizer.StableHash(hashBasis);
        return new($"recovered-flat-{hash[..16]}", plan.ReferenceKind, plan.RootRegionId, "XY", stitched,
            exactRegions, exactCuts, geometric.BendLines, geometric.SourceToFlatMappings, provenance.OrderBy(x => x.FlatSegmentId, StringComparer.Ordinal).ToArray(),
            plan, bounds, status, diagnostics, recoveryStitch.Acceptance, recoveryStitch.Repairs, recoveryStitch.Summary,
            hash, graphClock.Elapsed, unfoldClock.Elapsed, stitchClock.Elapsed);
    }

    public static SheetMetalFlatPatternIr ToFlatPattern(RecoveredFlatReference reference) => new(
        reference.StableId, reference.Status, reference.Regions, reference.BendLines, reference.InnerContours,
        reference.RegionMap, reference.OuterAndInnerContours is null ? reference.Regions.SelectMany(x => x.Boundary).ToArray() : Vertices(reference.OuterAndInnerContours),
        reference.Bounds,
        reference.ReferenceKind == RecoveredFlatReferenceKind.GeometricMidSurface ? new(.5d) : new(reference.BendLines.FirstOrDefault()?.KFactor ?? .5d),
        [], reference.Diagnostics, reference.DeterministicHash, reference.OuterAndInnerContours);

    private static PlanarContour2? ExtractRegionOuter(BrepBody body, SheetRegionIr region, SourceToFlatMapping mapping,
        ICollection<SheetMetalDiagnostic> diagnostics, ICollection<RecoveredFlatSegmentProvenance> provenance)
    {
        foreach (var faceId in region.Source.FaceIds.Order())
        {
            var face = body.Topology.GetFace(new(faceId));
            var loopId = face.LoopIds.FirstOrDefault();
            if (loopId == default) continue;
            var loop = ExtractLoop(body, loopId, region.StableId, region.Source.FaceIds, mapping, true, diagnostics, provenance);
            if (loop is not null)
                return new($"source-{region.StableId}", "XY", loop, [], $"Imported STEP face {faceId} exact outer loop unfolded from geometric mid-surface");
        }
        diagnostics.Add(new(SheetMetalDiagnosticCodes.SourceContourUnsupported, SheetMetalDiagnosticSeverity.Warning,
            $"No exact source outer loop could be extracted for '{region.StableId}'.", region.Source.FaceIds));
        return null;
    }

    private static PlanarContour2? ExtractFeature(BrepBody body, SheetFeatureIr feature, SourceToFlatMapping mapping,
        ICollection<SheetMetalDiagnostic> diagnostics, ICollection<RecoveredFlatSegmentProvenance> provenance)
    {
        var wanted = feature.Source.EdgeIds.ToHashSet();
        foreach (var faceId in feature.Source.FaceIds.Order())
        {
            var face = body.Topology.GetFace(new(faceId));
            foreach (var loopId in face.LoopIds.Skip(1))
            {
                var edges = body.GetCoedgeIds(loopId).Select(body.GetCoedgeEdgeId).Select(x => x.Value).ToArray();
                if (!edges.Any(wanted.Contains)) continue;
                var loop = ExtractLoop(body, loopId, feature.OwningRegionId, feature.Source.FaceIds, mapping, false, diagnostics, provenance);
                if (loop is not null)
                    return new($"source-{feature.StableId}", "XY", loop with { StableId = $"source-{feature.StableId}.inner", IsOuter = true }, [],
                        $"Imported STEP inner loop for {feature.StableId}; retained as standalone cut contour");
            }
        }
        return null;
    }

    private static PlanarContourLoop2? ExtractLoop(BrepBody body, Aetheris.Kernel.Core.Topology.LoopId loopId, string owner,
        IReadOnlyList<int> faceIds, SourceToFlatMapping mapping, bool outer, ICollection<SheetMetalDiagnostic> diagnostics,
        ICollection<RecoveredFlatSegmentProvenance> provenance)
    {
        var segments = new List<PlanarContourSegment2>();
        foreach (var coedgeId in body.GetCoedgeIds(loopId))
        {
            var coedge = body.Topology.GetCoedge(coedgeId); var edge = body.Topology.GetEdge(coedge.EdgeId);
            var startId = coedge.IsReversed ? edge.EndVertexId : edge.StartVertexId;
            var endId = coedge.IsReversed ? edge.StartVertexId : edge.EndVertexId;
            if (!body.TryGetVertexPoint(startId, out var start3) || !body.TryGetVertexPoint(endId, out var end3)) continue;
            var start = Map(mapping, start3); var end = Map(mapping, end3);
            var id = $"{owner}.source-e{edge.Id.Value:D4}";
            LineArcProfileCurve2D geometry;
            if (body.Bindings.TryGetEdgeBinding(edge.Id, out var binding) && body.Geometry.TryGetCurve(binding.CurveGeometryId, out var curve) && curve is not null && curve.Kind == CurveGeometryKind.Circle3 && curve.Circle3 is { } circle)
            {
                var center = Map(mapping, circle.Center);
                if (startId == endId && binding.TrimInterval is { } full && Math.Abs(full.End - full.Start) >= Math.PI * 2d - 1e-6)
                {
                    if (!outer)
                    {
                        var p0 = new ProfileSegmentProvenance(id + ".0", owner, $"STEP-edge-{edge.Id.Value}", "imported full circle split into two exact semicircles for bounded arrangement", "XY");
                        var p1 = p0 with { StableId = id + ".1" };
                        segments.Add(new(id + ".0", new LineArcCircularArc2D((center.X, center.Y), circle.Radius, 0d, Math.PI), p0));
                        segments.Add(new(id + ".1", new LineArcCircularArc2D((center.X, center.Y), circle.Radius, Math.PI, Math.PI), p1));
                        provenance.Add(new(id + ".0", owner, faceIds, [edge.Id.Value], "source BRep full circle -> exact recovered semicircle 1"));
                        provenance.Add(new(id + ".1", owner, faceIds, [edge.Id.Value], "source BRep full circle -> exact recovered semicircle 2"));
                        continue;
                    }
                    geometry = new LineArcFullCircle2D((center.X, center.Y), circle.Radius);
                }
                else
                {
                    var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
                    var positive = Math.Atan2(end.Y - center.Y, end.X - center.X) - startAngle;
                    while (positive <= 0d) positive += Math.PI * 2d;
                    var negative = positive - Math.PI * 2d;
                    var sourceMid = binding.TrimInterval is { } trim
                        ? circle.Evaluate((trim.Start + trim.End) / 2d)
                        : circle.Evaluate(0d);
                    var mid = Map(mapping, sourceMid);
                    if (coedge.IsReversed && binding.TrimInterval is { } reverseTrim)
                        mid = Map(mapping, circle.Evaluate((reverseTrim.End + reverseTrim.Start) / 2d));
                    var sweep = Distance(Point(center, circle.Radius, startAngle + positive / 2d), mid) <= Distance(Point(center, circle.Radius, startAngle + negative / 2d), mid) ? positive : negative;
                    geometry = new LineArcCircularArc2D((center.X, center.Y), circle.Radius, startAngle, sweep);
                }
            }
            else
            {
                geometry = new LineArcLineSegment2D((start.X, start.Y), (end.X, end.Y));
                if (body.Bindings.TryGetEdgeBinding(edge.Id, out var unsupportedBinding) && body.Geometry.TryGetCurve(unsupportedBinding.CurveGeometryId, out var unsupportedCurve) && unsupportedCurve?.Kind is not CurveGeometryKind.Line3)
                    diagnostics.Add(new(SheetMetalDiagnosticCodes.SourceContourUnsupported, SheetMetalDiagnosticSeverity.Warning,
                        $"Source edge {edge.Id.Value} on '{owner}' is {unsupportedCurve?.Kind}; its exact endpoints are retained as a bounded line fallback.", faceIds));
            }
            var p = new ProfileSegmentProvenance(id, owner, $"STEP-edge-{edge.Id.Value}", "imported BRep edge unfolded through recognized region transform", "XY");
            segments.Add(new(id, geometry, p));
            provenance.Add(new(id, owner, faceIds, [edge.Id.Value], "source BRep edge -> region mid-surface -> recovered flat contour"));
        }
        if (segments.Count == 0) return null;
        // Every extracted contour is standalone authority at this stage. Feature
        // loops become clockwise only when attached as holes to the stitched blank.
        var normalized = NormalizeWinding(segments, true);
        return new($"{owner}.source-loop-{loopId.Value:D4}", outer, normalized);
    }

    private static PlanarContour2? Stitch(RecognizedSheetMetalModel model, IReadOnlyList<FlatRegion2D> regions,
        IReadOnlyList<FlatCutLoop> cuts, ICollection<SheetMetalDiagnostic> diagnostics, out RecoveryContourStitchResult recovery)
    {
        recovery = new(null, RecoveredContourAcceptance.Failed, [], new(0, 0, 0, 0, 0, 0d, "none", []), []);
        if (regions.Any(x => x.ExactContour is null) || cuts.Any(x => x.ExactContour is null)) return null;
        var bySource = regions.ToDictionary(x => x.SourceRegionId, StringComparer.Ordinal);
        var ordered = new List<FlatRegion2D>(); var seen = new HashSet<string>(StringComparer.Ordinal); var queue = new Queue<string>();
        void Add(string sourceId) { if (bySource.TryGetValue(sourceId, out var region) && seen.Add(region.StableId)) ordered.Add(region); }
        Add(model.RootRegionId); queue.Enqueue(model.RootRegionId);
        var admitted = model.Bends.Where(x => x.Status == RecognizedBendStatus.Recognized).OrderBy(x => x.SourceBendId, StringComparer.Ordinal).ToArray();
        var reached = new HashSet<string>(StringComparer.Ordinal) { model.RootRegionId };
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var bend in admitted.Where(x => x.Geometry.AdjacentRegionA == current || x.Geometry.AdjacentRegionB == current))
            {
                Add(bend.SourceBendId);
                var neighbor = bend.Geometry.AdjacentRegionA == current ? bend.Geometry.AdjacentRegionB : bend.Geometry.AdjacentRegionA;
                if (reached.Add(neighbor)) { Add(neighbor); queue.Enqueue(neighbor); }
            }
        }
        foreach (var region in regions.OrderBy(x => x.StableId, StringComparer.Ordinal)) if (seen.Add(region.StableId)) ordered.Add(region);
        var operations = ordered.Select((x, i) => new PrismaticProfileOperation(
                x.StableId, i == 0 ? PrismaticProfileIntent.Base : PrismaticProfileIntent.Add, x.StableId, 0, 1,
                "recognized imported material region", x.SourceRegionId)).ToArray();
        var profiles = regions.ToDictionary(x => x.StableId, x => PlanarContourKernel.ToResolvedProfile(x.ExactContour!, x.StableId), StringComparer.Ordinal);
        var composed = ProfileArrangementBuilder.Compose("XY", operations, profiles, $"recovered-source-flat:{model.StableId}");
        PlanarContour2? contour;
        if (composed.Region is null)
        {
            foreach (var item in composed.Arrangement.Diagnostics)
                diagnostics.Add(new(SheetMetalDiagnosticCodes.UnfoldCrack, SheetMetalDiagnosticSeverity.Information, $"Strict arrangement boundary: {item}"));
            recovery = RecoveryContourStitcher.Stitch(composed.Arrangement, $"recovered-source-flat-{model.StableId}");
            foreach (var item in recovery.Diagnostics)
                diagnostics.Add(new(SheetMetalDiagnosticCodes.UnfoldCrack, SheetMetalDiagnosticSeverity.Warning, $"Recovery stitch: {item}"));
            contour = recovery.Contour;
        }
        else
        {
            var profile = new ResolvedProfile2D($"recovered-source-flat-{model.StableId}", "XY", [composed.Region.Outer.Loops.Single()]);
            contour = PlanarContourKernel.FromResolvedProfile(profile, "source-derived geometric mid-surface reference");
            recovery = new(contour, RecoveredContourAcceptance.Exact, [],
                new(composed.Arrangement.RetainedBoundaryFragmentCount, composed.Arrangement.IntersectionVertices.Count, 0, 1, 1, 0d, "strict-arrangement", []), []);
        }
        if (contour is null) return null;
        // Cut ownership and closed-loop topology are already source facts. Attach the
        // exact loops after material stitching so circle seam vertices are not mistaken
        // for zero-width material ligaments by the material-union arrangement.
        contour = contour with { InnerLoops = cuts.OrderBy(x => x.FeatureId, StringComparer.Ordinal)
            .Select(x => ReverseLoop(x.ExactContour!.OuterLoop with { StableId = $"{x.FeatureId}.inner", IsOuter = false })).ToArray() };
        var validation = PlanarContourKernel.Validate(contour);
        foreach (var item in validation.Diagnostics)
            diagnostics.Add(new(SheetMetalDiagnosticCodes.UnfoldCrack, item.Severity == PlanarContourDiagnosticSeverity.Error ? SheetMetalDiagnosticSeverity.Warning : SheetMetalDiagnosticSeverity.Information,
                $"Recovered stitched contour: {item.Code}: {item.Message}"));
        return validation.IsValid ? contour : null;
    }

    private static IReadOnlyList<PlanarContourSegment2> NormalizeWinding(IReadOnlyList<PlanarContourSegment2> segments, bool outer)
    {
        var area = Sample(segments).Select((p, i) => (p, q: Sample(segments)[(i + 1) % Sample(segments).Count])).Sum(x => x.p.X * x.q.Y - x.q.X * x.p.Y) / 2d;
        return outer == (area > 0d) ? segments : Reverse(segments);
    }

    private static PlanarContourLoop2 ReverseLoop(PlanarContourLoop2 loop) => loop with { Segments = Reverse(loop.Segments) };
    private static IReadOnlyList<PlanarContourSegment2> Reverse(IReadOnlyList<PlanarContourSegment2> segments) => segments.Reverse().Select(x => x with { Geometry = x.Geometry switch
    {
        LineArcLineSegment2D line => new LineArcLineSegment2D(line.End, line.Start),
        LineArcCircularArc2D arc => arc with { StartAngleRadians = arc.StartAngleRadians + arc.SweepAngleRadians, SweepAngleRadians = -arc.SweepAngleRadians },
        LineArcFullCircle2D circle => circle,
        _ => x.Geometry
    }}).ToArray();

    private static IReadOnlyList<SheetPoint2> Sample(IReadOnlyList<PlanarContourSegment2> segments) => segments.SelectMany(x => x.Geometry switch
    {
        LineArcLineSegment2D line => [new SheetPoint2(line.Start.X, line.Start.Y)],
        LineArcCircularArc2D arc => Enumerable.Range(0, Math.Max(2, (int)Math.Ceiling(Math.Abs(arc.SweepAngleRadians) / (Math.PI / 12d)))).Select(i =>
        {
            var a = arc.StartAngleRadians + arc.SweepAngleRadians * i / Math.Max(2, (int)Math.Ceiling(Math.Abs(arc.SweepAngleRadians) / (Math.PI / 12d)));
            return new SheetPoint2(arc.Center.X + arc.Radius * Math.Cos(a), arc.Center.Y + arc.Radius * Math.Sin(a));
        }),
        LineArcFullCircle2D circle => Enumerable.Range(0, 48).Select(i => Point(new(circle.Center.X, circle.Center.Y), circle.Radius, Math.PI * 2d * i / 48d)),
        _ => []
    }).ToArray();

    private static IReadOnlyList<SheetPoint2> Vertices(PlanarContour2 contour) => Sample(contour.OuterLoop.Segments);
    private static string ContourSignature(PlanarContour2? contour) => contour is null ? "none" : string.Join(',', contour.Loops.SelectMany(x => x.Segments).Select(x => $"{x.StableId}:{x.Geometry}"));
    private static SheetPoint2 Map(SourceToFlatMapping m, Point3D p)
    {
        var d = p - m.PlaneOrigin;
        return Add(m.FlatOrigin, Add(Scale(m.FlatU, d.Dot(m.SourceU)), Scale(m.FlatV, d.Dot(m.SourceV))));
    }
    private static SheetPoint2 Point(SheetPoint2 center, double radius, double angle) => new(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
    private static double Distance(SheetPoint2 a, SheetPoint2 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static SheetPoint2 Add(SheetPoint2 a, SheetPoint2 b) => new(a.X + b.X, a.Y + b.Y);
    private static SheetPoint2 Scale(SheetPoint2 a, double s) => new(a.X * s, a.Y * s);

    private static RecoveredFlatReference Failed(RecognizedSheetMetalModel model, SheetMetalRecognitionPlan plan,
        IReadOnlyList<SheetMetalDiagnostic> diagnostics, TimeSpan graphTime)
    {
        var hash = SheetMetalRecognizer.StableHash(plan.DeterministicHash + "|invalid");
        return new($"recovered-flat-{hash[..16]}", plan.ReferenceKind, plan.RootRegionId, "XY", null, [], [], [], [], [], plan,
            null, FlatPatternStatus.Unsupported, diagnostics, RecoveredContourAcceptance.Failed, [], null, hash, graphTime, TimeSpan.Zero, TimeSpan.Zero);
    }
}
