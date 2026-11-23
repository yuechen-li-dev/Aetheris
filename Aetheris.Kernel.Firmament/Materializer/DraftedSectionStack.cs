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
            d.Add($"compose-draft-unsupported:{kind}:{name}: a drafted Compose cannot carry {kind}; remove draft or the feature.");
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
            // The support is the exterior beneath: a hollow's own cavity under a boss is not a missing support.
            var supporting = feature.Operations.Where(o => o.Intent != PrismaticProfileIntent.Remove && o.From < level - Tol && o.To >= level - Tol).ToArray();
            if (starting.Length > 0 && supporting.Length > 0 && Section(feature, starting, parsed.Profiles, level, $"overhang={level:R}", d) is { } footprint
                && Section(feature, supporting, parsed.Profiles, level, $"support={level:R}", d) is { } support)
            {
                var overhang = ProfileArrangementBuilder.Difference(feature.Frame, footprint.MaterialRegions, support.MaterialRegions, $"overhang={level:R}:footprint-minus-below");
                if (overhang.MaterialRegions.Count > 0)
                    d.Add($"compose-draft-overhang: {string.Join(", ", starting.Select(o => o.Name))} at {level:0.###} mm is wider than the drafted material beneath it, which would be an undercut; place it within its support's narrowed top or reduce the draft.");
            }
            if (upward.MaterialRegions.Count > 0 || downward.MaterialRegions.Count > 0) transitions.Add(new(level, upward.MaterialRegions, downward.MaterialRegions));
        }
        var refused = d.Any(x => x.Contains("rejected", StringComparison.Ordinal) || x.StartsWith("draft-", StringComparison.Ordinal) || x.StartsWith("compose-draft-", StringComparison.Ordinal));
        diagnostics = d.Distinct().ToArray();
        return refused ? null : new(feature, slabs, transitions, volume, diagnostics);
    }

    /// <summary>
    /// The cavity of a drafted hollow: every wall of the exterior gets a parallel inner wall exactly the wall
    /// thickness t inside it. Each Profile moves inward by t / cos(draft) and keeps its operation's draft (parallel
    /// planes t apart, coaxial cones of one angle), and each closed end moves in by t.
    /// <list type="bullet">
    /// <item>The Base's cavity runs between its ends.</item>
    /// <item>A Boss on the closed top is hollow too: its cavity rises from the Base's cavity, t below its own top.</item>
    /// <item>A Pocket in the closed top dents the shell: a liner, the Pocket grown by t and drafted with it, stays
    /// solid from t below the Pocket's floor up to the cap, as a hole in the Base's cavity.</item>
    /// </list>
    /// Sharp inside corners stay sharp, so the wall there is thicker, as at a step. A feature too close to a wall
    /// or to another for an even wall is refused, as is any other operation.
    /// </summary>
    public static PrismaticProfileCompositionParseResult? DeriveHollow(PrismaticProfileCompositionParseResult parsed, List<string> d)
    {
        var feature = parsed.Feature!;
        var hollow = feature.Hollow!;
        var t = hollow.WallThickness;
        var exterior = feature.Operations.Where(o => o.SemanticFeatureKind is null or "Boss" or "Pocket").ToArray();
        var stocks = exterior.Where(o => o.Intent == PrismaticProfileIntent.Base).ToArray();
        if (stocks.Length != 1) { d.Add($"compose-draft-unsupported:Hollow: a drafted Compose<Hollow> is hollowed from one Base, not {stocks.Length}."); return null; }
        var stock = stocks[0];
        var bosses = exterior.Where(o => o.SemanticFeatureKind == "Boss").ToArray();
        var pockets = exterior.Where(o => o.SemanticFeatureKind == "Pocket").ToArray();
        foreach (var other in exterior.Where(o => o != stock && !bosses.Contains(o) && !pockets.Contains(o)))
            d.Add($"compose-draft-unsupported:Hollow:{other.Name}: a drafted hollow is built from its Base, Bosses on its top and Pockets in its top; {other.Intent} {other.Name} cannot be hollowed with it.");
        foreach (var feature_ in bosses.Concat(pockets))
        {
            var onTop = feature_.SemanticFeatureKind == "Boss" ? Math.Abs(feature_.From - stock.To) <= Tol : Math.Abs(feature_.To - stock.To) <= Tol && feature_.SemanticRole != "ThroughCut";
            if (!onTop) d.Add($"compose-draft-unsupported:Hollow:{feature_.Name}: a drafted hollow takes Bosses and Pockets on its Base's top only.");
            else if (hollow.OpenTop) d.Add($"compose-draft-unsupported:Hollow:{feature_.Name}: the hollow is open at the top, where {feature_.Name} would sit.");
        }
        if (d.Any(x => x.StartsWith("compose-draft-unsupported", StringComparison.Ordinal))) return null;

        var low = hollow.OpenBottom ? stock.From : stock.From + t;
        var high = hollow.OpenTop ? stock.To : stock.To - t;
        if (high - low <= Tol)
        {
            d.Add($"hollow-wall-offset-collapse:{feature.Name}: the body is {stock.To - stock.From:R} tall, too short for its wall of {t:R}");
            return null;
        }
        var profiles = parsed.Profiles.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var stockLaw = new DraftLaw(stock.DraftRadians, stock.DraftNeutral ?? stock.From, true);
        var stockLoops = Moved(parsed.Profiles[stock.ProfileReference], t / Math.Cos(stock.DraftRadians), d);
        if (stockLoops is null) return null;

        // Each pocket's liner: its Profile grown by t / cos(its draft), a hole in the cavity that follows the pocket.
        var liners = new List<(PrismaticProfileOperation Pocket, IReadOnlyList<ResolvedProfileLoop2D> Loops, DraftLaw Law, double Bottom)>();
        foreach (var pocket in pockets)
        {
            var bottom = pocket.From - t;
            if (bottom <= low + Tol)
            {
                d.Add($"hollow-pocket-too-deep:{pocket.Name}: its floor is {pocket.To - pocket.From:0.###} mm deep, which leaves no room for a {t:0.###} mm wall under it inside a cavity that starts at {low:0.###} mm.");
                continue;
            }
            var grown = Moved(parsed.Profiles[pocket.ProfileReference], -t / Math.Cos(pocket.DraftRadians), d);
            if (grown is null) continue;
            // As a hole of the cavity, the liner grows as the pocket's void shrinks: the opposite of its erosion.
            liners.Add((pocket, grown.Select(loop => loop with { Name = $"Liner{pocket.Name}{loop.Name}", IsOuter = false }).ToArray(),
                new DraftLaw(pocket.DraftRadians, pocket.DraftNeutral ?? pocket.To, false, -1d), bottom));
        }
        if (d.Any(x => x.StartsWith("hollow-", StringComparison.Ordinal))) return null;

        var operations = feature.Operations.ToList();
        var levels = feature.CriticalLevels.Append(low).Append(high).ToList();
        // The Base's cavity in bands: a pocket's liner starts at its own height.
        var cuts = liners.Select(l => l.Bottom).Append(low).Append(high).Order().Distinct().ToArray();
        for (var band = 0; band + 1 < cuts.Length; band++)
        {
            var (a, b) = (cuts[band], cuts[band + 1]);
            var name = $"{feature.Name}Cavity{band}";
            var active = liners.Where(l => l.Bottom <= a + Tol).ToArray();
            var loops = stockLoops.Concat(active.SelectMany(l => l.Loops)).ToArray();
            var laws = stockLoops.Select(_ => stockLaw).Concat(active.SelectMany(l => l.Loops.Select(_ => l.Law))).ToArray();
            profiles[name] = Renamed(parsed.Profiles[stock.ProfileReference] with { Loops = loops }, name);
            operations.Add(new(name, PrismaticProfileIntent.Remove, name, a, b, "HollowCavity", hollow.SourceSpan, $"hollow:{feature.Name}", "Hollow")
                { DraftRadians = stock.DraftRadians, LoopDraft = laws });
            levels.Add(a); levels.Add(b);
        }
        // Each boss's own cavity, from the Base's cavity up to t below the boss's top.
        foreach (var boss in bosses)
        {
            var name = $"{feature.Name}Cavity{boss.Name}";
            var loops = Moved(parsed.Profiles[boss.ProfileReference], t / Math.Cos(boss.DraftRadians), d);
            if (loops is null) return null;
            if (boss.To - t - high <= Tol) { d.Add($"hollow-wall-offset-collapse:{boss.Name}: the boss is {boss.To - boss.From:0.###} mm tall, too short to hollow with a {t:0.###} mm wall."); return null; }
            profiles[name] = Renamed(parsed.Profiles[boss.ProfileReference] with { Loops = loops }, name);
            operations.Add(new(name, PrismaticProfileIntent.Remove, name, high, boss.To - t, "HollowCavity", hollow.SourceSpan, $"hollow:{feature.Name}", "Hollow")
                { DraftRadians = boss.DraftRadians, DraftNeutral = boss.DraftNeutral ?? boss.From, DraftNarrowsUpward = true });
            levels.Add(boss.To - t);
        }
        var merged = levels.Order().Aggregate(new List<double>(), (list, z) => { if (list.Count == 0 || z - list[^1] > Tol) list.Add(z); return list; });
        var result = feature with { Operations = operations, CriticalLevels = merged, Hollow = null };

        // An even wall needs every inner surface inside the Base's cavity where it starts, and apart from the others.
        var cavities = operations.Where(o => o.SemanticFeatureKind == "Hollow").ToArray();
        foreach (var boss in bosses)
        {
            var bossCavity = cavities.Single(o => o.Name == $"{feature.Name}Cavity{boss.Name}");
            var below = cavities.Where(o => o.From < high - Tol && o.To >= high - Tol && o.Name != bossCavity.Name).ToArray();
            if (Outside(result, [bossCavity], below, profiles, high, d))
                d.Add($"hollow-feature-too-close:{boss.Name}: its cavity does not fit inside the Base's cavity at {high:0.###} mm, so a wall would be thinner than {t:0.###} mm; move it away from the outer wall or from the pockets.");
        }
        foreach (var liner in liners)
            foreach (var z in new[] { liner.Bottom, high })
            {
                var band = cavities.First(o => o.From <= z + Tol && o.To >= z - Tol && o.Name.StartsWith($"{feature.Name}Cavity", StringComparison.Ordinal) && o.LoopDraft is not null);
                var outerOnly = band with { LoopDraft = band.LoopDraft!.Take(stockLoops.Count).ToArray(), ProfileReference = band.ProfileReference + "#outer" };
                profiles[outerOnly.ProfileReference] = profiles[band.ProfileReference] with { Loops = profiles[band.ProfileReference].Loops.Take(stockLoops.Count).ToArray() };
                // As a region of its own the liner erodes as the pocket does (the cavity's hole law, negated).
                var linerOp = band with
                {
                    Intent = PrismaticProfileIntent.Base, ProfileReference = band.ProfileReference + "#" + liner.Pocket.Name,
                    LoopDraft = liner.Loops.Select(_ => liner.Law with { Sign = 1d }).ToArray(),
                };
                profiles[linerOp.ProfileReference] = profiles[band.ProfileReference] with { Loops = liner.Loops.Select(l => l with { IsOuter = true }).ToArray() };
                if (Outside(result, [linerOp], [outerOnly with { Intent = PrismaticProfileIntent.Base }], profiles, z, d))
                    d.Add($"hollow-feature-too-close:{liner.Pocket.Name}: the wall under it leaves the Base's cavity at {z:0.###} mm, so a wall would be thinner than {t:0.###} mm; move it away from the outer wall.");
                foreach (var boss in bosses.Where(_ => Math.Abs(z - high) <= Tol))
                {
                    var bossCavity = cavities.Single(o => o.Name == $"{feature.Name}Cavity{boss.Name}") with { Intent = PrismaticProfileIntent.Base };
                    if (!Outside(result, [bossCavity], [linerOp], profiles, z, d, overlap: true)) continue;
                    d.Add($"hollow-feature-too-close:{boss.Name}: its cavity meets the wall under {liner.Pocket.Name}, so a wall would be thinner than {t:0.###} mm; move them apart.");
                }
            }
        if (d.Any(x => x.StartsWith("hollow-", StringComparison.Ordinal))) return null;
        return new(result, profiles, parsed.Diagnostics, parsed.Expansion);
    }

    /// <summary>Whether the region of <paramref name="inner"/> at <paramref name="z"/> leaves that of
    /// <paramref name="outer"/> (or, with <paramref name="overlap"/>, meets it).</summary>
    private static bool Outside(PrismaticProfileCompositionFeature feature, IReadOnlyList<PrismaticProfileOperation> inner, IReadOnlyList<PrismaticProfileOperation> outer,
        IReadOnlyDictionary<string, ResolvedProfile2D> profiles, double z, List<string> d, bool overlap = false)
    {
        var scratch = new List<string>();
        var a = Section(feature, inner.Select(o => o with { Intent = PrismaticProfileIntent.Base }).ToArray(), profiles, z, $"hollow-check={z:R}:inner", scratch);
        var b = Section(feature, outer.Select(o => o with { Intent = PrismaticProfileIntent.Base }).ToArray(), profiles, z, $"hollow-check={z:R}:outer", scratch);
        if (a is null || b is null) return true;
        var outside = ProfileArrangementBuilder.Difference(feature.Frame, a.MaterialRegions, b.MaterialRegions, $"hollow-check={z:R}:difference");
        if (!overlap) return outside.MaterialRegions.Count > 0;
        // Overlap: some of A is not outside B.
        return Area(outside.MaterialRegions) < Area(a.MaterialRegions) - 1e-9;
    }

    /// <summary>A Profile's loops moved by <paramref name="distance"/> into its region (outward when negative).</summary>
    private static IReadOnlyList<ResolvedProfileLoop2D>? Moved(ResolvedProfile2D profile, double distance, List<string> d)
    {
        var loops = profile.Loops.Select(loop => new LineArcProfileLoop2D(loop.Segments.Select(s => s.Geometry).ToArray(), !loop.IsOuter)).ToArray();
        if (!ProfileDraftOffset.TryOffset(loops, distance, out var moved, out var error))
        {
            d.Add(error?.Replace("draft-offset-collapse", "hollow-wall-offset-collapse", StringComparison.Ordinal) ?? "hollow-wall-offset-collapse");
            return null;
        }
        return profile.Loops.Select((loop, li) => loop with { Segments = loop.Segments.Select((segment, si) => segment with { Geometry = moved[li].Curves[si] }).ToArray() }).ToArray();
    }

    /// <summary>The profile under a new name, its segments' provenance renamed with it.</summary>
    private static ResolvedProfile2D Renamed(ResolvedProfile2D profile, string name) => profile with
    {
        Name = name,
        Loops = profile.Loops.Select(loop => loop with
        {
            Segments = loop.Segments.Select(segment => segment with { Provenance = segment.Provenance with { StableId = $"profile:{name}.{loop.Name}.{segment.Name}" } }).ToArray(),
        }).ToArray(),
    };

    /// <summary>
    /// Features a drafted Compose does not draft: a countersink's cone is a machined form, and holes drilled from
    /// a side face or a construction plane are not along the pull, which takes a side action rather than draft.
    /// </summary>
    private static IEnumerable<(string Kind, string Name)> UnsupportedFeatures(PrismaticProfileCompositionFeature feature)
    {
        foreach (var hole in feature.CountersinkHoles ?? []) yield return ("Hole<Countersink>", hole.Name);
        foreach (var hole in feature.ConstructionPlaneBlindDrills ?? []) yield return ("a hole on a construction plane", hole.Name);
        foreach (var hole in feature.FaceHoles) yield return ("a hole on a side face", hole.Name);
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
            if (operation.LoopDraft is { } laws)
            {
                // Loops that follow different operations move by their own laws, each group offset together.
                var loops = profile.Loops.ToArray();
                foreach (var group in Enumerable.Range(0, loops.Length).GroupBy(li => laws[li]))
                {
                    var indices = group.ToArray();
                    var distance = group.Key.ErosionAt(z);
                    if (distance == 0d) continue;
                    var input = indices.Select(li => new LineArcProfileLoop2D(loops[li].Segments.Select(s => s.Geometry).ToArray(), !loops[li].IsOuter)).ToArray();
                    if (!ProfileDraftOffset.TryOffset(input, distance, out var moved, out var error))
                    {
                        d.Add($"{error ?? "draft-offset-collapse"} (operation {operation.Name}, height {z:0.###} mm)");
                        return null;
                    }
                    for (var k = 0; k < indices.Length; k++)
                        loops[indices[k]] = loops[indices[k]] with { Segments = loops[indices[k]].Segments.Select((segment, si) => segment with { Geometry = moved[k].Curves[si] }).ToArray() };
                }
                profile = profile with { Loops = loops };
            }
            else if (erosion != 0d)
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
