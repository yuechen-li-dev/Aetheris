namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Normalizes a Compose whose operations carry side draft. Each operation's own Profile region is eroded by its
/// draft at every height (<see cref="PrismaticProfileOperation.DraftErosionAt"/>), so the section between two
/// critical levels changes linearly. A slab is then the ruled sweep from its section at <c>From</c> to its section
/// at <c>To</c>: the two arrangements must have the same loops, curve for curve, and each wall is the drafted plane
/// or right cone through its two curves. Where two operations' walls meet, the meeting line is exact only between
/// two planes, so a meeting that involves an arc is refused rather than approximated.
/// </summary>
public static class DraftedSectionStack
{
    private const double Tol = 1e-7;

    public static bool IsDrafted(PrismaticProfileCompositionFeature feature) => feature.Operations.Any(o => o.DraftRadians != 0d);

    public static PrismaticSectionStackConstruction? Normalize(PrismaticProfileCompositionParseResult parsed, List<string> d, out IReadOnlyList<string> diagnostics)
    {
        var feature = parsed.Feature!;
        foreach (var (kind, name) in UnsupportedFeatures(feature))
            d.Add($"compose-draft-unsupported:{kind}:{name}: a drafted Compose cannot carry {kind} yet; remove draft or the feature.");
        if (d.Count != 0) { diagnostics = d.Distinct().ToArray(); return null; }

        var slabs = new List<PrismaticSectionSlab>();
        var volume = 0d;
        foreach (var pair in feature.CriticalLevels.Zip(feature.CriticalLevels.Skip(1)))
        {
            var mid = (pair.First + pair.Second) / 2d;
            var active = feature.Operations.Where(o => o.From < mid && mid < o.To).ToArray();
            if (active.Length == 0) continue;
            var label = $"slab=({pair.First:R},{pair.Second:R})";
            var lower = Section(feature, active, parsed.Profiles, pair.First, label + ":from", d);
            var upper = Section(feature, active, parsed.Profiles, pair.Second, label + ":to", d);
            var middle = Section(feature, active, parsed.Profiles, mid, label + ":mid", d);
            if (lower is null || upper is null || middle is null) continue;
            if (lower.MaterialRegions.Count == 0) continue;
            var aligned = Align(lower.MaterialRegions, upper.MaterialRegions, pair.First, pair.Second, d);
            if (aligned is null) continue;
            CheckJunctions(lower.MaterialRegions, pair.First, pair.Second, d);
            // The section area is quadratic in height, so Simpson's rule is exact on each slab.
            volume += (pair.Second - pair.First) / 6d * (Area(lower.MaterialRegions) + 4d * Area(middle.MaterialRegions) + Area(upper.MaterialRegions));
            slabs.Add(new(pair.First, pair.Second, lower.MaterialRegions[0], active.Select(x => x.Name).Order().ToArray(), lower.Arrangement, lower.MaterialRegions)
                { UpperRegionSet = aligned });
        }
        if (slabs.Count == 0 && !d.Any(x => x.StartsWith("arrangement-rejected", StringComparison.Ordinal) || x.StartsWith("draft-", StringComparison.Ordinal)
            || x.StartsWith("compose-draft-", StringComparison.Ordinal))) d.Add("compose-no-material-slabs");

        var transitions = new List<PrismaticSectionTransition>();
        foreach (var level in feature.CriticalLevels)
        {
            var below = slabs.SingleOrDefault(s => Math.Abs(s.To - level) < Tol)?.UpperRegions ?? [];
            var above = slabs.SingleOrDefault(s => Math.Abs(s.From - level) < Tol)?.MaterialRegions ?? [];
            var upward = ProfileArrangementBuilder.Difference(feature.Frame, below, above, $"transition={level:R}:below-minus-above");
            var downward = ProfileArrangementBuilder.Difference(feature.Frame, above, below, $"transition={level:R}:above-minus-below");
            d.AddRange(upward.Arrangement.Diagnostics);
            d.AddRange(downward.Arrangement.Diagnostics);
            // Above the bottom, material that faces down is an overhang: a feature authored on a support that its
            // draft has narrowed away from beneath it.
            // (A cavity's ceiling faces down too, and is not one.)
            var starting = feature.Operations.Where(o => Math.Abs(o.From - level) < Tol && o.Intent == PrismaticProfileIntent.Add).ToArray();
            if (starting.Length > 0 && below.Count > 0 && Section(feature, starting, parsed.Profiles, level, $"overhang={level:R}", d) is { } footprint)
            {
                var overhang = ProfileArrangementBuilder.Difference(feature.Frame, footprint.MaterialRegions, below, $"overhang={level:R}:footprint-minus-below");
                if (overhang.MaterialRegions.Count > 0)
                    d.Add($"compose-draft-overhang: {string.Join(", ", starting.Select(o => o.Name))} at {level:0.###} mm is wider than the drafted material beneath it, which would be an undercut; place it within its support's narrowed top or reduce the draft.");
            }
            if (upward.MaterialRegions.Count > 0 || downward.MaterialRegions.Count > 0) transitions.Add(new(level, upward.MaterialRegions, downward.MaterialRegions));
        }
        var refused = d.Any(x => x.Contains("rejected", StringComparison.Ordinal) || x.StartsWith("draft-", StringComparison.Ordinal) || x.StartsWith("compose-draft-", StringComparison.Ordinal));
        if (!refused) d.Add("compose-drafted-section-stack");
        diagnostics = d.Distinct().ToArray();
        return refused ? null : new(feature, slabs, transitions, volume, diagnostics);
    }

    /// <summary>
    /// The cavity of a drafted hollow Base: the Base's Profile moved inward by t / cos(draft) and drafted with it, so
    /// every wall is exactly t thick along its normal (parallel planes, coaxial cones of the same angle). Sharp
    /// inside corners of the Profile stay sharp, so the wall there is thicker, as at a step. A drafted hollow with
    /// Boss or Pocket operations is not built yet.
    /// </summary>
    public static PrismaticProfileCompositionParseResult? DeriveHollow(PrismaticProfileCompositionParseResult parsed, List<string> d)
    {
        var feature = parsed.Feature!;
        var hollow = feature.Hollow!;
        var t = hollow.WallThickness;
        var exterior = feature.Operations.Where(o => o.SemanticFeatureKind is null or "Boss" or "Pocket").ToArray();
        if (exterior.Length != 1 || exterior[0].Intent != PrismaticProfileIntent.Base)
        {
            d.Add($"compose-draft-unsupported:Hollow: a drafted Compose<Hollow> is built from its Base alone; {string.Join(", ", exterior.Where(o => o.Intent != PrismaticProfileIntent.Base).Select(o => o.Name))} cannot be hollowed with it yet.");
            return null;
        }
        var stock = exterior[0];
        var low = hollow.OpenBottom ? stock.From : stock.From + t;
        var high = hollow.OpenTop ? stock.To : stock.To - t;
        if (high - low <= Tol)
        {
            d.Add($"hollow-wall-offset-collapse:{feature.Name}: the body is {stock.To - stock.From:R} tall, too short for its wall of {t:R}");
            return null;
        }
        var profile = parsed.Profiles[stock.ProfileReference];
        var inset = t / Math.Cos(stock.DraftRadians);
        var loops = profile.Loops.Select(loop => new LineArcProfileLoop2D(loop.Segments.Select(s => s.Geometry).ToArray(), !loop.IsOuter)).ToArray();
        if (!ProfileDraftOffset.TryOffset(loops, inset, out var moved, out var error))
        {
            d.Add(error?.Replace("draft-offset-collapse", "hollow-wall-offset-collapse", StringComparison.Ordinal) ?? "hollow-wall-offset-collapse");
            return null;
        }
        var name = $"{feature.Name}Cavity0";
        var cavityProfile = profile with
        {
            Name = name,
            Loops = profile.Loops.Select((loop, li) => loop with
            {
                Segments = loop.Segments.Select((segment, si) => segment with
                {
                    Geometry = moved[li].Curves[si],
                    Provenance = segment.Provenance with { StableId = $"profile:{name}.{loop.Name}.{segment.Name}" },
                }).ToArray(),
            }).ToArray(),
        };
        var profiles = parsed.Profiles.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        profiles[name] = cavityProfile;
        var cavity = new PrismaticProfileOperation(name, PrismaticProfileIntent.Remove, name, low, high, "HollowCavity", hollow.SourceSpan, $"hollow:{feature.Name}", "Hollow")
        {
            DraftRadians = stock.DraftRadians, DraftNeutral = stock.DraftNeutral ?? stock.From, DraftNarrowsUpward = true,
        };
        var levels = feature.CriticalLevels.Append(low).Append(high).Order().Aggregate(new List<double>(), (list, z) => { if (list.Count == 0 || z - list[^1] > Tol) list.Add(z); return list; });
        return new(feature with { Operations = feature.Operations.Append(cavity).ToArray(), CriticalLevels = levels, Hollow = null }, profiles, parsed.Diagnostics, parsed.Expansion);
    }

    private static IEnumerable<(string Kind, string Name)> UnsupportedFeatures(PrismaticProfileCompositionFeature feature)
    {
        foreach (var hole in feature.ShaftHoles ?? []) yield return ("Hole<Shaft>", hole.Name);
        foreach (var hole in feature.CounterboreHoles ?? []) yield return ("Hole<Counterbore>", hole.Name);
        foreach (var hole in feature.CountersinkHoles ?? []) yield return ("Hole<Countersink>", hole.Name);
        foreach (var hole in feature.ConstructionPlaneBlindDrills ?? []) yield return ("Hole<Shaft>", hole.Name);
        foreach (var hole in feature.FaceHoles) yield return ("Hole", hole.Name);
        foreach (var slot in feature.AllSlotProfiles) yield return ("Slot", slot.Name);
        foreach (var offset in feature.MaterialOffsets ?? []) yield return ($"{offset.OperationKind}Offset", offset.Name);
    }

    /// <summary>The section at height <paramref name="z"/>: each active operation's region eroded by its own draft.</summary>
    private static ProfileArrangementResult? Section(PrismaticProfileCompositionFeature feature, IReadOnlyList<PrismaticProfileOperation> active,
        IReadOnlyDictionary<string, ResolvedProfile2D> profiles, double z, string label, List<string> d)
    {
        // Two operations may share a Profile at different erosions, so each gets its own copy.
        var offsetProfiles = new Dictionary<string, ResolvedProfile2D>(StringComparer.Ordinal);
        var operations = new List<PrismaticProfileOperation>();
        foreach (var operation in active)
        {
            var key = $"{operation.ProfileReference}@{operation.Name}";
            var profile = profiles[operation.ProfileReference];
            var erosion = operation.DraftErosionAt(z);
            if (erosion != 0d)
            {
                var loops = profile.Loops.Select(loop => new LineArcProfileLoop2D(loop.Segments.Select(s => s.Geometry).ToArray(), !loop.IsOuter)).ToArray();
                if (!ProfileDraftOffset.TryOffset(loops, erosion, out var moved, out var error))
                {
                    d.Add($"{error ?? "draft-offset-collapse"} (operation {operation.Name}, height {z:0.###} mm)");
                    return null;
                }
                profile = profile with
                {
                    Loops = profile.Loops.Select((loop, li) => loop with
                    {
                        Segments = loop.Segments.Select((segment, si) => segment with { Geometry = moved[li].Curves[si] }).ToArray(),
                    }).ToArray(),
                };
            }
            offsetProfiles[key] = profile;
            operations.Add(operation with { ProfileReference = key });
        }
        var result = ProfileArrangementBuilder.Compose(feature.Frame, operations, offsetProfiles, label);
        d.AddRange(result.Arrangement.Diagnostics);
        return result;
    }

    /// <summary>
    /// The upper section's regions in the lower section's order, each loop rotated so that curve i rises from
    /// lower curve i. Refused when the two sections differ: a wall that appears or vanishes within the slab.
    /// </summary>
    private static IReadOnlyList<PrismaticSectionRegion>? Align(IReadOnlyList<PrismaticSectionRegion> lower, IReadOnlyList<PrismaticSectionRegion> upper,
        double from, double to, List<string> d)
    {
        string Fail()
        {
            d.Add($"compose-draft-walls-cross: two drafted walls meet or part between {from:0.###} and {to:0.###} mm, so the section changes shape; move the features apart or reduce the draft.");
            return string.Empty;
        }
        if (lower.Count != upper.Count) { Fail(); return null; }
        var used = new HashSet<int>();
        var result = new List<PrismaticSectionRegion>();
        foreach (var region in lower)
        {
            var match = -1; ResolvedProfile2D? outer = null;
            for (var j = 0; j < upper.Count && outer is null; j++)
                if (!used.Contains(j) && Rotate(region.Outer, upper[j].Outer) is { } rotated) { match = j; outer = rotated; }
            if (outer is null) { Fail(); return null; }
            used.Add(match);
            var candidates = upper[match].Holes.ToList();
            var holes = new List<ResolvedProfile2D>();
            foreach (var hole in region.Holes)
            {
                var found = candidates.Select(c => (Candidate: c, Rotated: Rotate(hole, c))).FirstOrDefault(x => x.Rotated is not null);
                if (found.Rotated is null) { Fail(); return null; }
                candidates.Remove(found.Candidate);
                holes.Add(found.Rotated);
            }
            if (candidates.Count != 0) { Fail(); return null; }
            result.Add(upper[match] with { Outer = outer, Holes = holes });
        }
        return result;
    }

    /// <summary>The upper loop rotated to start where the lower one does, or null when their curves do not correspond.</summary>
    private static ResolvedProfile2D? Rotate(ResolvedProfile2D lower, ResolvedProfile2D upper)
    {
        var a = lower.Loops[0].Segments; var b = upper.Loops[0].Segments;
        if (a.Count != b.Count) return null;
        int? best = null; var bestDistance = double.PositiveInfinity;
        for (var k = 0; k < b.Count; k++)
        {
            var matches = true;
            for (var i = 0; i < a.Count && matches; i++)
            {
                var x = a[i]; var y = b[(i + k) % b.Count];
                matches = SourceId(x.Provenance.StableId) == SourceId(y.Provenance.StableId) && x.Geometry.GetType() == y.Geometry.GetType();
            }
            if (!matches) continue;
            var distance = Distance(Start(a[0].Geometry), Start(b[k].Geometry));
            if (distance < bestDistance) { best = k; bestDistance = distance; }
        }
        if (best is not { } shift) return null;
        return upper with { Loops = [upper.Loops[0] with { Segments = b.Skip(shift).Concat(b.Take(shift)).ToArray() }] };
    }

    /// <summary>
    /// Where the walls of two operations meet, the meeting line runs straight between its two sections only when
    /// both walls are planes; a plane or cone meeting a cone does so along a conic, which is refused.
    /// </summary>
    private static void CheckJunctions(IReadOnlyList<PrismaticSectionRegion> regions, double from, double to, List<string> d)
    {
        foreach (var loop in regions.SelectMany(r => r.Holes.Prepend(r.Outer)))
        {
            var segments = loop.Loops[0].Segments;
            for (var i = 0; i < segments.Count; i++)
            {
                var a = segments[i]; var b = segments[(i + 1) % segments.Count];
                if (LoopOf(a) == LoopOf(b)) continue;
                if (a.Geometry is LineArcLineSegment2D && b.Geometry is LineArcLineSegment2D) continue;
                var p = End(a.Geometry);
                d.Add($"compose-draft-curved-junction: drafted walls of two operations meet at ({p.X:0.###}, {p.Y:0.###}) mm between {from:0.###} and {to:0.###} mm, and one of them is curved; their meeting line is not straight. Keep curved walls of different operations apart.");
                return;
            }
        }
    }

    private static string LoopOf(ResolvedProfileSegment2D segment)
    {
        var id = SourceId(segment.Provenance.StableId);
        var dot = id.LastIndexOf('.');
        return dot < 0 ? id : id[..dot];
    }

    private static double Area(IReadOnlyList<PrismaticSectionRegion> regions) => regions.Sum(PrismaticSectionStackCompiler.Area);

    private static string SourceId(string stableId)
    {
        const string marker = ".arrangement.";
        var index = stableId.IndexOf(marker, StringComparison.Ordinal);
        return index >= 0 ? stableId[..index] : stableId;
    }

    private static (double X, double Y) Start(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => line.Start,
        LineArcCircularArc2D arc => (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)),
        LineArcCubicBezier2D cubic => cubic.Start,
        _ => (double.NaN, double.NaN),
    };

    private static (double X, double Y) End(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => line.End,
        LineArcCircularArc2D arc => (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians)),
        LineArcCubicBezier2D cubic => cubic.End,
        _ => (double.NaN, double.NaN),
    };

    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
